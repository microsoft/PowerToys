// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Helpers;

internal static partial class IconPathConverter
{
    private const string InvalidGlyph = "\u25CC";
    private const int DefaultBinaryIconSize = 256;

    public static PreparedIcon Prepare(
        string iconPath,
        string? fontFamily,
        int targetSize,
        ElementTheme theme = ElementTheme.Default)
    {
        if (string.IsNullOrEmpty(iconPath))
        {
            return PreparedIcon.Empty();
        }

        if (IconProtocolRegistry.Find(iconPath) is { } protocolProcessor)
        {
            try
            {
                return protocolProcessor.TryPrepareSynchronously(iconPath, targetSize, theme, out var protocolIcon)
                    ? protocolIcon
                    : PreparedIcon.Empty();
            }
            catch
            {
                // A claimed protocol must not fall through and become a glyph or URI.
                return PreparedIcon.Empty();
            }
        }

        if (IconPathParser.TryParseBinaryIconReference(iconPath, out var binaryIcon))
        {
            var bitmap = ExtractBinaryIcon(binaryIcon, targetSize >= 0 ? targetSize : DefaultBinaryIconSize);
            return PreparedIcon.FromBinary(bitmap);
        }

        // Font glyphs start outside ASCII, while every supported URI starts inside it.
        // Avoid using exception-based URI probing for the common Fluent glyph case.
        if (iconPath[0] < 128 && Uri.TryCreate(iconPath, UriKind.Absolute, out var uri))
        {
            var isSvg = uri.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
            return PreparedIcon.FromUri(uri, isSvg, targetSize);
        }

        var glyphKind = FontIconGlyphClassifier.Classify(iconPath);
        var glyph = glyphKind == FontIconGlyphKind.Invalid ? InvalidGlyph : iconPath;
        var family = FontIconGlyphClassifier.GetFontFamily(glyphKind, fontFamily);
        return PreparedIcon.FromGlyph(glyph, family, targetSize > 0 ? targetSize : 8);
    }

    public static Task<IconSource> CreateIconSourceAsync(PreparedIcon icon)
    {
        try
        {
            if (TryCreateIconSourceSynchronously(icon, out var iconSource))
            {
                return Task.FromResult(iconSource);
            }

            return CompleteIconSourceCreationAsync(icon);
        }
        catch (Exception exception)
        {
            // Preserve async exception delivery for invalid callers now that this
            // entry point no longer has an async state machine.
            return Task.FromException<IconSource>(exception);
        }
    }

    /// <summary>
    /// Attempts to create an icon source without an asynchronous bitmap transfer.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> only when generated SVG data or a populated binary icon
    /// must be transferred to its XAML image source asynchronously.
    /// </returns>
    public static bool TryCreateIconSourceSynchronously(
        PreparedIcon icon,
        [MaybeNullWhen(false)] out IconSource iconSource)
    {
        try
        {
            switch (icon.Kind)
            {
                case PreparedIconKind.BitmapUri:
                    var bitmap = new BitmapImage
                    {
                        DecodePixelWidth = icon.TargetSize > 0 ? icon.TargetSize : 0,
                        UriSource = icon.Uri!,
                    };
                    iconSource = new ImageIconSource { ImageSource = bitmap };
                    return true;

                case PreparedIconKind.SvgUri:
                    var svg = new SvgImageSource(icon.Uri!);
                    if (icon.TargetSize > 0)
                    {
                        svg.RasterizePixelWidth = icon.TargetSize;
                    }

                    iconSource = new ImageIconSource { ImageSource = svg };
                    return true;

                case PreparedIconKind.SvgData:
                    iconSource = null!;
                    return false;

                case PreparedIconKind.Glyph:
                    iconSource = new FontIconSource
                    {
                        FontFamily = new FontFamily(icon.FontFamily!),
                        FontSize = icon.TargetSize,
                        Glyph = icon.Glyph!,
                    };
                    return true;

                case PreparedIconKind.Binary:
                    if (icon.SoftwareBitmap is not null)
                    {
                        iconSource = null!;
                        return false;
                    }

                    iconSource = new ImageIconSource();
                    return true;

                default:
                    iconSource = CreateEmptyIconSource();
                    return true;
            }
        }
        catch
        {
            iconSource = icon.Kind is PreparedIconKind.Binary or PreparedIconKind.SvgData
                ? new ImageIconSource()
                : CreateEmptyIconSource();
            return true;
        }
    }

    /// <summary>
    /// Completes icon-source creation after <see cref="TryCreateIconSourceSynchronously"/>
    /// returned <see langword="false"/> for the same prepared icon.
    /// </summary>
    public static Task<IconSource> CompleteIconSourceCreationAsync(PreparedIcon icon)
    {
        if (icon.Kind == PreparedIconKind.Binary)
        {
            return icon.TakeSoftwareBitmap() is { } softwareBitmap
                ? CreateBinaryIconSourceAsync(softwareBitmap)
                : Task.FromResult<IconSource>(new ImageIconSource());
        }

        return icon.Kind == PreparedIconKind.SvgData
            ? CreateSvgIconSourceAsync(icon.SvgData!, icon.TargetSize)
            : Task.FromResult<IconSource>(CreateEmptyIconSource());
    }

