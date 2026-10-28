// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.TextExpander;
using Microsoft.PowerToys.TextExpander.Editor.Helpers;
using Microsoft.PowerToys.TextExpander.Editor.UnitTests;
using Microsoft.PowerToys.TextExpander.Editor.ViewModels;
using Microsoft.PowerToys.TextExpander.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Choice = Microsoft.PowerToys.TextExpander.Editor.ViewModels.TextExpanderFolderConflictChoice;

namespace ViewModelTests
{
    [TestClass]
    public class TextExpanderStorage
    {
        private const string Current = TextExpanderSnippetFileNames.Current;
        private const string Legacy = TextExpanderSnippetFileNames.Legacy;
        private const string Stale = "TextExpander_SnippetFolder_ChangeStale";
        private const string RollbackFailed = "TextExpander_SnippetFolder_RollbackFailed";
        private const string SettingsSaveFailed = "TextExpander_SnippetFolder_SettingsSaveFailed";
        private const string SettingsCommittedAfterError = "TextExpander_SnippetFolder_SettingsCommittedAfterError";
        private string _root;
        private string _local;
        private TestStorageEnvironment _environment;

        private string Shared => Path.Combine(_root, "shared");

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
            string source = Write(_local, Legacy, "!local=Local");
            TextExpanderViewModel model = CreateViewModel();

            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);
            Assert.IsFalse(change.HasExistingFile);
            Assert.IsNull(model.ApplyFolderChange(change, Choice.ReplaceFolder, out string backup));

            Assert.AreEqual(string.Empty, backup);
            Assert.AreEqual(Shared, model.SnippetsPath);
            Assert.AreEqual(File.ReadAllText(source), File.ReadAllText(Path.Combine(Shared, Current)));
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        [DataRow(Current)]
        [DataRow(Legacy)]
        public void OpenSnippetsFileUsesTheResolvedName(string name)
        {
            string file = Write(_local, name, "!local=Local");
            string opened = null;
            TextExpanderViewModel model = CreateViewModel(openFile: path => opened = path);

            Assert.IsNull(model.OpenSnippetsFile());
            Assert.AreEqual(file, opened);
        }

        [TestMethod]
        public void OpenSnippetsFileFollowsAnAppliedFolderChange()
        {
            Write(_local, Legacy, "!local=Local");
            string target = Write(Shared, Current, "!shared=Shared");
            string opened = null;
            TextExpanderViewModel model = CreateViewModel(openFile: path => opened = path);

            Assert.IsNull(model.ApplyFolderChange(model.PrepareFolderChange(Shared), Choice.UseFolder, out _));
            Assert.IsNull(model.OpenSnippetsFile());
            Assert.AreEqual(target, opened);
        }

        [TestMethod]
        public void OpenSnippetsFileReportsLaunchAndMissingFileFailures()
        {
            string current = Write(_local, Current, "!local=Local");
            TextExpanderViewModel model = CreateViewModel(openFile: _ => throw new InvalidOperationException("Injected launch failure."));

            Assert.AreEqual("TextExpander_SnippetFile_OpenFailed", model.OpenSnippetsFile());

            File.Delete(current);
            Assert.AreEqual("TextExpander_SnippetFile_OpenMissing", model.OpenSnippetsFile());
        }

