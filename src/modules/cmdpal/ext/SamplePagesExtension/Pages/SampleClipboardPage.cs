// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace SamplePagesExtension.Pages;

internal sealed partial class SampleClipboardPage : ListPage
{
    private const string SampleText = "Command Palette clipboard sample: Caf\u00e9, \u4e16\u754c, \ud83d\ude80\r\nSecond line.";
    private const string RtfPlainText = "Command Palette clipboard sample: bold, italic, and blue.";
    private const string RtfText = @"{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}{\colortbl;\red0\green120\blue215;}\f0\fs24 Command Palette clipboard sample: \b bold\b0 , \i italic\i0 , and \cf1 blue\cf0 .}";
    private const string HtmlPlainText = "Command Palette clipboard sample: bold and italic.";
    private const string HtmlFragment = "<p>Command Palette clipboard sample: <strong>bold</strong> and <em>italic</em>.</p>";
    private const string DeferredText = "This text came from a delayed data provider.";

    private readonly IListItem[] _items;

    public SampleClipboardPage()
    {
        Name = Title = "Clipboard Helper Samples";
        Icon = new IconInfo("\uE77F");
        ShowDetails = true;

        _items =
        [
            CreateItem(
                "SetText: copy Unicode text",
                "Paste into Notepad to check Unicode characters and line breaks",
                () => CopyAndReadText(() => ClipboardHelper.SetText(SampleText), SampleText)),
            CreateItem(
                "GetText: read the clipboard",
                "Copy text in another app, then run this command to display it here",
                () =>
                {
                    var text = ClipboardHelper.GetText();
                    return string.IsNullOrEmpty(text) ? "The clipboard contains no text." : text;
                }),
            CreateItem(
                "SetRtf: copy formatted text",
                "Paste into Word to check bold, italic, and blue text; paste into Notepad to check the plain text fallback",
                () => CopyAndReadText(() => ClipboardHelper.SetRtf(RtfPlainText, RtfText), RtfPlainText)),
            CreateItem(
                "SetImage: copy a bitmap",
                "Paste into Paint to check the Swirls image",
                () =>
                {
                    var image = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/Images/Swirls.png"));
                    ClipboardHelper.SetImage(image);
                    return "The image command completed. Paste into Paint to verify the bitmap.";
                }),
            CreateItem(
                "SetContent: copy HTML and text",
                "Paste into Word to check formatted HTML; paste into Notepad to check the plain text fallback",
                CopyHtml),
            CreateItem(
                "SetContent: copy delayed text",
                "Paste into Notepad to check text supplied by a delayed data provider",
                CopyDeferredText),
        ];
    }

    public override IListItem[] GetItems() => _items;

    private static ListItem CreateItem(string title, string instructions, Func<string> operation)
    {
        var result = new PlainTextContent
        {
            Text = "Run this command to see its result.",
            FontFamily = FontFamily.Monospace,
            WrapWords = true,
        };
        var details = new Details
        {
            Title = title,
            Body = instructions,
            Content = [result],
            Size = ContentSize.Medium,
        };

        return new ListItem(new AnonymousCommand(() =>
        {
            try
            {
                result.Text = operation();
                details.Title = "Result";
            }
            catch (Exception exception)
            {
                result.Text = $"{exception.GetType().Name}: {exception.Message}";
                details.Title = "Failed";
            }
        })
        {
            Name = "Test",
            Result = CommandResult.KeepOpen(),
        })
        {
            Title = title,
            Subtitle = instructions,
            Icon = new IconInfo("\uE77F"),
            Details = details,
        };
    }

    private static string CopyAndReadText(Action copy, string expectedText)
    {
        copy();
        var actualText = ClipboardHelper.GetText();
        if (!string.Equals(expectedText, actualText, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The clipboard text did not match the expected text.");
        }

        return $"Plain text readback matched:\r\n\r\n{actualText}";
    }

    private static string CopyHtml()
    {
        var package = new DataPackage();
        package.SetText(HtmlPlainText);
        package.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat(HtmlFragment));
        return CopyAndReadText(() => ClipboardHelper.SetContent(package), HtmlPlainText);
    }

    private static string CopyDeferredText()
    {
        var package = new DataPackage();
        package.SetDataProvider(StandardDataFormats.Text, request => request.SetData(DeferredText));
        return CopyAndReadText(() => ClipboardHelper.SetContent(package), DeferredText);
    }
}
