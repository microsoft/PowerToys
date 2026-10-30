// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class VariableTests
{
    // What Settings shows for Insert variable's ready-made date formats and its format guide's
    // codes at this moment (TextExpanderDateFormats in the Settings tests). The engine has to
    // type exactly the same.
    private static readonly DateTime SettingsSampleMoment = new(2026, 10, 18, 16, 2, 9);
    private static readonly CultureInfo EnglishUS = CultureInfo.GetCultureInfo("en-US");
    private static readonly string[] SettingsPresets = ["dddd", "MMMM d, yyyy", "dddd, MMMM d, yyyy", "MMM d", "MM/dd/yyyy", "yyyy-MM-dd", "h:mm tt", "HH:mm"];
    private static readonly string[] SettingsPresetsShown = ["Sunday", "October 18, 2026", "Sunday, October 18, 2026", "Oct 18", "10/18/2026", "2026-10-18", "4:02 PM", "16:02"];
    private static readonly string[] SettingsCodes = ["yyyy", "yy", "MMMM", "MMM", "MM", "M", "dddd", "ddd", "dd", "d", "h", "HH", "mm", "tt"];
    private static readonly string[] SettingsCodesShown = ["2026", "26", "October", "Oct", "10", "10", "Sunday", "Sun", "18", "18", "4", "16", "02", "PM"];

    [TestMethod]
    public void DateFormatsSettingsOffersAreTypedAsSettingsShowsThem()
    {
        CollectionAssert.AreEqual(SettingsPresetsShown, SettingsPresets.Select(format => Variables.FormatDate(SettingsSampleMoment, format, EnglishUS)).ToArray());
        CollectionAssert.AreEqual(SettingsCodesShown, SettingsCodes.Select(code => Variables.FormatDate(SettingsSampleMoment, code, EnglishUS)).ToArray());
    }

    [TestMethod]
    public void CustomDateFormatsSupportDotNetAndStrftime()
    {
        var date = new DateTime(2026, 9, 9, 14, 5, 7);
        Assert.AreEqual("09/09/2026", Variables.FormatDate(date, "MM/dd/yyyy", EnglishUS));
        Assert.AreEqual(Variables.FormatDate(date, "MM/dd/yyyy", EnglishUS), Variables.FormatDate(date, "%m/%d/%Y", EnglishUS));
        Assert.AreEqual("Wednesday", Variables.FormatDate(date, "dddd", EnglishUS));
        Assert.AreEqual("100% sure", Variables.FormatDate(date, "100%% sure", EnglishUS));
        Assert.AreEqual("252", Variables.FormatDate(date, "%j", EnglishUS));
        Assert.AreEqual("\"", Variables.FormatDate(date, "\"", EnglishUS));
    }

    [TestMethod]
    [DataRow("en-US", "10/18/2026", "4:02 PM")]
    [DataRow("en-GB", "18/10/2026", "16:02")]
    [DataRow("de-DE", "18.10.2026", "16:02")]
    [DataRow("fr-FR", "18/10/2026", "16:02")]
    [DataRow("ja-JP", "2026/10/18", "16:02")]
    [DataRow("fi-FI", "18.10.2026", "16.02")]
    public void DateAndTimeAreWrittenInTheRegionalFormat(string name, string date, string time)
    {
        // F020: {{date}}, {{time}} and {{datetime}} use the region's short formats; {{isodate}} never changes.
        CultureInfo culture = CultureInfo.GetCultureInfo(name);
        (string text, _) = Variables.ExpandWithPromptRequests("{{date}}|{{time}}|{{datetime}}|{{isodate}}", _ => null, null, SettingsSampleMoment, culture);
        Assert.AreEqual($"{date}|{time}|{date} {time}|2026-10-18", text);
    }

    [TestMethod]
    [DataRow("en-US")]
    [DataRow("en-GB")]
    [DataRow("de-DE")]
    [DataRow("fr-FR")]
    [DataRow("ja-JP")]
    [DataRow("fi-FI")]
    public void CustomFormatsUseTheRegionsNamesButTypeSeparatorsAsWritten(string name)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(name);
        DateTimeFormatInfo info = culture.DateTimeFormat;

        Assert.AreEqual(info.GetDayName(DayOfWeek.Sunday), Variables.FormatDate(SettingsSampleMoment, "dddd", culture));
        Assert.AreEqual(info.GetDayName(DayOfWeek.Sunday), Variables.FormatDate(SettingsSampleMoment, "%A", culture));
        Assert.AreEqual(info.GetMonthName(10), Variables.FormatDate(SettingsSampleMoment, "MMMM", culture));
        Assert.AreEqual(info.GetMonthName(10), Variables.FormatDate(SettingsSampleMoment, "%B", culture));
        Assert.AreEqual($"4:02 {info.PMDesignator}", Variables.FormatDate(SettingsSampleMoment, "h:mm tt", culture));
        Assert.AreEqual($"04:02 {info.PMDesignator}", Variables.FormatDate(SettingsSampleMoment, "%I:%M %p", culture));

        // A "/" or ":" is typed as written, whatever separators the region uses.
        Assert.AreEqual("10/18/2026 16:02", Variables.FormatDate(SettingsSampleMoment, "MM/dd/yyyy HH:mm", culture));
        Assert.AreEqual("10/18/2026 16:02:09", Variables.FormatDate(SettingsSampleMoment, "%m/%d/%Y %T", culture));

        // %x and %X are the region's own date and time; %F, like {{isodate}}, never changes.
        Assert.AreEqual(SettingsSampleMoment.ToString("d", culture), Variables.FormatDate(SettingsSampleMoment, "%x", culture));
        Assert.AreEqual(SettingsSampleMoment.ToString("T", culture), Variables.FormatDate(SettingsSampleMoment, "%X", culture));
        Assert.AreEqual("2026-10-18", Variables.FormatDate(SettingsSampleMoment, "%F", culture));
    }

    [TestMethod]
    public void CustomFormatsSpellNamesInTheRegionsLanguage()
    {
        Assert.AreEqual("Sonntag, 18. Oktober", Variables.FormatDate(SettingsSampleMoment, "dddd, d. MMMM", CultureInfo.GetCultureInfo("de-DE")));
        Assert.AreEqual("dimanche 18 octobre", Variables.FormatDate(SettingsSampleMoment, "dddd d MMMM", CultureInfo.GetCultureInfo("fr-FR")));
        Assert.AreEqual("Sunday 18 October", Variables.FormatDate(SettingsSampleMoment, "dddd d MMMM", CultureInfo.GetCultureInfo("en-GB")));
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
    public void TemplatesAreReadIntoTheirValuesInOnePass()
    {
        CollectionAssert.AreEqual(
            new SnippetToken[]
            {
                new(SnippetTokenKind.Text, "Hi "),
                new(SnippetTokenKind.Prompt, "Name", Required: true),
                new(SnippetTokenKind.Text, ", "),
                new(SnippetTokenKind.Prompt, string.Empty),
                new(SnippetTokenKind.LineBreak),
                new(SnippetTokenKind.Date, "datetime", Fixed: true),
                new(SnippetTokenKind.Tab),
                new(SnippetTokenKind.Date, "yyyy-MM-dd"),
                new(SnippetTokenKind.Date, string.Empty),
                new(SnippetTokenKind.Time, "time", Fixed: true),
                new(SnippetTokenKind.Text, " "),
                new(SnippetTokenKind.Clipboard),
                new(SnippetTokenKind.Cursor),
                new(SnippetTokenKind.Text, " $|$ {{prompt:open"),
            },
            SnippetTemplate.Parse("Hi {{prompt:Name}}, {{prompt?:}}\n{{datetime}}\t{{date:yyyy-MM-dd}}{{date:}}{{time}} {{clipboard}}$|$ $|$ {{prompt:open").ToArray());
        CollectionAssert.AreEqual(
            new[] { (2, 5), (6, 19), (20, 32), (37, 46) },
            SnippetTemplate.FindVariables("a $|$ {{clipboard}} {{prompt:x}} $|$ {{date:}}").ToArray());

        // PERF: unclosed values used to be searched again from each one, and plain text was copied a character at a time.
        string unclosed = string.Concat(Enumerable.Repeat("{{prompt:", 120_000));
        string plain = new('x', 1 << 20);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        IReadOnlyList<SnippetToken> unclosedTokens = SnippetTemplate.Parse(unclosed);
        IReadOnlyList<(int Start, int End)> unclosedVariables = SnippetTemplate.FindVariables(unclosed);
        IReadOnlyList<SnippetToken> plainTokens = SnippetTemplate.Parse(plain);
        clock.Stop();

        Assert.AreEqual(new SnippetToken(SnippetTokenKind.Text, unclosed), unclosedTokens.Single());
        Assert.AreEqual(0, unclosedVariables.Count);
        Assert.AreEqual(new SnippetToken(SnippetTokenKind.Text, plain), plainTokens.Single());
        Assert.IsTrue(clock.ElapsedMilliseconds < 1000, $"{clock.ElapsedMilliseconds} ms");
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
        // Answers are not read for values: no paste from a typed {{clipboard}}, and a typed placeholder is not taken
        // for the snippet's next question.
        string expanded = Variables.Expand(
            "Path: {{prompt:Folder}}\n{{prompt:Note}} [{{clipboard}}]",
            label => label == "Folder" ? @"C:\temp\new" : "{{clipboard}} {{date}} \\t {{prompt:Folder}}",
            () => "copied");

        Assert.AreEqual("Path: C:\\temp\\new\n{{clipboard}} {{date}} \\t {{prompt:Folder}} [copied]", expanded);
    }

    [TestMethod]
    public void CancellingAPromptLeavesItsSpotBlank()
    {
        Assert.AreEqual("Hi , bye\tnow", Variables.Expand("Hi {{prompt:Name}}, bye\tnow", _ => null));
    }

    [TestMethod]
    public void ABackslashIsTypedAsItIs()
    {
        // The replacement is the exact text: a line break or a tab is the character itself, and "\n" is a backslash
        // and an n.
        foreach (string snippet in (ReadOnlySpan<string>)[@"C:\temp\new", @"\n and \t", @"\\server\share", "a\nb\tc", @"end\"])
        {
            Assert.AreEqual(snippet, Variables.Expand(snippet, _ => null), snippet);
        }

        Assert.AreEqual("Path: \\new\n", Variables.Expand("Path: \\new{{date:}}\n", _ => null), "Each piece of text between values is read the same way.");
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
    public void AFormattedDateIsTypedAsItPrints()
    {
        // FUZZ-2: a format that prints a backslash and "n" keeps them, in either syntax, beside the snippet's own
        // line break.
        (string text, int cursor) = Variables.ExpandWithPromptRequests(@"[{{date:\\n}}]" + "\n" + @"[{{date:%Y\t}}]", _ => null, null, SettingsSampleMoment, EnglishUS);

        Assert.AreEqual("[\\n]\n[2026\\t]", text);
        Assert.AreEqual(-1, cursor);
        Assert.AreEqual("\\n", Variables.FormatDate(new DateTime(2024, 2, 29), @"\\n", EnglishUS), "The formatter itself is unchanged.");

        // Nor can its output and the text after it make a placeholder.
        Assert.AreEqual("{{clipboard}} x", Variables.Expand("{{date:'{{clipboard'}}}} x", _ => null, () => "pasted"));
    }

    [TestMethod]
    public void OnlyTheSnippetsOwnMarkerPlacesTheCursor()
    {
        // QA-10: a "$|$" typed into a prompt, copied, or printed by a date stays literal, and the
        // cursor goes where the snippet put its marker.
        (string text, int cursor) = Variables.ExpandWithPromptRequests(
            "{{prompt:A}} {{clipboard}} {{date:'$|$'}} [$|$] $|$",
            _ => "a$|$",
            () => "c$|$");

        Assert.AreEqual("a$|$ c$|$ $|$ [] $|$", text);
        Assert.AreEqual(text.IndexOf("[]", StringComparison.Ordinal) + 1, cursor);

        Assert.AreEqual((string.Empty, 0), Variables.ExpandWithPromptRequests("$|$", _ => null));
        Assert.AreEqual(("abc", -1), Variables.ExpandWithPromptRequests("abc", _ => null));
        Assert.AreEqual(("x\ny", 1), Variables.ExpandWithPromptRequests("x$|$\ny", _ => null), "Counted in the text as it's typed.");

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
        string text = string.Concat(Enumerable.Repeat("Dear {{prompt:Name}}, on {{date}} at {{time}}\n", 2000));
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
