// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace AdvancedPaste.Services;

public sealed class PasteFormatExecutor(AICompletionsHelper aiHelper) : IPasteFormatExecutor
{
    private readonly AICompletionsHelper _aiHelper = aiHelper;

    public async Task<string> ExecutePasteFormatAsync(PasteFormat pasteFormat, PasteActionSource source)
    {
        if (!pasteFormat.IsEnabled)
        {
            return null;
        }

        WriteTelemetry(pasteFormat.Format, source);

        return await ExecutePasteFormatCoreAsync(pasteFormat, Clipboard.GetContent());
    }

    private async Task<string> ExecutePasteFormatCoreAsync(PasteFormat pasteFormat, DataPackageView clipboardData)
    {
        switch (pasteFormat.Format)
        {
            case PasteFormats.PlainText:
                ToPlainText(clipboardData);
                return null;

            case PasteFormats.Markdown:
                ToMarkdown(clipboardData);
                return null;

            case PasteFormats.Json:
                ToJson(clipboardData);
                return null;

            case PasteFormats.ImageToText:
                await ImageToTextAsync(clipboardData);
                return null;

            case PasteFormats.PasteAsTxtFile:
                await ToTxtFileAsync(clipboardData);
                return null;

            case PasteFormats.PasteAsPngFile:
                await ToPngFileAsync(clipboardData);
                return null;

            case PasteFormats.PasteAsHtmlFile:
                await ToHtmlFileAsync(clipboardData);
                return null;

            case PasteFormats.Custom:
                return await ToCustomAsync(pasteFormat.Prompt, clipboardData);

            default:
                throw new ArgumentException($"Unknown paste format {pasteFormat.Format}", nameof(pasteFormat));
        }
    }

    private static void WriteTelemetry(PasteFormats format, PasteActionSource source)
    {
        switch (source)
        {
            case PasteActionSource.ContextMenu:
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteFormatClickedEvent(format));
                break;

            case PasteActionSource.InAppKeyboardShortcut:
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteInAppKeyboardShortcutEvent(format));
                break;

            case PasteActionSource.GlobalKeyboardShortcut:
            case PasteActionSource.PromptBox:
                break; // no telemetry yet for these sources

            default:
                throw new ArgumentOutOfRangeException(nameof(format));
        }
    }

    private void ToPlainText(DataPackageView clipboardData)
    {
        Logger.LogTrace();
        SetClipboardTextContent(MarkdownHelper.PasteAsPlainTextFromClipboard(clipboardData));
    }

    private void ToMarkdown(DataPackageView clipboardData)
    {
        Logger.LogTrace();
        SetClipboardTextContent(MarkdownHelper.ToMarkdown(clipboardData));
    }

    private void ToJson(DataPackageView clipboardData)
    {
        Logger.LogTrace();
        SetClipboardTextContent(JsonHelper.ToJsonFromXmlOrCsv(clipboardData));
    }

    private async Task ImageToTextAsync(DataPackageView clipboardData)
    {
        Logger.LogTrace();
        SoftwareBitmap bitmap = null;
        try
        {
            bitmap = await ClipboardHelper.GetClipboardImageContentAsync(clipboardData);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error getting image content from clipboard.", ex);
            SetClipboardTextContent(string.Empty);
            return;
        }

        var text = await OcrHelpers.ExtractTextAsync(bitmap);
        SetClipboardTextContent(text);
    }

    private async Task ToPngFileAsync(DataPackageView clipboardData)
    {
        Logger.LogTrace();

        var clipboardBitmap = await ClipboardHelper.GetClipboardImageContentAsync(clipboardData);

        using var pngStream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, pngStream);
        encoder.SetSoftwareBitmap(clipboardBitmap);
        await encoder.FlushAsync();

        await SetClipboardFileContentAsync(pngStream.AsStreamForRead(), "png");
    }

    private async Task ToTxtFileAsync(DataPackageView clipboardData)
    {
        Logger.LogTrace();

        var text = await ClipboardHelper.GetClipboardTextOrHtmlTextAsync(clipboardData);
        await SetClipboardFileContentAsync(text, "txt");
    }

    private async Task ToHtmlFileAsync(DataPackageView clipboardData)
    {
        Logger.LogTrace();

        var cfHtml = await ClipboardHelper.GetClipboardHtmlContentAsync(clipboardData);
        var html = RemoveHtmlMetadata(cfHtml);

        await SetClipboardFileContentAsync(html, "html");
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

    private static async Task SetClipboardFileContentAsync(string data, string fileExtension)
    {
        if (string.IsNullOrEmpty(data))
        {
            throw new ArgumentException($"Empty value in {nameof(SetClipboardFileContentAsync)}", nameof(data));
        }

        var path = GetPasteAsFileTempFilePath(fileExtension);

        await File.WriteAllTextAsync(path, data);
        await ClipboardHelper.SetClipboardFileContentAsync(path);
    }

    private static async Task SetClipboardFileContentAsync(Stream stream, string fileExtension)
    {
        var path = GetPasteAsFileTempFilePath(fileExtension);

        using var fileStream = File.Create(path);
        await stream.CopyToAsync(fileStream);

        await ClipboardHelper.SetClipboardFileContentAsync(path);
    }

    private static string GetPasteAsFileTempFilePath(string fileExtension)
    {
        var prefix = ResourceLoaderInstance.ResourceLoader.GetString("PasteAsFile_FilePrefix");
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

        return Path.Combine(Path.GetTempPath(), $"{prefix}{timestamp}.{fileExtension}");
    }

    private async Task<string> ToCustomAsync(string prompt, DataPackageView clipboardData)
    {
        Logger.LogTrace();

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return string.Empty;
        }

        if (!clipboardData.Contains(StandardDataFormats.Text))
        {
            Logger.LogWarning("Clipboard does not contain text data");
            return string.Empty;
        }

        var currentClipboardText = await clipboardData.GetTextAsync();

        if (string.IsNullOrWhiteSpace(currentClipboardText))
        {
            Logger.LogWarning("Clipboard has no usable text data");
            return string.Empty;
        }

        var aiResponse = await Task.Run(() => _aiHelper.AIFormatString(prompt, currentClipboardText));

        return aiResponse.ApiRequestStatus == (int)HttpStatusCode.OK
            ? aiResponse.Response
            : throw new PasteActionException(TranslateErrorText(aiResponse.ApiRequestStatus));
    }

    private void SetClipboardTextContent(string content)
    {
        if (!string.IsNullOrEmpty(content))
        {
            ClipboardHelper.SetClipboardTextContent(content);
        }
    }

    private static string TranslateErrorText(int apiRequestStatus) => (HttpStatusCode)apiRequestStatus switch
    {
        HttpStatusCode.TooManyRequests => ResourceLoaderInstance.ResourceLoader.GetString("OpenAIApiKeyTooManyRequests"),
        HttpStatusCode.Unauthorized => ResourceLoaderInstance.ResourceLoader.GetString("OpenAIApiKeyUnauthorized"),
        HttpStatusCode.OK => string.Empty,
        _ => ResourceLoaderInstance.ResourceLoader.GetString("OpenAIApiKeyError") + apiRequestStatus.ToString(CultureInfo.InvariantCulture),
    };
}
