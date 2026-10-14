// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.UnitTests.HelpersTests;

[TestClass]
public sealed class HtmlToTextHelperTests
{
    [TestMethod]
    [DataRow("", "")]
    [DataRow(" \r\n\t ", "")]
    [DataRow("<p>Offline text file</p>", "Offline text file")]
    [DataRow("<p>first</p><p>second</p>", "first\nsecond")]
    [DataRow("<div>first<br>second<br><br>fourth</div>", "first\nsecond\n\nfourth")]
    [DataRow("<H1>Heading</H1><P>Paragraph</P>", "Heading\nParagraph")]
    [DataRow("<p><b>hel</b><i>lo</i> <a href=\"https://example.test\">world</a></p>", "hello world")]
    [DataRow("<div>\n  one\t <b> two </b>\n three \n</div>", "one two three")]
    [DataRow("<ul><li>one</li><li>two<ul><li>nested</li></ul></li></ul>", "one\ntwo\nnested")]
    [DataRow("<table><tr><th>One</th><th>Two</th></tr><tr><td>A</td><td>B</td></tr></table>", "One\tTwo\nA\tB")]
    [DataRow("<p>&lt;tag&gt; &amp; &quot;quote&quot; &#39; &#x1F680; &nbsp;</p>", "<tag> & \"quote\" ' \U0001f680 \u00a0")]
    [DataRow("<p>&amp;lt; is decoded once</p>", "&lt; is decoded once")]
    [DataRow("<p>a\u2009b\u202fc\u3000d\u200be</p>", "a\u2009b\u202fc\u3000d\u200be")]
    [DataRow("<html><head><title>hidden</title></head><body>visible<script>bad()</script><style>bad{}</style><!-- hidden --><template>hidden</template></body></html>", "visible")]
    [DataRow("<div hidden>hidden</div><p>visible</p>", "visible")]
    [DataRow("<div>one<br>two<b>three", "one\ntwothree")]
    [DataRow("<pre>  first\n\tsecond  \n</pre>", "  first\n\tsecond  \n")]
    [DataRow("<pre><table><tr><td>a\t\t</td></tr></table></pre>", "a\t\t")]
    public void ConvertsHtmlWithoutNativeRendering(string html, string expected)
    {
        Assert.AreEqual(expected.ReplaceLineEndings(Environment.NewLine), HtmlToTextHelper.ToPlainText(html));
    }

    [TestMethod]
    public void TraversesDeepMarkupWithoutRecursiveTextExtraction()
    {
        var html = new StringBuilder();
        for (var level = 0; level < 2_000; level++)
        {
            html.Append("<span>");
        }

        html.Append("content");
        for (var level = 0; level < 2_000; level++)
        {
            html.Append("</span>");
        }

        Assert.AreEqual("content", HtmlToTextHelper.ToPlainText(html.ToString()));
    }

    [TestMethod]
    public async Task BothClipboardFallbacksExtractUnicodeFragmentWithoutMetadata()
    {
        var data = new DataPackage();
        data.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat("<p>caf\u00e9 \u4e2d\u6587 \U0001f680 &amp;</p><p>next<br>line</p>"));
        var expected = "caf\u00e9 \u4e2d\u6587 \U0001f680 &\nnext\nline".ReplaceLineEndings(Environment.NewLine);

        Assert.AreEqual(expected, await data.GetView().GetTextOrHtmlTextAsync());
        Assert.AreEqual(expected, await data.GetView().GetClipboardTextOrThrowAsync());
    }

    [TestMethod]
    public async Task BothClipboardFallbacksPreferUnmodifiedPlainText()
    {
        const string original = "  plain\ttext\r\n\U0001f680  ";
        var data = new DataPackage();
        data.SetText(original);
        data.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat("<p>Do not use the HTML alternative</p>"));

        Assert.AreEqual(original, await data.GetView().GetTextOrHtmlTextAsync());
        Assert.AreEqual(original, await data.GetView().GetClipboardTextOrThrowAsync());
    }

    [TestMethod]
    public async Task HtmlWithoutTextReturnsEmptyText()
    {
        var data = new DataPackage();
        data.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat("<p></p>"));

        Assert.AreEqual(string.Empty, await data.GetView().GetTextOrHtmlTextAsync());
        Assert.AreEqual(string.Empty, await data.GetView().GetClipboardTextOrThrowAsync());
    }

    [TestMethod]
    public void NullHtmlIsRejected()
    {
        Assert.ThrowsException<ArgumentNullException>(() => HtmlToTextHelper.ToPlainText(null!));
    }

    [TestMethod]
    public async Task UnsupportedClipboardFormatsKeepTheirExistingErrorBehavior()
    {
        var data = new DataPackage();

        Assert.AreEqual(string.Empty, await data.GetView().GetTextOrHtmlTextAsync());
        await Assert.ThrowsExceptionAsync<PasteActionException>(() => data.GetView().GetClipboardTextOrThrowAsync());
    }
}
