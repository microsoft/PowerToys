// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public static class ThumbnailHelper
{
    private static readonly string[] ImageExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".bmp",
        ".tiff",
        ".ico",
    ];

    public static async Task<IRandomAccessStream?> GetThumbnail(string path, bool jumbo = false)
    {
        var extension = Path.GetExtension(path).ToLower(CultureInfo.InvariantCulture);
        var isImage = ImageExtensions.Contains(extension);
        if (isImage)
        {
            try
            {
                var result = await GetImageThumbnailAsync(path, jumbo);
                if (result is not null)
                {
                    return result;
                }
            }
            catch (Exception)
            {
                // ignore and fall back to icon
            }
        }

        try
        {
            return await GetFileIconStream(path, jumbo);
        }
        catch (Exception)
        {
            // ignore and return null
        }

        return null;
    }

    // these are windows constants and mangling them is goofy
#pragma warning disable SA1310 // Field names should not contain underscore
#pragma warning disable SA1306 // Field names should begin with lower-case letter
    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_SHELLICONSIZE = 0x000000004;
    private const uint SHGFI_SYSICONINDEX = 0x000004000;
    private const uint SHGFI_PIDL = 0x000000008;
    private const int SHIL_JUMBO = 4;
    private const int ILD_TRANSPARENT = 1;
