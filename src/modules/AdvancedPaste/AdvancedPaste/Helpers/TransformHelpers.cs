// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Models;
using ManagedCommon;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace AdvancedPaste.Helpers;

public static class TransformHelpers
{
    public static async Task<DataPackage> TransformAsync(PasteFormats format, DataPackageView clipboardData, CancellationToken cancellationToken, IProgress<double> progress)
    {
        return format switch
        {
            PasteFormats.PlainText => await ToPlainTextAsync(clipboardData),
            PasteFormats.Markdown => await ToMarkdownAsync(clipboardData),
            PasteFormats.Json => await ToJsonAsync(clipboardData),
            PasteFormats.ImageToText => await ImageToTextAsync(clipboardData, cancellationToken),
            PasteFormats.PasteAsTxtFile => await ToTxtFileAsync(clipboardData, cancellationToken),
            PasteFormats.PasteAsPngFile => await ToPngFileAsync(clipboardData, cancellationToken),
            PasteFormats.PasteAsHtmlFile => await ToHtmlFileAsync(clipboardData, cancellationToken),
            PasteFormats.TranscodeToMp3 => await TranscodeHelpers.TranscodeToMp3Async(clipboardData, cancellationToken, progress),
            PasteFormats.TranscodeToMp4 => await TranscodeHelpers.TranscodeToMp4Async(clipboardData, cancellationToken, progress),
            PasteFormats.KernelQuery => throw new ArgumentException($"Unsupported format {format}", nameof(format)),
            PasteFormats.CustomTextTransformation => throw new ArgumentException($"Unsupported format {format}", nameof(format)),
            _ => throw new ArgumentException($"Unknown value {format}", nameof(format)),
        };
    }

    private static async Task<DataPackage> ToPlainTextAsync(DataPackageView clipboardData)
    {
        Logger.LogTrace();
        return CreateDataPackageFromText(await clipboardData.GetTextOrEmptyAsync());
    }

    private static async Task<DataPackage> ToMarkdownAsync(DataPackageView clipboardData)
    {
        Logger.LogTrace();
        return CreateDataPackageFromText(await MarkdownHelper.ToMarkdownAsync(clipboardData));
    }

    private static async Task<DataPackage> ToJsonAsync(DataPackageView clipboardData)
    {
        Logger.LogTrace();
        return CreateDataPackageFromText(await JsonHelper.ToJsonFromXmlOrCsvAsync(clipboardData));
    }

    private static async Task<DataPackage> ImageToTextAsync(DataPackageView clipboardData, CancellationToken cancellationToken)
    {
        Logger.LogTrace();

        var bitmap = await clipboardData.GetImageContentAsync() ?? throw new ArgumentException("No image content found in clipboard", nameof(clipboardData));
        var text = await OcrHelpers.ExtractTextAsync(bitmap, cancellationToken);
        return CreateDataPackageFromText(text);
    }

    private static async Task<DataPackage> ToPngFileAsync(DataPackageView clipboardData, CancellationToken cancellationToken)
    {
        Logger.LogTrace();

        var clipboardBitmap = await clipboardData.GetImageContentAsync();

        using var pngStream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, pngStream);
        encoder.SetSoftwareBitmap(clipboardBitmap);
        await encoder.FlushAsync();

        return await CreateDataPackageFromFileContentAsync(pngStream.AsStreamForRead(), "png", cancellationToken);
    }

    private static async Task<DataPackage> ToTxtFileAsync(DataPackageView clipboardData, CancellationToken cancellationToken)
    {
        Logger.LogTrace();

        var text = await clipboardData.GetTextOrHtmlTextAsync();
        return await CreateDataPackageFromFileContentAsync(text, "txt", cancellationToken);
    }

    private static async Task<DataPackage> ToHtmlFileAsync(DataPackageView clipboardData, CancellationToken cancellationToken)
    {
        Logger.LogTrace();

        var cfHtml = await clipboardData.GetHtmlContentAsync();
        var html = RemoveHtmlMetadata(cfHtml);

        return await CreateDataPackageFromFileContentAsync(html, "html", cancellationToken);
    }

    /// <summary>
    /// Removes leading CF_HTML metadata from HTML clipboard data.
    /// See: https://learn.microsoft.com/en-us/windows/win32/dataxchg/html-clipboard-format
    /// </summary>
    private static string RemoveHtmlMetadata(string cfHtml)
    {
        int? GetIntTagValue(string tagName)
        {
            var tagNameWithColon = tagName + ":";
            int tagStartPos = cfHtml.IndexOf(tagNameWithColon, StringComparison.InvariantCulture);

            const int tagValueLength = 10;
            return tagStartPos != -1 && int.TryParse(cfHtml.AsSpan(tagStartPos + tagNameWithColon.Length, tagValueLength), CultureInfo.InvariantCulture, out int result) ? result : null;
        }

        var startFragmentIndex = GetIntTagValue("StartFragment");
        var endFragmentIndex = GetIntTagValue("EndFragment");

        return (startFragmentIndex == null || endFragmentIndex == null) ? cfHtml : cfHtml[startFragmentIndex.Value..endFragmentIndex.Value];
    }

    private static async Task<DataPackage> CreateDataPackageFromFileContentAsync(string data, string fileExtension, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(data))
        {
            throw new ArgumentException($"Empty value in {nameof(CreateDataPackageFromFileContentAsync)}", nameof(data));
        }

        var path = GetPasteAsFileTempFilePath(fileExtension);

        await File.WriteAllTextAsync(path, data, cancellationToken);
        return await DataPackageHelpers.CreateFromFileAsync(path);
    }

    private static async Task<DataPackage> CreateDataPackageFromFileContentAsync(Stream stream, string fileExtension, CancellationToken cancellationToken)
    {
        var path = GetPasteAsFileTempFilePath(fileExtension);

        using var fileStream = File.Create(path);
        await stream.CopyToAsync(fileStream, cancellationToken);

        return await DataPackageHelpers.CreateFromFileAsync(path);
    }

    private static string GetPasteAsFileTempFilePath(string fileExtension)
    {
        var prefix = ResourceLoaderInstance.ResourceLoader.GetString("PasteAsFile_FilePrefix");
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

        return Path.Combine(Path.GetTempPath(), $"{prefix}{timestamp}.{fileExtension}");
    }

    private static DataPackage CreateDataPackageFromText(string content) => DataPackageHelpers.CreateFromText(content);
}
