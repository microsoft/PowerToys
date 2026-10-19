// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.UnitTests.ServicesTests;

[TestClass]
public class RichTextHelperTests
{
    [TestMethod]
    public async Task ToRichTextAsync_ConvertsMarkdownToHtml()
    {
        var markdown = "**Bold** and *italic* text";
        var package = new DataPackage();
        package.SetText(markdown);
        var view = package.GetView();

        var html = await RichTextHelper.ToRichTextAsync(view);

        Assert.IsTrue(html.Contains("<strong>Bold</strong>", StringComparison.Ordinal), "Missing bold tag");
        Assert.IsTrue(html.Contains("<em>italic</em>", StringComparison.Ordinal), "Missing italic tag");
    }

    [TestMethod]
    public async Task ToRichTextAsync_ReturnsEmptyString_WhenEmpty()
    {
        var package = new DataPackage();
        package.SetText(string.Empty);
        var view = package.GetView();

        var html = await RichTextHelper.ToRichTextAsync(view);

        Assert.AreEqual(string.Empty, html);
    }

    [TestMethod]
    public async Task ToRichTextAsync_ProducesValidHtmlFragment()
    {
        var markdown = "# Heading\n\n**Bold** text";
        var package = new DataPackage();
        package.SetText(markdown);
        var view = package.GetView();

        var html = await RichTextHelper.ToRichTextAsync(view);
        var cfHtml = HtmlFormatHelper.CreateHtmlFormat(html);
        var fragment = HtmlFormatHelper.GetStaticFragment(cfHtml);

        Assert.IsTrue(fragment.Contains("<h1", StringComparison.Ordinal), "Missing heading in fragment");
        Assert.IsTrue(fragment.Contains("<strong>Bold</strong>", StringComparison.Ordinal), "Missing bold in fragment");
        Assert.IsFalse(fragment.Contains("Version:", StringComparison.Ordinal), "Fragment should not contain CF_HTML headers");
        Assert.IsFalse(fragment.Contains("StartFragment:", StringComparison.Ordinal), "Fragment should not contain StartFragment header");
    }

    [TestMethod]
    public async Task CreateFromHtml_IncludesHtmlAndPlainTextFallback()
    {
        var rawText = "**Bold**";
        var package = DataPackageHelpers.CreateFromHtml("<strong>Bold</strong>", rawText);
        var view = package.GetView();

        Assert.IsTrue(view.Contains(StandardDataFormats.Html), "Package should contain HTML format");
        Assert.IsTrue(view.Contains(StandardDataFormats.Text), "Package should contain Text format fallback");
        Assert.AreEqual(rawText, await view.GetTextAsync());
    }
}