    private static async Task<IconSource> CreateSvgIconSourceAsync(byte[] svgData, int targetSize)
    {
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream))
            {
                writer.WriteBytes(svgData);
                await writer.StoreAsync();
                writer.DetachStream();
            }

            stream.Seek(0);
            var svg = new SvgImageSource();
            if (targetSize > 0)
            {
                svg.RasterizePixelWidth = targetSize;
                svg.RasterizePixelHeight = targetSize;
            }

            await svg.SetSourceAsync(stream);
            return new ImageIconSource { ImageSource = svg };
        }
        catch
        {
            return new ImageIconSource();
        }
    }

    internal static async Task<IconSource> CreateBinaryIconSourceAsync(SoftwareBitmap softwareBitmap)
    {
        var ownershipTransferred = false;
        try
        {
            var bitmapSource = new SoftwareBitmapSource();
            try
            {
                await bitmapSource.SetBitmapAsync(softwareBitmap);

                var iconSource = new ImageIconSource { ImageSource = bitmapSource };

                // SetBitmapAsync can finish before WinUI's AsyncCopyToSurfaceTask.
                // Once XAML accepts the bitmap, explicitly closing either object can
                // fail-fast that later copy with RO_E_CLOSED. Release both through
                // their normal WinRT reference lifetimes instead.
                ownershipTransferred = true;
                return iconSource;
            }
            catch
            {
                // The source has not escaped to a caller or visual tree.
                bitmapSource.Dispose();
                throw;
            }
        }
        catch
        {
            return new ImageIconSource();
        }
        finally
        {
            if (!ownershipTransferred)
            {
                softwareBitmap.Dispose();
            }
        }
    }

    // Keep the empty value non-null. A virtualized ListView can crash when a
    // data-bound IconSourceElement alternates between null and non-null sources;
    // a BitmapIconSource with a null URI remains visually empty without crossing
    // that unstable boundary.
    private static BitmapIconSource CreateEmptyIconSource() => new() { UriSource = null };

    private static SoftwareBitmap? ExtractBinaryIcon(BinaryIconReference iconReference, int targetSize)
    {
        try
        {
            _ = NativeMethods.SHDefExtractIcon(
                iconReference.Path,
                iconReference.Index,
                0,
                out var iconHandle,
                0,
                (uint)targetSize);
            return HIconSoftwareBitmapConverter.ConvertAndDestroy(iconHandle);
        }
        catch
        {
            return null;
        }
    }

    internal sealed partial class PreparedIcon : IDisposable
    {
        private SoftwareBitmap? _softwareBitmap;

        private PreparedIcon(
            PreparedIconKind kind,
            Uri? uri = null,
            string? glyph = null,
            string? fontFamily = null,
            byte[]? svgData = null,
            SoftwareBitmap? softwareBitmap = null,
            int targetSize = 0)
        {
            Kind = kind;
            Uri = uri;
            Glyph = glyph;
            FontFamily = fontFamily;
            SvgData = svgData;
            _softwareBitmap = softwareBitmap;
            TargetSize = targetSize;
        }

        public PreparedIconKind Kind { get; }

        public Uri? Uri { get; }

        public string? Glyph { get; }

        public string? FontFamily { get; }

        public byte[]? SvgData { get; }

        public SoftwareBitmap? SoftwareBitmap => _softwareBitmap;

        public int TargetSize { get; }

        public static PreparedIcon Empty() => new(PreparedIconKind.Empty);

        public static PreparedIcon FromUri(Uri uri, bool isSvg, int targetSize) =>
            new(isSvg ? PreparedIconKind.SvgUri : PreparedIconKind.BitmapUri, uri: uri, targetSize: targetSize);

        public static PreparedIcon FromGlyph(string glyph, string fontFamily, int targetSize) =>
            new(PreparedIconKind.Glyph, glyph: glyph, fontFamily: fontFamily, targetSize: targetSize);

        public static PreparedIcon FromSvgData(byte[] svgData, int targetSize) =>
            new(PreparedIconKind.SvgData, svgData: svgData, targetSize: targetSize);

        public static PreparedIcon FromBinary(SoftwareBitmap? bitmap) =>
            new(PreparedIconKind.Binary, softwareBitmap: bitmap);

        // Asynchronous materialization takes ownership before the PreparedIcon is
        // disposed. On success, XAML owns the bitmap's remaining lifetime.
        public SoftwareBitmap? TakeSoftwareBitmap() =>
            Interlocked.Exchange(ref _softwareBitmap, null);

        public void Dispose()
        {
            Interlocked.Exchange(ref _softwareBitmap, null)?.Dispose();
        }
    }

    internal enum PreparedIconKind
    {
        Empty,
        BitmapUri,
        SvgUri,
        SvgData,
        Glyph,
        Binary,
    }

    private static partial class NativeMethods
    {
        [LibraryImport("shell32.dll", EntryPoint = "SHDefExtractIconW", StringMarshalling = StringMarshalling.Utf16)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial int SHDefExtractIcon(
            string iconFile,
            int iconIndex,
            uint flags,
            out nint largeIcon,
            nint smallIcon,
            uint iconSize);
    }
}
