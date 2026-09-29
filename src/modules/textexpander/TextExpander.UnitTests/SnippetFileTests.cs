// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class SnippetFileTests
{
    [TestMethod]
    public void EditableModelAgreesWithEngineParserAndPreservesLines()
    {
        string[] lines = ["// header", string.Empty, "#sig=Best regards, Yu", "#block<<<", "one", "two", ">>>", "not a snippet"];
        SnippetFile file = SnippetFile.Parse(lines);
        Dictionary<string, string> engine = SnippetStore.Parse(lines);
        Assert.AreEqual(engine.Count, file.Snippets.Count);
        CollectionAssert.AreEqual(lines, file.ToLines());
        Assert.AreEqual("one\ntwo", engine["#block"]);

        file.Upsert(file.Snippets[0].Id, "#sig", "Best regards,\nYu");
        Dictionary<string, string> reparsed = SnippetStore.Parse(file.ToLines());
        Assert.AreEqual("Best regards,\nYu", reparsed["#sig"]);
        Assert.AreEqual("one\ntwo", reparsed["#block"]);
    }

    [TestMethod]
    public void InvalidTriggersAndDangerousReplacementsAreRejected()
    {
        Assert.IsFalse(SnippetFile.IsValidTrigger("a=b", out _));
        Assert.IsFalse(SnippetFile.IsValidTrigger("oops<<<", out _));
        Assert.IsFalse(SnippetFile.IsValidTrigger("//x", out _));
        Assert.IsFalse(SnippetFile.IsValidTrigger("a\nb", out _));
        Assert.IsTrue(SnippetFile.IsValidTrigger("#sig", out _));
        Assert.IsFalse(SnippetFile.IsValidReplacement("a\n>>>\nb", out _));
        Assert.IsTrue(SnippetFile.IsValidReplacement(">>>", out _));
    }

    [TestMethod]
    public void EditingOneEntryRewritesOnlyThatEntry()
    {
        string[] lines = ["// header", string.Empty, "#sig=Best regards, Yu", "#block<<<", "one", "two", ">>>", "// tail"];
        SnippetFile file = SnippetFile.Parse(lines);
        file.Upsert(file.Snippets[0].Id, "#sig", "Best regards,\nYu");
        CollectionAssert.AreEqual(
            new[]
        {
            "// header",
            string.Empty,
            "#sig<<<",
            "Best regards,",
            "Yu",
            ">>>",
            "#block<<<",
            "one",
            "two",
            ">>>",
            "// tail",
        },
            file.ToLines());
    }

    [TestMethod]
    public void AddRenameRemoveAndDuplicateDetectionMatchEngineView()
    {
        SnippetFile file = SnippetFile.Parse(["#sig=old", "#block<<<", "one", "two", ">>>"]);
        file.Upsert(null, "#new", "added");
        file.Upsert(file.Snippets[1].Id, "#renamed", "one\ntwo");
        file.Remove(file.Snippets[0].Id);
        Dictionary<string, string> parsed = SnippetStore.Parse(file.ToLines());
        Assert.AreEqual("added", parsed["#new"]);
        Assert.AreEqual("one\ntwo", parsed["#renamed"]);
        Assert.IsFalse(parsed.ContainsKey("#block"));
        Assert.IsFalse(parsed.ContainsKey("#sig"));

        SnippetFile duplicates = SnippetFile.Parse(["x=first", "x=second"]);
        Assert.AreEqual(2, duplicates.Snippets.Count);
        Assert.IsNotNull(duplicates.FindDuplicate("x", ignoringId: duplicates.Snippets[^1].Id));
        SnippetFile single = SnippetFile.Parse(["x=first"]);
        Assert.IsNull(single.FindDuplicate("x", ignoringId: single.Snippets[0].Id));
    }

    [TestMethod]
    public void AcceptedTriggersRoundTripThroughEngineParser()
    {
        foreach (string trigger in (ReadOnlySpan<string>)["#sig", "btw", ":shrug", "#a-b_c", "#\u00e9t\u00e9", ">>>"])
        {
            Assert.IsTrue(SnippetFile.IsValidTrigger(trigger, out _));
            SnippetFile file = SnippetFile.Parse([]);
            file.Upsert(null, trigger, "value");
            Assert.AreEqual("value", SnippetStore.Parse(file.ToLines())[trigger]);
        }
    }

    [TestMethod]
    public void ReplacementsCannotOpenOrCloseUnexpectedBlocks()
    {
        SnippetFile file = SnippetFile.Parse(["a=old", "b=keep"]);
        file.Upsert(file.Snippets.First(s => s.Trigger == "a").Id, "a", "literal<<<   ");
        Dictionary<string, string> parsed = SnippetStore.Parse(file.ToLines());
        Assert.AreEqual("literal<<<   ", parsed["a"]);
        Assert.AreEqual("keep", parsed["b"]);
        Assert.ThrowsException<ArgumentException>(() => SnippetFile.Parse([]).Upsert(null, "a", "x\n>>>\ny"));
    }

    [TestMethod]
    public void LoneCarriageReturnsNormalizeWithoutOverwritingNeighbor()
    {
        SnippetFile file = SnippetFile.Parse(["a=old", "b=keep"]);
        file.Upsert(file.Snippets.First(s => s.Trigger == "a").Id, "a", "before\rb=overwritten");
        Dictionary<string, string> parsed = SnippetStore.Parse(file.ToLines());
        Assert.AreEqual("before\nb=overwritten", parsed["a"]);
        Assert.AreEqual("keep", parsed["b"]);
    }

    [TestMethod]
    public void UnterminatedBlockCanStillReceiveAppendedSnippet()
    {
        SnippetFile file = SnippetFile.Parse(["a<<<", "first"]);
        file.Upsert(null, "b", "second");
        Dictionary<string, string> parsed = SnippetStore.Parse(file.ToLines());
        Assert.AreEqual("first", parsed["a"]);
        Assert.AreEqual("second", parsed["b"]);
        Assert.IsTrue(SnippetFile.Parse(["a<<<", "first"]).ToLines().SequenceEqual(["a<<<", "first"]));
    }

    [TestMethod]
    public void StaleIdsAndRenameCollisionsThrow()
    {
        SnippetFile stale = SnippetFile.Parse(["x=first"]);
        int id = stale.Snippets[0].Id;
        stale.Remove(id);
        Assert.ThrowsException<InvalidOperationException>(() => stale.Upsert(id, "x", "v"));

        SnippetFile clash = SnippetFile.Parse(["a=1", "b=2"]);
        Assert.ThrowsException<InvalidOperationException>(() => clash.Upsert(clash.Snippets.First(s => s.Trigger == "a").Id, "b", "3"));
    }

    [TestMethod]
    public void AdditionalInvalidTriggersAreRejected()
    {
        Assert.IsFalse(SnippetFile.IsValidTrigger("#sig ", out _));
        Assert.IsFalse(SnippetFile.IsValidTrigger("  ", out _));
        Assert.IsFalse(SnippetFile.IsValidTrigger("a\rb", out _));
        Assert.IsFalse(SnippetFile.IsValidTrigger("\uFEFFx", out _));
    }
}
