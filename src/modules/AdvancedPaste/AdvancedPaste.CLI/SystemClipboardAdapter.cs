// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;

using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace AdvancedPaste.Cli;

internal sealed class SystemClipboardAdapter : IClipboardAdapter
{
    public DataPackageView Read()
        => CreateDataPackageView(NativeClipboard.Read());

    public void Write(DataPackage content)
        => NativeClipboard.Write(CreateClipboardSnapshotAsync(content.GetView()).GetAwaiter().GetResult());

    internal static DataPackageView CreateDataPackageView(ClipboardSnapshot snapshot)
    {
        var package = new DataPackage();
        if (snapshot.Text is string text)
        {
            package.SetText(text);
        }

        if (snapshot.Html is string html)
        {
            package.SetHtmlFormat(html);
        }

        var storageItems = GetStorageItems(snapshot.FilePaths);
        if (storageItems.Count > 0)
        {
            package.SetStorageItems(storageItems);
        }

        if (snapshot.BitmapBytes is byte[] bitmapBytes)
        {
            var randomAccessStream = new InMemoryRandomAccessStream();
            randomAccessStream.WriteAsync(bitmapBytes.AsBuffer()).AsTask().GetAwaiter().GetResult();
            randomAccessStream.Seek(0);
            package.SetBitmap(RandomAccessStreamReference.CreateFromStream(randomAccessStream));
        }

        return package.GetView();
    }

    internal static async Task<ClipboardSnapshot> CreateClipboardSnapshotAsync(DataPackageView content)
    {
        var text = content.Contains(StandardDataFormats.Text)
            ? await content.GetTextAsync().AsTask().ConfigureAwait(false)
            : null;
        var html = content.Contains(StandardDataFormats.Html)
            ? await content.GetHtmlFormatAsync().AsTask().ConfigureAwait(false)
            : null;
        string[] paths = content.Contains(StandardDataFormats.StorageItems)
            ? (await content.GetStorageItemsAsync().AsTask().ConfigureAwait(false)).Select(item => item.Path).ToArray()
            : [];

        byte[]? bitmapBytes = null;
        if (content.Contains(StandardDataFormats.Bitmap))
        {
            var reference = await content.GetBitmapAsync().AsTask().ConfigureAwait(false);
            using var randomAccessStream = await reference.OpenReadAsync().AsTask().ConfigureAwait(false);
            using var stream = randomAccessStream.AsStreamForRead();
            using var bitmap = new MemoryStream();
            await stream.CopyToAsync(bitmap).ConfigureAwait(false);
            bitmapBytes = bitmap.ToArray();
        }

        return new ClipboardSnapshot(text, html, paths, bitmapBytes);
    }

    private static IReadOnlyList<IStorageItem> GetStorageItems(IEnumerable<string> paths)
    {
        var storageItems = new List<IStorageItem>();
        foreach (var path in paths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    storageItems.Add(StorageFolder.GetFolderFromPathAsync(path).AsTask().GetAwaiter().GetResult());
                }
                else if (File.Exists(path))
                {
                    storageItems.Add(StorageFile.GetFileFromPathAsync(path).AsTask().GetAwaiter().GetResult());
                }
            }
            catch (FileNotFoundException)
            {
                // The item can disappear between the existence check and WinRT resolution.
            }
            catch (DirectoryNotFoundException)
            {
                // The item can disappear between the existence check and WinRT resolution.
            }
        }

        return storageItems;
    }
}
