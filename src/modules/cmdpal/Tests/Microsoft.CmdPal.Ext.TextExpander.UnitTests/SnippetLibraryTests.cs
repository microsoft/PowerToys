// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;
using Microsoft.PowerToys.TextExpander;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

[TestClass]
public sealed class SnippetLibraryTests
{
    [TestMethod]
    public void Effective_DuplicateTriggers_LastOneWinsAsInTextExpander()
    {
        var file = SnippetFile.Read("""
            { "snippets": [
              { "trigger": "!sig", "replacement": "First" },
              { "trigger": "!email", "replacement": "you@example.com" },
              { "trigger": "!sig", "replacement": "Second" }
            ] }
            """u8);

        var snippets = SnippetLibrary.Effective(file.Snippets, caseSensitive: true);
        string[] expected = ["!email", "!sig"];

        CollectionAssert.AreEqual(expected, snippets.Select(snippet => snippet.Trigger).ToArray());
        Assert.AreEqual("Second", snippets[1].Replacement);
    }

    [TestMethod]
    public void Effective_IgnoringCapitalization_MergesTriggersThatDifferOnlyInCase()
    {
        var file = SnippetFile.Read("""
            { "snippets": [
              { "trigger": "Sig", "replacement": "First" },
              { "trigger": "sig", "replacement": "Second" }
            ] }
            """u8);

        Assert.AreEqual(2, SnippetLibrary.Effective(file.Snippets, caseSensitive: true).Count);
        Assert.AreEqual("Second", SnippetLibrary.Effective(file.Snippets, caseSensitive: false).Single().Replacement);
    }

    [TestMethod]
    public void Effective_TriggersThatCannotBeTyped_AreLeftOut()
    {
        var file = SnippetFile.Read("""
            { "snippets": [
              { "trigger": "", "replacement": "Empty" },
              { "replacement": "No trigger" },
              { "trigger": " !pad", "replacement": "Space" },
              { "trigger": "!a\nb", "replacement": "Line break" },
              { "trigger": "\uFEFF!bom", "replacement": "Hidden" },
              { "trigger": "!ok", "replacement": "Fine" }
            ] }
            """u8);

        Assert.AreEqual("!ok", SnippetLibrary.Effective(file.Snippets, caseSensitive: true).Single().Trigger);
    }

    [TestMethod]
    public void Load_FileUnchanged_ReturnsWhatWasReadBefore()
    {
        using var folder = new TestFolder();
        var path = folder.WriteSnippets([("!mysig", "Best regards,\nYour name")]);
        var library = new SnippetLibrary();

        var first = library.Load(path, isDefault: true, caseSensitive: true);

        Assert.AreSame(first, library.Load(path, isDefault: true, caseSensitive: true));
        Assert.AreNotSame(first, library.Load(path, isDefault: true, caseSensitive: false));
    }

    [TestMethod]
    public void Load_FileThatIsNotJson_ReportsTheLine()
    {
        using var folder = new TestFolder();
        var path = Path.Combine(folder.Root, "snippets.json");
        File.WriteAllText(path, "{\n  \"snippets\": [\n    { \"trigger\": \"!a\" \"replacement\": \"b\" }\n  ]\n}");

        var snapshot = new SnippetLibrary().Load(path, isDefault: false, caseSensitive: true);

        Assert.AreEqual(SnippetLibraryStatus.NotSnippetsFile, snapshot.Status);
        Assert.AreEqual(3L, snapshot.ErrorLine);
    }

    [TestMethod]
    public void Load_JsonWithoutASnippetsList_IsNotASnippetsFile()
    {
        using var folder = new TestFolder();
        var path = Path.Combine(folder.Root, "snippets.json");
        File.WriteAllText(path, "{ \"version\": 1 }");

        var snapshot = new SnippetLibrary().Load(path, isDefault: false, caseSensitive: true);

        Assert.AreEqual(SnippetLibraryStatus.NotSnippetsFile, snapshot.Status);
        Assert.IsNull(snapshot.ErrorLine);
    }

    [TestMethod]
    public void Load_FileWithNoSnippets_HasNoSnippetsYet()
    {
        using var folder = new TestFolder();
        var path = folder.WriteSnippets([]);

        Assert.AreEqual(SnippetLibraryStatus.NoSnippetsYet, new SnippetLibrary().Load(path, isDefault: true, caseSensitive: true).Status);
    }

    [TestMethod]
    public void Load_FileLockedWhileItIsSaved_IsTriedAgainNextTime()
    {
        using var folder = new TestFolder();
        var path = folder.WriteSnippets([("!mysig", "Best regards,\nYour name")]);
        var library = new SnippetLibrary();

        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.AreEqual(SnippetLibraryStatus.Unreadable, library.Load(path, isDefault: true, caseSensitive: true).Status);
        }

        Assert.AreEqual(SnippetLibraryStatus.Ready, library.Load(path, isDefault: true, caseSensitive: true).Status);
    }

    [TestMethod]
    public void Load_SnippetsFileWithByteOrderMarkAndComments_IsReadAsTextExpanderReadsIt()
    {
        using var folder = new TestFolder();
        var path = Path.Combine(folder.Root, "snippets.json");
        File.WriteAllText(path, "{\n  // Saved by a text editor\n  \"snippets\": [ { \"trigger\": \"!myemail\", \"replacement\": \"you@example.com\" }, ]\n}", new System.Text.UTF8Encoding(true));

        var snapshot = new SnippetLibrary().Load(path, isDefault: false, caseSensitive: true);

        Assert.AreEqual(SnippetLibraryStatus.Ready, snapshot.Status);
        Assert.AreEqual("you@example.com", snapshot.Snippets.Single().Replacement);
    }
}
