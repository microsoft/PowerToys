// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;
using WinClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace AdvancedPaste.UITests;

[TestClass]
[DoNotParallelize]
[TestCategory("AdvancedPaste")]
public sealed class AdvancedPasteTextTests : AdvancedPasteTestBase
{
    public enum Invocation
    {
        DirectShortcut,
        Menu,
        Accelerator,
    }

    [TestMethod]
    [DataRow(Invocation.DirectShortcut)]
    [DataRow(Invocation.Menu)]
    [DataRow(Invocation.Accelerator)]
    public void PlainTextRemovesRichFormattingAndReplacesClipboard(Invocation invocation)
    {
        const string text = "Bold and plain\r\nSecond line";
        SetRichTextClipboard(text, @"{\rtf1\ansi{\fonttbl{\f0 Segoe UI;}}\f0\fs28\b Bold\b0  and plain\par Second line}");

        Step("Proving normal paste preserves the rich-text source");
        Target.Paste();
        Target.AssertText(text);
        WaitUntil(() => Target.IsBold(0, 4), "The rich-text fixture was not bold before conversion.");
        Target.Clear();

        InvokeCoreAction(ProductStrings.PasteAsPlainText, Key.O, Key.Num1, invocation);
        Target.AssertText(text);
        Assert.IsFalse(Target.IsBold(0, 4), "Paste as plain text retained bold formatting.");
        var clipboard = WinClipboard.GetContent();
        Assert.IsFalse(clipboard.Contains(StandardDataFormats.Rtf), "Rich text remained on the transformed clipboard.");
        Assert.IsFalse(clipboard.Contains(StandardDataFormats.Html), "HTML remained on the transformed clipboard.");
        WaitUntil(() => !IsAdvancedPasteVisible(), "Advanced Paste did not finish the plain-text paste.");

        Step("Verifying subsequent normal paste also contains only plain text");
        Target.Clear();
        Target.Paste();
        Target.AssertText(text);
        Assert.IsFalse(Target.IsBold(0, 4), "Subsequent normal paste restored rich formatting.");
    }

    [TestMethod]
    [DataRow(Invocation.DirectShortcut)]
    [DataRow(Invocation.Menu)]
    [DataRow(Invocation.Accelerator)]
    public void LegacyHtmlConvertsToMarkdown(Invocation invocation)
    {
        SetClipboardText(File.ReadAllText(FixturePath("PasteAsMarkdownFile.html")));
        InvokeCoreAction(ProductStrings.PasteAsMarkdown, Key.M, Key.Num2, invocation);
        AssertTransformedText();
        var expected = NormalizeMarkdown(File.ReadAllText(FixturePath("PasteAsMarkdownResultFile.txt")));
        Assert.AreEqual(expected, NormalizeMarkdown(Target.Text), "The legacy HTML fixture did not produce the expected Markdown.");
    }

    [TestMethod]
    [DataRow(Invocation.DirectShortcut)]
    [DataRow(Invocation.Menu)]
    [DataRow(Invocation.Accelerator)]
    public void LegacyXmlConvertsToJson(Invocation invocation)
    {
        SetClipboardText(File.ReadAllText(FixturePath("PasteAsJsonFile.xml")));
        InvokeCoreAction(ProductStrings.PasteAsJson, Key.J, Key.Num3, invocation);
        AssertTransformedText();
        AssertJson(File.ReadAllText(FixturePath("PasteAsJsonResultFile.txt")));
    }

    [TestMethod]
    [DataRow("Name,Count\r\ntea,2", "[[\"Name\",\"Count\"],[\"tea\",\"2\"]]")]
    [DataRow("Name;Count\r\ntea;2", "[[\"Name\",\"Count\"],[\"tea\",\"2\"]]")]
    [DataRow("Name\tCount\r\ntea\t2", "[[\"Name\",\"Count\"],[\"tea\",\"2\"]]")]
    [DataRow("sep=;\r\nName;Count\r\ntea;2", "[[\"Name\",\"Count\"],[\"tea\",\"2\"]]")]
    [DataRow("\"a,b\",\"say \"\"hello\"\"\"\r\n\"\",42", "[[\"a,b\",\"say \\\"hello\\\"\"],[\"\",\"42\"]]")]
    [DataRow("; ignored\r\n[general]\r\nkey=value\r\nempty=\r\n[second]\r\nname=caf\u00e9", "{\"general\":{\"key\":\"value\",\"empty\":\"\"},\"second\":{\"name\":\"caf\\u00e9\"}}")]
    [DataRow("First line\r\n\r\nSecond line", "[\"First line\",\"Second line\"]")]
    [DataRow("<broken>\r\nA \"quote\" and a \\ path", "[\"<broken>\",\"A \\\"quote\\\" and a \\\\ path\"]")]
    public void JsonSupportsOfflineInputFormats(string input, string expected)
    {
        SetClipboardText(input);
        SelectAction(OpenAdvancedPaste(), ProductStrings.PasteAsJson);
        AssertTransformedText();
        AssertJson(expected);
    }

