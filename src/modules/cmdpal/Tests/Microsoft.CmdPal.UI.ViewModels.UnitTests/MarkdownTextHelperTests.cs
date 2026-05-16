// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class MarkdownTextHelperTests
{
    [TestMethod]
    public void SanitizeMarkdown_NullInput_ReturnsEmpty()
    {
        var result = MarkdownTextHelper.SanitizeMarkdown(null);
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void SanitizeMarkdown_EmptyInput_ReturnsEmpty()
    {
        var result = MarkdownTextHelper.SanitizeMarkdown(string.Empty);
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void SanitizeMarkdown_CleanMarkdown_ReturnsUnchanged()
    {
        const string input = "# Hello\n\nThis is **bold** and _italic_.\n\n- Item 1\n- Item 2\n";
        var result = MarkdownTextHelper.SanitizeMarkdown(input);
        Assert.AreEqual(input, result);
    }

    [DataTestMethod]
    [DataRow("\x00")]          // NUL
    [DataRow("\x01")]          // SOH
    [DataRow("\x07")]          // BEL
    [DataRow("\x08")]          // BS
    [DataRow("\x0B")]          // VT (Vertical Tab)
    [DataRow("\x0C")]          // FF (Form Feed)
    [DataRow("\x0E")]          // SO
    [DataRow("\x1F")]          // US
    [DataRow("\x7F")]          // DEL
    [DataRow("\x80")]          // C1 start
    [DataRow("\x9F")]          // C1 end
    public void SanitizeMarkdown_SingleControlChar_IsRemoved(string controlChar)
    {
        var input = $"before{controlChar}after";
        var result = MarkdownTextHelper.SanitizeMarkdown(input);
        Assert.AreEqual("beforeafter", result);
    }

    [TestMethod]
    public void SanitizeMarkdown_NulBytesWithinText_AreRemoved()
    {
        // NUL bytes in clipboard content (the main bug trigger from the issue)
        var input = "Hello\x00World\x00";
        var result = MarkdownTextHelper.SanitizeMarkdown(input);
        Assert.AreEqual("HelloWorld", result);
    }

    [TestMethod]
    public void SanitizeMarkdown_StandardWhitespacePreserved()
    {
        // TAB (0x09), LF (0x0A), CR (0x0D) must be kept — Markdown relies on them
        const string input = "Column1\tColumn2\r\nLine2\nLine3";
        var result = MarkdownTextHelper.SanitizeMarkdown(input);
        Assert.AreEqual(input, result);
    }

    [TestMethod]
    public void SanitizeMarkdown_MixedControlAndNormalChars_OnlyControlCharsRemoved()
    {
        // Simulates clipboard text with embedded NUL bytes interspersed with normal text
        var input = "This is a short \x00demo paragraph, meant \x00to include literal N\x00UL bytes.";
        var result = MarkdownTextHelper.SanitizeMarkdown(input);
        Assert.AreEqual("This is a short demo paragraph, meant to include literal NUL bytes.", result);
    }

    [TestMethod]
    public void SanitizeMarkdown_UnicodeAndEmoji_NotAffected()
    {
        // Non-ASCII and emoji must pass through unchanged
        const string input = "Café ☕ \U0001F600 résumé";
        var result = MarkdownTextHelper.SanitizeMarkdown(input);
        Assert.AreEqual(input, result);
    }
}
