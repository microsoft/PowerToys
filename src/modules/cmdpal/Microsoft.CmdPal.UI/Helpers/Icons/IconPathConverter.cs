// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using DrawingIcon = System.Drawing.Icon;
using DrawingImageLockMode = System.Drawing.Imaging.ImageLockMode;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingRectangle = System.Drawing.Rectangle;

namespace Microsoft.CmdPal.UI.Helpers;

internal static partial class IconPathConverter
{
    private const string InvalidGlyph = "\u25CC";
    private const int DefaultBinaryIconSize = 256;

    public static PreparedIcon Prepare(string iconPath, string? fontFamily, int targetSize)
    {
        if (string.IsNullOrEmpty(iconPath))
        {
            return PreparedIcon.Empty();
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

    public static async Task<IconSource> CreateIconSourceAsync(PreparedIcon icon)
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
                    return new ImageIconSource { ImageSource = bitmap };

                case PreparedIconKind.SvgUri:
                    var svg = new SvgImageSource(icon.Uri!);
                    if (icon.TargetSize > 0)
                    {
                        svg.RasterizePixelWidth = icon.TargetSize;
                    }

                    return new ImageIconSource { ImageSource = svg };

                case PreparedIconKind.Glyph:
                    return new FontIconSource
                    {
                        FontFamily = new FontFamily(icon.FontFamily!),
                        FontSize = icon.TargetSize,
                        Glyph = icon.Glyph!,
                    };

                case PreparedIconKind.Binary:
                    var softwareBitmap = icon.TakeSoftwareBitmap();
                    if (softwareBitmap is null)
                    {
                        return new ImageIconSource();
                    }

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
                    finally
                    {
                        if (!ownershipTransferred)
                        {
                            softwareBitmap.Dispose();
                        }
                    }

                default:
                    return CreateEmptyIconSource();
            }
        }
        catch
        {
            return icon.Kind == PreparedIconKind.Binary
                ? new ImageIconSource()
                : CreateEmptyIconSource();
        }
    }

    // Keep the empty value non-null. A virtualized ListView can crash when a
    // data-bound IconSourceElement alternates between null and non-null sources;
    // a BitmapIconSource with a null URI remains visually empty without crossing
    // that unstable boundary.
    private static BitmapIconSource CreateEmptyIconSource() => new() { UriSource = null };

    private static SoftwareBitmap? ExtractBinaryIcon(BinaryIconReference iconReference, int targetSize)
    {
        nint iconHandle = 0;
        try
        {
            _ = NativeMethods.SHDefExtractIcon(
                iconReference.Path,
                iconReference.Index,
                0,
                out iconHandle,
                0,
                (uint)targetSize);
            if (iconHandle == 0)
            {
                return null;
            }

            using var icon = DrawingIcon.FromHandle(iconHandle);
            using var sourceBitmap = icon.ToBitmap();
            using var bitmap = sourceBitmap.Clone(
                new DrawingRectangle(0, 0, sourceBitmap.Width, sourceBitmap.Height),
                DrawingPixelFormat.Format32bppPArgb);

            var rectangle = new DrawingRectangle(0, 0, bitmap.Width, bitmap.Height);
            var bitmapData = bitmap.LockBits(rectangle, DrawingImageLockMode.ReadOnly, DrawingPixelFormat.Format32bppPArgb);
            try
            {
                var bytesPerRow = checked(bitmap.Width * 4);
                var pixelBufferLength = checked(bytesPerRow * bitmap.Height);
                var pixels = ArrayPool<byte>.Shared.Rent(pixelBufferLength);
                try
                {
                    if (bitmapData.Stride == bytesPerRow)
                    {
                        Marshal.Copy(bitmapData.Scan0, pixels, 0, pixelBufferLength);
                    }
                    else
                    {
                        for (var row = 0; row < bitmap.Height; row++)
                        {
                            var source = nint.Add(bitmapData.Scan0, row * bitmapData.Stride);
                            Marshal.Copy(source, pixels, row * bytesPerRow, bytesPerRow);
                        }
                    }

                    var softwareBitmap = new SoftwareBitmap(
                        BitmapPixelFormat.Bgra8,
                        bitmap.Width,
                        bitmap.Height,
                        BitmapAlphaMode.Premultiplied);
                    try
                    {
                        softwareBitmap.CopyFromBuffer(pixels.AsBuffer(0, pixelBufferLength));
                        return softwareBitmap;
                    }
                    catch
                    {
                        softwareBitmap.Dispose();
                        throw;
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(pixels);
                }
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            if (iconHandle != 0)
            {
                _ = NativeMethods.DestroyIcon(iconHandle);
            }
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
            SoftwareBitmap? softwareBitmap = null,
            int targetSize = 0)
        {
            Kind = kind;
            Uri = uri;
            Glyph = glyph;
            FontFamily = fontFamily;
            _softwareBitmap = softwareBitmap;
            TargetSize = targetSize;
        }

        public PreparedIconKind Kind { get; }

        public Uri? Uri { get; }

        public string? Glyph { get; }

        public string? FontFamily { get; }

        public SoftwareBitmap? SoftwareBitmap => _softwareBitmap;

        public int TargetSize { get; }

        public static PreparedIcon Empty() => new(PreparedIconKind.Empty);

        public static PreparedIcon FromUri(Uri uri, bool isSvg, int targetSize) =>
            new(isSvg ? PreparedIconKind.SvgUri : PreparedIconKind.BitmapUri, uri: uri, targetSize: targetSize);

        public static PreparedIcon FromGlyph(string glyph, string fontFamily, int targetSize) =>
            new(PreparedIconKind.Glyph, glyph: glyph, fontFamily: fontFamily, targetSize: targetSize);

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

        [LibraryImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial int DestroyIcon(nint icon);
    }
}