        [TestMethod]
        public void UsingFolderFileBacksUpThePreviousActiveFile()
        {
            string source = Write(_local, Current, "!local=Local");
            Write(Shared, Current, "!shared=Shared");
            TextExpanderViewModel model = CreateViewModel();

            Assert.IsNull(model.ApplyFolderChange(model.PrepareFolderChange(Shared), Choice.UseFolder, out string backup));

            Assert.IsTrue(File.Exists(backup));
            Assert.AreEqual(File.ReadAllText(source), File.ReadAllText(backup));
            Assert.AreEqual("!shared", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public void ReplacingFolderFileBacksUpItsPreviousContents()
        {
            Write(_local, Current, "!local=Local");
            string target = Write(Shared, Current, "!shared=Shared");
            TextExpanderViewModel model = CreateViewModel();

            Assert.IsNull(model.ApplyFolderChange(model.PrepareFolderChange(Shared), Choice.ReplaceFolder, out string backup));

            Assert.AreEqual("!shared=Shared", File.ReadAllText(backup));
            Assert.AreEqual("!local=Local", File.ReadAllText(target));
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public void FileAppearingAfterFolderSelectionIsNotOverwritten()
        {
            Write(_local, Current, "!local=Local");
            TextExpanderViewModel model = CreateViewModel();

            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);
            Assert.IsFalse(change.HasExistingFile);
            string target = Write(Shared, Current, "!shared=Shared");

            AssertStale(model, change);
            Assert.AreEqual("!shared=Shared", File.ReadAllText(target));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void LegacyDestinationChangingToCurrentRequiresANewDecision()
        {
            Write(_local, Current, "!local=Local");
            string legacy = Write(Shared, Legacy, "!legacy=Legacy");
            TextExpanderViewModel model = CreateViewModel();

            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);
            Assert.AreEqual(legacy, change.FilePath);
            string current = Write(Shared, Current, "!current=Current");

            AssertStale(model, change);
            Assert.AreEqual("!legacy=Legacy", File.ReadAllText(legacy));
            Assert.AreEqual("!current=Current", File.ReadAllText(current));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void SelectedDestinationDisappearingRequiresANewDecision()
        {
            Write(_local, Current, "!local=Local");
            string target = Write(Shared, Legacy, "!shared=Shared");
            TextExpanderViewModel model = CreateViewModel();

            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);
            Assert.AreEqual(target, change.FilePath);
            File.Delete(target);

            AssertStale(model, change, Choice.UseFolder);
            Assert.IsFalse(File.Exists(target));
            Assert.IsFalse(File.Exists(Path.Combine(Shared, Current)));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void SourceDisappearingBeforeReplacePreservesDestinationBytes()
        {
            string source = Write(_local, Current, "!local=Local");
            string target = Write(Shared, Current, "!shared=Shared");
            byte[] destinationBytes = File.ReadAllBytes(target);
            TextExpanderViewModel model = CreateViewModel();

            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);
            File.Delete(source);

            AssertStale(model, change);
            CollectionAssert.AreEqual(destinationBytes, File.ReadAllBytes(target));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void SettingsRechecksLegacyAtStarterPublicationBoundary()
        {
            string legacy = Path.Combine(_local, Legacy);
            byte[] legacyBytes = Utf8("!legacy=arrived\r\n");
            _environment.PublishAfterMissingProbe(legacy, legacyBytes);

            TextExpanderViewModel model = CreateViewModel();

            Assert.AreEqual(legacy, model.SnippetsFilePath);
            Assert.AreEqual("!legacy", model.Snippets.Single().Trigger);
            CollectionAssert.AreEqual(legacyBytes, File.ReadAllBytes(legacy));
            Assert.IsFalse(File.Exists(Path.Combine(_local, Current)), "Settings must use the same folder-aware publication rule as the engine.");
        }

        [TestMethod]
        public void SettingsWatcherHandoffReloadsLegacyArrivingAfterStarterResolution()
        {
            string current = Path.Combine(_local, Current);
            string legacy = Path.Combine(_local, Legacy);
            byte[] legacyBytes = Utf8("!legacy=arrived-before-watch\r\n");
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
            string legacy = Write(_local, Legacy, "!legacy=Legacy");
            string current = Path.Combine(_local, Current);
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
            string current = Write(_local, Current, "!one=1");
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

            Assert.IsNull(model.ApplyFolderChange(model.PrepareFolderChange(Shared), Choice.ReplaceFolder, out string backup));

            Assert.AreEqual(string.Empty, backup);
            Assert.AreEqual(TextExpanderStarter.Content, File.ReadAllText(Path.Combine(Shared, Current)));
            Assert.AreEqual(6, model.Snippets.Count);
        }

        [TestMethod]
        public void ExistingZeroByteSourceRemainsZeroBytesInEmptyDestination()
        {
            string source = Write(_local, Current, string.Empty);
            TextExpanderViewModel model = CreateViewModel();

            Assert.IsNull(model.ApplyFolderChange(model.PrepareFolderChange(Shared), Choice.ReplaceFolder, out _));

            Assert.AreEqual(0, new FileInfo(source).Length);
            Assert.AreEqual(0, new FileInfo(Path.Combine(Shared, Current)).Length);
            Assert.AreEqual(0, model.Snippets.Count);
        }

        [TestMethod]
        public void ReplacingDestinationPreservesSourceAndDestinationBytes()
        {
            var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            var utf16 = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
            byte[] sourceBytes = utf8Bom.GetPreamble().Concat(utf8Bom.GetBytes("!source=Keep\r\n")).ToArray();
            byte[] destinationBytes = utf16.GetPreamble().Concat(utf16.GetBytes("!destination=Keep\n")).ToArray();
            string source = Path.Combine(_local, Current);
            File.WriteAllBytes(source, sourceBytes);
            Directory.CreateDirectory(Shared);
            string target = Path.Combine(Shared, Legacy);
            File.WriteAllBytes(target, destinationBytes);
            TextExpanderViewModel model = CreateViewModel();

            Assert.IsNull(model.ApplyFolderChange(model.PrepareFolderChange(Shared), Choice.ReplaceFolder, out string backup));

            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source));
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(target));
            CollectionAssert.AreEqual(destinationBytes, File.ReadAllBytes(backup));
        }

