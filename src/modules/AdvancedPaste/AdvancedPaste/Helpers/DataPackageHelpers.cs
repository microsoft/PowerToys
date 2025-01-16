// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using AdvancedPaste.Models;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Html;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace AdvancedPaste.Helpers;

internal static class DataPackageHelpers
{
    private static readonly Lazy<HashSet<string>> ImageFileTypes = new(GetImageFileTypes());

    private static readonly (string DataFormat, ClipboardFormat ClipboardFormat)[] DataFormats =
    [
        (StandardDataFormats.Text, ClipboardFormat.Text),
        (StandardDataFormats.Html, ClipboardFormat.Html),
        (StandardDataFormats.Bitmap, ClipboardFormat.Image),
    ];

    internal static DataPackage CreateFromText(string text)
    {
        DataPackage dataPackage = new();
        dataPackage.SetText(text);
        return dataPackage;
    }

    internal static async Task<DataPackage> CreateFromFileAsync(string fileName)
    {
        var storageFile = await StorageFile.GetFileFromPathAsync(fileName);

        DataPackage dataPackage = new();
        dataPackage.SetStorageItems([storageFile]);
        return dataPackage;
    }

    internal static async Task<ClipboardFormat> GetAvailableFormatsAsync(this DataPackageView dataPackageView)
    {
        var availableFormats = DataFormats.Aggregate(
            ClipboardFormat.None,
            (result, formatPair) => dataPackageView.Contains(formatPair.DataFormat) ? (result | formatPair.ClipboardFormat) : result);

        if (dataPackageView.Contains(StandardDataFormats.StorageItems))
        {
            var storageItems = await dataPackageView.GetStorageItemsAsync();

            if (storageItems.Count == 1 && storageItems.Single() is StorageFile file)
            {
                availableFormats |= ClipboardFormat.File;

                if (ImageFileTypes.Value.Contains(file.FileType))
                {
                    availableFormats |= ClipboardFormat.Image;
                }
            }
        }

        return FixFormatsForAI(availableFormats);
    }

    private static ClipboardFormat FixFormatsForAI(ClipboardFormat formats)
    {
        var result = formats;

        if (result.HasFlag(ClipboardFormat.File) && result != ClipboardFormat.File)
        {
            // Advertise the "generic" File format only if there is no other specific format available; confusing for AI otherwise.
            result &= ~ClipboardFormat.File;
        }

        if (result == (ClipboardFormat.Image | ClipboardFormat.Html))
        {
            // The Windows Photo application advertises Image and Html when copying an image; this Html format is not easily usable and is confusing for AI.
            result &= ~ClipboardFormat.Html;
        }

        return result;
    }

    internal static async Task<bool> HasUsableDataAsync(this DataPackageView dataPackageView)
    {
        var availableFormats = await GetAvailableFormatsAsync(dataPackageView);

        return availableFormats == ClipboardFormat.Text ? !string.IsNullOrEmpty(await dataPackageView.GetTextAsync()) : availableFormats != ClipboardFormat.None;
    }

    internal static async Task<string> GetTextOrEmptyAsync(this DataPackageView dataPackageView) =>
        dataPackageView.Contains(StandardDataFormats.Text) ? await dataPackageView.GetTextAsync() : string.Empty;

    internal static async Task<string> GetTextOrHtmlTextAsync(this DataPackageView dataPackageView)
    {
        if (dataPackageView.Contains(StandardDataFormats.Text))
        {
            return await dataPackageView.GetTextAsync();
        }
        else if (dataPackageView.Contains(StandardDataFormats.Html))
        {
            var html = await dataPackageView.GetHtmlFormatAsync();
            return HtmlUtilities.ConvertToText(html);
        }
        else
        {
            return string.Empty;
        }
    }

    internal static async Task<string> GetHtmlContentAsync(this DataPackageView dataPackageView) =>
        dataPackageView.Contains(StandardDataFormats.Html) ? await dataPackageView.GetHtmlFormatAsync() : string.Empty;

    internal static async Task<SoftwareBitmap> GetImageContentAsync(this DataPackageView dataPackageView)
    {
        using var stream = await dataPackageView.GetImageStreamAsync();
        if (stream != null)
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);
            return await decoder.GetSoftwareBitmapAsync();
        }

        return null;
    }

    private static async Task<IRandomAccessStream> GetImageStreamAsync(this DataPackageView dataPackageView)
    {
        if (dataPackageView.Contains(StandardDataFormats.StorageItems))
        {
            var storageItems = await dataPackageView.GetStorageItemsAsync();
            var file = storageItems.Count == 1 ? storageItems[0] as StorageFile : null;
            if (file != null)
            {
                return await file.OpenReadAsync();
            }
        }

        if (dataPackageView.Contains(StandardDataFormats.Bitmap))
        {
            var bitmap = await dataPackageView.GetBitmapAsync();
            return await bitmap.OpenReadAsync();
        }

        return null;
    }

    private static HashSet<string> GetImageFileTypes() =>
               BitmapDecoder.GetDecoderInformationEnumerator()
                            .SelectMany(di => di.FileExtensions)
                            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
}
