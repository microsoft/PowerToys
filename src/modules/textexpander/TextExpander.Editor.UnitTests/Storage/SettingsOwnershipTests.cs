// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.TextExpander.Editor.ViewModels;
using Microsoft.PowerToys.TextExpander.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.PowerToys.TextExpander.Editor.UnitTests;

[TestClass]
public sealed class SettingsOwnershipTests
{
    [TestMethod]
    public void UpdatingOptionsPreservesTheEditorsCurrentFolder()
    {
        var fileSystem = new MockFileSystem();
        var store = new TestSettingsStore(fileSystem);
        var settings = new TextExpanderSettings();
        settings.Properties.SnippetsPath.Value = @"C:\Snippets";
        store.SaveSettings(settings.ToJsonString(), TextExpanderSettings.ModuleName);
        TextExpanderSettings updated = store.UpdateOptions(properties => properties.CaseSensitive = false);
        Assert.AreEqual(@"C:\Snippets", updated.Properties.SnippetsPath.Value);
        Assert.IsFalse(updated.Properties.CaseSensitive);
        Assert.IsFalse(store.GetSettingsOrDefaultReadOnly<TextExpanderSettings>(TextExpanderSettings.ModuleName).Properties.CaseSensitive);
    }

    [TestMethod]
    public void PublishingAnEditorFolderPreservesNewerEngineOptions()
    {
        var fileSystem = new MockFileSystem();
        var store = new TestSettingsStore(fileSystem);
        var staleEditorSettings = new TextExpanderSettings();
        staleEditorSettings.Properties.SnippetsPath.Value = @"C:\NewSnippetFolder";
        var latestSettings = new TextExpanderSettings();
        latestSettings.Properties.CaseSensitive = false;
        store.SaveSettings(latestSettings.ToJsonString(), TextExpanderSettings.ModuleName);
        store.SaveSettingsOrThrow(staleEditorSettings.ToJsonString(), TextExpanderSettings.ModuleName);
        TextExpanderSettings actual = store.GetSettingsOrDefaultReadOnly<TextExpanderSettings>(TextExpanderSettings.ModuleName);
        Assert.AreEqual(@"C:\NewSnippetFolder", actual.Properties.SnippetsPath.Value);
        Assert.IsFalse(actual.Properties.CaseSensitive);
    }

    [TestMethod]
    public void ReadOnlySettingsWaitForPublicationInsteadOfReturningTransientDefaults()
    {
        var fileSystem = new MockFileSystem();
        var store = new TestSettingsStore(fileSystem);
        var settings = new TextExpanderSettings();
        settings.Properties.SnippetsPath.Value = @"C:\Snippets";
        store.SaveSettings(settings.ToJsonString(), TextExpanderSettings.ModuleName);
        string path = store.GetSettingsFilePath(TextExpanderSettings.ModuleName);
        string publishingPath = path + ".publishing";
        using var readerStarted = new ManualResetEventSlim();
        Task<TextExpanderSettings> reader = null;
        store.UpdateOptions(properties =>
        {
            fileSystem.File.Move(path, publishingPath);
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
                fileSystem.File.Move(publishingPath, path);
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
        string directory = Path.Combine(Path.GetTempPath(), "PowerToys-TextExpander-Initialization-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, TextExpanderSnippetFileNames.Current), "!test=Test");
            var stale = new TextExpanderSettings();
            stale.Properties.SnippetsPath.Value = directory;
            stale.Properties.WordBoundaryKeepsSpace = false;
            var latest = new TextExpanderSettings();
            latest.Properties.SnippetsPath.Value = directory;
            latest.Properties.WordBoundaryKeepsSpace = false;
            latest.Properties.CaseSensitive = false;
            var fileSystem = new MockFileSystem();
            var store = new TestSettingsStore(fileSystem, stale);
            store.SaveSettings(latest.ToJsonString(), TextExpanderSettings.ModuleName);
            var repository = new Mock<ISettingsRepository<GeneralSettings>>();
            repository.SetupGet(instance => instance.SettingsConfig).Returns(new GeneralSettings());

            using var editor = new TextExpanderViewModel(store, repository.Object, _ => 0, null, false, key => key);

            TextExpanderSettings actual = store.GetSettingsOrDefaultReadOnly<TextExpanderSettings>(TextExpanderSettings.ModuleName);
            Assert.IsTrue(actual.Properties.WordBoundaryKeepsSpace);
            Assert.IsFalse(actual.Properties.CaseSensitive);
            Assert.IsFalse(editor.CaseSensitive);
            Assert.AreEqual(directory, actual.Properties.SnippetsPath.Value);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class TestSettingsStore : TextExpanderSettingsUtils
    {
        private readonly MockFileSystem _fileSystem;
        private readonly TextExpanderSettings _initialSettings;

        public TestSettingsStore(MockFileSystem fileSystem, TextExpanderSettings initialSettings = null)
            : base(fileSystem)
        {
            _fileSystem = fileSystem;
            _initialSettings = initialSettings;
        }

        public override T GetSettingsOrDefault<T>(string powertoy = "", string fileName = DefaultFileName)
            => typeof(T) == typeof(TextExpanderSettings) && _initialSettings is not null
                ? (T)(object)_initialSettings
                : base.GetSettingsOrDefault<T>(powertoy, fileName);

        protected override HandleBoundFileDeletionResult TryDeleteOwnedAtomicSaveArtifact(string path, byte[] ownedBytes)
        {
            if (!_fileSystem.File.Exists(path))
            {
                return new(HandleBoundFileDeletionStatus.NotFound, path, null);
            }

            if (!_fileSystem.File.ReadAllBytes(path).AsSpan().SequenceEqual(ownedBytes))
            {
                return new(HandleBoundFileDeletionStatus.IdentityMismatch, path, new IOException("Unexpected file contents."));
            }

            _fileSystem.File.Delete(path);
            return new(HandleBoundFileDeletionStatus.Deleted, path, null);
        }
    }
}
