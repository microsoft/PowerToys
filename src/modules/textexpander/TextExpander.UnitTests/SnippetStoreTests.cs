// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class SnippetStoreTests
{
    [TestMethod]
    public void ConfiguredPathUsesFolderFileOrHostDefault()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string folder = Path.Combine(scratch.Path, "library");
        Assert.AreEqual(Path.Combine(folder, "snippets.txt"), SnippetStore.ResolveConfiguredPath(folder, null));
        Assert.IsTrue(Directory.Exists(folder));

        string direct = Path.Combine(scratch.Path, "direct.txt");
        File.WriteAllText(direct, "#x=y");
        Assert.AreEqual(direct, SnippetStore.ResolveConfiguredPath(direct, null));

        string host = Path.Combine(scratch.Path, "host");
        Assert.AreEqual(Path.Combine(host, "snippets.txt"), SnippetStore.ResolveConfiguredPath(null, host));
        Assert.AreEqual(Path.Combine(host, "snippets.txt"), SnippetStore.ResolveConfiguredPath("relative", host));
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
        Assert.AreEqual(Path.Combine(host, "snippets.txt"), SnippetStore.ResolveConfiguredPath(null, host));
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
        StringAssert.Contains(text, "Text Expander snippets");
    }
}
