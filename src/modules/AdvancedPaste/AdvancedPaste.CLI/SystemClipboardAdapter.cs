// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

using FormsClipboard = System.Windows.Forms.Clipboard;
using FormsDataFormats = System.Windows.Forms.DataFormats;
using FormsDataObject = System.Windows.Forms.DataObject;
using FormsIDataObject = System.Windows.Forms.IDataObject;

namespace AdvancedPaste.Cli;

internal sealed class SystemClipboardAdapter : IClipboardAdapter
{
    public DataPackageView Read()
        => RunOnSta(() => CreateDataPackageView(FormsClipboard.GetDataObject()));

    public void Write(DataPackage content)
        => RunOnSta(() =>
        {
            var dataObject = CreateDataObjectAsync(content.GetView()).GetAwaiter().GetResult();
            FormsClipboard.SetDataObject(dataObject, copy: true, retryTimes: 5, retryDelay: 100);
            return true;
        });

    internal static DataPackageView CreateDataPackageView(FormsIDataObject? dataObject)
    {
        var package = new DataPackage();
        if (dataObject is null)
        {
            return package.GetView();
        }

        if (dataObject.GetDataPresent(FormsDataFormats.UnicodeText, autoConvert: true) &&
            dataObject.GetData(FormsDataFormats.UnicodeText, autoConvert: true) is string text)
        {
            package.SetText(text);
        }

        if (dataObject.GetDataPresent(FormsDataFormats.Html, autoConvert: false) &&
            dataObject.GetData(FormsDataFormats.Html, autoConvert: false) is string html)
        {
            package.SetHtmlFormat(html);
        }

        if (dataObject.GetDataPresent(FormsDataFormats.FileDrop, autoConvert: false) &&
            dataObject.GetData(FormsDataFormats.FileDrop, autoConvert: false) is string[] paths)
        {
            var storageItems = GetStorageItems(paths);
            if (storageItems.Count > 0)
            {
                package.SetStorageItems(storageItems);
            }
        }

        if (dataObject.GetDataPresent(FormsDataFormats.Bitmap, autoConvert: true) &&
            dataObject.GetData(FormsDataFormats.Bitmap, autoConvert: true) is Image image)
        {
            using var stream = new MemoryStream();
            image.Save(stream, ImageFormat.Png);
            var randomAccessStream = new InMemoryRandomAccessStream();
            randomAccessStream.WriteAsync(stream.ToArray().AsBuffer()).AsTask().GetAwaiter().GetResult();
            randomAccessStream.Seek(0);
            package.SetBitmap(RandomAccessStreamReference.CreateFromStream(randomAccessStream));
        }

        return package.GetView();
    }

    internal static async Task<FormsDataObject> CreateDataObjectAsync(DataPackageView content)
    {
        var dataObject = new FormsDataObject();
        if (content.Contains(StandardDataFormats.Text))
        {
            dataObject.SetText(await content.GetTextAsync());
        }

        if (content.Contains(StandardDataFormats.Html))
        {
            dataObject.SetData(FormsDataFormats.Html, autoConvert: false, await content.GetHtmlFormatAsync());
        }

        if (content.Contains(StandardDataFormats.StorageItems))
        {
            var paths = new StringCollection();
            paths.AddRange((await content.GetStorageItemsAsync()).Select(item => item.Path).ToArray());
            dataObject.SetFileDropList(paths);
        }

        if (content.Contains(StandardDataFormats.Bitmap))
        {
            using var randomAccessStream = await (await content.GetBitmapAsync()).OpenReadAsync();
            using var stream = randomAccessStream.AsStreamForRead();
            using var image = Image.FromStream(stream);
            dataObject.SetImage(new Bitmap(image));
        }

        return dataObject;
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

    // Async entry points can resume on an MTA thread; OLE clipboard calls always need STA.
    internal static T RunOnSta<T>(Func<T> operation)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return operation();
        }

        T? result = default;
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = operation();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
        return result!;
    }
}
