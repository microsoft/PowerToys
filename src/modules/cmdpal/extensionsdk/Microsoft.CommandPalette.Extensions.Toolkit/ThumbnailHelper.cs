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

    public static Task<IRandomAccessStream?> GetThumbnail(string path)
    {
        var extension = Path.GetExtension(path).ToLower(CultureInfo.InvariantCulture);
        try
        {
            if (ImageExtensions.Contains(extension))
            {
                return GetImageThumbnailAsync(path);
            }
            else
            {
                return GetFileIconStream(path);
            }
        }
        catch (Exception)
        {
        }

        return Task.FromResult<IRandomAccessStream?>(null);
    }

    private const uint SHGFIICON = 0x000000100;
    private const uint SHGFILARGEICON = 0x000000000;

    private static async Task<IRandomAccessStream?> GetFileIconStream(string filePath)
    {
        var shinfo = default(NativeMethods.SHFILEINFO);
        var hr = NativeMethods.SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFIICON | SHGFILARGEICON);

        if (hr == 0 || shinfo.hIcon == 0)
        {
            return null;
        }

        using var icon = Icon.FromHandle(shinfo.hIcon);
        var stream = new InMemoryRandomAccessStream();
        using (var memoryStream = new MemoryStream())
        {
            icon.ToBitmap().Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
            memoryStream.Position = 0;

            using var outputStream = stream.GetOutputStreamAt(0);
            using var dataWriter = new DataWriter(outputStream);
            dataWriter.WriteBytes(memoryStream.ToArray());

            // dataWriter.StoreAsync().GetAwaiter().GetResult();
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
}
