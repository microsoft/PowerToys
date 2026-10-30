// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class SnippetFileTests
{
    [TestMethod]
    public void WhatIsWrittenReadsBackTheSameSnippets()
    {
        var file = new SnippetFile();
        (string Trigger, string Replacement)[] snippets =
        [
            ("!sig", "Best regards,\nYu"),
            ("!tabs", "Name\tEmail"),
            ("!values", "{{date:dddd}} $|$ {{prompt:First name}} {{clipboard}}"),
            ("!marks", "\"quoted\" C:\\temp\\new \\n <b>&amp;</b> 'single'"),
            ("!caf\u00e9", "\u00e9t\u00e9 \u00b7 \u4e2d\u6587 \U0001F600"),
            ("a=b", "// <<< >>>"),
            ("!empty", string.Empty),
        ];
        foreach ((string trigger, string replacement) in snippets)
        {
            file.Upsert(null, trigger, replacement);
        }

        byte[] bytes = file.ToBytes();
        SnippetFile read = SnippetFile.Read(bytes);
        CollectionAssert.AreEqual(snippets, read.Snippets.Select(entry => (entry.Trigger, entry.Replacement)).ToArray());

        string text = Encoding.UTF8.GetString(bytes);
        Assert.AreNotEqual(0xEF, bytes[0], "No byte order mark.");
        StringAssert.StartsWith(text, "{" + Environment.NewLine + "  \"version\": 1," + Environment.NewLine + "  \"snippets\": [");
        StringAssert.Contains(text, "\"replacement\": \"Best regards,\\nYu\"", "Line breaks are written as \\n.");
        StringAssert.Contains(text, "\"replacement\": \"Name\\tEmail\"");
        StringAssert.Contains(text, "\u00e9t\u00e9 \u00b7 \u4e2d\u6587", "Letters and symbols are written as they are, for people to read.");
        StringAssert.Contains(text, "<b>&amp;</b> 'single'");
    }

    [TestMethod]
    public void HandEditsWithCommentsTrailingCommasAndAByteOrderMarkStillRead()
    {
        const string Json = """
            // My snippets
            {
              /* written by hand */
              "version": 1,
              "snippets": [
                { "trigger": "!a", "replacement": "one", }, // a trailing comma
                { "trigger": "!b", "replacement": "two" },
              ],
            }
            """;
        byte[] withMark = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(Json)];
        foreach (byte[] bytes in new[] { Encoding.UTF8.GetBytes(Json), withMark })
        {
            SnippetFile file = SnippetFile.Read(bytes);
            Assert.AreEqual("!a=one,!b=two", string.Join(",", file.Snippets.Select(entry => entry.Trigger + "=" + entry.Replacement)));
        }
    }

    [TestMethod]
    public void UnknownPropertiesTheVersionAndMissingValuesArePassedOver()
    {
        const string Json = """
            {
              "version": 7,
              "theme": { "nested": [1, 2, { "deep": true }] },
              "Snippets": [
                { "Trigger": "!a", "Replacement": "one", "enabled": false, "tags": ["x"] },
                { "trigger": "!b" },
                { "replacement": "no trigger" },
                null,
                { "trigger": null, "replacement": null }
              ]
            }
            """;
        SnippetFile file = SnippetFile.Read(Encoding.UTF8.GetBytes(Json));

        // A later version may add properties, and a property's capitalization doesn't matter.
        Assert.AreEqual("!a=one|!b=|=no trigger|=", string.Join("|", file.Snippets.Select(entry => entry.Trigger + "=" + entry.Replacement)));
        Assert.IsFalse(SnippetFile.IsValidTrigger(file.Snippets[2].Trigger, out _), "A snippet without a trigger is kept, and can be fixed in the editor.");
    }

    [TestMethod]
    public void PropertiesThisVersionDoesNotKnowAreWrittenBack()
    {
        const string Json = """
            {
              "version": 1,
              "theme": { "nested": [1, 2, { "deep": true }] },
              "snippets": [
                { "trigger": "!a", "replacement": "one", "enabled": false, "tags": ["x"] },
                { "trigger": "!b", "replacement": "two" }
              ]
            }
            """;
        SnippetFile file = SnippetFile.Read(Encoding.UTF8.GetBytes(Json)).Clone();
        file.Upsert(file.Snippets[0].Id, "!a", "edited");
        file.Upsert(null, "!c", "three");

        using JsonDocument written = JsonDocument.Parse(file.ToBytes());
        JsonElement root = written.RootElement;
        Assert.IsTrue(root.GetProperty("theme").GetProperty("nested")[2].GetProperty("deep").GetBoolean());
        JsonElement[] snippets = [.. root.GetProperty("snippets").EnumerateArray()];
        Assert.AreEqual("edited", snippets[0].GetProperty("replacement").GetString());
        Assert.IsFalse(snippets[0].GetProperty("enabled").GetBoolean(), "An edited snippet keeps them.");
        Assert.AreEqual("x", snippets[0].GetProperty("tags")[0].GetString());
        Assert.IsFalse(snippets[2].TryGetProperty("enabled", out _), "A new snippet has none.");

        SnippetFile twice = SnippetFile.Read("{ \"a\": 1, \"a\": 2, \"snippets\": [ { \"trigger\": \"!x\", \"b\": 1, \"b\": 2 } ] }"u8);
        using JsonDocument kept = JsonDocument.Parse(twice.ToBytes());
        Assert.AreEqual(2, kept.RootElement.GetProperty("a").GetInt32(), "The same property twice keeps the last, as for snippets.");
        Assert.AreEqual(2, kept.RootElement.GetProperty("snippets")[0].GetProperty("b").GetInt32());
    }

    [TestMethod]
    public void TextThatCantBeWrittenBackStillReadsAndExpands()
    {
        // Half of an emoji, as a script that cuts text short may leave it, in a property Text Expander doesn't use.
        byte[] bytes = Encoding.UTF8.GetBytes("""{ "version": 1, "snippets": [ { "trigger": "!a", "replacement": "x", "note": "\ud83d" } ] }""");

        SnippetFile file = SnippetFile.Read(bytes);
        Assert.AreEqual("!a=x", string.Join("|", file.Snippets.Select(entry => entry.Trigger + "=" + entry.Replacement)));
        Assert.AreEqual("x", SnippetStore.Parse(bytes)["!a"], "The engine expands it.");

        SnippetFileWriteException error = Assert.ThrowsException<SnippetFileWriteException>(() => file.ToBytes());
        Assert.IsInstanceOfType<InvalidOperationException>(error, "It isn't taken for a file that can't be read.");
    }

    [TestMethod]
    [DataRow("{ \"snippets\": [] }", 1, DisplayName = "none")]
    [DataRow("{ \"version\": 1, \"snippets\": [] }", 1, DisplayName = "this version")]
    [DataRow("{ \"version\": 7, \"snippets\": [] }", 7, DisplayName = "a newer version")]
    public void TheVersionIsReadAndWrittenBack(string json, int version)
    {
        SnippetFile file = SnippetFile.Read(Encoding.UTF8.GetBytes(json));

        Assert.AreEqual(version, file.Version);
        Assert.AreEqual(version, SnippetFile.Read(file.Clone().ToBytes()).Version);
    }

    [TestMethod]
    [DataRow("{ \"version\": \"1\", \"snippets\": [] }", DisplayName = "text")]
    [DataRow("{ \"version\": 1.0, \"snippets\": [] }", DisplayName = "a fraction")]
    public void AVersionThatIsNotAWholeNumberIsAMistake(string json)
    {
        Assert.AreEqual(1L, Assert.ThrowsException<SnippetFileFormatException>(() => SnippetFile.Read(Encoding.UTF8.GetBytes(json))).LineNumber);
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty")]
    [DataRow("!a=one", DisplayName = "the old text format")]
    [DataRow("{ \"snippets\": [ { \"trigger\": \"!a\" } ", DisplayName = "cut off")]
    [DataRow("{ \"snippets\": { \"trigger\": \"!a\" } }", DisplayName = "an object for the list")]
    [DataRow("{ \"snippets\": [ \"!a\" ] }", DisplayName = "a text for a snippet")]
    [DataRow("{ \"snippets\": [ { \"trigger\": 5 } ] }", DisplayName = "a number for a trigger")]
    [DataRow("[ { \"trigger\": \"!a\", \"replacement\": \"one\" } ]", DisplayName = "a list without the object")]
    public void AFileThatIsNotJsonSaysOnWhichLine(string json)
    {
        SnippetFileFormatException error = Assert.ThrowsException<SnippetFileFormatException>(() => SnippetFile.Read(Encoding.UTF8.GetBytes(json)));
        Assert.IsTrue(error.LineNumber >= 1, error.Message);
        Assert.IsInstanceOfType<IOException>(error, "Callers report it like any file they can't read.");
    }

    [TestMethod]
    public void TheLineNumberCountsFromOne()
    {
        string json = "{\n  \"snippets\": [\n    { \"trigger\": \"!a\" \"replacement\": \"one\" }\n  ]\n}";
        Assert.AreEqual(3L, Assert.ThrowsException<SnippetFileFormatException>(() => SnippetFile.Read(Encoding.UTF8.GetBytes(json))).LineNumber);
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("{ \"version\": 1 }")]
    [DataRow("{ \"snippets\": null }")]
    [DataRow("null")]
    public void JsonWithoutASnippetsListIsNotASnippetsFile(string json)
    {
        SnippetFileFormatException error = Assert.ThrowsException<SnippetFileFormatException>(() => SnippetFile.Read(Encoding.UTF8.GetBytes(json)));
        Assert.IsNull(error.LineNumber);
        Assert.AreEqual(0, SnippetFile.Read(Encoding.UTF8.GetBytes("{ \"snippets\": [] }")).Snippets.Count, "An empty list is a snippets file.");
    }

    [TestMethod]
    public void DuplicatesStayInTheFileAndTheLastOneExpands()
    {
        byte[] bytes = SnippetJson.Bytes("x=first", "y=other", "x=second");
        SnippetFile file = SnippetFile.Read(bytes);
        Assert.AreEqual(3, file.Snippets.Count);
        Assert.AreSame(file.Snippets[0], file.FindDuplicate("x", ignoringId: file.Snippets[2].Id));
        Assert.AreEqual(3, SnippetFile.Read(file.ToBytes()).Snippets.Count, "Saving keeps both.");

        Dictionary<string, string> engine = SnippetStore.Parse(bytes);
        Assert.AreEqual("second", engine["x"]);
        Assert.AreEqual(2, engine.Count);

        // Without case sensitivity, the last spelling wins too.
        Dictionary<string, string> anyCase = SnippetStore.Parse(SnippetJson.Bytes("Key=first", "key=second"), caseSensitive: false);
        Assert.AreEqual("second", anyCase["KEY"]);
        Assert.AreEqual("key", anyCase.Keys.Single());
    }

    [TestMethod]
    public void SnippetsFollowEditsWithoutRebuildingTheList()
    {
        // PERF-6: every read of Snippets filtered the whole file into a new list.
        SnippetFile file = SnippetFile.Read(SnippetJson.Bytes("#a=1", "#b=two", "#a=again"));
        IReadOnlyList<SnippetFile.Entry> snippets = file.Snippets;
        Assert.AreSame(snippets, file.Snippets);
        Assert.AreEqual("#a,#b,#a", string.Join(",", snippets.Select(entry => entry.Trigger)));

        SnippetFile.Entry added = file.Upsert(null, "#c", "3");
        Assert.AreEqual("#a,#b,#a,#c", string.Join(",", snippets.Select(entry => entry.Trigger)), "The list follows an added snippet.");
        file.Upsert(added.Id, "#d", "4");
        Assert.AreEqual("#d", snippets[3].Trigger);

        Assert.IsTrue(file.Remove(snippets[0].Id));
        Assert.IsFalse(file.Remove(9999));
        Assert.AreEqual("#b,#a,#d", string.Join(",", snippets.Select(entry => entry.Trigger)));
        Assert.ThrowsException<InvalidOperationException>(() => file.Upsert(9999, "#x", "y"));
        Assert.AreEqual("#b=two|#a=again|#d=4", string.Join("|", SnippetStore.Parse(file.ToBytes()).Select(pair => pair.Key + "=" + pair.Value)));
    }

    [TestMethod]
    public void ACloneIsEditedOnItsOwn()
    {
        SnippetFile file = SnippetFile.Read(SnippetJson.Bytes("#a=1"));
        SnippetFile clone = file.Clone();
        clone.Upsert(clone.Snippets[0].Id, "#a", "changed");
        SnippetFile.Entry added = clone.Upsert(null, "#b", "2");

        Assert.AreEqual("1", file.Snippets.Single().Replacement);
        Assert.AreEqual(1, added.Id, "A clone keeps numbering where the file was.");
    }

    [TestMethod]
    public void StaleIdsAndRenameCollisionsThrow()
    {
        SnippetFile stale = SnippetFile.Read(SnippetJson.Bytes("x=first"));
        int id = stale.Snippets[0].Id;
        stale.Remove(id);
        Assert.ThrowsException<InvalidOperationException>(() => stale.Upsert(id, "x", "v"));

        SnippetFile clash = SnippetFile.Read(SnippetJson.Bytes("a=1", "b=2"));
        Assert.ThrowsException<InvalidOperationException>(() => clash.Upsert(clash.Snippets.First(s => s.Trigger == "a").Id, "b", "3"));
        Assert.ThrowsException<ArgumentException>(() => clash.Upsert(null, " c", "3"));
    }

    [TestMethod]
    public void LineBreaksAreKeptAsNewLines()
    {
        var file = new SnippetFile();
        file.Upsert(null, "a", "one\r\ntwo\rthree\nfour");
        Assert.AreEqual("one\ntwo\nthree\nfour", SnippetFile.Read(file.ToBytes()).Snippets.Single().Replacement);

        // A file can still hold others, typed exactly as they are.
        Assert.AreEqual("one\r\ntwo", SnippetFile.Read(Encoding.UTF8.GetBytes("""{ "snippets": [ { "trigger": "a", "replacement": "one\r\ntwo" } ] }""")).Snippets.Single().Replacement);
    }

    [TestMethod]
    public void OnlyTriggersThatCantBeTypedAreRejected()
    {
        // The text format's own rules are gone: these are triggers like any other.
        foreach (string trigger in (ReadOnlySpan<string>)["a=b", "oops<<<", "//x", ">>>", "#sig", ":shrug", "#\u00e9t\u00e9"])
        {
            Assert.IsTrue(SnippetFile.IsValidTrigger(trigger, out SnippetProblem none), trigger);
            Assert.AreEqual(SnippetProblem.None, none);
        }

        foreach ((string trigger, SnippetProblem expected) in new[]
        {
            (string.Empty, SnippetProblem.EmptyTrigger),
            ("  ", SnippetProblem.EmptyTrigger),
            ("a\nb", SnippetProblem.TriggerHasLineBreak),
            ("a\rb", SnippetProblem.TriggerHasLineBreak),
            ("\uFEFFx", SnippetProblem.TriggerStartsWithHiddenCharacter),
            ("#sig ", SnippetProblem.TriggerHasOuterSpace),
            (" #sig", SnippetProblem.TriggerHasOuterSpace),
        })
        {
            Assert.IsFalse(SnippetFile.IsValidTrigger(trigger, out SnippetProblem problem), trigger);
            Assert.AreEqual(expected, problem, trigger);
        }
    }
}
