// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using AdvancedPaste.Models;
using ManagedCommon;
using Microsoft.Win32;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Html;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace AdvancedPaste.Helpers;

internal static class DataPackageHelpers
{
    private static readonly (string DataFormat, ClipboardFormat ClipboardFormat)[] DataFormats =
    [
        (StandardDataFormats.Text, ClipboardFormat.Text),
        (StandardDataFormats.Html, ClipboardFormat.Html),
        (StandardDataFormats.Bitmap, ClipboardFormat.Image),
    ];

    private static readonly Lazy<(ClipboardFormat Format, HashSet<string> FileTypes)[]> SupportedFileTypes =
        new(() =>
        [
            (ClipboardFormat.Image, GetImageFileTypes()),
            (ClipboardFormat.Audio, GetMediaFileTypes("audio")),
            (ClipboardFormat.Video, GetMediaFileTypes("video")),
        ]);

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

                foreach (var (format, fileTypes) in SupportedFileTypes.Value)
                {
                    if (fileTypes.Contains(file.FileType))
                    {
                        availableFormats |= format;
                    }
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

    internal static async Task TryCleanupAfterDelayAsync(this DataPackageView dataPackageView, TimeSpan delay)
    {
        try
        {
            var tempFile = await GetSingleTempFileOrNullAsync(dataPackageView);

            if (tempFile != null)
            {
                await Task.Delay(delay);

                Logger.LogDebug($"Cleaning up temporary file with extension [{tempFile.Extension}] from data package after delay");

                tempFile.Delete();
                if (NormalizeDirectoryPath(tempFile.Directory?.Parent?.FullName) == NormalizeDirectoryPath(Path.GetTempPath()))
                {
                    tempFile.Directory?.Delete();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to clean up temporary files", ex);
        }
    }

    private static async Task<FileInfo> GetSingleTempFileOrNullAsync(this DataPackageView dataPackageView)
    {
        if (!dataPackageView.Contains(StandardDataFormats.StorageItems))
        {
            return null;
        }

        var storageItems = await dataPackageView.GetStorageItemsAsync();

        if (storageItems.Count != 1 || storageItems.Single() is not StorageFile file)
        {
            return null;
        }

        FileInfo fileInfo = new(file.Path);
        var tempPathDirectory = NormalizeDirectoryPath(Path.GetTempPath());

        var directoryPaths = new[] { fileInfo.Directory, fileInfo.Directory?.Parent }
                            .Where(directory => directory != null)
                            .Select(directory => NormalizeDirectoryPath(directory.FullName));

        return directoryPaths.Contains(NormalizeDirectoryPath(Path.GetTempPath())) ? fileInfo : null;
    }

    private static string NormalizeDirectoryPath(string path) =>
        Path.GetFullPath(new Uri(path).LocalPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();

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

    private static HashSet<string> GetMediaFileTypes(string mediaKind)
    {
        static string AssocQueryString(NativeMethods.AssocStr assocStr, string extension)
        {
            uint pcchOut = 0;

            NativeMethods.AssocQueryString(NativeMethods.AssocF.None, assocStr, extension, null, null, ref pcchOut);

            StringBuilder pszOut = new((int)pcchOut);
            var hResult = NativeMethods.AssocQueryString(NativeMethods.AssocF.None, assocStr, extension, null, pszOut, ref pcchOut);
            return hResult == NativeMethods.HResult.Ok ? pszOut.ToString() : string.Empty;
        }

        var comparison = StringComparison.OrdinalIgnoreCase;
        var extensions = from extension in Registry.ClassesRoot.GetSubKeyNames()
                         where extension.StartsWith('.')
                         where AssocQueryString(NativeMethods.AssocStr.PerceivedType, extension).Equals(mediaKind, comparison) ||
                               AssocQueryString(NativeMethods.AssocStr.ContentType, extension).StartsWith($"{mediaKind}/", comparison)
                         select extension;

        return extensions.ToHashSet(StringComparer.InvariantCultureIgnoreCase);
    }
}
