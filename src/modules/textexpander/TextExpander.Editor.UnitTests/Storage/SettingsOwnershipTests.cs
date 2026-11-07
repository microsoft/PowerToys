// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.TextExpander.Editor.ViewModels;
using Microsoft.PowerToys.TextExpander.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.Editor.UnitTests;

[TestClass]
public sealed class SettingsOwnershipTests
{
    [TestMethod]
    public void UpdatingOptionsPreservesTheEditorsCurrentFolder()
    {
        var store = new MockSettingsStore();
        store.SaveSettings(EditorSettings.For(@"C:\Snippets").ToJsonString(), TextExpanderSettings.ModuleName);
        TextExpanderSettings updated = store.UpdateOptions(properties => properties.CaseSensitive = false);
        Assert.AreEqual(@"C:\Snippets", updated.Properties.SnippetsPath.Value);
        Assert.IsFalse(updated.Properties.CaseSensitive);
        Assert.IsFalse(store.GetSettingsOrDefaultReadOnly<TextExpanderSettings>(TextExpanderSettings.ModuleName).Properties.CaseSensitive);
    }

    [TestMethod]
    public void PublishingAnEditorFolderPreservesNewerEngineOptions()
    {
        var store = new MockSettingsStore();
        var latestSettings = new TextExpanderSettings();
        latestSettings.Properties.CaseSensitive = false;
        store.SaveSettings(latestSettings.ToJsonString(), TextExpanderSettings.ModuleName);
        TextExpanderSettings updated = store.UpdateSnippetsPath(@"C:\NewSnippetFolder");
        TextExpanderSettings actual = store.GetSettingsOrDefaultReadOnly<TextExpanderSettings>(TextExpanderSettings.ModuleName);
        Assert.AreEqual(@"C:\NewSnippetFolder", actual.Properties.SnippetsPath.Value);
        Assert.IsFalse(actual.Properties.CaseSensitive);
        Assert.AreEqual(updated.ToJsonString(), actual.ToJsonString());
    }

    [TestMethod]
    public void SavingSettingsOrThrowKeepsEveryPropertyItIsGiven()
    {
        var store = new MockSettingsStore();
        store.SaveSettings(EditorSettings.For(@"C:\Snippets").ToJsonString(), TextExpanderSettings.ModuleName);
        TextExpanderSettings saved = EditorSettings.For(@"C:\NewSnippetFolder");
        saved.Properties.CaseSensitive = false;
        saved.Properties.InjectionBackend.Value = "type";

        store.SaveSettingsOrThrow(saved.ToJsonString(), TextExpanderSettings.ModuleName);

        Assert.AreEqual(saved.ToJsonString(), store.GetSettingsOrDefaultReadOnly<TextExpanderSettings>(TextExpanderSettings.ModuleName).ToJsonString());
    }

    [TestMethod]
    public void SavingSettingsWaitsForAnOptionsUpdateInProgress()
    {
        var store = new MockSettingsStore();
        store.SaveSettings(EditorSettings.For(@"C:\Snippets").ToJsonString(), TextExpanderSettings.ModuleName);
        Task writer = null;
        store.UpdateOptions(properties =>
        {
            writer = Task.Run(() => store.SaveSettings(EditorSettings.For(@"C:\Later").ToJsonString(), TextExpanderSettings.ModuleName));
            Assert.IsFalse(writer.Wait(TimeSpan.FromMilliseconds(200)), "SaveSettings must wait for the settings claim.");
            properties.CaseSensitive = false;
        });

        Assert.IsTrue(writer.Wait(TimeSpan.FromSeconds(5)));
        TextExpanderSettings actual = store.GetSettingsOrDefaultReadOnly<TextExpanderSettings>(TextExpanderSettings.ModuleName);
        Assert.AreEqual(@"C:\Later", actual.Properties.SnippetsPath.Value);
        Assert.IsTrue(actual.Properties.CaseSensitive);
    }

    [TestMethod]
    public void FailedSettingsSaveIsUndoneWithoutThrowing()
    {
        var store = new MockSettingsStore();
        store.SaveSettings(EditorSettings.For(@"C:\Snippets").ToJsonString(), TextExpanderSettings.ModuleName);
        string path = store.GetSettingsFilePath(TextExpanderSettings.ModuleName);
        byte[] original = store.FileSystem.File.ReadAllBytes(path);
        store.ThrowAfterPublication = true;

        store.SaveSettings(EditorSettings.For(@"C:\Other").ToJsonString(), TextExpanderSettings.ModuleName);

        StringAssert.Contains(store.StagedContents, "Other");
        CollectionAssert.AreEqual(original, store.FileSystem.File.ReadAllBytes(path));
    }

    [TestMethod]
    public void ReadOnlySettingsWaitForPublicationInsteadOfReturningTransientDefaults()
    {
        var store = new MockSettingsStore();
        store.SaveSettings(EditorSettings.For(@"C:\Snippets").ToJsonString(), TextExpanderSettings.ModuleName);
        string path = store.GetSettingsFilePath(TextExpanderSettings.ModuleName);
        string publishingPath = path + ".publishing";
        using var readerStarted = new ManualResetEventSlim();
        Task<TextExpanderSettings> reader = null;
        store.UpdateOptions(properties =>
        {
            store.FileSystem.File.Move(path, publishingPath);
            try
            {
                reader = Task.Run(() =>
                {
                    readerStarted.Set();
                    return store.GetSettingsOrDefaultReadOnly<TextExpanderSettings>(TextExpanderSettings.ModuleName);
                });
                Assert.IsTrue(readerStarted.Wait(TimeSpan.FromSeconds(5)));
                Assert.IsFalse(reader.Wait(TimeSpan.FromMilliseconds(200)), "Readers must wait for the settings publication claim.");
            }
            finally
            {
                store.FileSystem.File.Move(publishingPath, path);
            }

            properties.CaseSensitive = false;
        });

        TextExpanderSettings actual = reader.GetAwaiter().GetResult();
        Assert.AreEqual(@"C:\Snippets", actual.Properties.SnippetsPath.Value);
        Assert.IsFalse(actual.Properties.CaseSensitive);
    }

    [TestMethod]
    public void EditorInitializationMigrationPreservesAConcurrentOptionsUpdate()
    {
        using var folder = new TempFolder();
        File.WriteAllText(Path.Combine(folder.Path, TextExpanderSnippetFileNames.Current), "!test=Test");
        TextExpanderSettings stale = EditorSettings.For(folder.Path);
        stale.Properties.WordBoundaryKeepsSpace = false;
        TextExpanderSettings latest = EditorSettings.For(folder.Path);
        latest.Properties.WordBoundaryKeepsSpace = false;
        latest.Properties.CaseSensitive = false;
        var store = new MockSettingsStore { InitialSettings = stale };
        store.SaveSettings(latest.ToJsonString(), TextExpanderSettings.ModuleName);

        using var editor = new TextExpanderViewModel(store, false, key => key);

        TextExpanderSettings actual = store.GetSettingsOrDefaultReadOnly<TextExpanderSettings>(TextExpanderSettings.ModuleName);
        Assert.IsTrue(actual.Properties.WordBoundaryKeepsSpace);
        Assert.IsFalse(actual.Properties.CaseSensitive);
        Assert.IsFalse(editor.CaseSensitive);
        Assert.AreEqual(folder.Path, actual.Properties.SnippetsPath.Value);
    }
}