#pragma warning restore SA1306 // Field names should begin with lower-case letter
#pragma warning restore SA1310 // Field names should not contain underscore

    // This will call DestroyIcon on the hIcon passed in.
    // Duplicate it if you need it again after this.
    private static MemoryStream GetMemoryStreamFromIcon(IntPtr hIcon)
    {
        var memoryStream = new MemoryStream();

        // Ensure disposing the icon before freeing the handle
        using (var icon = Icon.FromHandle(hIcon))
        {
            icon.ToBitmap().Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
        }

        // Clean up the unmanaged handle without risking a use-after-free.
        NativeMethods.DestroyIcon(hIcon);

        memoryStream.Position = 0;
        return memoryStream;
    }

    private static async Task<IRandomAccessStream?> GetFileIconStream(string filePath, bool jumbo)
    {
        return await TryExtractUsingPIDL(filePath, jumbo)
               ?? await GetFileIconStreamUsingFilePath(filePath, jumbo);
    }

    private static async Task<IRandomAccessStream?> TryExtractUsingPIDL(string shellPath, bool jumbo)
    {
        IntPtr pidl = 0;
        try
        {
            var hr = NativeMethods.SHParseDisplayName(shellPath, IntPtr.Zero, out pidl, 0, out _);
            if (hr != 0 || pidl == IntPtr.Zero)
            {
                return null;
            }

            nint hIcon = 0;
            if (jumbo)
            {
                hIcon = GetLargestIcon(pidl);
            }

            if (hIcon == 0)
            {
                var shinfo = default(NativeMethods.SHFILEINFO);
                var fileInfoResult = NativeMethods.SHGetFileInfo(pidl, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_SHELLICONSIZE | SHGFI_LARGEICON | SHGFI_PIDL);
                if (fileInfoResult != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
                {
                    hIcon = shinfo.hIcon;
                }
            }

            if (hIcon == 0)
            {
                return null;
            }

            return await FromHIconToStream(hIcon);
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            if (pidl != IntPtr.Zero)
            {
                NativeMethods.CoTaskMemFree(pidl);
            }
        }
    }

    private static async Task<IRandomAccessStream?> GetFileIconStreamUsingFilePath(string filePath, bool jumbo)
    {
        nint hIcon = 0;

        // If requested, look up the Jumbo icon
        if (jumbo)
        {
            hIcon = GetLargestIcon(filePath);
        }

        // If we didn't want the JUMBO icon, or didn't find it, fall back to
        // the normal icon lookup
        if (hIcon == 0)
        {
            var shinfo = default(NativeMethods.SHFILEINFO);

            var hr = NativeMethods.SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_SHELLICONSIZE);

            if (hr == 0 || shinfo.hIcon == 0)
            {
                return null;
            }

            hIcon = shinfo.hIcon;
        }

        if (hIcon == 0)
        {
            return null;
        }

        return await FromHIconToStream(hIcon);
    }

    private static async Task<IRandomAccessStream?> GetImageThumbnailAsync(string filePath, bool jumbo)
    {
        var file = await StorageFile.GetFileFromPathAsync(filePath);
        var thumbnail = await file.GetThumbnailAsync(
            jumbo ? ThumbnailMode.SingleItem : ThumbnailMode.ListView,
            jumbo ? 64u : 20u);
        return thumbnail;
    }

    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Win32 Naming/Private")]
    private static readonly Guid IID_IImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");

    private static nint GetLargestIcon(string path)
    {
        var shinfo = default(NativeMethods.SHFILEINFO);
        NativeMethods.SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_SYSICONINDEX);

        var hIcon = IntPtr.Zero;
        var iID_IImageList = IID_IImageList;

        if (NativeMethods.SHGetImageList(SHIL_JUMBO, ref iID_IImageList, out var imageListPtr) == 0 && imageListPtr != IntPtr.Zero)
        {
            hIcon = NativeMethods.ImageList_GetIcon(imageListPtr, shinfo.iIcon, ILD_TRANSPARENT);
        }

        return hIcon;
    }

    private static nint GetLargestIcon(IntPtr pidl)
    {
        var shinfo = default(NativeMethods.SHFILEINFO);
        NativeMethods.SHGetFileInfo(pidl, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_SYSICONINDEX | SHGFI_PIDL);

        var hIcon = IntPtr.Zero;
        var iID_IImageList = IID_IImageList;

        if (NativeMethods.SHGetImageList(SHIL_JUMBO, ref iID_IImageList, out var imageListPtr) == 0 && imageListPtr != IntPtr.Zero)
        {
            hIcon = NativeMethods.ImageList_GetIcon(imageListPtr, shinfo.iIcon, ILD_TRANSPARENT);
        }

        return hIcon;
    }

    /// <summary>
    /// Get an icon stream for a registered URI protocol (e.g. "mailto:", "http:", "steam:").
    /// </summary>
    public static async Task<IRandomAccessStream?> GetProtocolIconStream(string protocol, bool jumbo)
    {
        // 1) Ask the shell for the protocol's default icon "path,index"
        var iconRef = QueryProtocolIconReference(protocol);
        if (string.IsNullOrWhiteSpace(iconRef))
        {
            return null;
        }

        // Indirect reference:
        if (iconRef.StartsWith('@'))
        {
            if (TryLoadIndirectString(iconRef, out var expanded) && !string.IsNullOrWhiteSpace(expanded))
            {
                iconRef = expanded;
            }
        }

        // 2) Handle image files from a store app
        if (File.Exists(iconRef))
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(iconRef);
                var thumbnail = await file.GetThumbnailAsync(
                    jumbo ? ThumbnailMode.SingleItem : ThumbnailMode.ListView,
                    jumbo ? 64u : 20u);
                return thumbnail;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // 3) Parse "path,index" (index can be negative)
        if (!TryParseIconReference(iconRef, out var path, out var index))
        {
            return null;
        }

        // if it's and .exe and without a path, let's find on path:
        if (Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase) && !Path.IsPathRooted(path))
        {
            var paths = Environment.GetEnvironmentVariable("PATH")?.Split(';') ?? [];
            foreach (var p in paths)
            {
                var candidate = Path.Combine(p, path);
                if (File.Exists(candidate))
                {
                    path = candidate;
                    break;
                }
            }
        }

        // 3) Extract an HICON, preferably ~256px when jumbo==true
        var hIcon = ExtractIconHandle(path, index, jumbo);
        if (hIcon == 0)
        {
            return null;
        }

        return await FromHIconToStream(hIcon);
    }

    private static bool TryLoadIndirectString(string input, out string? output)
    {
        var outBuffer = new StringBuilder(1024);
        var hr = NativeMethods.SHLoadIndirectString(input, outBuffer, outBuffer.Capacity, IntPtr.Zero);
        if (hr == 0)
        {
            output = outBuffer.ToString();
            return !string.IsNullOrWhiteSpace(output);
        }

        output = null;
        return false;
    }

    private static async Task<IRandomAccessStream?> FromHIconToStream(IntPtr hIcon)
    {
        var stream = new InMemoryRandomAccessStream();

        using var memoryStream = GetMemoryStreamFromIcon(hIcon); // this will DestroyIcon hIcon
        using var outputStream = stream.GetOutputStreamAt(0);
        using var dataWriter = new DataWriter(outputStream);

        dataWriter.WriteBytes(memoryStream.ToArray());
        await dataWriter.StoreAsync();
        await dataWriter.FlushAsync();

        return stream;
    }

    private static string? QueryProtocolIconReference(string protocol)
    {
        // First try DefaultIcon (most widely populated for protocols)
        // If you want to try AppIconReference as a fallback, you can repeat with AssocStr.AppIconReference.
        var iconReference = AssocQueryStringSafe(NativeMethods.AssocStr.DefaultIcon, protocol);
        if (!string.IsNullOrWhiteSpace(iconReference))
        {
            return iconReference;
        }

        // Optional fallback – some registrations use AppIconReference:
        iconReference = AssocQueryStringSafe(NativeMethods.AssocStr.AppIconReference, protocol);
        return iconReference;

        static unsafe string? AssocQueryStringSafe(NativeMethods.AssocStr what, string protocol)
        {
            uint cch = 0;

            // First call: get required length (incl. null)
            _ = NativeMethods.AssocQueryStringW(NativeMethods.AssocF.IsProtocol, what, protocol, null, null, ref cch);
            if (cch == 0)
            {
                return null;
            }

            // Small buffers on stack; large on heap
            var span = cch <= 512 ? stackalloc char[(int)cch] : new char[(int)cch];

            fixed (char* p = span)
            {
                var hr = NativeMethods.AssocQueryStringW(NativeMethods.AssocF.IsProtocol, what, protocol, null, p, ref cch);
                if (hr != 0 || cch == 0)
                {
                    return null;
                }

                // cch includes the null terminator; slice it off
                var len = (int)cch - 1;
                if (len < 0)
                {
                    len = 0;
                }

                return new string(span.Slice(0, len));
            }
        }
    }

    private static bool TryParseIconReference(string iconRef, out string path, out int index)
    {
        // Typical shapes:
        //   "C:\Program Files\Outlook\OUTLOOK.EXE,-1"
        //   "shell32.dll,21"
        //   "\"C:\Some Path\app.dll\",-325"

        // If there's no comma, assume ",0"
        index = 0;
        path = iconRef.Trim();

        // Split only on the last comma so paths with commas still work
        var lastComma = path.LastIndexOf(',');
        if (lastComma >= 0)
        {
            var idxPart = path[(lastComma + 1)..].Trim();
            path = path[..lastComma].Trim();
            _ = int.TryParse(idxPart, out index);
        }

        // Trim quotes around path
        path = path.Trim('"');
        if (path.Length > 1 && path[0] == '"' && path[^1] == '"')
        {
            path = path.Substring(1, path.Length - 2);
        }

        // Basic sanity
        return !string.IsNullOrWhiteSpace(path);
    }

    private static nint ExtractIconHandle(string path, int index, bool jumbo)
    {
        // Request sizes: LOWORD=small, HIWORD=large.
        // Ask for 256 when jumbo, else fall back to 32/16.
        var small = jumbo ? 256 : 16;
        var large = jumbo ? 256 : 32;
        var sizeParam = (large << 16) | (small & 0xFFFF);

        var hr = NativeMethods.SHDefExtractIconW(path, index, 0, out var hLarge, out var hSmall, sizeParam);
        if (hr == 0 && hLarge != 0)
        {
            return hLarge;
        }

        if (hr == 0 && hSmall != 0)
        {
            return hSmall;
        }

        // Final fallback: try 32/16 explicitly in case the resource can’t upscale
        sizeParam = (32 << 16) | 16;
        hr = NativeMethods.SHDefExtractIconW(path, index, 0, out hLarge, out hSmall, sizeParam);
        if (hr == 0 && hLarge != 0)
        {
            return hLarge;
        }

        if (hr == 0 && hSmall != 0)
        {
            return hSmall;
        }

        return 0;
    }
}