        [TestMethod]
        public void SupportedFileArrivingInFinalMoveWindowIsPreservedAndChangeIsStale()
        {
            string source = Write(_local, Current, "!local=Local");
            byte[] sourceBytes = File.ReadAllBytes(source);
            string legacy = Path.Combine(Shared, Legacy);
            byte[] legacyBytes = Utf8("!legacy=Arrived");
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);
            _environment.BeforeNextPublication(() => File.WriteAllBytes(legacy, legacyBytes));

            AssertStale(model, change);
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source));
            CollectionAssert.AreEqual(legacyBytes, File.ReadAllBytes(legacy));
            Assert.IsFalse(File.Exists(Path.Combine(Shared, Current)));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void SupportedFileArrivingInFinalReplaceWindowRollsBackOwnedReplacement()
        {
            string source = Write(_local, Current, "!local=Local");
            byte[] sourceBytes = File.ReadAllBytes(source);
            string current = Write(Shared, Current, "!shared=Original");
            byte[] currentBytes = File.ReadAllBytes(current);
            string legacy = Path.Combine(Shared, Legacy);
            byte[] legacyBytes = Utf8("!legacy=Arrived");
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);
            _environment.BeforeNextPublication(() => File.WriteAllBytes(legacy, legacyBytes));

            AssertStale(model, change);
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source));
            CollectionAssert.AreEqual(currentBytes, File.ReadAllBytes(current));
            CollectionAssert.AreEqual(legacyBytes, File.ReadAllBytes(legacy));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void SourceMutationInFinalMoveWindowRollsBackOwnedPublication()
        {
            string source = Write(_local, Current, "!local=Original");
            byte[] mutatedBytes = Utf8("!local=Mutated");
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);
            _environment.BeforeNextPublication(() => File.WriteAllBytes(source, mutatedBytes));

            AssertStale(model, change);
            CollectionAssert.AreEqual(mutatedBytes, File.ReadAllBytes(source));
            Assert.IsFalse(File.Exists(Path.Combine(Shared, Current)));
            Assert.IsFalse(File.Exists(Path.Combine(Shared, Legacy)));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void SourceDisappearanceInFinalReplaceWindowRollsBackOwnedReplacement()
        {
            string source = Write(_local, Current, "!local=Original");
            string target = Write(Shared, Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);
            _environment.BeforeNextPublication(() => File.Delete(source));

            AssertStale(model, change);
            Assert.IsFalse(File.Exists(source));
            CollectionAssert.AreEqual(targetBytes, File.ReadAllBytes(target));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void StagingReplacementAtHandleBoundCleanupSurvivesAndIsSurfaced()
        {
            string source = Write(_local, Current, "!local=Original");
            byte[] mutatedSource = Utf8("!local=Mutated");
            byte[] replacementBytes = Utf8("!external=StagingReplacement");
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);
            _environment.AfterNextStagingWrite(() => File.WriteAllBytes(source, mutatedSource));
            _environment.ReplaceBeforeCleanupDisposition(name => name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase), replacementBytes);

            string error = model.ApplyFolderChange(change, Choice.ReplaceFolder, out string recovery);

            string replacementPath = _environment.ReplacedCleanupPath;
            Assert.IsNotNull(error);
            StringAssert.Contains(error, "staging file");
            StringAssert.Contains(error, replacementPath);
            Assert.AreEqual(replacementPath, recovery);
            CollectionAssert.AreEqual(replacementBytes, File.ReadAllBytes(replacementPath));
            Assert.IsFalse(File.Exists(replacementPath + ".owned"), "Disposition must delete the verified object after it is renamed.");
            CollectionAssert.AreEqual(mutatedSource, File.ReadAllBytes(source));
            Assert.IsFalse(File.Exists(Path.Combine(Shared, Current)));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void PostPublicationIdentityReadFailureRestoresDestinationBytes()
        {
            string source = Write(_local, Current, "!local=Original");
            byte[] sourceBytes = File.ReadAllBytes(source);
            string target = Write(Shared, Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);
            _environment.FailNextReadAfterPublication();

            Assert.AreEqual(
                "Injected post-publication identity read failure.",
                model.ApplyFolderChange(change, Choice.ReplaceFolder, out string backup));

            Assert.AreEqual(string.Empty, backup);
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source));
            CollectionAssert.AreEqual(targetBytes, File.ReadAllBytes(target));
            Assert.AreEqual(_local, model.SnippetsPath);
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public void FolderQuarantineReplacementAtHandleBoundCleanupSurvivesAndIsSurfaced()
        {
            string source = Write(_local, Current, "!local=Original");
            byte[] sourceBytes = File.ReadAllBytes(source);
            byte[] replacementBytes = Utf8("!external=QuarantineReplacement");
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);
            _environment.FailNextReadAfterPublication();
            _environment.ReplaceBeforeCleanupDisposition(name => name.Contains(".rollback-quarantine", StringComparison.OrdinalIgnoreCase), replacementBytes);

            Assert.AreEqual(RollbackFailed, model.ApplyFolderChange(change, Choice.ReplaceFolder, out string recovery));

            string replacementPath = _environment.ReplacedCleanupPath;
            Assert.AreEqual(replacementPath, recovery);
            CollectionAssert.AreEqual(replacementBytes, File.ReadAllBytes(replacementPath));
            Assert.IsFalse(File.Exists(replacementPath + ".owned"), "Disposition must delete the verified quarantined object.");
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source));
            Assert.IsFalse(File.Exists(Path.Combine(Shared, Current)));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void BackupReplacementAtHandleBoundCleanupSurvivesAndIsSurfaced()
        {
            Write(_local, Current, "!local=Original");
            string target = Write(Shared, Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            byte[] replacementBytes = Utf8("!external=BackupReplacement");
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);
            _environment.FailNextReadAfterPublication(() => File.WriteAllBytes(target, targetBytes));
            _environment.ReplaceBeforeCleanupDisposition(name => name.Contains(".backup-", StringComparison.OrdinalIgnoreCase), replacementBytes);

            Assert.AreEqual(RollbackFailed, model.ApplyFolderChange(change, Choice.ReplaceFolder, out string recovery));

            string replacementPath = _environment.ReplacedCleanupPath;
            string[] recoveryPaths = SplitRecoveryPaths(recovery);
            CollectionAssert.Contains(recoveryPaths, replacementPath);
            CollectionAssert.Contains(recoveryPaths, target);
            CollectionAssert.AreEqual(replacementBytes, File.ReadAllBytes(replacementPath));
            Assert.IsFalse(File.Exists(replacementPath + ".owned"), "Disposition must delete the verified backup object.");
            CollectionAssert.AreEqual(targetBytes, File.ReadAllBytes(target));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void NewerExternalFileArrivingDuringNewDestinationRollbackIsRestoredAndSurfaced()
        {
            string source = Write(_local, Current, "!local=Original");
            byte[] sourceBytes = File.ReadAllBytes(source);
            string target = Path.Combine(Shared, Current);
            byte[] externalBytes = Utf8("!external=Newer");
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);
            _environment.FailNextReadAfterPublication();
            _environment.BeforeNextRollbackQuarantine(() => File.WriteAllBytes(target, externalBytes));

            Assert.AreEqual(RollbackFailed, model.ApplyFolderChange(change, Choice.ReplaceFolder, out string recovery));

            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source));
            CollectionAssert.AreEqual(externalBytes, File.ReadAllBytes(target));
            CollectionAssert.AreEqual(new[] { target }, SplitRecoveryPaths(recovery));
            Assert.IsFalse(Directory.GetFiles(Shared).Any(IsRollbackQuarantine));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void NewerExternalFileArrivingDuringReplacedDestinationRollbackPreservesBothWriters()
        {
            string source = Write(_local, Current, "!local=Original");
            byte[] sourceBytes = File.ReadAllBytes(source);
            string target = Write(Shared, Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            byte[] externalBytes = Utf8("!external=Newer");
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);
            _environment.FailNextReadAfterPublication();
            _environment.BeforeNextRollbackQuarantine(() => File.WriteAllBytes(target, externalBytes));

            Assert.AreEqual(RollbackFailed, model.ApplyFolderChange(change, Choice.ReplaceFolder, out string recovery));

            string[] recoveryPaths = SplitRecoveryPaths(recovery);
            Assert.AreEqual(2, recoveryPaths.Length);
            Assert.IsTrue(recoveryPaths.Any(path => File.ReadAllBytes(path).SequenceEqual(targetBytes)));
            Assert.IsTrue(recoveryPaths.Any(path => File.ReadAllBytes(path).SequenceEqual(externalBytes)));
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source));
            CollectionAssert.AreEqual(externalBytes, File.ReadAllBytes(target));
            Assert.IsFalse(recoveryPaths.Any(IsRollbackQuarantine));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void FailedUnexpectedRollbackRestorationSurfacesQuarantineAndBackup()
        {
            Write(_local, Current, "!local=Original");
            string target = Write(Shared, Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            byte[] externalBytes = Utf8("!external=Displaced");
            byte[] blockerBytes = Utf8("!external=Newest");
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);
            _environment.FailNextReadAfterPublication();
            _environment.BeforeNextRollbackQuarantine(() => File.WriteAllBytes(target, externalBytes));
            _environment.BeforeUnexpectedRollbackRestore(() => File.WriteAllBytes(target, blockerBytes));

            Assert.AreEqual(RollbackFailed, model.ApplyFolderChange(change, Choice.ReplaceFolder, out string recovery));

            string[] recoveryPaths = SplitRecoveryPaths(recovery);
            Assert.IsTrue(recoveryPaths.Any(path => File.ReadAllBytes(path).SequenceEqual(targetBytes)));
            Assert.IsTrue(recoveryPaths.Any(path => IsRollbackQuarantine(path) && File.ReadAllBytes(path).SequenceEqual(externalBytes)));
            Assert.IsTrue(recoveryPaths.Any(path => File.ReadAllBytes(path).SequenceEqual(blockerBytes)));
            CollectionAssert.AreEqual(blockerBytes, File.ReadAllBytes(target));
            Assert.AreEqual(_local, model.SnippetsPath);
        }

        [TestMethod]
        public void RollbackApiFailurePreservesAndSurfacesOriginalDestinationBackup()
        {
            string source = Write(_local, Current, "!local=Original");
            byte[] sourceBytes = File.ReadAllBytes(source);
            string target = Write(Shared, Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            TextExpanderViewModel model = CreateViewModel();
            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);
            _environment.FailNextReadAfterPublication();
            _environment.FailNextRollback();

            Assert.AreEqual(RollbackFailed, model.ApplyFolderChange(change, Choice.ReplaceFolder, out string backup));

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
            string source = Write(_local, Current, "!local=Original");
            string target = Write(Shared, Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            TextExpanderViewModel model = null;
            bool persistenceSawOldLibrary = false;
            model = CreateViewModel(store => SetupSettingsSave(store)
                .Callback(() =>
                {
                    persistenceSawOldLibrary = model.SnippetsPath == _local
                        && model.Snippets.Single().Trigger == "!local";
                })
                .Throws(VerifiedPreviousSettingsFailure()));
            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);

            Assert.AreEqual(SettingsSaveFailed, model.ApplyFolderChange(change, Choice.ReplaceFolder, out string backup));

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
            string source = Write(_local, Current, "!local=Original");
            byte[] sourceBytes = File.ReadAllBytes(source);
            string target = Write(Shared, Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            TextExpanderViewModel model = CreateViewModel(store => SetupSettingsSave(store).Throws(VerifiedPreviousSettingsFailure()));
            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);
            _environment.FailNextRollback();

            Assert.AreEqual(RollbackFailed, model.ApplyFolderChange(change, Choice.ReplaceFolder, out string backup));

            Assert.IsFalse(string.IsNullOrEmpty(backup));
            Assert.IsTrue(File.Exists(backup));
            CollectionAssert.AreEqual(targetBytes, File.ReadAllBytes(backup));
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(target));
            Assert.AreEqual(_local, model.SnippetsPath);
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void AtomicSettingsPublicationFailureRestoresSettingsAndFolderState(bool settingsExisted)
        {
            string source = Write(_local, Current, "!local=Original");
            string target = Write(Shared, Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            var store = new MockSettingsStore { InitialSettings = EditorSettings.For(_local), ReadOnlyReturnsInitialSettings = true, ThrowAfterPublication = true };
            if (settingsExisted)
            {
                SaveInitialSettings(store);
            }

            string settingsPath = store.GetSettingsFilePath(TextExpanderSettings.ModuleName);
            byte[] settingsBytes = settingsExisted ? store.FileSystem.File.ReadAllBytes(settingsPath) : null;
            TextExpanderViewModel model = CreateViewModel(store);
            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);

            Assert.AreEqual(SettingsSaveFailed, model.ApplyFolderChange(change, Choice.ReplaceFolder, out string backup));

            Assert.AreEqual(string.Empty, backup);
            if (settingsExisted)
            {
                CollectionAssert.AreEqual(settingsBytes, store.FileSystem.File.ReadAllBytes(settingsPath));
            }
            else
            {
                Assert.IsFalse(store.FileSystem.File.Exists(settingsPath));
            }

            CollectionAssert.AreEqual(targetBytes, File.ReadAllBytes(target));
            Assert.AreEqual(_local, model.SnippetsPath);
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
            Assert.AreEqual("!local=Original", File.ReadAllText(source));
            StringAssert.Contains(store.StagedContents, Shared.Replace("\\", "\\\\", StringComparison.Ordinal));
        }

        [TestMethod]
        public void SettingsRestorationLateFailureStillRollsBackFolderAndMemory()
        {
            string source = Write(_local, Current, "!local=Original");
            string target = Write(Shared, Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            MockSettingsStore store = SaveInitialSettings(new MockSettingsStore { ThrowAfterPublication = true, FailAfterCompensation = true });
            TextExpanderViewModel model = CreateViewModel(store);
            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);

            Assert.AreEqual(SettingsSaveFailed, model.ApplyFolderChange(change, Choice.ReplaceFolder, out string backup));

            Assert.AreEqual(string.Empty, backup);
            Assert.AreEqual(AtomicSettingsPublicationOutcome.PreviousSettingsVerified, store.LastError?.Outcome);
            Assert.AreEqual(_local, PersistedSnippetsPath(store));
            Assert.AreEqual(_local, model.SnippetsPath);
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
            Assert.AreEqual("!local=Original", File.ReadAllText(source));
            CollectionAssert.AreEqual(targetBytes, File.ReadAllBytes(target));
            Assert.IsTrue(store.FileSystem.File.Exists(store.GetSettingsFilePath(TextExpanderSettings.ModuleName)));
        }

        [TestMethod]
        public void VerifiedNewSettingsAfterLateFailureKeepFolderMemoryAndPersistedPathAligned()
        {
            Write(_local, Current, "!local=Original");
            string target = Write(Shared, Current, "!shared=Original");
            MockSettingsStore store = SaveInitialSettings(new MockSettingsStore { ThrowAfterPublication = true, FailCompensation = true });
            TextExpanderViewModel model = CreateViewModel(store);
            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);

            Assert.AreEqual(SettingsCommittedAfterError, model.ApplyFolderChange(change, Choice.ReplaceFolder, out string backup));

            Assert.AreEqual(AtomicSettingsPublicationOutcome.NewSettingsVerified, store.LastError?.Outcome);
            Assert.IsFalse(string.IsNullOrEmpty(backup));
            Assert.IsTrue(File.Exists(backup));
            Assert.AreEqual("!shared=Original", File.ReadAllText(backup));
            Assert.AreEqual("!local=Original", File.ReadAllText(target));
            Assert.AreEqual(Shared, model.SnippetsPath);
            Assert.AreEqual(target, model.SnippetsFilePath);
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
            Assert.AreEqual(Shared, PersistedSnippetsPath(store));
        }

        [TestMethod]
        public void ReadableUncertainSettingsAreInspectedAndMatchingNewPathIsKept()
        {
            Write(_local, Current, "!local=Original");
            string target = Write(Shared, Current, "!shared=Original");
            MockSettingsStore store = SaveInitialSettings(new MockSettingsStore
            {
                ThrowAfterPublication = true,
                WrittenBeforeCompensation = EditorSettings.For(Shared).ToJsonString() + Environment.NewLine,
            });
            TextExpanderViewModel model = CreateViewModel(store);
            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);

            Assert.AreEqual(SettingsCommittedAfterError, model.ApplyFolderChange(change, Choice.ReplaceFolder, out string backup));

            Assert.AreEqual(AtomicSettingsPublicationOutcome.StateUncertain, store.LastError?.Outcome);
            Assert.IsFalse(string.IsNullOrEmpty(backup));
            Assert.AreEqual(Shared, model.SnippetsPath);
            Assert.AreEqual(target, model.SnippetsFilePath);
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
            Assert.AreEqual(Shared, PersistedSnippetsPath(store));
        }

        [TestMethod]
        public void ReadableUncertainPreviousPathRollsFolderBackWithoutClaimingEverythingWasUndone()
        {
            Write(_local, Current, "!local=Original");
            string target = Write(Shared, Current, "!shared=Original");
            byte[] targetBytes = File.ReadAllBytes(target);
            MockSettingsStore store = SaveInitialSettings(new MockSettingsStore
            {
                ThrowAfterPublication = true,
                WrittenBeforeCompensation = EditorSettings.For(_local).ToJsonString() + Environment.NewLine,
            });
            TextExpanderViewModel model = CreateViewModel(store);
            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);

            Assert.AreEqual(
                "TextExpander_SnippetFolder_SettingsStateUncertain",
                model.ApplyFolderChange(change, Choice.ReplaceFolder, out string backup));

            Assert.AreEqual(AtomicSettingsPublicationOutcome.StateUncertain, store.LastError?.Outcome);
            Assert.IsFalse(string.IsNullOrEmpty(backup));
            Assert.IsTrue(store.FileSystem.File.Exists(backup));
            Assert.IsTrue(store.LastError.RecoveryPaths.Contains(backup, StringComparer.OrdinalIgnoreCase));
            CollectionAssert.AreEqual(targetBytes, File.ReadAllBytes(target));
            Assert.AreEqual(_local, model.SnippetsPath);
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public void UnreadableUncertainSettingsPreserveFolderAndSurfaceAllRecoveryPaths()
        {
            string source = Write(_local, Current, "!local=Original");
            MockSettingsStore store = SaveInitialSettings(new MockSettingsStore { ThrowAfterPublication = true, WrittenBeforeCompensation = "{not-json" });
            TextExpanderViewModel model = CreateViewModel(
                store,
                key => key == "TextExpander_SnippetFolder_SettingsStateUncertain" ? "Settings path: {0}. Recovery paths: {1}." : key);
            TextExpanderFolderChange change = model.PrepareFolderChange(Shared);

            string error = model.ApplyFolderChange(change, Choice.ReplaceFolder, out string backup);

            Assert.AreEqual(AtomicSettingsPublicationOutcome.StateUncertain, store.LastError?.Outcome);
            Assert.IsNotNull(error);
            StringAssert.Contains(error, store.LastError.SettingsPath);
            foreach (string recoveryPath in store.LastError.RecoveryPaths)
            {
                StringAssert.Contains(error, recoveryPath);
                Assert.IsTrue(store.FileSystem.File.Exists(recoveryPath));
            }

            string target = Path.Combine(Shared, Current);
            Assert.AreEqual(target, backup, "The first published folder file is the only folder recovery copy and must be surfaced.");
            Assert.IsTrue(File.Exists(backup));
            Assert.AreEqual(File.ReadAllText(source), File.ReadAllText(target));
            Assert.AreEqual(_local, model.SnippetsPath);
            Assert.AreEqual("!local", model.Snippets.Single().Trigger);
        }

        [TestMethod]
        public void SettingsProcessChangesRefreshTheFolderAndMatchingRules()
        {
            Write(_local, Current, "!old=Old");
            string nextFolder = Path.Combine(_root, "next");
            Write(nextFolder, Current, "!next=Next");
            TextExpanderSettings latest = EditorSettings.For(nextFolder);
            latest.Properties.CaseSensitive = false;
            latest.Properties.RequireWordBoundary = false;
            using TextExpanderViewModel model = CreateViewModel(latest: () => latest);

            model.RefreshSettings();

            Assert.AreEqual(nextFolder, model.SnippetsPath);
            Assert.AreEqual("!next", model.Snippets.Single().Trigger);
            Assert.IsFalse(model.CaseSensitive);
            Assert.IsFalse(model.RequireWordBoundary);
            Assert.IsTrue(File.Exists(Path.Combine(_local, Current)));
        }

        [TestMethod]
        public void UnrelatedEngineOptionsDoNotInvalidateAnOpenEditor()
        {
            Write(_local, Current, "!old=Old");
            TextExpanderSettings latest = EditorSettings.For(_local);
            latest.Properties.InjectionBackend.Value = "type";
            using TextExpanderViewModel model = CreateViewModel(latest: () => latest);
            int revision = model.SnippetRevision;
            TextExpanderSnippetViewModel row = model.Snippets.Single();

            model.RefreshSettings();

            Assert.AreEqual(revision, model.SnippetRevision);
            Assert.AreSame(row, model.Snippets.Single());
        }

        [TestMethod]
        public void MatchingRuleChangesRefreshRowsWithoutInvalidatingAnOpenDraft()
        {
            Write(_local, Current, "!a=First\n!A=Last\n!abc=Longer");
            TextExpanderSettings latest = EditorSettings.For(_local);
            latest.Properties.CaseSensitive = false;
            latest.Properties.RequireWordBoundary = false;
            using TextExpanderViewModel model = CreateViewModel(latest: () => latest);
            int revision = model.SnippetRevision;
            TextExpanderSnippetViewModel row = model.Snippets.First();
            Assert.IsFalse(row.HasWarning);

            model.RefreshSettings();

            Assert.AreEqual(revision, model.SnippetRevision);
            Assert.AreSame(row, model.Snippets.First());
            Assert.IsTrue(row.HasWarning);
            Assert.IsTrue(model.Snippets.Last().HasWarning);
            Assert.IsFalse(model.CaseSensitive);
            Assert.IsFalse(model.RequireWordBoundary);
        }

        [TestMethod]
        public void FailedSettingsRefreshIsVisibleAndPreservesTheLoadedLibrary()
        {
            Write(_local, Current, "!old=Old");
            using TextExpanderViewModel model = CreateViewModel(latest: () => throw new InvalidOperationException("The settings file could not be read consistently."));
            int revision = model.SnippetRevision;

            model.RefreshSettings();

            Assert.IsTrue(model.HasSnippetError);
            Assert.AreEqual("!old", model.Snippets.Single().Trigger);
            Assert.AreEqual(revision, model.SnippetRevision);
        }

        private TextExpanderViewModel CreateViewModel(
            Action<Mock<TextExpanderSettingsUtils>> configureSettings = null,
            Action<string> openFile = null,
            Func<TextExpanderSettings> latest = null)
        {
            Mock<TextExpanderSettingsUtils> store = EditorSettings.Store(EditorSettings.For(_local), latest);
            configureSettings?.Invoke(store);
            return CreateViewModel(store.Object, openFile: openFile);
        }

        private TextExpanderViewModel CreateViewModel(
            TextExpanderSettingsUtils settingsUtils,
            Func<string, string> getResourceString = null,
            Action<string> openFile = null)
            => new(settingsUtils, null, isElevated: false, getResourceString ?? (key => key), _environment, openFile);

        private MockSettingsStore SaveInitialSettings(MockSettingsStore store)
        {
            store.InitialSettings ??= EditorSettings.For(_local);
            store.SaveSettings(store.InitialSettings.ToJsonString(), TextExpanderSettings.ModuleName);
            return store;
        }

        private static Moq.Language.Flow.ISetup<TextExpanderSettingsUtils> SetupSettingsSave(Mock<TextExpanderSettingsUtils> store)
            => store.Setup(s => s.SaveSettingsOrThrow(It.IsAny<string>(), TextExpanderSettings.ModuleName, SettingsUtils.DefaultFileName));

        private static string PersistedSnippetsPath(MockSettingsStore store)
            => store.GetSettingsOrDefaultReadOnly<TextExpanderSettings>(TextExpanderSettings.ModuleName).Properties.SnippetsPath.Value;

        private void AssertStale(TextExpanderViewModel model, TextExpanderFolderChange change, Choice choice = Choice.ReplaceFolder)
        {
            Assert.AreEqual(Stale, model.ApplyFolderChange(change, choice, out string backup));
            Assert.AreEqual(string.Empty, backup);
        }

        private static string Write(string folder, string name, string content)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, content);
            return path;
        }

        private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

        private static bool IsRollbackQuarantine(string path)
            => Path.GetFileName(path).Contains(".rollback-quarantine", StringComparison.OrdinalIgnoreCase);

        private static string[] SplitRecoveryPaths(string paths)
            => paths.Split("; ", StringSplitOptions.RemoveEmptyEntries);

        private static AtomicSettingsPublicationException VerifiedPreviousSettingsFailure()
            => new(AtomicSettingsPublicationOutcome.PreviousSettingsVerified, "settings.json", recoveryPaths: null, new IOException("Injected settings write failure."));

        private sealed class TestStorageEnvironment : ITextExpanderStorageEnvironment
        {
            private readonly string _root;
            private Action _filesChanged;
            private string _missingProbePath;
            private Action _afterMissingProbe;
            private Action _whenWatchStarts;
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

            public string ReplacedCleanupPath { get; private set; }

            public string ExpandEnvironmentVariables(string value) => value;

            public bool FileExists(string path)
            {
                string guarded = Guard(path);
                bool exists = File.Exists(guarded);
                if (!exists && string.Equals(guarded, _missingProbePath, StringComparison.OrdinalIgnoreCase))
                {
                    _missingProbePath = null;
                    InvokeAndClear(ref _afterMissingProbe);
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
                bool quarantineMove = IsRollbackQuarantine(destination);
                bool unexpectedRestore = IsRollbackQuarantine(source);
                bool rollback = quarantineMove || unexpectedRestore || IsRollbackSource(source);
                FailIfRollbackShouldFail(rollback);
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
                    InvokeAndClear(ref _beforeNextPublication);
                }

                File.Move(Guard(source), Guard(destination), overwrite: false);
                _publicationCompleted = !rollback;
            }

            public void ReplaceFile(string source, string destination, string backup)
            {
                bool rollback = IsRollbackSource(source);
                FailIfRollbackShouldFail(rollback);
                if (!rollback)
                {
                    InvokeAndClear(ref _beforeNextPublication);
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
                    beforeDisposition = () => action(guarded);
                }

                return HandleBoundFileDeletion.TryDelete(guarded, expectedBytes, beforeDisposition);
            }

            public IDisposable WatchForFiles(string folder, IReadOnlyList<string> fileNames, Action changed)
            {
                Guard(folder);
                InvokeAndClear(ref _whenWatchStarts);
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
                string guarded = Guard(path);
                _missingProbePath = guarded;
                _afterMissingProbe = () => File.WriteAllBytes(guarded, bytes);
            }

            public void PublishWhenWatchStarts(string path, byte[] bytes)
            {
                string guarded = Guard(path);
                _whenWatchStarts = () => File.WriteAllBytes(guarded, bytes);
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

            /// <summary>Just before cleanup deletes a file whose name matches, moves it aside and puts <paramref name="replacement"/> in its place.</summary>
            public void ReplaceBeforeCleanupDisposition(Func<string, bool> fileNameMatches, byte[] replacement)
            {
                _cleanupDispositionPredicate = path => fileNameMatches(Path.GetFileName(path));
                _beforeCleanupDisposition = path =>
                {
                    ReplacedCleanupPath = path;
                    File.Move(path, path + ".owned");
                    File.WriteAllBytes(path, replacement);
                };
            }

            private void FailIfRollbackShouldFail(bool rollback)
            {
                if (rollback && _failNextRollback)
                {
                    _failNextRollback = false;
                    throw new IOException("Injected rollback API failure.");
                }
            }

            private static void InvokeAndClear(ref Action action)
            {
                Action pending = action;
                action = null;
                pending?.Invoke();
            }

            private static bool IsRollbackSource(string path)
                => Path.GetFileName(path).Contains(".backup-", StringComparison.OrdinalIgnoreCase);

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
    }
}