    [TestMethod]
    public void ExistingJsonIsPastedWithoutReformatting()
    {
        const string input = " { \"value\" : [true, null, 42], \"text\" : \"caf\\u00e9\" } ";
        SetClipboardText(input);
        InvokeCoreAction(ProductStrings.PasteAsJson, Key.J, Key.Num3, Invocation.DirectShortcut);
        Target.AssertText(input);
        Assert.AreEqual(input, ReadClipboardText(), "Already-valid JSON was reformatted.");
    }

    [TestMethod]
    public void MarkdownPrefersHtmlAndRemovesScriptsAndFootnotes()
    {
        SetHtmlClipboard("<h2>Offline</h2><p><strong>Bold</strong> and <em>italic</em> <a href=\"https://example.test/\">link</a></p><script>never paste this</script><sup>omit footnote</sup>", "This fallback must not be used");
        SelectAction(OpenAdvancedPaste(), ProductStrings.PasteAsMarkdown);
        AssertTransformedText();

        // CF_HTML boundary comments remain inert HTML comments in the generated Markdown.
        Assert.AreEqual("<!--StartFragment -->\n## Offline\n\n**Bold** and *italic* [link](https://example.test/)\n<!--EndFragment -->", NormalizeMarkdown(Target.Text));
    }

    [TestMethod]
    public void PlainTextPreservesUnicodeWhitespaceAndLargeContent()
    {
        var input = "  caf\u00e9 \u4e2d\u6587 \U0001f680\r\n\tsecond line\r\n" + new string('x', 32_768) + "  ";
        SetClipboardText(input);
        InvokeCoreAction(ProductStrings.PasteAsPlainText, Key.O, Key.Num1, Invocation.DirectShortcut);
        Target.AssertText(input);
        Assert.AreEqual(input, ReadClipboardText(), "Plain-text conversion changed Unicode or whitespace.");
    }

    [TestMethod]
    public void EscapeDismissesWithoutChangingTheClipboardOrDestination()
    {
        const string input = "Escape leaves this clipboard untouched";
        SetClipboardText(input);
        var window = OpenAdvancedPaste();
        Assert.IsTrue(WindowControl.WaitForForeground(new IntPtr(window.WindowHandle), timeoutMS: 10_000));
        Step("Dismissing Advanced Paste with Escape");
        SendShortcut(Key.Esc);
        WaitUntil(() => !IsAdvancedPasteVisible(), "Escape did not dismiss Advanced Paste.");
        Assert.AreEqual(input, ReadClipboardText());
        Assert.AreEqual(string.Empty, Target.Text, "Escape unexpectedly pasted content.");
        OpenAdvancedPaste();
        Assert.IsTrue(IsAdvancedPasteVisible(), "Advanced Paste could not reopen after Escape.");
    }

    [TestMethod]
    public void EmptyClipboardDoesNotOfferEnabledActions()
    {
        Assert.IsTrue(ClipboardHelper.Clear(), "Could not arrange the empty clipboard.");
        var window = OpenAdvancedPaste();
        var actions = window.FindAll<Element>(By.Name("Ctrl+"), 0)
            .Where(element => element.ControlType.Equals("ListItem", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.IsEmpty(actions, "An empty clipboard exposed an enabled action/accelerator.");
        Assert.AreEqual(string.Empty, Target.Text);
        Assert.AreEqual(string.Empty, ReadClipboardText());
    }

    private void InvokeCoreAction(string name, Key directKey, Key accelerator, Invocation invocation)
    {
        Target.Focus();
        Step($"Executing '{name}' through {invocation}");
        if (invocation == Invocation.DirectShortcut)
        {
            SendShortcut(Key.Ctrl, Key.Alt, Key.LWin, directKey);
        }
        else
        {
            var window = OpenAdvancedPaste();
            if (invocation == Invocation.Menu)
            {
                SelectAction(window, name);
            }
            else
            {
                Assert.IsTrue(WindowControl.WaitForForeground(new IntPtr(window.WindowHandle), timeoutMS: 10_000));
                SendShortcut(Key.Ctrl, accelerator);
            }
        }
    }

    private void AssertTransformedText()
    {
        WaitUntil(() => !string.IsNullOrEmpty(Target.Text), "The destination did not receive transformed text.");
        var result = WaitHelper.WaitForStable(
            () => (ClipboardText: ReadClipboardText().ReplaceLineEndings("\n"), PastedText: Target.Text),
            state => !string.IsNullOrEmpty(state.PastedText) && state.ClipboardText == state.PastedText,
            timeoutMS: 15_000,
            requiredConsecutiveMatches: 2);
        Assert.IsTrue(
            result.Succeeded,
            $"The clipboard and actual pasted text did not reach the same nonempty value. " +
            $"Clipboard: '{result.LastObservation.ClipboardText}'; pasted: '{result.LastObservation.PastedText}'.");
        WaitUntil(() => !IsAdvancedPasteVisible(), "Advanced Paste did not hide after pasting.");
    }

    private void AssertJson(string expected)
    {
        Assert.IsTrue(
            JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(Target.Text)),
            $"The JSON result differs. Expected: {expected}; actual: {Target.Text}");
    }

    private static string NormalizeMarkdown(string text) =>
        string.Join("\n", text.ReplaceLineEndings("\n").Split('\n').Select(line => string.IsNullOrWhiteSpace(line) ? string.Empty : line)).TrimEnd();
}
