// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.TextExpander.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class TextExpanderStorage
    {
        private string _root;
        private string _local;
        private TestStorageEnvironment _environment;

        [TestInitialize]
        public void Initialize()
        {
            _root = Path.Combine(Path.GetTempPath(), "PowerToys-TextExpander-Storage-" + Guid.NewGuid().ToString("N"));
            _local = Path.Combine(_root, "local");
            Directory.CreateDirectory(_local);
            _environment = new TestStorageEnvironment(_root, _local);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.Delete(_root, recursive: true);
        }

        [TestMethod]
        public void EmptyFolderReceivesCurrentFileBeforeItBecomesActive()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Legacy, "!local=Local");
            string targetFolder = Path.Combine(_root, "shared");
            TextExpanderViewModel model = CreateViewModel();

            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            Assert.IsFalse(change.HasExistingFile);
            Assert.IsNull(model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));

            string target = Path.Combine(targetFolder, TextExpanderSnippetFileNames.Current);
            Assert.AreEqual(string.Empty, backup);
            Assert.AreEqual(targetFolder, model.SnippetsPath);
            Assert.AreEqual(File.ReadAllText(source), File.ReadAllText(target));
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public void OpenSnippetsFileUsesResolvedCurrentName()
        {
            string current = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Local");
            string opened = null;
            TextExpanderViewModel model = CreateViewModel(openFile: path =>
            {
                opened = path;
            });

            Assert.IsNull(model.OpenSnippetsFile());
            Assert.AreEqual(current, opened);
        }

        [TestMethod]
        public void OpenSnippetsFileUsesResolvedLegacyName()
        {
            string legacy = Write(_local, TextExpanderSnippetFileNames.Legacy, "!local=Local");
            string opened = null;
            TextExpanderViewModel model = CreateViewModel(openFile: path =>
            {
                opened = path;
            });

            Assert.IsNull(model.OpenSnippetsFile());
            Assert.AreEqual(legacy, opened);
        }

        [TestMethod]
        public void OpenSnippetsFileFollowsAnAppliedFolderChange()
        {
            Write(_local, TextExpanderSnippetFileNames.Legacy, "!local=Local");
            string targetFolder = Path.Combine(_root, "shared");
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Shared");
            string opened = null;
            TextExpanderViewModel model = CreateViewModel(openFile: path =>
            {
                opened = path;
            });

            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            Assert.IsNull(model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.UseFolder, out _));
            Assert.IsNull(model.OpenSnippetsFile());
            Assert.AreEqual(target, opened);
        }

        [TestMethod]
        public void OpenSnippetsFileReportsLaunchAndMissingFileFailures()
        {
            string current = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Local");
            TextExpanderViewModel model = CreateViewModel(openFile: _ => throw new InvalidOperationException("Injected launch failure."));

            Assert.AreEqual("TextExpander_SnippetFile_OpenFailed", model.OpenSnippetsFile());

            File.Delete(current);
            Assert.AreEqual("TextExpander_SnippetFile_OpenMissing", model.OpenSnippetsFile());
        }

        [TestMethod]
        public void UsingFolderFileBacksUpThePreviousActiveFile()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Local");
            string targetFolder = Path.Combine(_root, "shared");
            Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Shared");
            TextExpanderViewModel model = CreateViewModel();

            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            Assert.IsNull(model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.UseFolder, out string backup));

            Assert.IsTrue(File.Exists(backup));
            Assert.AreEqual(File.ReadAllText(source), File.ReadAllText(backup));
            Assert.AreEqual("!shared", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public void ReplacingFolderFileBacksUpItsPreviousContents()
        {
            Write(_local, TextExpanderSnippetFileNames.Current, "!local=Local");
            string targetFolder = Path.Combine(_root, "shared");
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Shared");
            TextExpanderViewModel model = CreateViewModel();

            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            Assert.IsNull(model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));

            Assert.AreEqual("!shared=Shared", File.ReadAllText(backup));
            Assert.AreEqual("!local=Local", File.ReadAllText(target));
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public void FileAppearingAfterFolderSelectionIsNotOverwritten()
        {
            Write(_local, TextExpanderSnippetFileNames.Current, "!local=Local");
            string targetFolder = Path.Combine(_root, "shared");
            TextExpanderViewModel model = CreateViewModel();

            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            Assert.IsFalse(change.HasExistingFile);
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Shared");

            Assert.AreEqual(
                "TextExpander_SnippetFolder_ChangeStale",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));
            Assert.AreEqual(string.Empty, backup);
            Assert.AreEqual("!shared=Shared", File.ReadAllText(target));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void LegacyDestinationChangingToCurrentRequiresANewDecision()
        {
            Write(_local, TextExpanderSnippetFileNames.Current, "!local=Local");
            string targetFolder = Path.Combine(_root, "shared");
            string legacy = Write(targetFolder, TextExpanderSnippetFileNames.Legacy, "!legacy=Legacy");
            TextExpanderViewModel model = CreateViewModel();

            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            Assert.AreEqual(legacy, change.FilePath);
            string current = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!current=Current");

            Assert.AreEqual(
                "TextExpander_SnippetFolder_ChangeStale",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));
            Assert.AreEqual(string.Empty, backup);
            Assert.AreEqual("!legacy=Legacy", File.ReadAllText(legacy));
            Assert.AreEqual("!current=Current", File.ReadAllText(current));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void SelectedDestinationDisappearingRequiresANewDecision()
        {
            Write(_local, TextExpanderSnippetFileNames.Current, "!local=Local");
            string targetFolder = Path.Combine(_root, "shared");
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Legacy, "!shared=Shared");
            TextExpanderViewModel model = CreateViewModel();

            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            Assert.AreEqual(target, change.FilePath);
            File.Delete(target);

            Assert.AreEqual(
                "TextExpander_SnippetFolder_ChangeStale",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.UseFolder, out string backup));
            Assert.AreEqual(string.Empty, backup);
            Assert.IsFalse(File.Exists(target));
            Assert.IsFalse(File.Exists(Path.Combine(targetFolder, TextExpanderSnippetFileNames.Current)));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void SourceDisappearingBeforeReplacePreservesDestinationBytes()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Local");
            string targetFolder = Path.Combine(_root, "shared");
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Shared");
            byte[] destinationBytes = File.ReadAllBytes(target);
            TextExpanderViewModel model = CreateViewModel();

            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            File.Delete(source);

            Assert.AreEqual(
                "TextExpander_SnippetFolder_ChangeStale",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));
            Assert.AreEqual(string.Empty, backup);
            CollectionAssert.AreEqual(destinationBytes, File.ReadAllBytes(target));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void SettingsRechecksLegacyAtStarterPublicationBoundary()
        {
            string current = Path.Combine(_local, TextExpanderSnippetFileNames.Current);
            string legacy = Path.Combine(_local, TextExpanderSnippetFileNames.Legacy);
            byte[] legacyBytes = System.Text.Encoding.UTF8.GetBytes("!legacy=arrived\r\n");
            _environment.PublishAfterMissingProbe(legacy, legacyBytes);

            TextExpanderViewModel model = CreateViewModel();

            Assert.AreEqual(legacy, model.SnippetsFilePath);
            Assert.AreEqual("!legacy", model.Snippets.Single().Trigger);
            CollectionAssert.AreEqual(legacyBytes, File.ReadAllBytes(legacy));
            Assert.IsFalse(File.Exists(current), "Settings must use the same folder-aware publication rule as the engine.");
        }

        [TestMethod]
        public void SettingsWatcherHandoffReloadsLegacyArrivingAfterStarterResolution()
        {
            string current = Path.Combine(_local, TextExpanderSnippetFileNames.Current);
            string legacy = Path.Combine(_local, TextExpanderSnippetFileNames.Legacy);
            byte[] legacyBytes = System.Text.Encoding.UTF8.GetBytes("!legacy=arrived-before-watch\r\n");
            TextExpanderViewModel model = CreateViewModel();
            Assert.IsTrue(TextExpanderStarter.IsUntouchedStarterBytes(File.ReadAllBytes(current)));

            _environment.PublishWhenWatchStarts(legacy, legacyBytes);
            model.Dispatch = action => action();

            Assert.AreEqual(legacy, model.SnippetsFilePath);
            Assert.AreEqual("!legacy", model.Snippets.Single().Trigger);
            CollectionAssert.AreEqual(legacyBytes, File.ReadAllBytes(legacy));
            Assert.IsTrue(TextExpanderStarter.IsUntouchedStarterBytes(File.ReadAllBytes(current)));
        }

        [TestMethod]
        public void PreferredPathChangesReloadAndRaiseRevisionForOpenEditors()
        {
            string legacy = Write(_local, TextExpanderSnippetFileNames.Legacy, "!legacy=Legacy");
            string current = Path.Combine(_local, TextExpanderSnippetFileNames.Current);
            TextExpanderViewModel model = CreateViewModel();
            int revisionNotifications = 0;
            model.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TextExpanderViewModel.SnippetRevision))
                {
                    revisionNotifications++;
                }
            };
            model.Dispatch = action => action();

            int legacyRevision = model.SnippetRevision;
            File.WriteAllText(current, "!current=Current");
            Assert.AreEqual(legacy, model.SnippetsFilePath, "The displayed path must remain the file actually loaded until the watcher reloads.");
            _environment.NotifyFilesChanged();

            Assert.AreEqual(current, model.SnippetsFilePath);
            Assert.AreEqual("!current", model.Snippets.Single().Trigger);
            Assert.IsTrue(model.SnippetRevision > legacyRevision);
            Assert.AreEqual(1, revisionNotifications, "The page uses this notification to invalidate its editor and close its Prompt window.");

            int currentRevision = model.SnippetRevision;
            File.Delete(current);
            _environment.NotifyFilesChanged();

            Assert.AreEqual(legacy, model.SnippetsFilePath);
            Assert.AreEqual("!legacy", model.Snippets.Single().Trigger);
            Assert.IsTrue(model.SnippetRevision > currentRevision);
            Assert.AreEqual(2, revisionNotifications);
        }

        [TestMethod]
        public void OwnAtomicSavesDoNotReloadOrCreateFalseConflicts()
        {
            string current = Write(_local, TextExpanderSnippetFileNames.Current, "!one=1");
            TextExpanderViewModel model = CreateViewModel();
            model.Dispatch = action => action();

            Assert.IsNull(model.SaveSnippet(null, "!two", "2"));
            int savedRevision = model.SnippetRevision;
            byte[] savedBytes = File.ReadAllBytes(current);

            _environment.NotifyFilesChanged();
            _environment.NotifyFilesChanged();

            Assert.AreEqual(savedRevision, model.SnippetRevision, "Watcher events for Settings' own bytes must be ignored.");
            CollectionAssert.AreEqual(savedBytes, File.ReadAllBytes(current));
            Assert.IsNull(model.SaveSnippet(null, "!three", "3"), "Ignoring an own-save event must not manufacture an external-change conflict.");
            Assert.AreEqual(3, model.Snippets.Count);

            int ownSaveRevision = model.SnippetRevision;
            File.WriteAllText(current, "!external=Elsewhere");
            _environment.NotifyFilesChanged();
            Assert.IsTrue(model.SnippetRevision > ownSaveRevision);
            Assert.AreEqual("!external", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public void MissingSourceAndEmptyDestinationReceiveTheStarter()
        {
            TextExpanderViewModel model = CreateViewModel();
            File.Delete(model.SnippetsFilePath);
            string targetFolder = Path.Combine(_root, "shared");

            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            Assert.IsNull(model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));

            string target = Path.Combine(targetFolder, TextExpanderSnippetFileNames.Current);
            Assert.AreEqual(string.Empty, backup);
            Assert.AreEqual(TextExpanderStarter.Content, File.ReadAllText(target));
            Assert.AreEqual(6, model.Snippets.Count);
        }

        [TestMethod]
        public void ExistingZeroByteSourceRemainsZeroBytesInEmptyDestination()
        {
            string source = Path.Combine(_local, TextExpanderSnippetFileNames.Current);
            File.WriteAllBytes(source, Array.Empty<byte>());
            TextExpanderViewModel model = CreateViewModel();
            string targetFolder = Path.Combine(_root, "shared");

            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            Assert.IsNull(model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out _));

            string target = Path.Combine(targetFolder, TextExpanderSnippetFileNames.Current);
            Assert.AreEqual(0, new FileInfo(source).Length);
            Assert.AreEqual(0, new FileInfo(target).Length);
            Assert.AreEqual(0, model.Snippets.Count);
        }

        [TestMethod]
        public void ReplacingDestinationPreservesSourceAndDestinationBytes()
        {
            var utf8Bom = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            byte[] sourceBytes = utf8Bom.GetPreamble().Concat(utf8Bom.GetBytes("!source=Keep\r\n")).ToArray();
            string source = Path.Combine(_local, TextExpanderSnippetFileNames.Current);
            File.WriteAllBytes(source, sourceBytes);

            string targetFolder = Path.Combine(_root, "shared");
            Directory.CreateDirectory(targetFolder);
            string target = Path.Combine(targetFolder, TextExpanderSnippetFileNames.Legacy);
            byte[] destinationBytes = new System.Text.UnicodeEncoding(bigEndian: false, byteOrderMark: true)
                .GetPreamble()
                .Concat(new System.Text.UnicodeEncoding(false, true).GetBytes("!destination=Keep\n"))
                .ToArray();
            File.WriteAllBytes(target, destinationBytes);
            TextExpanderViewModel model = CreateViewModel();

            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            Assert.IsNull(model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));

            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source));
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(target));
            CollectionAssert.AreEqual(destinationBytes, File.ReadAllBytes(backup));
        }

        [TestMethod]
        public void SupportedFileArrivingInFinalMoveWindowIsPreservedAndChangeIsStale()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Local");
            byte[] sourceBytes = File.ReadAllBytes(source);
            string targetFolder = Path.Combine(_root, "shared");
            string legacy = Path.Combine(targetFolder, TextExpanderSnippetFileNames.Legacy);
            byte[] legacyBytes = System.Text.Encoding.UTF8.GetBytes("!legacy=Arrived");
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            _environment.BeforeNextPublication(() => File.WriteAllBytes(legacy, legacyBytes));

            Assert.AreEqual(
                "TextExpander_SnippetFolder_ChangeStale",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));

            Assert.AreEqual(string.Empty, backup);
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source));
            CollectionAssert.AreEqual(legacyBytes, File.ReadAllBytes(legacy));
            Assert.IsFalse(File.Exists(Path.Combine(targetFolder, TextExpanderSnippetFileNames.Current)));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void SupportedFileArrivingInFinalReplaceWindowRollsBackOwnedReplacement()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Local");
            byte[] sourceBytes = File.ReadAllBytes(source);
            string targetFolder = Path.Combine(_root, "shared");
            string current = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Original");
            byte[] currentBytes = File.ReadAllBytes(current);
            string legacy = Path.Combine(targetFolder, TextExpanderSnippetFileNames.Legacy);
            byte[] legacyBytes = System.Text.Encoding.UTF8.GetBytes("!legacy=Arrived");
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            _environment.BeforeNextPublication(() => File.WriteAllBytes(legacy, legacyBytes));

            Assert.AreEqual(
                "TextExpander_SnippetFolder_ChangeStale",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));

            Assert.AreEqual(string.Empty, backup);
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source));
            CollectionAssert.AreEqual(currentBytes, File.ReadAllBytes(current));
            CollectionAssert.AreEqual(legacyBytes, File.ReadAllBytes(legacy));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void SourceMutationInFinalMoveWindowRollsBackOwnedPublication()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Original");
            byte[] mutatedBytes = System.Text.Encoding.UTF8.GetBytes("!local=Mutated");
            string targetFolder = Path.Combine(_root, "shared");
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            _environment.BeforeNextPublication(() => File.WriteAllBytes(source, mutatedBytes));

            Assert.AreEqual(
                "TextExpander_SnippetFolder_ChangeStale",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));

            Assert.AreEqual(string.Empty, backup);
            CollectionAssert.AreEqual(mutatedBytes, File.ReadAllBytes(source));
            Assert.IsFalse(File.Exists(Path.Combine(targetFolder, TextExpanderSnippetFileNames.Current)));
            Assert.IsFalse(File.Exists(Path.Combine(targetFolder, TextExpanderSnippetFileNames.Legacy)));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void SourceDisappearanceInFinalReplaceWindowRollsBackOwnedReplacement()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Original");
            string targetFolder = Path.Combine(_root, "shared");
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            _environment.BeforeNextPublication(() => File.Delete(source));

            Assert.AreEqual(
                "TextExpander_SnippetFolder_ChangeStale",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));

            Assert.AreEqual(string.Empty, backup);
            Assert.IsFalse(File.Exists(source));
            CollectionAssert.AreEqual(targetBytes, File.ReadAllBytes(target));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void StagingReplacementAtHandleBoundCleanupSurvivesAndIsSurfaced()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Original");
            byte[] mutatedSource = System.Text.Encoding.UTF8.GetBytes("!local=Mutated");
            byte[] replacementBytes = System.Text.Encoding.UTF8.GetBytes("!external=StagingReplacement");
            string targetFolder = Path.Combine(_root, "shared");
            string replacementPath = null;
            string ownedObjectPath = null;
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            _environment.AfterNextStagingWrite(() => File.WriteAllBytes(source, mutatedSource));
            _environment.BeforeNextCleanupDisposition(
                path => Path.GetFileName(path).EndsWith(".tmp", StringComparison.OrdinalIgnoreCase),
                path =>
                {
                    replacementPath = path;
                    ownedObjectPath = path + ".owned";
                    File.Move(path, ownedObjectPath);
                    File.WriteAllBytes(path, replacementBytes);
                });

            string error = model.ApplyFolderChange(
                change,
                TextExpanderFolderConflictChoice.ReplaceFolder,
                out string recovery);

            Assert.IsNotNull(error);
            StringAssert.Contains(error, "staging file");
            StringAssert.Contains(error, replacementPath);
            Assert.AreEqual(replacementPath, recovery);
            CollectionAssert.AreEqual(replacementBytes, File.ReadAllBytes(replacementPath));
            Assert.IsFalse(File.Exists(ownedObjectPath), "Disposition must delete the verified object after it is renamed.");
            CollectionAssert.AreEqual(mutatedSource, File.ReadAllBytes(source));
            Assert.IsFalse(File.Exists(Path.Combine(targetFolder, TextExpanderSnippetFileNames.Current)));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void PostPublicationIdentityReadFailureRestoresDestinationBytes()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Original");
            byte[] sourceBytes = File.ReadAllBytes(source);
            string targetFolder = Path.Combine(_root, "shared");
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            _environment.FailNextReadAfterPublication();

            Assert.AreEqual(
                "Injected post-publication identity read failure.",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));

            Assert.AreEqual(string.Empty, backup);
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source));
            CollectionAssert.AreEqual(targetBytes, File.ReadAllBytes(target));
            Assert.AreEqual(_local, model.SnippetsPath);
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public void FolderQuarantineReplacementAtHandleBoundCleanupSurvivesAndIsSurfaced()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Original");
            byte[] sourceBytes = File.ReadAllBytes(source);
            byte[] replacementBytes = System.Text.Encoding.UTF8.GetBytes("!external=QuarantineReplacement");
            string targetFolder = Path.Combine(_root, "shared");
            string replacementPath = null;
            string ownedObjectPath = null;
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            _environment.FailNextReadAfterPublication();
            _environment.BeforeNextCleanupDisposition(
                path => Path.GetFileName(path).Contains(".rollback-quarantine", StringComparison.OrdinalIgnoreCase),
                path =>
                {
                    replacementPath = path;
                    ownedObjectPath = path + ".owned";
                    File.Move(path, ownedObjectPath);
                    File.WriteAllBytes(path, replacementBytes);
                });

            Assert.AreEqual(
                "TextExpander_SnippetFolder_RollbackFailed",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string recovery));

            Assert.AreEqual(replacementPath, recovery);
            CollectionAssert.AreEqual(replacementBytes, File.ReadAllBytes(replacementPath));
            Assert.IsFalse(File.Exists(ownedObjectPath), "Disposition must delete the verified quarantined object.");
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source));
            Assert.IsFalse(File.Exists(Path.Combine(targetFolder, TextExpanderSnippetFileNames.Current)));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void BackupReplacementAtHandleBoundCleanupSurvivesAndIsSurfaced()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Original");
            string targetFolder = Path.Combine(_root, "shared");
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            byte[] replacementBytes = System.Text.Encoding.UTF8.GetBytes("!external=BackupReplacement");
            string replacementPath = null;
            string ownedObjectPath = null;
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            _environment.FailNextReadAfterPublication(() => File.WriteAllBytes(target, targetBytes));
            _environment.BeforeNextCleanupDisposition(
                path => Path.GetFileName(path).Contains(".backup-", StringComparison.OrdinalIgnoreCase),
                path =>
                {
                    replacementPath = path;
                    ownedObjectPath = path + ".owned";
                    File.Move(path, ownedObjectPath);
                    File.WriteAllBytes(path, replacementBytes);
                });

            Assert.AreEqual(
                "TextExpander_SnippetFolder_RollbackFailed",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string recovery));

            string[] recoveryPaths = SplitRecoveryPaths(recovery);
            CollectionAssert.Contains(recoveryPaths, replacementPath);
            CollectionAssert.Contains(recoveryPaths, target);
            CollectionAssert.AreEqual(replacementBytes, File.ReadAllBytes(replacementPath));
            Assert.IsFalse(File.Exists(ownedObjectPath), "Disposition must delete the verified backup object.");
            CollectionAssert.AreEqual(targetBytes, File.ReadAllBytes(target));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void NewerExternalFileArrivingDuringNewDestinationRollbackIsRestoredAndSurfaced()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Original");
            byte[] sourceBytes = File.ReadAllBytes(source);
            string targetFolder = Path.Combine(_root, "shared");
            string target = Path.Combine(targetFolder, TextExpanderSnippetFileNames.Current);
            byte[] externalBytes = System.Text.Encoding.UTF8.GetBytes("!external=Newer");
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            _environment.FailNextReadAfterPublication();
            _environment.BeforeNextRollbackQuarantine(() => File.WriteAllBytes(target, externalBytes));

            Assert.AreEqual(
                "TextExpander_SnippetFolder_RollbackFailed",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string recovery));

            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source));
            CollectionAssert.AreEqual(externalBytes, File.ReadAllBytes(target));
            CollectionAssert.AreEqual(new[] { target }, SplitRecoveryPaths(recovery));
            Assert.IsFalse(Directory.GetFiles(targetFolder).Any(path =>
                Path.GetFileName(path).Contains(".rollback-quarantine", StringComparison.OrdinalIgnoreCase)));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void NewerExternalFileArrivingDuringReplacedDestinationRollbackPreservesBothWriters()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Original");
            byte[] sourceBytes = File.ReadAllBytes(source);
            string targetFolder = Path.Combine(_root, "shared");
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            byte[] externalBytes = System.Text.Encoding.UTF8.GetBytes("!external=Newer");
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            _environment.FailNextReadAfterPublication();
            _environment.BeforeNextRollbackQuarantine(() => File.WriteAllBytes(target, externalBytes));

            Assert.AreEqual(
                "TextExpander_SnippetFolder_RollbackFailed",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string recovery));

            string[] recoveryPaths = SplitRecoveryPaths(recovery);
            Assert.AreEqual(2, recoveryPaths.Length);
            Assert.IsTrue(recoveryPaths.Any(path => File.ReadAllBytes(path).SequenceEqual(targetBytes)));
            Assert.IsTrue(recoveryPaths.Any(path => File.ReadAllBytes(path).SequenceEqual(externalBytes)));
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source));
            CollectionAssert.AreEqual(externalBytes, File.ReadAllBytes(target));
            Assert.IsFalse(recoveryPaths.Any(path =>
                Path.GetFileName(path).Contains(".rollback-quarantine", StringComparison.OrdinalIgnoreCase)));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void FailedUnexpectedRollbackRestorationSurfacesQuarantineAndBackup()
        {
            Write(_local, TextExpanderSnippetFileNames.Current, "!local=Original");
            string targetFolder = Path.Combine(_root, "shared");
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            byte[] externalBytes = System.Text.Encoding.UTF8.GetBytes("!external=Displaced");
            byte[] blockerBytes = System.Text.Encoding.UTF8.GetBytes("!external=Newest");
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            _environment.FailNextReadAfterPublication();
            _environment.BeforeNextRollbackQuarantine(() => File.WriteAllBytes(target, externalBytes));
            _environment.BeforeUnexpectedRollbackRestore(() => File.WriteAllBytes(target, blockerBytes));

            Assert.AreEqual(
                "TextExpander_SnippetFolder_RollbackFailed",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string recovery));

            string[] recoveryPaths = SplitRecoveryPaths(recovery);
            Assert.IsTrue(recoveryPaths.Any(path => File.ReadAllBytes(path).SequenceEqual(targetBytes)));
            Assert.IsTrue(recoveryPaths.Any(path =>
                Path.GetFileName(path).Contains(".rollback-quarantine", StringComparison.OrdinalIgnoreCase)
                && File.ReadAllBytes(path).SequenceEqual(externalBytes)));
            Assert.IsTrue(recoveryPaths.Any(path => File.ReadAllBytes(path).SequenceEqual(blockerBytes)));
            CollectionAssert.AreEqual(blockerBytes, File.ReadAllBytes(target));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void RollbackApiFailurePreservesAndSurfacesOriginalDestinationBackup()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Original");
            byte[] sourceBytes = File.ReadAllBytes(source);
            string targetFolder = Path.Combine(_root, "shared");
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            _environment.FailNextReadAfterPublication();
            _environment.FailNextRollback();

            Assert.AreEqual(
                "TextExpander_SnippetFolder_RollbackFailed",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));

            Assert.IsFalse(string.IsNullOrEmpty(backup));
            Assert.IsTrue(File.Exists(backup));
            CollectionAssert.AreEqual(targetBytes, File.ReadAllBytes(backup));
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(target));
            Assert.AreEqual(_local, model.SnippetsPath);
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public void SettingsWriteFailureRestoresDestinationAndKeepsOldLibraryLoaded()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Original");
            string targetFolder = Path.Combine(_root, "shared");
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            TextExpanderViewModel model = null;
            bool persistenceSawOldLibrary = false;
            model = CreateViewModel(utils => utils
                .Setup(u => u.SaveSettingsOrThrow(
                    It.IsAny<string>(),
                    TextExpanderSettings.ModuleName,
                    SettingsUtils.DefaultFileName))
                .Callback(() =>
                {
                    persistenceSawOldLibrary = model.SnippetsPath == _local
                        && model.Snippets.Single().Trigger == "!local";
                })
                .Throws(VerifiedPreviousSettingsFailure()));
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);

            Assert.AreEqual(
                "TextExpander_SnippetFolder_SettingsSaveFailed",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));

            Assert.IsTrue(persistenceSawOldLibrary, "The ViewModel must not switch libraries before settings persistence succeeds.");
            Assert.AreEqual(string.Empty, backup);
            CollectionAssert.AreEqual(targetBytes, File.ReadAllBytes(target));
            Assert.AreEqual(_local, model.SnippetsPath);
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
            Assert.AreEqual("!local=Original", File.ReadAllText(source));
        }

        [TestMethod]
        public void SettingsWriteAndRollbackFailurePreserveAndSurfaceBackup()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Original");
            byte[] sourceBytes = File.ReadAllBytes(source);
            string targetFolder = Path.Combine(_root, "shared");
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            TextExpanderViewModel model = CreateViewModel(utils => utils
                .Setup(u => u.SaveSettingsOrThrow(
                    It.IsAny<string>(),
                    TextExpanderSettings.ModuleName,
                    SettingsUtils.DefaultFileName))
                .Throws(VerifiedPreviousSettingsFailure()));
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);
            _environment.FailNextRollback();

            Assert.AreEqual(
                "TextExpander_SnippetFolder_RollbackFailed",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));

            Assert.IsFalse(string.IsNullOrEmpty(backup));
            Assert.IsTrue(File.Exists(backup));
            CollectionAssert.AreEqual(targetBytes, File.ReadAllBytes(backup));
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(target));
            Assert.AreEqual(_local, model.SnippetsPath);
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public void AtomicSettingsPublicationFailureRestoresExistingSettingsAndFolderState()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Original");
            string targetFolder = Path.Combine(_root, "shared");
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            var settings = new TextExpanderSettings();
            settings.Properties.SnippetsPath.Value = _local;
            var settingsFileSystem = new MockFileSystem();
            var settingsUtils = new ThrowAfterPublicationSettingsUtils(settingsFileSystem, settings);
            settingsUtils.SaveSettings(settings.ToJsonString(), TextExpanderSettings.ModuleName);
            string settingsPath = settingsUtils.GetSettingsFilePath(TextExpanderSettings.ModuleName);
            byte[] settingsBytes = settingsFileSystem.File.ReadAllBytes(settingsPath);
            TextExpanderViewModel model = CreateViewModel(settingsUtils);
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);

            Assert.AreEqual(
                "TextExpander_SnippetFolder_SettingsSaveFailed",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));

            Assert.AreEqual(string.Empty, backup);
            CollectionAssert.AreEqual(settingsBytes, settingsFileSystem.File.ReadAllBytes(settingsPath));
            CollectionAssert.AreEqual(targetBytes, File.ReadAllBytes(target));
            Assert.AreEqual(_local, model.SnippetsPath);
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
            Assert.AreEqual("!local=Original", File.ReadAllText(source));
            StringAssert.Contains(settingsUtils.StagedContents, targetFolder.Replace("\\", "\\\\", StringComparison.Ordinal));
        }

        [TestMethod]
        public void AtomicSettingsPublicationFailureRemovesFirstSettingsFileAndRestoresFolderState()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Original");
            string targetFolder = Path.Combine(_root, "shared");
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            var settings = new TextExpanderSettings();
            settings.Properties.SnippetsPath.Value = _local;
            var settingsFileSystem = new MockFileSystem();
            var settingsUtils = new ThrowAfterPublicationSettingsUtils(settingsFileSystem, settings);
            string settingsPath = settingsUtils.GetSettingsFilePath(TextExpanderSettings.ModuleName);
            TextExpanderViewModel model = CreateViewModel(settingsUtils);
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);

            Assert.AreEqual(
                "TextExpander_SnippetFolder_SettingsSaveFailed",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));

            Assert.AreEqual(string.Empty, backup);
            Assert.IsFalse(settingsFileSystem.File.Exists(settingsPath));
            CollectionAssert.AreEqual(targetBytes, File.ReadAllBytes(target));
            Assert.AreEqual(_local, model.SnippetsPath);
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
            Assert.AreEqual("!local=Original", File.ReadAllText(source));
            StringAssert.Contains(settingsUtils.StagedContents, targetFolder.Replace("\\", "\\\\", StringComparison.Ordinal));
        }

        [TestMethod]
        public void SettingsRestorationLateFailureStillRollsBackFolderAndMemory()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Original");
            string targetFolder = Path.Combine(_root, "shared");
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            var settings = new TextExpanderSettings();
            settings.Properties.SnippetsPath.Value = _local;
            var settingsFileSystem = new MockFileSystem();
            var settingsUtils = new ThrowAfterCompensationSettingsUtils(settingsFileSystem, settings);
            settingsUtils.SaveSettings(settings.ToJsonString(), TextExpanderSettings.ModuleName);
            string settingsPath = settingsUtils.GetSettingsFilePath(TextExpanderSettings.ModuleName);
            TextExpanderViewModel model = CreateViewModel(settingsUtils);
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);

            Assert.AreEqual(
                "TextExpander_SnippetFolder_SettingsSaveFailed",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));

            Assert.AreEqual(string.Empty, backup);
            Assert.AreEqual(AtomicSettingsPublicationOutcome.PreviousSettingsVerified, settingsUtils.LastError?.Outcome);
            Assert.AreEqual(_local, settingsUtils.GetSettingsOrDefaultReadOnly<TextExpanderSettings>(TextExpanderSettings.ModuleName).Properties.SnippetsPath.Value);
            Assert.AreEqual(_local, model.SnippetsPath);
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
            Assert.AreEqual("!local=Original", File.ReadAllText(source));
            CollectionAssert.AreEqual(targetBytes, File.ReadAllBytes(target));
            Assert.IsTrue(settingsFileSystem.File.Exists(settingsPath));
        }

        [TestMethod]
        public void VerifiedNewSettingsAfterLateFailureKeepFolderMemoryAndPersistedPathAligned()
        {
            Write(_local, TextExpanderSnippetFileNames.Current, "!local=Original");
            string targetFolder = Path.Combine(_root, "shared");
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Original");
            var settings = new TextExpanderSettings();
            settings.Properties.SnippetsPath.Value = _local;
            var settingsFileSystem = new MockFileSystem();
            var settingsUtils = new FailCompensationSettingsUtils(settingsFileSystem, settings);
            settingsUtils.SaveSettings(settings.ToJsonString(), TextExpanderSettings.ModuleName);
            TextExpanderViewModel model = CreateViewModel(settingsUtils);
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);

            Assert.AreEqual(
                "TextExpander_SnippetFolder_SettingsCommittedAfterError",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));

            Assert.AreEqual(AtomicSettingsPublicationOutcome.NewSettingsVerified, settingsUtils.LastError?.Outcome);
            Assert.IsFalse(string.IsNullOrEmpty(backup));
            Assert.IsTrue(File.Exists(backup));
            Assert.AreEqual("!shared=Original", File.ReadAllText(backup));
            Assert.AreEqual("!local=Original", File.ReadAllText(target));
            Assert.AreEqual(targetFolder, model.SnippetsPath);
            Assert.AreEqual(target, model.SnippetsFilePath);
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
            Assert.AreEqual(
                targetFolder,
                settingsUtils.GetSettingsOrDefaultReadOnly<TextExpanderSettings>(TextExpanderSettings.ModuleName).Properties.SnippetsPath.Value);
        }

        [TestMethod]
        public void ReadableUncertainSettingsAreInspectedAndMatchingNewPathIsKept()
        {
            Write(_local, TextExpanderSnippetFileNames.Current, "!local=Original");
            string targetFolder = Path.Combine(_root, "shared");
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Original");
            var settings = new TextExpanderSettings();
            settings.Properties.SnippetsPath.Value = _local;
            var concurrentSettings = new TextExpanderSettings();
            concurrentSettings.Properties.SnippetsPath.Value = targetFolder;
            string concurrentJson = concurrentSettings.ToJsonString() + Environment.NewLine;
            var settingsFileSystem = new MockFileSystem();
            var settingsUtils = new MutateBeforeCompensationSettingsUtils(
                settingsFileSystem,
                settings,
                concurrentJson);
            settingsUtils.SaveSettings(settings.ToJsonString(), TextExpanderSettings.ModuleName);
            TextExpanderViewModel model = CreateViewModel(settingsUtils);
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);

            Assert.AreEqual(
                "TextExpander_SnippetFolder_SettingsCommittedAfterError",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));

            Assert.AreEqual(AtomicSettingsPublicationOutcome.StateUncertain, settingsUtils.LastError?.Outcome);
            Assert.IsFalse(string.IsNullOrEmpty(backup));
            Assert.AreEqual(targetFolder, model.SnippetsPath);
            Assert.AreEqual(target, model.SnippetsFilePath);
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
            Assert.AreEqual(
                targetFolder,
                settingsUtils.GetSettingsOrDefaultReadOnly<TextExpanderSettings>(TextExpanderSettings.ModuleName).Properties.SnippetsPath.Value);
        }

        [TestMethod]
        public void ReadableUncertainPreviousPathRollsFolderBackWithoutClaimingEverythingWasUndone()
        {
            Write(_local, TextExpanderSnippetFileNames.Current, "!local=Original");
            string targetFolder = Path.Combine(_root, "shared");
            string target = Write(targetFolder, TextExpanderSnippetFileNames.Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            var settings = new TextExpanderSettings();
            settings.Properties.SnippetsPath.Value = _local;
            string concurrentJson = settings.ToJsonString() + Environment.NewLine;
            var settingsFileSystem = new MockFileSystem();
            var settingsUtils = new MutateBeforeCompensationSettingsUtils(
                settingsFileSystem,
                settings,
                concurrentJson);
            settingsUtils.SaveSettings(settings.ToJsonString(), TextExpanderSettings.ModuleName);
            TextExpanderViewModel model = CreateViewModel(settingsUtils);
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);

            Assert.AreEqual(
                "TextExpander_SnippetFolder_SettingsStateUncertain",
                model.ApplyFolderChange(change, TextExpanderFolderConflictChoice.ReplaceFolder, out string backup));

            Assert.AreEqual(AtomicSettingsPublicationOutcome.StateUncertain, settingsUtils.LastError?.Outcome);
            Assert.IsFalse(string.IsNullOrEmpty(backup));
            Assert.IsTrue(settingsFileSystem.File.Exists(backup));
            Assert.IsTrue(settingsUtils.LastError.RecoveryPaths.Contains(backup, StringComparer.OrdinalIgnoreCase));
            CollectionAssert.AreEqual(targetBytes, File.ReadAllBytes(target));
            Assert.AreEqual(_local, model.SnippetsPath);
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public void UnreadableUncertainSettingsPreserveFolderAndSurfaceAllRecoveryPaths()
        {
            string source = Write(_local, TextExpanderSnippetFileNames.Current, "!local=Original");
            string targetFolder = Path.Combine(_root, "shared");
            var settings = new TextExpanderSettings();
            settings.Properties.SnippetsPath.Value = _local;
            var settingsFileSystem = new MockFileSystem();
            var settingsUtils = new MutateBeforeCompensationSettingsUtils(
                settingsFileSystem,
                settings,
                "{not-json");
            settingsUtils.SaveSettings(settings.ToJsonString(), TextExpanderSettings.ModuleName);
            TextExpanderViewModel model = CreateViewModel(
                settingsUtils,
                key => key switch
                {
                    "TextExpander_SnippetFolder_SettingsStateUncertain" =>
                        "Settings path: {0}. Recovery paths: {1}.",
                    _ => key,
                });
            TextExpanderFolderChange change = model.PrepareFolderChange(targetFolder);

            string error = model.ApplyFolderChange(
                change,
                TextExpanderFolderConflictChoice.ReplaceFolder,
                out string backup);

            Assert.AreEqual(AtomicSettingsPublicationOutcome.StateUncertain, settingsUtils.LastError?.Outcome);
            Assert.IsNotNull(error);
            StringAssert.Contains(error, settingsUtils.LastError.SettingsPath);
            foreach (string recoveryPath in settingsUtils.LastError.RecoveryPaths)
            {
                StringAssert.Contains(error, recoveryPath);
                Assert.IsTrue(settingsFileSystem.File.Exists(recoveryPath));
            }

            string target = Path.Combine(targetFolder, TextExpanderSnippetFileNames.Current);
            Assert.AreEqual(target, backup, "The first published folder file is the only folder recovery copy and must be surfaced.");
            Assert.IsTrue(File.Exists(backup));
            Assert.AreEqual(File.ReadAllText(source), File.ReadAllText(target));
            Assert.AreEqual(_local, model.SnippetsPath);
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
        }

        private TextExpanderViewModel CreateViewModel(
            Action<Mock<SettingsUtils>> configureSettings = null,
            Action<string> openFile = null)
        {
            var settings = new TextExpanderSettings();
            settings.Properties.SnippetsPath.Value = _local;
            var utils = new Mock<SettingsUtils>(new FileSystem(), null);
            utils.Setup(u => u.GetSettingsOrDefault<TextExpanderSettings>(TextExpanderSettings.ModuleName, SettingsUtils.DefaultFileName)).Returns(settings);
            utils.Setup(u => u.GetSettingsOrDefaultReadOnly<TextExpanderSettings>(TextExpanderSettings.ModuleName, SettingsUtils.DefaultFileName)).Returns(settings);
            configureSettings?.Invoke(utils);
            return CreateViewModel(utils.Object, openFile: openFile);
        }

        private TextExpanderViewModel CreateViewModel(
            SettingsUtils settingsUtils,
            Func<string, string> getResourceString = null,
            Action<string> openFile = null)
        {
            var repository = new Mock<ISettingsRepository<GeneralSettings>>();
            repository.SetupGet(r => r.SettingsConfig).Returns(new GeneralSettings());
            return new TextExpanderViewModel(
                settingsUtils,
                repository.Object,
                _ => 0,
                null,
                isElevated: false,
                getResourceString: getResourceString ?? (key => key),
                storageEnvironment: _environment,
                openFile: openFile);
        }

        private static string Write(string folder, string name, string content)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, content);
            return path;
        }

        private static string[] SplitRecoveryPaths(string paths)
            => paths.Split("; ", StringSplitOptions.RemoveEmptyEntries);

        private static AtomicSettingsPublicationException VerifiedPreviousSettingsFailure()
            => new AtomicSettingsPublicationException(
                AtomicSettingsPublicationOutcome.PreviousSettingsVerified,
                "settings.json",
                recoveryPaths: null,
                new IOException("Injected settings write failure."));

        private sealed class TestStorageEnvironment : ITextExpanderStorageEnvironment
        {
            private readonly string _root;
            private Action _filesChanged;
            private string _publishAfterMissingPath;
            private byte[] _publishAfterMissingBytes;
            private string _publishWhenWatchStartsPath;
            private byte[] _publishWhenWatchStartsBytes;
            private Action _beforeNextPublication;
            private Action _afterNextStagingWrite;
            private bool _publicationCompleted;
            private bool _failNextReadAfterPublication;
            private Action _beforeNextReadFailure;
            private bool _failNextRollback;
            private Action _beforeNextRollbackQuarantine;
            private Action _beforeUnexpectedRollbackRestore;
            private Func<string, bool> _cleanupDispositionPredicate;
            private Action<string> _beforeCleanupDisposition;

            public TestStorageEnvironment(string root, string defaultSnippetsFolder)
            {
                _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
                DefaultSnippetsFolder = defaultSnippetsFolder;
            }

            public string DefaultSnippetsFolder { get; }

            public string ExpandEnvironmentVariables(string value) => value;

            public bool FileExists(string path)
            {
                string guarded = Guard(path);
                bool exists = File.Exists(guarded);
                if (!exists && string.Equals(guarded, _publishAfterMissingPath, StringComparison.OrdinalIgnoreCase))
                {
                    string publishPath = _publishAfterMissingPath;
                    byte[] publishBytes = _publishAfterMissingBytes;
                    _publishAfterMissingPath = null;
                    _publishAfterMissingBytes = null;
                    File.WriteAllBytes(publishPath, publishBytes);
                }

                return exists;
            }

            public void CreateDirectory(string path) => Directory.CreateDirectory(Guard(path));

            public byte[] ReadAllBytes(string path)
            {
                string guarded = Guard(path);
                if (_publicationCompleted && _failNextReadAfterPublication)
                {
                    _failNextReadAfterPublication = false;
                    InvokeAndClear(ref _beforeNextReadFailure);
                    throw new IOException("Injected post-publication identity read failure.");
                }

                return File.ReadAllBytes(guarded);
            }

            public void WriteNewFile(string path, byte[] bytes)
            {
                using var stream = new FileStream(Guard(path), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(bytes, 0, bytes.Length);
                if (Path.GetFileName(path).EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                {
                    InvokeAndClear(ref _afterNextStagingWrite);
                }
            }

            public void MoveFile(string source, string destination)
            {
                bool quarantineMove = IsRollbackQuarantinePath(destination);
                bool unexpectedRestore = IsRollbackQuarantinePath(source);
                bool rollback = quarantineMove || unexpectedRestore || IsRollbackSource(source);
                if (rollback && _failNextRollback)
                {
                    _failNextRollback = false;
                    throw new IOException("Injected rollback API failure.");
                }

                if (quarantineMove)
                {
                    InvokeAndClear(ref _beforeNextRollbackQuarantine);
                }
                else if (unexpectedRestore)
                {
                    InvokeAndClear(ref _beforeUnexpectedRollbackRestore);
                }

                if (!rollback)
                {
                    InvokeBeforeNextPublication();
                }

                File.Move(Guard(source), Guard(destination), overwrite: false);
                _publicationCompleted = !rollback;
            }

            public void ReplaceFile(string source, string destination, string backup)
            {
                bool rollback = IsRollbackSource(source);
                if (rollback && _failNextRollback)
                {
                    _failNextRollback = false;
                    throw new IOException("Injected rollback API failure.");
                }

                if (!rollback)
                {
                    InvokeBeforeNextPublication();
                }

                File.Replace(Guard(source), Guard(destination), backup == null ? null : Guard(backup), ignoreMetadataErrors: true);
                _publicationCompleted = !rollback;
            }

            public HandleBoundFileDeletionResult DeleteOwnedFile(string path, byte[] expectedBytes)
            {
                string guarded = Guard(path);
                Action beforeDisposition = null;
                if (_cleanupDispositionPredicate?.Invoke(guarded) == true)
                {
                    Action<string> action = _beforeCleanupDisposition;
                    _cleanupDispositionPredicate = null;
                    _beforeCleanupDisposition = null;
                    beforeDisposition = () => action?.Invoke(guarded);
                }

                return HandleBoundFileDeletion.TryDelete(guarded, expectedBytes, beforeDisposition);
            }

            public IDisposable WatchForFiles(string folder, IReadOnlyList<string> fileNames, Action changed)
            {
                Guard(folder);
                if (_publishWhenWatchStartsPath != null)
                {
                    string publishPath = _publishWhenWatchStartsPath;
                    byte[] publishBytes = _publishWhenWatchStartsBytes;
                    _publishWhenWatchStartsPath = null;
                    _publishWhenWatchStartsBytes = null;
                    File.WriteAllBytes(publishPath, publishBytes);
                }

                _filesChanged = changed;
                return new CallbackDisposable(() =>
                {
                    if (ReferenceEquals(_filesChanged, changed))
                    {
                        _filesChanged = null;
                    }
                });
            }

            public void NotifyFilesChanged() => _filesChanged?.Invoke();

            public void PublishAfterMissingProbe(string path, byte[] bytes)
            {
                _publishAfterMissingPath = Guard(path);
                _publishAfterMissingBytes = bytes;
            }

            public void PublishWhenWatchStarts(string path, byte[] bytes)
            {
                _publishWhenWatchStartsPath = Guard(path);
                _publishWhenWatchStartsBytes = bytes;
            }

            public void BeforeNextPublication(Action action)
                => _beforeNextPublication = action;

            public void AfterNextStagingWrite(Action action)
                => _afterNextStagingWrite = action;

            public void FailNextReadAfterPublication(Action beforeFailure = null)
            {
                _failNextReadAfterPublication = true;
                _beforeNextReadFailure = beforeFailure;
            }

            public void FailNextRollback()
                => _failNextRollback = true;

            public void BeforeNextRollbackQuarantine(Action action)
                => _beforeNextRollbackQuarantine = action;

            public void BeforeUnexpectedRollbackRestore(Action action)
                => _beforeUnexpectedRollbackRestore = action;

            public void BeforeNextCleanupDisposition(
                Func<string, bool> predicate,
                Action<string> action)
            {
                _cleanupDispositionPredicate = predicate;
                _beforeCleanupDisposition = action;
            }

            private void InvokeBeforeNextPublication()
            {
                Action action = _beforeNextPublication;
                _beforeNextPublication = null;
                action?.Invoke();
            }

            private static void InvokeAndClear(ref Action action)
            {
                Action pending = action;
                action = null;
                pending?.Invoke();
            }

            private static bool IsRollbackSource(string path)
                => Path.GetFileName(path).Contains(".backup-", StringComparison.OrdinalIgnoreCase);

            private static bool IsRollbackQuarantinePath(string path)
                => Path.GetFileName(path).Contains(".rollback-quarantine", StringComparison.OrdinalIgnoreCase);

            private string Guard(string path)
            {
                string full = Path.GetFullPath(path);
                if (!full.Equals(_root, StringComparison.OrdinalIgnoreCase)
                    && !full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Fail("The test touched a path outside its temporary folder: " + full);
                }

                return full;
            }
        }

        private sealed class CallbackDisposable : IDisposable
        {
            private readonly Action _dispose;

            public CallbackDisposable(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose() => _dispose();
        }

        private abstract class MockFileSystemSettingsUtils : SettingsUtils
        {
            protected MockFileSystemSettingsUtils(MockFileSystem fileSystem)
                : base(fileSystem)
            {
                FileSystem = fileSystem;
            }

            protected MockFileSystem FileSystem { get; }

            protected override HandleBoundFileDeletionResult TryDeleteOwnedAtomicSaveArtifact(
                string path,
                byte[] ownedBytes)
            {
                if (!FileSystem.File.Exists(path))
                {
                    return new HandleBoundFileDeletionResult(
                        HandleBoundFileDeletionStatus.NotFound,
                        path,
                        Error: null);
                }

                if (!FileSystem.File.ReadAllBytes(path).AsSpan().SequenceEqual(ownedBytes))
                {
                    return new HandleBoundFileDeletionResult(
                        HandleBoundFileDeletionStatus.IdentityMismatch,
                        path,
                        new IOException($"The mock file at '{path}' is not the owned cleanup artifact."));
                }

                FileSystem.File.Delete(path);
                return new HandleBoundFileDeletionResult(
                    HandleBoundFileDeletionStatus.Deleted,
                    path,
                    Error: null);
            }
        }

        private sealed class ThrowAfterPublicationSettingsUtils : MockFileSystemSettingsUtils
        {
            private readonly MockFileSystem _fileSystem;
            private readonly TextExpanderSettings _settings;

            public ThrowAfterPublicationSettingsUtils(MockFileSystem fileSystem, TextExpanderSettings settings)
                : base(fileSystem)
            {
                _fileSystem = fileSystem;
                _settings = settings;
            }

            public string StagedContents { get; private set; }

            public override T GetSettingsOrDefault<T>(string powertoy = "", string fileName = SettingsUtils.DefaultFileName)
            {
                if (typeof(T) == typeof(TextExpanderSettings))
                {
                    return (T)(object)_settings;
                }

                return base.GetSettingsOrDefault<T>(powertoy, fileName);
            }

            public override T GetSettingsOrDefaultReadOnly<T>(string powertoy = "", string fileName = SettingsUtils.DefaultFileName)
            {
                if (typeof(T) == typeof(TextExpanderSettings))
                {
                    return (T)(object)_settings;
                }

                return base.GetSettingsOrDefaultReadOnly<T>(powertoy, fileName);
            }

            protected override void PublishStagedSettingsFile(
                string stagingPath,
                string settingsPath,
                string backupPath,
                bool destinationExisted)
            {
                StagedContents = _fileSystem.File.ReadAllText(stagingPath);
                base.PublishStagedSettingsFile(stagingPath, settingsPath, backupPath, destinationExisted);
                throw new IOException("Injected failure after atomic settings publication.");
            }
        }

        private abstract class AtomicFailureSettingsUtils : MockFileSystemSettingsUtils
        {
            private readonly TextExpanderSettings _settings;

            protected AtomicFailureSettingsUtils(MockFileSystem fileSystem, TextExpanderSettings settings)
                : base(fileSystem)
            {
                _settings = settings;
            }

            public AtomicSettingsPublicationException LastError { get; private set; }

            public override T GetSettingsOrDefault<T>(string powertoy = "", string fileName = SettingsUtils.DefaultFileName)
            {
                if (typeof(T) == typeof(TextExpanderSettings))
                {
                    return (T)(object)_settings;
                }

                return base.GetSettingsOrDefault<T>(powertoy, fileName);
            }

            public override void SaveSettingsOrThrow(
                string jsonSettings,
                string powertoy = "",
                string fileName = SettingsUtils.DefaultFileName)
            {
                try
                {
                    base.SaveSettingsOrThrow(jsonSettings, powertoy, fileName);
                }
                catch (AtomicSettingsPublicationException ex)
                {
                    LastError = ex;
                    throw;
                }
            }

            protected override void PublishStagedSettingsFile(
                string stagingPath,
                string settingsPath,
                string backupPath,
                bool destinationExisted)
            {
                base.PublishStagedSettingsFile(stagingPath, settingsPath, backupPath, destinationExisted);
                throw new IOException("Injected failure after atomic settings publication.");
            }
        }

        private sealed class ThrowAfterCompensationSettingsUtils : AtomicFailureSettingsUtils
        {
            public ThrowAfterCompensationSettingsUtils(
                MockFileSystem fileSystem,
                TextExpanderSettings settings)
                : base(fileSystem, settings)
            {
            }

            protected override void CompensateFailedSettingsPublication(
                string stagingPath,
                string settingsPath,
                string backupPath,
                string displacedPath,
                bool destinationExisted,
                byte[] originalBytes,
                byte[] publishedBytes)
            {
                base.CompensateFailedSettingsPublication(
                    stagingPath,
                    settingsPath,
                    backupPath,
                    displacedPath,
                    destinationExisted,
                    originalBytes,
                    publishedBytes);
                throw new IOException("Injected failure after settings restoration.");
            }
        }

        private sealed class FailCompensationSettingsUtils : AtomicFailureSettingsUtils
        {
            public FailCompensationSettingsUtils(
                MockFileSystem fileSystem,
                TextExpanderSettings settings)
                : base(fileSystem, settings)
            {
            }

            protected override void CompensateFailedSettingsPublication(
                string stagingPath,
                string settingsPath,
                string backupPath,
                string displacedPath,
                bool destinationExisted,
                byte[] originalBytes,
                byte[] publishedBytes)
            {
                throw new IOException("Injected settings restoration failure.");
            }
        }

        private sealed class MutateBeforeCompensationSettingsUtils : AtomicFailureSettingsUtils
        {
            private readonly string _replacementSettings;

            public MutateBeforeCompensationSettingsUtils(
                MockFileSystem fileSystem,
                TextExpanderSettings settings,
                string replacementSettings)
                : base(fileSystem, settings)
            {
                _replacementSettings = replacementSettings;
            }

            protected override void BeforeSettingsPublicationCompensation(
                string stagingPath,
                string settingsPath,
                string backupPath,
                bool destinationExisted)
            {
                FileSystem.File.WriteAllText(settingsPath, _replacementSettings);
            }
        }
    }
}
