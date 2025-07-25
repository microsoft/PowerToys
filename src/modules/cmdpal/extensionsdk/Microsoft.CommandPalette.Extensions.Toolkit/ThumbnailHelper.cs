// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public class ThumbnailHelper
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

    public static Task<IRandomAccessStream?> GetThumbnail(string path, bool jumbo = false)
    {
        var extension = Path.GetExtension(path).ToLower(CultureInfo.InvariantCulture);
        try
        {
            return ImageExtensions.Contains(extension) ? GetImageThumbnailAsync(path) : GetFileIconStream(path, jumbo);
        }
        catch (Exception)
        {
        }

        return Task.FromResult<IRandomAccessStream?>(null);
    }

    // these are windows constants and mangling them is goofy
#pragma warning disable SA1310 // Field names should not contain underscore
#pragma warning disable SA1306 // Field names should begin with lower-case letter
    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_SHELLICONSIZE = 0x000000004;
    private const int SHGFI_SYSICONINDEX = 0x000004000;
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

        var stream = new InMemoryRandomAccessStream();

        using var memoryStream = GetMemoryStreamFromIcon(hIcon); // this will DestroyIcon hIcon
        using var outputStream = stream.GetOutputStreamAt(0);
        using (var dataWriter = new DataWriter(outputStream))
        {
            dataWriter.WriteBytes(memoryStream.ToArray());
            await dataWriter.StoreAsync();
            await dataWriter.FlushAsync();
        }

        return stream;
    }

    private static async Task<IRandomAccessStream?> GetImageThumbnailAsync(string filePath)
    {
        var file = await StorageFile.GetFileFromPathAsync(filePath);
        var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.PicturesView);
        return thumbnail;
    }

    private static nint GetLargestIcon(string path)
    {
        var shinfo = default(NativeMethods.SHFILEINFO);
        NativeMethods.SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_SYSICONINDEX);

        var hIcon = IntPtr.Zero;
        var iID_IImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");

        if (NativeMethods.SHGetImageList(SHIL_JUMBO, ref iID_IImageList, out nint imageListPtr) == 0 && imageListPtr != IntPtr.Zero)
        {
            hIcon = NativeMethods.ImageList_GetIcon(imageListPtr, shinfo.iIcon, ILD_TRANSPARENT);
        }

        return hIcon;
    }
}
