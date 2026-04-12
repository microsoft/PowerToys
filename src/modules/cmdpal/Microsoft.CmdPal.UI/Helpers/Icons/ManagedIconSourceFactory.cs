// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed partial class ManagedIconSourceFactory
{
    private const int StringIconCacheSize = 1024;
    private const int ThumbnailIconCacheSize = 256;

    // SvgImageSource / BitmapImage require their backing stream to stay open for
    // the lifetime of the image source.  These weak tables prevent the GC from
    // collecting the stream while the image source is alive, without preventing
    // either from being collected once the image source is no longer referenced.
    private static readonly ConditionalWeakTable<SvgImageSource, IRandomAccessStream> SvgStreamLifetime = [];
    private static readonly ConditionalWeakTable<BitmapImage, IRandomAccessStream> BitmapStreamLifetime = [];

    // Same pattern for SoftwareBitmapSource: SetBitmapAsync starts an internal
    // AsyncCopyToSurfaceTask that continues reading from the source bitmap after
    // the IAsyncAction completes.  Without this table the SoftwareBitmap can be
    // disposed or GC'd before the GPU surface copy finishes, causing a FailFast
    // with width=0 / height=0.
    private static readonly ConditionalWeakTable<SoftwareBitmapSource, SoftwareBitmap> SoftwareBitmapLifetime = [];

    private readonly ILogger<ManagedIconSourceFactory> _logger;
    private readonly AdaptiveCache<StringIconCacheKey, Task<ManagedIconSourceResult>> _stringIconCache;
    private readonly AdaptiveCache<ThumbnailIconCacheKey, Task<IconSource?>> _thumbnailIconCache;
    private readonly Lock _stringIconCacheLock = new();
    private readonly Lock _thumbnailIconCacheLock = new();

    public ManagedIconSourceFactory(ILogger<ManagedIconSourceFactory> logger)
    {
        _logger = logger;
        _stringIconCache = new AdaptiveCache<StringIconCacheKey, Task<ManagedIconSourceResult>>(StringIconCacheSize, TimeSpan.FromMinutes(60));
        _thumbnailIconCache = new AdaptiveCache<ThumbnailIconCacheKey, Task<IconSource?>>(ThumbnailIconCacheSize, TimeSpan.FromMinutes(60));
    }

    internal readonly record struct ManagedIconSourceResult(bool WasHandled, IconSource? Source, bool StopFallback = false);

    internal ManagedIconSourceFactoryDiagnostics GetDiagnostics()
    {
        return new(
        [
            new(ManagedIconSourceFactoryCacheKind.StringIcons, "Managed string icons", _stringIconCache.GetStatistics()),
            new(ManagedIconSourceFactoryCacheKind.Thumbnails, "Managed thumbnails", _thumbnailIconCache.GetStatistics()),
        ]);
    }

    internal void PruneStringIconCache()
    {
        _stringIconCache.Clear();
    }

    internal void PruneThumbnailIconCache()
    {
        _thumbnailIconCache.Clear();
    }

    public async Task<ManagedIconSourceResult> CreateFromStringAsync(
        string iconString,
        string? fontFamily,
        Size size,
        ElementTheme theme,
        DispatcherQueue dispatcherQueue,
        DispatcherQueuePriority priority)
    {
        var key = new StringIconCacheKey(iconString, fontFamily, size, theme);
        if (_stringIconCache.TryGet(key, out var cachedTask))
        {
            return await cachedTask.ConfigureAwait(false);
        }

        return await GetOrCreateStringIconSourceSlowPath(
            key,
            iconString,
            fontFamily,
            size,
            theme,
            dispatcherQueue,
            priority).ConfigureAwait(false);
    }

    private async Task<ManagedIconSourceResult> CreateFromStringCoreAsync(
        string iconString,
        string? fontFamily,
        Size size,
        ElementTheme theme,
        DispatcherQueue dispatcherQueue,
        DispatcherQueuePriority priority)
    {
        var parsed = IconStringParser.Parse(iconString);
        if (parsed.Kind is IconStringKind.NullSource)
        {
            return new(true, null, StopFallback: true);
        }

        if (parsed.Kind is IconStringKind.ImageSource && parsed.Uri is not null)
        {
            return new(true, await CreateFromUriAsync(parsed.Uri, size, dispatcherQueue, priority).ConfigureAwait(false));
        }

        if (parsed.Kind is IconStringKind.CmdPalIcon && parsed.CmdPalIcon is not null)
        {
            return new(true, await CreateFromCmdPalIconAsync(parsed.CmdPalIcon, size, theme, dispatcherQueue, priority).ConfigureAwait(false));
        }

        if (parsed.Kind is IconStringKind.ShellIcon && !string.IsNullOrEmpty(parsed.BinaryPath))
        {
            return new(true, await CreateFromBinaryIconAsync(parsed.BinaryPath, parsed.BinaryIconIndex, size, dispatcherQueue, priority).ConfigureAwait(false));
        }

        if (parsed.Kind is IconStringKind.Glyph)
        {
            return new(true, await CreateFromGlyphAsync(parsed.Glyph ?? iconString, parsed.FontFamily ?? fontFamily, size, dispatcherQueue, priority).ConfigureAwait(false));
        }

        return new(false, null);
    }

    private Task<ManagedIconSourceResult> GetOrCreateStringIconSourceSlowPath(
        StringIconCacheKey key,
        string iconString,
        string? fontFamily,
        Size size,
        ElementTheme theme,
        DispatcherQueue dispatcherQueue,
        DispatcherQueuePriority priority)
    {
        lock (_stringIconCacheLock)
        {
            if (_stringIconCache.TryGet(key, out var existingTask))
            {
                return existingTask;
            }

            var task = CreateFromStringCoreAsync(iconString, fontFamily, size, theme, dispatcherQueue, priority);
            RemoveStringIconCacheEntryOnFailure(task, key);
            _stringIconCache.Add(key, task);
            return task;
        }
    }

    public async Task<IconSource?> CreateFromStreamAsync(
        IRandomAccessStream stream,
        Uri? sourceUri,
        Size size,
        DispatcherQueue dispatcherQueue,
        DispatcherQueuePriority priority)
    {
        var iconContent = await AnalyzeIconContentAsync(stream, sourceUri).ConfigureAwait(false);
        return await RunOnDispatcherAsync(
            dispatcherQueue,
            priority,
            () => BuildImageIconSourceAsync(iconContent, size)).ConfigureAwait(false);
    }

    private async Task<IconSource?> CreateFromUriAsync(
        Uri iconUri,
        Size size,
        DispatcherQueue dispatcherQueue,
        DispatcherQueuePriority priority)
    {
        try
        {
            if (IsSvgByExtension(iconUri))
            {
                try
                {
                    return await RunOnDispatcherAsync(
                        dispatcherQueue,
                        priority,
                        async () =>
                        {
                            var svg = new SvgImageSource(iconUri);
                            ApplyRasterizeSize(svg, size);
                            return new ImageIconSource { ImageSource = svg };
                        })
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogFailedToLoadSvg(ex, iconUri);
                    return null;
                }
            }
            else
            {
                SoftwareBitmap? softwareBitmap = null;
                try
                {
                    IRandomAccessStreamWithContentType stream;
                    if (iconUri.IsFile)
                    {
                        stream = await RandomAccessStreamReference.CreateFromFile(await GetStorageFileFromUriAsync(iconUri)).OpenReadAsync();
                    }
                    else
                    {
                        stream = await RandomAccessStreamReference.CreateFromUri(iconUri).OpenReadAsync();
                    }

                    // Decode off the UI thread so only the lightweight
                    // SetBitmapAsync runs on the dispatcher.  The source
                    // stream is not needed after decoding — the SoftwareBitmap
                    // owns its pixel data independently.
                    using (stream)
                    {
                        softwareBitmap = await DecodeSoftwareBitmapAsync(stream, size).ConfigureAwait(false);
                    }

                    if (softwareBitmap == null)
                    {
                        return null;
                    }

                    return await RunOnDispatcherAsync(
                            dispatcherQueue,
                            priority,
                            async () =>
                            {
                                var source = new SoftwareBitmapSource();
                                await source.SetBitmapAsync(softwareBitmap);
                                SoftwareBitmapLifetime.AddOrUpdate(source, softwareBitmap);
                                return new ImageIconSource { ImageSource = source };
                            })
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    softwareBitmap?.Dispose();
                    LogFailedToDecodeRaster(ex, iconUri);
                    return null;
                }
            }
        }
        catch (Exception ex)
        {
            LogFailedToLoadIcon(ex, iconUri);
            return null;
        }
    }

    private async Task<IconSource?> CreateFromBinaryIconAsync(
        string binaryPath,
        int iconIndex,
        Size size,
        DispatcherQueue dispatcherQueue,
        DispatcherQueuePriority priority)
    {
        var iconSize = size.IsEmpty
            ? 256
            : Math.Max((int)Math.Ceiling(Math.Max(size.Width, size.Height)), 1);

        var softwareBitmap = await Task.Run(
            () => ShellIconBitmapExtractor.GetBinaryIconBitmap(binaryPath, iconIndex, iconSize))
            .ConfigureAwait(false);

        if (softwareBitmap == null)
        {
            return null;
        }

        try
        {
            return await RunOnDispatcherAsync(
                dispatcherQueue,
                priority,
                async () =>
                {
                    var bitmapSource = new SoftwareBitmapSource();
                    await bitmapSource.SetBitmapAsync(softwareBitmap);

                    // Do NOT dispose softwareBitmap here — the internal
                    // AsyncCopyToSurfaceTask may still be reading from it.
                    SoftwareBitmapLifetime.AddOrUpdate(bitmapSource, softwareBitmap);
                    return (IconSource)new ImageIconSource { ImageSource = bitmapSource };
                }).ConfigureAwait(false);
        }
        catch
        {
            softwareBitmap.Dispose();
            throw;
        }
    }

    private async Task<IconSource?> CreateFromCmdPalIconAsync(
        CmdPalIconDescriptorInfo descriptor,
        Size size,
        ElementTheme theme,
        DispatcherQueue dispatcherQueue,
        DispatcherQueuePriority priority)
    {
        foreach (var candidate in EnumerateCandidatesForTheme(descriptor, theme))
        {
            if (candidate.Kind is CmdPalIconSourceKind.Thumbnail)
            {
                var thumbnailSource = await CreateFromThumbnailAsync(candidate.Source, size, dispatcherQueue, priority).ConfigureAwait(false);
                if (thumbnailSource is not null)
                {
                    return thumbnailSource;
                }
            }
            else
            {
                var result = await CreateFromStringAsync(candidate.Source, fontFamily: null, size, theme, dispatcherQueue, priority).ConfigureAwait(false);
                if (result.StopFallback || result.Source is not null)
                {
                    return result.Source;
                }
            }
        }

        return null;
    }

    private Task<IconSource?> CreateFromGlyphAsync(
        string iconString,
        string? fontFamily,
        Size size,
        DispatcherQueue dispatcherQueue,
        DispatcherQueuePriority priority)
    {
        var glyphKind = FontIconGlyphClassifier.Classify(iconString);
        return RunOnDispatcherAsync(
            dispatcherQueue,
            priority,
            () => Task.FromResult<IconSource?>(BuildGlyphIconSource(iconString, fontFamily, glyphKind, size)));
    }

    private async Task<IconSource?> CreateFromThumbnailAsync(
        string path,
        Size size,
        DispatcherQueue dispatcherQueue,
        DispatcherQueuePriority priority)
    {
        var key = new ThumbnailIconCacheKey(path, size);
        if (_thumbnailIconCache.TryGet(key, out var cachedTask))
        {
            return await cachedTask.ConfigureAwait(false);
        }

        return await GetOrCreateThumbnailIconSlowPath(key, path, size, dispatcherQueue, priority).ConfigureAwait(false);
    }

    private async Task<IconSource?> CreateFromThumbnailCoreAsync(
        string path,
        Size size,
        DispatcherQueue dispatcherQueue,
        DispatcherQueuePriority priority)
    {
        try
        {
            var jumbo = Math.Max(size.Width, size.Height) >= 64;
            var stream = await ThumbnailHelper.GetThumbnail(path, jumbo).ConfigureAwait(false);
            if (stream is null)
            {
                return null;
            }

            return await CreateFromStreamAsync(stream, sourceUri: null, size, dispatcherQueue, priority).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogFailedToCreateThumbnailIcon(ex, path);
            return null;
        }
    }

    private Task<IconSource?> GetOrCreateThumbnailIconSlowPath(
        ThumbnailIconCacheKey key,
        string path,
        Size size,
        DispatcherQueue dispatcherQueue,
        DispatcherQueuePriority priority)
    {
        lock (_thumbnailIconCacheLock)
        {
            if (_thumbnailIconCache.TryGet(key, out var existingTask))
            {
                return existingTask;
            }

            var task = CreateFromThumbnailCoreAsync(path, size, dispatcherQueue, priority);
            RemoveThumbnailCacheEntryOnFailure(task, key);
            _thumbnailIconCache.Add(key, task);
            return task;
        }
    }

    private async Task<AnalyzedIconContent> AnalyzeIconContentAsync(
        IRandomAccessStream stream,
        Uri? sourceUri)
    {
        var isSvg = IsSvgByExtension(sourceUri) || SniffSvg(stream);
        LogStreamClassified(isSvg ? "svg" : "raster", sourceUri);
        if (isSvg)
        {
            using (stream)
            {
                var ownedSvgStream = await CopyToOwnedMemoryStreamAsync(stream).ConfigureAwait(false);
                return new(ownedSvgStream, sourceUri, IsSvg: true);
            }
        }

        using (stream)
        {
            var ownedRasterStream = await CopyToOwnedMemoryStreamAsync(stream).ConfigureAwait(false);
            return new(ownedRasterStream, sourceUri, IsSvg: false);
        }
    }

    private async Task<IconSource?> BuildImageIconSourceAsync(
        AnalyzedIconContent iconContent,
        Size size)
    {
        // Do NOT dispose iconContent.Stream here - both the SVG and raster
        // builders register the stream in a ConditionalWeakTable so it stays
        // alive as long as the ImageSource does.  Disposing it here would
        // invalidate the image source.
        var imageSource = iconContent.IsSvg
            ? await BuildSvgImageSourceAsync(iconContent.Stream, size)
            : await BuildRasterImageSourceAsync(iconContent.Stream, size);

        return new ImageIconSource { ImageSource = imageSource };
    }

    private static async Task<ImageSource> BuildSvgImageSourceAsync(
        IRandomAccessStream stream,
        Size size)
    {
        TryResetStreamPosition(stream);

        var svg = new SvgImageSource();
        ApplyRasterizeSize(svg, size);
        await svg.SetSourceAsync(stream);

        // Keep the stream alive for the lifetime of the SvgImageSource - disposing
        // it would invalidate the image.  The weak table lets both be GC'd together.
        SvgStreamLifetime.Add(svg, stream);
        return svg;
    }

    private static async Task<ImageSource> BuildRasterImageSourceAsync(IRandomAccessStream stream, Size size)
    {
        TryResetStreamPosition(stream);

        var bitmap = new BitmapImage();
        ApplyDecodeSize(bitmap, size);
        await bitmap.SetSourceAsync(stream);

        // Keep the stream alive for the lifetime of the BitmapImage - disposing
        // it would invalidate the image.  The weak table lets both be GC'd together.
        BitmapStreamLifetime.Add(bitmap, stream);
        return bitmap;
    }

    private static async Task<SoftwareBitmap?> DecodeSoftwareBitmapAsync(
        IRandomAccessStream sourceStream, Size size)
    {
        var decoder = await BitmapDecoder.CreateAsync(sourceStream);
        if (decoder.PixelWidth == 0 || decoder.PixelHeight == 0)
        {
            return null;
        }

        // Always create a BitmapTransform — passing null causes E_POINTER
        // inside the WinRT interop layer.
        var transform = new BitmapTransform
        {
            ScaledWidth = decoder.PixelWidth,
            ScaledHeight = decoder.PixelHeight,
            InterpolationMode = BitmapInterpolationMode.Fant,
        };

        if (!size.IsEmpty && size.Width > 0 && size.Height > 0)
        {
            var scale = Math.Min(size.Width / decoder.PixelWidth, size.Height / decoder.PixelHeight);
            scale = Math.Min(scale, 1.0); // do not upscale
            transform.ScaledWidth = Math.Max(1u, (uint)Math.Round(decoder.PixelWidth * scale));
            transform.ScaledHeight = Math.Max(1u, (uint)Math.Round(decoder.PixelHeight * scale));
        }

        return await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage);
    }

    private static async Task<StorageFile?> GetStorageFileFromUriAsync(Uri uri)
    {
        if (!uri.IsAbsoluteUri)
        {
            return null;
        }

        if (uri.IsFile)
        {
            return await StorageFile.GetFileFromPathAsync(uri.LocalPath);
        }

        if (uri.Scheme.Equals("ms-appx", StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals("ms-appdata", StringComparison.OrdinalIgnoreCase))
        {
            return await StorageFile.GetFileFromApplicationUriAsync(uri);
        }

        return null;
    }

    private static void ApplyDecodeSize(BitmapImage bitmap, Size size)
    {
        if (size.IsEmpty)
        {
            return;
        }

        if (size.Width >= size.Height)
        {
            bitmap.DecodePixelWidth = (int)size.Width;
        }
        else
        {
            bitmap.DecodePixelHeight = (int)size.Height;
        }
    }

    private static void ApplyRasterizeSize(SvgImageSource svg, Size size)
    {
        if (size.IsEmpty)
        {
            return;
        }

        if (size.Width >= size.Height)
        {
            svg.RasterizePixelWidth = size.Width;
        }
        else
        {
            svg.RasterizePixelHeight = size.Height;
        }
    }

    private static IconSource BuildGlyphIconSource(
        string iconString,
        string? fontFamily,
        FontIconGlyphKind glyphKind,
        Size size)
    {
        var fontIcon = new FontIconSource
        {
            FontFamily = new FontFamily(GetGlyphFontFamily(fontFamily, glyphKind)),
            FontSize = GetGlyphFontSize(size),
            Glyph = glyphKind == FontIconGlyphKind.Invalid ? "\u25CC" : iconString,
        };

        return fontIcon;
    }

    private static string GetGlyphFontFamily(string? requestedFontFamily, FontIconGlyphKind glyphKind) =>
        glyphKind switch
        {
            FontIconGlyphKind.Invalid => "Segoe UI",
            _ when !string.IsNullOrWhiteSpace(requestedFontFamily) => requestedFontFamily,
            FontIconGlyphKind.FluentSymbol => "Segoe Fluent Icons, Segoe MDL2 Assets",
            FontIconGlyphKind.Emoji => "Segoe UI Emoji, Segoe UI",
            _ => "Segoe UI",
        };

    private static double GetGlyphFontSize(Size size)
    {
        var fontSize = Math.Max(size.Width, size.Height);
        return fontSize > 0 ? fontSize : 8;
    }

    private static IEnumerable<CmdPalIconSourceCandidate> EnumerateCandidatesForTheme(
        CmdPalIconDescriptorInfo descriptor,
        ElementTheme theme)
    {
        if (descriptor.IsNil)
        {
            yield break;
        }

        IEnumerable<CmdPalIconSourceCandidate> themedCandidates = theme switch
        {
            ElementTheme.Dark => descriptor.DarkSources,
            ElementTheme.Light => descriptor.LightSources,
            _ => descriptor.Sources,
        };

        foreach (var candidate in themedCandidates)
        {
            yield return candidate;
        }

        if (theme is ElementTheme.Dark or ElementTheme.Light)
        {
            foreach (var candidate in descriptor.Sources)
            {
                yield return candidate;
            }
        }
        else
        {
            foreach (var candidate in descriptor.LightSources)
            {
                yield return candidate;
            }

            foreach (var candidate in descriptor.DarkSources)
            {
                yield return candidate;
            }
        }
    }

    private static bool IsSvgByExtension(Uri? uri)
    {
        var source = uri?.ToString();
        if (!string.IsNullOrEmpty(source) &&
            source.Contains(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool SniffSvg(IRandomAccessStream stream)
    {
        try
        {
            const int maxProbe = 1024;

            // Clone so the original stream's position is never touched.
            using var probeStream = stream.CloneStream();
            probeStream.Seek(0);

            using var reader = probeStream.AsStreamForRead();
            var toRead = (int)Math.Min(probeStream.Size, maxProbe);
            var buffer = new byte[toRead];
            var read = reader.Read(buffer, 0, toRead);

            if (read <= 0)
            {
                return false;
            }

            var head = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
            return head.Contains("<svg", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<InMemoryRandomAccessStream> CopyToOwnedMemoryStreamAsync(IRandomAccessStream stream)
    {
        TryResetStreamPosition(stream);

        var ownedStream = new InMemoryRandomAccessStream();
        using var outputStream = ownedStream.GetOutputStreamAt(0);

        const uint bufferSize = 16 * 1024;
        while (true)
        {
            var readBuffer = await stream.ReadAsync(
                new global::Windows.Storage.Streams.Buffer(bufferSize),
                bufferSize,
                InputStreamOptions.None);

            if (readBuffer.Length == 0)
            {
                break;
            }

            await outputStream.WriteAsync(readBuffer);
        }

        await outputStream.FlushAsync();
        ownedStream.Seek(0);
        return ownedStream;
    }

    private static Task<T> RunOnDispatcherAsync<T>(
        DispatcherQueue dispatcherQueue,
        DispatcherQueuePriority priority,
        Func<Task<T>> callback)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        var enqueued = dispatcherQueue.TryEnqueue(
            priority,
            async () =>
            {
                try
                {
                    var result = await callback();
                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

        if (!enqueued)
        {
            tcs.TrySetException(new InvalidOperationException("Failed to enqueue icon creation work on the UI thread."));
        }

        return tcs.Task;
    }

    private static void TryResetStreamPosition(IRandomAccessStream stream)
    {
        try
        {
            stream.Seek(0);
        }
        catch
        {
        }
    }

    private void RemoveStringIconCacheEntryOnFailure(Task<ManagedIconSourceResult> task, StringIconCacheKey key)
    {
        _ = task.ContinueWith(
            _ =>
            {
                lock (_stringIconCacheLock)
                {
                    _stringIconCache.TryRemove(key);
                }
            },
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private void RemoveThumbnailCacheEntryOnFailure(Task<IconSource?> task, ThumbnailIconCacheKey key)
    {
        _ = task.ContinueWith(
            _ =>
            {
                lock (_thumbnailIconCacheLock)
                {
                    _thumbnailIconCache.TryRemove(key);
                }
            },
            TaskContinuationOptions.OnlyOnFaulted);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Icon stream classified as {Route} for source {Source}")]
    private partial void LogStreamClassified(string route, Uri? source);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to create thumbnail-backed icon for path '{Path}'")]
    private partial void LogFailedToCreateThumbnailIcon(Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load SVG from {Uri}")]
    private partial void LogFailedToLoadSvg(Exception ex, Uri uri);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to decode raster icon from {Uri}")]
    private partial void LogFailedToDecodeRaster(Exception ex, Uri uri);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load icon from {Uri}")]
    private partial void LogFailedToLoadIcon(Exception ex, Uri uri);

    private readonly struct StringIconCacheKey : IEquatable<StringIconCacheKey>
    {
        private readonly string _iconString;
        private readonly string? _fontFamily;
        private readonly int _width;
        private readonly int _height;
        private readonly ElementTheme _theme;

        public StringIconCacheKey(string iconString, string? fontFamily, Size size, ElementTheme theme)
        {
            _iconString = iconString;
            _fontFamily = fontFamily;
            _width = EncodeSizeDimension(size.Width);
            _height = EncodeSizeDimension(size.Height);
            _theme = IconStringParser.RequiresTheme(iconString)
                ? theme
                : ElementTheme.Default;
        }

        public bool Equals(StringIconCacheKey other) =>
            _iconString == other._iconString &&
            _fontFamily == other._fontFamily &&
            _width == other._width &&
            _height == other._height &&
            _theme == other._theme;

        public override bool Equals(object? obj) => obj is StringIconCacheKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(_iconString, _fontFamily, _width, _height, _theme);
    }

    private readonly struct ThumbnailIconCacheKey : IEquatable<ThumbnailIconCacheKey>
    {
        private readonly string _path;
        private readonly int _width;
        private readonly int _height;

        public ThumbnailIconCacheKey(string path, Size size)
        {
            _path = path;
            _width = EncodeSizeDimension(size.Width);
            _height = EncodeSizeDimension(size.Height);
        }

        public bool Equals(ThumbnailIconCacheKey other) =>
            _path == other._path &&
            _width == other._width &&
            _height == other._height;

        public override bool Equals(object? obj) => obj is ThumbnailIconCacheKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(_path, _width, _height);
    }

    private static int EncodeSizeDimension(double value) => (int)(100 * Math.Round(value, 2));

    private sealed record AnalyzedIconContent(
        IRandomAccessStream Stream,
        Uri? SourceUri,
        bool IsSvg);
}
