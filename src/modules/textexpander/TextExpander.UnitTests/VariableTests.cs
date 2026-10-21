// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class VariableTests
{
    // What Settings shows for Insert variable's ready-made date formats and its format guide's
    // codes at this moment (TextExpanderDateFormats in the Settings tests). The engine has to
    // type exactly the same.
    private static readonly DateTime SettingsSampleMoment = new(2026, 10, 18, 16, 2, 9);
    private static readonly string[] SettingsPresets = ["dddd", "MMMM d, yyyy", "dddd, MMMM d, yyyy", "MMM d", "MM/dd/yyyy", "yyyy-MM-dd", "h:mm tt", "HH:mm"];
    private static readonly string[] SettingsPresetsShown = ["Sunday", "October 18, 2026", "Sunday, October 18, 2026", "Oct 18", "10/18/2026", "2026-10-18", "4:02 PM", "16:02"];
    private static readonly string[] SettingsCodes = ["yyyy", "yy", "MMMM", "MMM", "MM", "M", "dddd", "ddd", "dd", "d", "h", "HH", "mm", "tt"];
    private static readonly string[] SettingsCodesShown = ["2026", "26", "October", "Oct", "10", "10", "Sunday", "Sun", "18", "18", "4", "16", "02", "PM"];

    [TestMethod]
    public void DateFormatsSettingsOffersAreTypedAsSettingsShowsThem()
    {
        CollectionAssert.AreEqual(SettingsPresetsShown, SettingsPresets.Select(format => Variables.FormatDate(SettingsSampleMoment, format)).ToArray());
        CollectionAssert.AreEqual(SettingsCodesShown, SettingsCodes.Select(code => Variables.FormatDate(SettingsSampleMoment, code)).ToArray());
    }

    [TestMethod]
    public void CustomDateFormatsSupportDotNetAndStrftime()
    {
        var date = new DateTime(2026, 9, 9, 14, 5, 7);
        Assert.AreEqual("09/09/2026", Variables.FormatDate(date, "MM/dd/yyyy"));
        Assert.AreEqual(Variables.FormatDate(date, "MM/dd/yyyy"), Variables.FormatDate(date, "%m/%d/%Y"));
        Assert.AreEqual("Wednesday", Variables.FormatDate(date, "dddd"));
        Assert.AreEqual("100% sure", Variables.FormatDate(date, "100%% sure"));
        Assert.AreEqual("252", Variables.FormatDate(date, "%j"));
        Assert.AreEqual("\"", Variables.FormatDate(date, "\""));
    }

    [TestMethod]
    public void ClipboardVariableIsInsertedVerbatimAndOnlyWhenRequested()
    {
        Assert.AreEqual("[pasted]", Variables.Expand("[{{clipboard}}]", _ => null, () => "pasted"));
        Assert.AreEqual("[]", Variables.Expand("[{{clipboard}}]", _ => null));
        Assert.AreEqual("{{date}} and \\n literal", Variables.Expand("{{clipboard}}", _ => null, () => "{{date}} and \\n literal"));

        int reads = 0;
        string? ReadClipboard()
        {
            reads++;
            return "x";
        }

        _ = Variables.Expand("{{date}} only", _ => null, ReadClipboard);
        Assert.AreEqual(0, reads);
    }

    [TestMethod]
    public void PromptVariablesAreDetectedAndExpandedPerOccurrence()
    {
        int count = 0;
        string expanded = Variables.Expand("{{prompt:Name}} {{prompt:Name}}", label => label + ++count);
        Assert.AreEqual("Name1 Name2", expanded);
        Assert.IsTrue(Variables.NeedsPrompt("before {{prompt:Name}} after"));
    }

    [TestMethod]
    public void PromptVariablesPreserveWhetherAnAnswerIsRequired()
    {
        var requests = new List<PromptRequest>();
        (string text, _) = Variables.ExpandWithPromptRequests(
            "{{prompt:Name}}/{{prompt?:Note}}",
            request =>
            {
                requests.Add(request);
                return request.Label;
            });

        Assert.AreEqual("Name/Note", text);
        Assert.AreEqual(new PromptRequest("Name", Required: true), requests[0]);
        Assert.AreEqual(new PromptRequest("Note", Required: false), requests[1]);
        Assert.IsTrue(Variables.NeedsPrompt("{{prompt?:Optional}}"));
    }

    [TestMethod]
    public void PromptAnswersAreInsertedExactlyAsTyped()
    {
        // Answers are not read for codes: no tab from "\temp", no paste from a typed
        // {{clipboard}}, and a typed placeholder is not taken for the snippet's next question.
        string expanded = Variables.Expand(
            "Path: {{prompt:Folder}}\\n{{prompt:Note}} [{{clipboard}}]",
            label => label == "Folder" ? @"C:\temp\new" : "{{clipboard}} {{date}} \\t {{prompt:Folder}}",
            () => "copied");

        Assert.AreEqual("Path: C:\\temp\\new\n{{clipboard}} {{date}} \\t {{prompt:Folder}} [copied]", expanded);
    }

    [TestMethod]
    public void CancellingAPromptLeavesItsSpotBlank()
    {
        Assert.AreEqual("Hi , bye\tnow", Variables.Expand("Hi {{prompt:Name}}, bye\\tnow", _ => null));
    }

    [TestMethod]
    public void ClipboardIsReadOnceHoweverOftenItIsUsed()
    {
        int reads = 0;
        string Copied()
        {
            reads++;
            return "x";
        }

        Assert.AreEqual("x {{a}} x", Variables.Expand("{{clipboard}} {{prompt:A}} {{clipboard}}", _ => "{{a}}", Copied));
        Assert.AreEqual(1, reads);
    }

    [TestMethod]
    public void AFormattedDateIsNotReadForCodes()
    {
        // FUZZ-2: a format that prints a backslash and "n" keeps them, in either syntax, while the
        // snippet's own "\n" around it is still a new line.
        (string text, int cursor) = Variables.ExpandWithCursor(@"[{{date:\\n}}]\n[{{date:%Y\t}}]", _ => null);
        string year = DateTime.Now.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);

        Assert.AreEqual("[\\n]\n[" + year + "\\t]", text);
        Assert.AreEqual(-1, cursor);
        Assert.AreEqual("\\n", Variables.FormatDate(new DateTime(2024, 2, 29), @"\\n"), "The formatter itself is unchanged.");

        // Nor can its output and the text after it make a placeholder.
        Assert.AreEqual("{{clipboard}} x", Variables.Expand("{{date:'{{clipboard'}}}} x", _ => null, () => "pasted"));
    }

    [TestMethod]
    public void OnlyTheSnippetsOwnMarkerPlacesTheCursor()
    {
        // QA-10: a "$|$" typed into a prompt, copied, or printed by a date stays literal, and the
        // cursor goes where the snippet put its marker.
        (string text, int cursor) = Variables.ExpandWithCursor(
            "{{prompt:A}} {{clipboard}} {{date:'$|$'}} [$|$] $|$",
            _ => "a$|$",
            () => "c$|$");

        Assert.AreEqual("a$|$ c$|$ $|$ [] $|$", text);
        Assert.AreEqual(text.IndexOf("[]", StringComparison.Ordinal) + 1, cursor);

        Assert.AreEqual((string.Empty, 0), Variables.ExpandWithCursor("$|$", _ => null));
        Assert.AreEqual(("abc", -1), Variables.ExpandWithCursor("abc", _ => null));
        Assert.AreEqual(("x\ny", 1), Variables.ExpandWithCursor("x$|$\\ny", _ => null), "Counted after the snippet's codes are applied.");

        // Expand keeps the marker where the snippet put it, as before.
        Assert.AreEqual("a$|$ [$|$]", Variables.Expand("{{prompt:A}} [$|$]", _ => "a$|$"));
    }

    [TestMethod]
    public void ASnippetIsReadOnceFromTheLeft()
    {
        // A placeholder inside a label is part of the label, as the Settings preview shows it.
        var labels = new List<string>();
        Assert.AreEqual("[{{date]}} x", Variables.Expand("{{prompt:{{date}}}} x", label =>
        {
            labels.Add(label);
            return "[" + label + "]";
        }));
        Assert.AreEqual("{{date", labels.Single());

        // PERF-3: 100 KB with 6,000 values used to allocate about 84 MB.
        string text = string.Concat(Enumerable.Repeat("Dear {{prompt:Name}}, on {{date}} at {{time}}\\n", 2000));
        _ = Variables.Expand("warm {{date}} up", _ => null);
        int asked = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        string expanded = Variables.Expand(text, _ =>
        {
            asked++;
            return "Sam";
        });
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.AreEqual(2000, asked);
        StringAssert.StartsWith(expanded, "Dear Sam, on ");
        Assert.IsTrue(allocated < 10_000_000, $"{allocated} bytes for {text.Length} characters");
    }
}
