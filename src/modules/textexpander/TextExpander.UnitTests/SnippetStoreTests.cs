// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class SnippetStoreTests
{
    private static readonly string[] StarterTriggers = ["!ddate", "!ttime", "!ddt", "!isodate", "!mysig", "!myemail"];
    private static readonly string[] ExampleTriggers = ["!hello", "!mdlink", "!longdate", "!time24", "!todo", "!myaddr", "!tabhead", "!meeting"];

    [TestMethod]
    public void ConfiguredPathUsesFolderFileOrHostDefault()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string folder = Path.Combine(scratch.Path, "library");
        Assert.AreEqual(Path.Combine(folder, "PowerToysTextExpanderSnippets.txt"), SnippetStore.ResolveConfiguredPath(folder, null));
        Assert.IsTrue(Directory.Exists(folder));

        string direct = Path.Combine(scratch.Path, "direct.txt");
        File.WriteAllText(direct, "#x=y");
        Assert.AreEqual(direct, SnippetStore.ResolveConfiguredPath(direct, null));

        string host = Path.Combine(scratch.Path, "host");
        Assert.AreEqual(Path.Combine(host, "PowerToysTextExpanderSnippets.txt"), SnippetStore.ResolveConfiguredPath(null, host));
        Assert.AreEqual(Path.Combine(host, "PowerToysTextExpanderSnippets.txt"), SnippetStore.ResolveConfiguredPath("relative", host));
    }

    [TestMethod]
    public void AFolderUsesTheNewNameThenTheLegacyName()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        foreach ((bool hasNew, bool hasLegacy, string expected) in new[]
        {
            (false, false, "PowerToysTextExpanderSnippets.txt"),
            (false, true, "snippets.txt"),
            (true, false, "PowerToysTextExpanderSnippets.txt"),
            (true, true, "PowerToysTextExpanderSnippets.txt"),
        })
        {
            string folder = Path.Combine(scratch.Path, $"new-{hasNew}-legacy-{hasLegacy}");
            Directory.CreateDirectory(folder);
            if (hasNew)
            {
                File.WriteAllText(Path.Combine(folder, "PowerToysTextExpanderSnippets.txt"), "!new=1\n");
            }

            if (hasLegacy)
            {
                File.WriteAllText(Path.Combine(folder, "snippets.txt"), "!old=1\n");
            }

            string want = Path.Combine(folder, expected);
            Assert.AreEqual(want, SnippetStore.ResolveInFolder(folder), folder);
            Assert.AreEqual(want, SnippetStore.ResolveConfiguredPath(folder, null), "Configured folder: " + folder);
            Assert.AreEqual(want, SnippetStore.ResolveConfiguredPath(null, folder), "Host folder: " + folder);
        }

        // A file path is used as it is, whatever its name.
        string direct = Path.Combine(scratch.Path, "new-False-legacy-True", "snippets.txt");
        Assert.AreEqual(direct, SnippetStore.ResolveConfiguredPath(direct, null));
    }

    [TestMethod]
    public void AFolderStoreSwitchesWhenThePreferredFileArrivesOrLeaves()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string legacy = Path.Combine(scratch.Path, SnippetStore.LegacyFileName);
        string current = Path.Combine(scratch.Path, SnippetStore.FileName);
        File.WriteAllText(legacy, "!legacy=old\n");

        SnippetPathResolution resolution = SnippetStore.ResolveConfiguredPathInfo(scratch.Path, null);
        Assert.IsTrue(resolution.ResolveFolderNames);
        using var store = new SnippetStore(resolution.FilePath, resolution.ResolveFolderNames);
        Assert.AreEqual(1, store.Load());
        Assert.AreEqual(legacy, store.FilePath);

        File.WriteAllText(current, "!current=new\n");
        Assert.AreEqual(1, store.ReloadPreferredPath());
        Assert.AreEqual(current, store.FilePath);
        Assert.IsTrue(store.Current.Map.ContainsKey("!current"));

        File.Delete(current);
        Assert.AreEqual(1, store.ReloadPreferredPath());
        Assert.AreEqual(legacy, store.FilePath);
        Assert.IsTrue(store.Current.Map.ContainsKey("!legacy"));

        SnippetPathResolution direct = SnippetStore.ResolveConfiguredPathInfo(legacy, null);
        Assert.IsFalse(direct.ResolveFolderNames, "An explicit file path stays on that file.");
    }

    [TestMethod]
    public void ALegacyFileIsReadInPlaceAndNeverRenamed()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string legacy = Path.Combine(scratch.Path, "snippets.txt");
        File.WriteAllText(legacy, "!mine=kept\n");

        string path = SnippetStore.ResolveConfiguredPath(scratch.Path, null);
        Assert.AreEqual(legacy, path);
        using var store = new SnippetStore(path);
        Assert.AreEqual(1, store.Load());
        Assert.AreEqual("!mine=kept\n", File.ReadAllText(legacy));
        Assert.IsFalse(File.Exists(Path.Combine(scratch.Path, "PowerToysTextExpanderSnippets.txt")), "No starter file next to a legacy one.");

        // An empty folder gets the starter file under the new name.
        string empty = Path.Combine(scratch.Path, "empty");
        using var seeded = new SnippetStore(SnippetStore.ResolveConfiguredPath(empty, null));
        Assert.AreEqual(6, seeded.Load());
        Assert.IsTrue(File.Exists(Path.Combine(empty, "PowerToysTextExpanderSnippets.txt")));
        Assert.IsFalse(File.Exists(Path.Combine(empty, "snippets.txt")));
    }

    [TestMethod]
    public void SettingsAndTheEngineAgreeOnTheSnippetFile()
    {
        Assert.AreEqual("PowerToysTextExpanderSnippets.txt", SnippetStore.FileName);
        Assert.AreEqual("snippets.txt", SnippetStore.LegacyFileName);
        Assert.AreEqual(TextExpanderSnippetFileNames.Current, SnippetStore.FileName);
        Assert.AreEqual(TextExpanderSnippetFileNames.Legacy, SnippetStore.LegacyFileName);

        using ScratchDirectory scratch = ScratchDirectory.Create();
        foreach (int files in new[] { 0, 1, 2, 3 })
        {
            string folder = Path.Combine(scratch.Path, files.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);
            if ((files & 1) != 0)
            {
                File.WriteAllText(Path.Combine(folder, SnippetStore.FileName), "!a=1\n");
            }

            if ((files & 2) != 0)
            {
                File.WriteAllText(Path.Combine(folder, SnippetStore.LegacyFileName), "!b=1\n");
            }

            Assert.AreEqual(SnippetStore.ResolveInFolder(folder), TextExpanderSnippetFileNames.ResolveInFolder(folder, File.Exists), folder);
            Assert.AreEqual(files == 0 ? null : SnippetStore.ResolveInFolder(folder), TextExpanderSnippetFileNames.FindInFolder(folder, File.Exists), folder);
        }
    }

    [TestMethod]
    public void ParsingAndRepointingKeepTheLiveStoreObject()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string first = Path.Combine(scratch.Path, "a");
        string second = Path.Combine(scratch.Path, "b");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        File.WriteAllText(Path.Combine(first, "snippets.txt"), "#one=first\n");
        File.WriteAllText(Path.Combine(second, "snippets.txt"), "#two=second\n#three=third\n");

        using var store = new SnippetStore(Path.Combine(first, "snippets.txt"));
        Assert.AreEqual(1, store.Load());
        Assert.IsTrue(store.Current.Map.ContainsKey("#one"));
        Assert.AreEqual(2, store.Repoint(Path.Combine(second, "snippets.txt")));
        Assert.IsTrue(store.Current.Map.ContainsKey("#two"));
        Assert.IsFalse(store.Current.Map.ContainsKey("#one"));
        Assert.AreEqual(-1, store.Repoint(Path.Combine(second, "snippets.txt")));
    }

    [TestMethod]
    public void ConfiguredPathFallsBackOnBlankAndInvalidValues()
    {
        string defaultPath = SnippetStore.ResolveDefaultPath();
        Assert.AreEqual(defaultPath, SnippetStore.ResolveConfiguredPath(null, null));
        Assert.AreEqual(defaultPath, SnippetStore.ResolveConfiguredPath("   ", null));
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string host = Path.Combine(scratch.Path, "host");
        Assert.AreEqual(Path.Combine(host, "PowerToysTextExpanderSnippets.txt"), SnippetStore.ResolveConfiguredPath(null, host));
        Assert.AreEqual(defaultPath, SnippetStore.ResolveConfiguredPath(null, "::\0 not a path"));
    }

    [TestMethod]
    public void ParserHandlesInlineBlocksCommentsAndDuplicateLastWriterWins()
    {
        Dictionary<string, string> parsed = SnippetStore.Parse([
            "// comment",
            "a=one",
            "block<<<",
            "line one",
            "line two",
            ">>>",
            "a=two"]);
        Assert.AreEqual("two", parsed["a"]);
        Assert.AreEqual("line one\nline two", parsed["block"]);
        Assert.AreEqual(2, parsed.Count);
    }

    [TestMethod]
    public void LoadCreatesStarterFileWhenMissing()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.txt");
        using var store = new SnippetStore(path);
        Assert.IsTrue(store.Load() >= 1);
        string text = File.ReadAllText(path);
        StringAssert.StartsWith(text, "// ── PowerToys Text Expander");
    }

    [TestMethod]
    public void StarterSnippetsUseBangTriggersAndNoRuntimeInput()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.txt");
        using var store = new SnippetStore(path);
        Assert.AreEqual(6, store.Load());

        Dictionary<string, string> starters = SnippetStore.Parse(File.ReadAllLines(path));
        CollectionAssert.AreEquivalent(StarterTriggers, starters.Keys.ToArray());
        Assert.AreEqual("{{date}}", starters["!ddate"]);
        Assert.AreEqual("{{time}}", starters["!ttime"]);
        Assert.AreEqual("{{datetime}}", starters["!ddt"]);
        Assert.AreEqual("{{isodate}}", starters["!isodate"]);
        Assert.AreEqual(@"Best regards,\nYour Name", starters["!mysig"]);
        Assert.AreEqual("you@example.com", starters["!myemail"]);

        foreach ((string trigger, string replacement) in starters)
        {
            Assert.IsTrue(SnippetFile.IsValidTrigger(trigger, out _), trigger);

            // An accidental trigger must never paste the clipboard or open a prompt.
            Assert.IsFalse(replacement.Contains("{{clipboard}}", StringComparison.Ordinal), trigger);
            Assert.IsFalse(Variables.NeedsPrompt(replacement), trigger);

            // Without word mode a trigger that prefixes another would always fire first.
            Assert.IsFalse(starters.Keys.Any(other => other != trigger && other.StartsWith(trigger, StringComparison.Ordinal)), trigger);
        }

        // The header documents every variable and the cursor marker.
        string text = File.ReadAllText(path);
        foreach (string token in new[] { "{{date}}", "{{time}}", "{{datetime}}", "{{isodate}}", "{{date:FORMAT}}", "{{date:dddd, MMMM d}}", "{{clipboard}}", "{{prompt:Question}}", "{{prompt?:Question}}", "$|$", "\\n", "\\t", "<<<", ">>>" })
        {
            StringAssert.Contains(text, token);
        }

        // An existing library is never replaced by the starters.
        File.WriteAllText(path, "!mine=kept\n");
        Assert.AreEqual(1, store.Load());
        Assert.AreEqual("!mine=kept\n", File.ReadAllText(path));
    }

    [TestMethod]
    public void StarterFileOpensWithTheCredit()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.txt");
        using var store = new SnippetStore(path);
        store.Load();

        string[] lines = File.ReadAllLines(path);
        StringAssert.StartsWith(lines[0], "// ── PowerToys Text Expander ──");
        Assert.AreEqual("// Built by Gary Hsu · https://github.com/howdygary", lines[1]);
        Assert.AreEqual("// Project: https://github.com/howdygary/text-expander", lines[2]);
        Assert.AreEqual("// Website: https://howdygary.github.io/text-expander/", lines[3]);
    }

    [TestMethod]
    public void StarterGuideLinesUpAndItsDateTipWorks()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.txt");
        using var store = new SnippetStore(path);
        store.Load();

        // QA-21: every description in the variables guide starts in the same column.
        string[] lines = File.ReadAllLines(path);
        int start = Array.FindIndex(lines, l => l.Contains("── Variables", StringComparison.Ordinal));
        int end = Array.FindIndex(lines, l => l.Contains("── Tips", StringComparison.Ordinal));
        string[] guide = lines[(start + 1)..end].Where(line => line != "//").ToArray();
        int column = guide[0].IndexOf("today's date", StringComparison.Ordinal);
        Assert.IsTrue(guide.Length >= 10 && column > 0);
        foreach (string line in guide)
        {
            Assert.IsTrue(line.Length > column && line[column - 1] == ' ' && line[column] != ' ', line);
        }

        // The variables with nothing to show ahead of time each get an example.
        StringAssert.Contains(string.Join('\n', guide), "{{prompt:First name}}");
        Assert.IsTrue(guide.Any(line => line.StartsWith("// {{clipboard}}", StringComparison.Ordinal) && line.Contains("such as", StringComparison.Ordinal)));
        Assert.IsTrue(guide.Any(line => line.StartsWith("// $|$", StringComparison.Ordinal) && line.Contains("Dear $|$", StringComparison.Ordinal)));

        // The tip about words in a date format gives what it says.
        string tip = guide.Single(line => line.Contains("{{date:'", StringComparison.Ordinal));
        int open = tip.IndexOf("{{date:", StringComparison.Ordinal) + "{{date:".Length;
        int close = tip.IndexOf("}}", open, StringComparison.Ordinal);
        string said = tip[(tip.IndexOf(" gives ", StringComparison.Ordinal) + " gives ".Length)..];
        Assert.AreEqual(said, Variables.FormatDate(new DateTime(2026, 6, 1), tip[open..close]));
        Assert.AreNotEqual(said, Variables.FormatDate(new DateTime(2026, 6, 1), tip[open..close].Replace("'", string.Empty, StringComparison.Ordinal)), "Without the quotes the word is read as codes.");
    }

    [TestMethod]
    public void StarterExamplesWorkOnceUncommented()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.txt");
        using var store = new SnippetStore(path);
        store.Load();

        // Do exactly what the heading says, on every line of the examples.
        string[] lines = File.ReadAllLines(path);
        int heading = Array.FindIndex(lines, l => l.Contains("── Examples:", StringComparison.Ordinal));
        StringAssert.Contains(lines[heading], "remove the // and the space after it", "QA-6: the instruction names exactly what the test removes.");
        int first = heading + 1;
        int last = Array.FindIndex(lines, l => l.Contains("── Your snippets", StringComparison.Ordinal));
        Assert.IsTrue(first > 0 && last > first, "The examples sit between their heading and Your snippets.");
        Assert.IsTrue(lines[first..last].Where(line => line != "//").All(line => line.StartsWith("// ", StringComparison.Ordinal)), "Every example line has the // and the space to remove.");
        string[] uncommented = lines
            .Select((line, i) => i >= first && i < last && line != "//" ? line["// ".Length..] : line)
            .ToArray();

        Dictionary<string, string> all = SnippetStore.Parse(uncommented);
        CollectionAssert.AreEquivalent(StarterTriggers.Concat(ExampleTriggers).ToArray(), all.Keys.ToArray());
        Assert.AreEqual("Hi {{prompt:First name}}, thanks for reaching out!", all["!hello"]);
        Assert.AreEqual("[{{prompt:Link text}}]({{clipboard}})", all["!mdlink"]);
        Assert.AreEqual("{{date:dddd, MMMM d, yyyy}}", all["!longdate"]);
        Assert.AreEqual("{{date:HH:mm}}", all["!time24"]);
        Assert.AreEqual("// TODO: $|$ ({{isodate}})", all["!todo"]);
        Assert.AreEqual(@"123 Example Street\nSpringfield, ZZ 12345", all["!myaddr"]);
        Assert.AreEqual(@"Name\tEmail\tTeam", all["!tabhead"]);
        Assert.AreEqual("Meeting notes · {{date:dddd, MMMM d, yyyy}}\nAttendees: $|$\nNext steps:", all["!meeting"]);

        foreach (string trigger in all.Keys)
        {
            Assert.IsTrue(SnippetFile.IsValidTrigger(trigger, out _), trigger);

            // Without word mode a trigger that starts another would always fire first.
            Assert.IsFalse(all.Keys.Any(other => other != trigger && other.StartsWith(trigger, StringComparison.Ordinal)), trigger);
        }

        // Left commented, the examples stay inert: only the six starters load.
        Assert.AreEqual(6, SnippetStore.Parse(lines).Count);
    }
}
