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
public sealed class SnippetSourceTests
{
    [TestMethod]
    public void GetState_TextExpanderNeverTurnedOn_IsOffWithNoSnippets()
    {
        using var environment = new TestEnvironment();
        environment.Folder.WriteSnippets([("!mysig", "Best regards,\nYour name")]);

        var state = environment.Source.GetState();

        Assert.AreEqual(TextExpanderAvailability.Off, state.Availability);
        Assert.AreEqual(0, state.Library.Snippets.Count);
    }

    [TestMethod]
    public void GetState_TurnedOffInSettings_IsOff()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!mysig", "Best regards,\nYour name"));
        environment.Folder.SetEnabled(false);

        Assert.AreEqual(TextExpanderAvailability.Off, environment.Source.GetState().Availability);
    }

    [TestMethod]
    public void GetState_PolicyTurnsTextExpanderOff_IsOffByPolicyEvenWhenOnInSettings()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!mysig", "Best regards,\nYour name"));
        environment.Policies.Values[TextExpanderPolicy.TextExpanderValueName] = 0;

        var state = environment.Source.GetState();

        Assert.AreEqual(TextExpanderAvailability.OffByPolicy, state.Availability);
        Assert.AreEqual(0, state.Library.Snippets.Count);
    }

    [TestMethod]
    public void GetState_PolicyTurnsTextExpanderOn_IsOnEvenWhenOffInSettings()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!mysig", "Best regards,\nYour name"));
        environment.Folder.SetEnabled(false);
        environment.Policies.Values[TextExpanderPolicy.TextExpanderValueName] = 1;

        Assert.IsTrue(environment.Source.GetState().IsOn);
    }

    [TestMethod]
    public void GetState_DefaultSnippetsFile_ListsItsSnippets()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!mysig", "Best regards,\nYour name"), ("!myemail", "you@example.com"));

        var state = environment.Source.GetState();
        string[] triggers = ["!mysig", "!myemail"];

        Assert.AreEqual(SnippetLibraryStatus.Ready, state.Library.Status);
        Assert.AreEqual(environment.Folder.DefaultSnippetsFile, state.Library.FilePath);
        CollectionAssert.AreEqual(triggers, state.Library.Snippets.Select(snippet => snippet.Trigger).ToArray());
    }

    [TestMethod]
    public void GetState_SnippetsFileChosenInSettings_ReadsThatFile()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!mysig", "Best regards,\nYour name"));
        var chosen = environment.Folder.WriteSnippets([("!myphone", "555-0100")], Path.Combine(environment.Folder.Root, "Synced", "snippets.json"));
        environment.Folder.SetModuleSettings(snippetsPath: $"\"{chosen}\"");

        var state = environment.Source.GetState();

        Assert.AreEqual(chosen, state.Library.FilePath);
        Assert.AreEqual("!myphone", state.Library.Snippets.Single().Trigger);
    }

    [TestMethod]
    public void GetState_ChosenFileMissing_ReportsItInsteadOfUsingTheDefault()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!mysig", "Best regards,\nYour name"));
        var chosen = Path.Combine(environment.Folder.Root, "Not ready yet", "snippets.json");
        environment.Folder.SetModuleSettings(snippetsPath: chosen);

        var library = environment.Source.GetState().Library;

        Assert.AreEqual(SnippetLibraryStatus.FileNotFound, library.Status);
        Assert.AreEqual(chosen, library.FilePath);
        Assert.IsFalse(File.Exists(chosen), "The extension must never create the snippets file.");
    }

    [TestMethod]
    public void GetState_DefaultFileNotCreatedYet_HasNoSnippetsYetAndCreatesNothing()
    {
        using var environment = new TestEnvironment();
        environment.Folder.SetEnabled(true);

        var library = environment.Source.GetState().Library;

        Assert.AreEqual(SnippetLibraryStatus.NoSnippetsYet, library.Status);
        Assert.IsFalse(File.Exists(environment.Folder.DefaultSnippetsFile));
    }

    [TestMethod]
    public void GetState_SnippetsChangeAfterwards_ReadsThemAgain()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!mysig", "Best regards,\nYour name"));
        Assert.AreEqual(1, environment.Source.GetState().Library.Snippets.Count);

        environment.Folder.WriteSnippets([("!mysig", "Best regards,\nYour name"), ("!myemail", "you@example.com")]);
        File.SetLastWriteTimeUtc(environment.Folder.DefaultSnippetsFile, DateTime.UtcNow.AddMinutes(1));

        Assert.AreEqual(2, environment.Source.GetState().Library.Snippets.Count);
    }

    [TestMethod]
    public void GetState_SettingsCaughtMidSave_KeepsTheSettingsReadBefore()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!mysig", "Best regards,\nYour name"));
        environment.Folder.SetModuleSettings(caseSensitive: false, injectionBackend: "type");
        Assert.AreEqual(InjectionBackend.Typing, environment.Source.GetState().Settings.Backend);

        File.WriteAllText(environment.Folder.Paths.ModuleSettingsFile, "{ \"properties\": ");

        var settings = environment.Source.GetState().Settings;
        Assert.AreEqual(InjectionBackend.Typing, settings.Backend);
        Assert.IsFalse(settings.CaseSensitive);
    }

    [TestMethod]
    public void GetState_WithinMaxAge_ReusesWhatWasRead()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!mysig", "Best regards,\nYour name"));

        var first = environment.Source.GetState(SnippetSource.SearchMaxAge);
        environment.Milliseconds += 500;
        Assert.AreSame(first, environment.Source.GetState(SnippetSource.SearchMaxAge));

        environment.Milliseconds += 600;
        Assert.AreNotSame(first, environment.Source.GetState(SnippetSource.SearchMaxAge));
    }

    [TestMethod]
    public void ResolveSnippetsFile_ChosenPath_IsTrimmedAndUnquotedButAPartialPathFallsBackToTheDefault()
    {
        var folder = Path.Combine(Path.GetTempPath(), "PowerToysTest");

        Assert.AreEqual((@"C:\Snippets\mine.json", false), SnippetSource.ResolveSnippetsFile("  \"C:\\Snippets\\mine.json\" ", folder));
        Assert.AreEqual((Path.Combine(folder, "PowerToysTextExpanderSnippets.json"), true), SnippetSource.ResolveSnippetsFile("mine.json", folder));
        Assert.AreEqual((Path.Combine(folder, "PowerToysTextExpanderSnippets.json"), true), SnippetSource.ResolveSnippetsFile(null, folder));
    }
}
