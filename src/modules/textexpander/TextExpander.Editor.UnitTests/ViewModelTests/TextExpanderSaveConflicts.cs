// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.PowerToys.TextExpander;
using Microsoft.PowerToys.TextExpander.Editor.Helpers;
using Microsoft.PowerToys.TextExpander.Editor.UnitTests;
using Microsoft.PowerToys.TextExpander.Editor.ViewModels;
using Microsoft.PowerToys.TextExpander.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public sealed class TextExpanderSaveConflicts
    {
        private const string Current = TextExpanderSnippetFileNames.Current;

        private static readonly Lazy<Dictionary<string, string>> English = new(() => RepositoryFiles.Xml(RepositoryFiles.EditorResources)
            .Descendants("data")
            .ToDictionary(data => (string)data.Attribute("name"), data => data.Element("value").Value));

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void AnExternalSaveAfterThePrecheckKeepsBothVersionsAndReportsTheConflict(bool handwritten, bool externalReplace)
        {
            using var folder = new TempFolder();
            var environment = new SaveEnvironment(folder.Path);
            string current = Path.Combine(folder.Path, Current);
            byte[] loaded = handwritten ? Encoding.UTF8.GetBytes("// My layout\n" + SnippetJson.Of("!loaded=Loaded")) : Canonical("!loaded=Loaded");
            File.WriteAllBytes(current, loaded);
            using TextExpanderViewModel model = CreateViewModel(environment);
            var queued = new List<Action>();
            model.Dispatch = queued.Add;

            byte[] external = Encoding.UTF8.GetBytes("// Changed elsewhere\n" + SnippetJson.Of("!external=External") + "\n");
            environment.BeforeReplace = (target, _) => WriteExternal(target, external, externalReplace);
            environment.AfterReplace = (_, _) => environment.NotifyFilesChanged();

            string error = model.SaveSnippet(null, "!local", "Local", out int savedId);

            Assert.AreEqual(-1, savedId, "A concurrent edit must not be reported as an ordinary successful save.");
            StringAssert.Contains(error, "changed somewhere else while it was being saved");
            byte[] edited = Canonical("!loaded=Loaded", "!local=Local");
            AssertRecoveryCopies(folder.Path, external, edited, error);
            CollectionAssert.AreEqual(edited, File.ReadAllBytes(current));
            Assert.AreEqual("!loaded,!local", string.Join(',', model.Snippets.Select(row => row.Trigger)));
            Assert.AreEqual(error, model.SnippetError, "Dialog revalidation can hide its own error, so the page must retain the recovery advice.");
            Assert.AreEqual("Review your saved snippets", model.SnippetErrorTitle);
            Assert.IsTrue(model.HasSnippetMessage);
            Assert.IsFalse(model.HasSnippetError, "Recovery advice must not make a readable database unavailable.");
            Assert.IsTrue(model.CanAddSnippets);
            Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, English.Value["TextExpander_SnippetTotal"], 2), model.SnippetCountText);

            int revision = model.SnippetRevision;
            queued.Single()();
            Assert.AreEqual(revision, model.SnippetRevision);
            Assert.AreEqual(error, model.SnippetError, "The queued event for this publication must not clear its warning.");

            model.LoadSnippets();
            Assert.IsFalse(model.HasSnippetMessage, "An explicit reload can dismiss the advice after the recovery files have been reviewed.");
            Assert.IsFalse(model.HasSnippetError);
            Assert.IsTrue(model.CanAddSnippets);
        }

        [TestMethod]
        public void AThirdWriterAfterPublicationIsNotOverwrittenByConflictRecovery()
        {
            using var folder = new TempFolder();
            var environment = new SaveEnvironment(folder.Path);
            string current = Path.Combine(folder.Path, Current);
            File.WriteAllBytes(current, Canonical("!loaded=Loaded"));
            using TextExpanderViewModel model = CreateViewModel(environment);
            byte[] external = Canonical("!external=External");
            byte[] later = Canonical("!later=Later");
            environment.BeforeReplace = (target, _) => WriteExternal(target, external, replace: true);
            environment.AfterReplace = (target, _) => WriteExternal(target, later, replace: true);

            string error = model.SaveSnippet(null, "!local", "Local");

            Assert.IsNotNull(error);
            CollectionAssert.AreEqual(later, File.ReadAllBytes(current), "Recovery must never restore the displaced version over a newer writer.");
            AssertRecoveryCopies(folder.Path, external, Canonical("!loaded=Loaded", "!local=Local"), error);
            Assert.AreEqual("!later", model.Snippets.Single().Trigger, "The list must reload what is currently at the destination.");
            Assert.AreEqual(error, model.SnippetError);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void AutomaticReloadsKeepConflictAdviceUntilAnExplicitReload(bool invalidExternalFile)
        {
            using var folder = new TempFolder();
            var environment = new SaveEnvironment(folder.Path);
            string current = Path.Combine(folder.Path, Current);
            File.WriteAllBytes(current, Canonical("!loaded=Loaded"));
            using TextExpanderViewModel model = CreateViewModel(environment);
            model.Dispatch = action => action();
            byte[] external = Canonical("!external=External");
            environment.BeforeReplace = (target, _) => WriteExternal(target, external, replace: true);

            string error = model.SaveSnippet(null, "!local", "Local");
            Assert.IsNotNull(error);

            byte[] later = invalidExternalFile ? Encoding.UTF8.GetBytes("not a snippets database") : Canonical("!later=Later");
            WriteExternal(current, later, replace: true);
            environment.NotifyFilesChanged();

            StringAssert.Contains(model.SnippetError, error, "A subsequent sync must not hide the recovery files before they can be reviewed.");
            Assert.AreEqual("Review your saved snippets", model.SnippetErrorTitle);
            Assert.IsTrue(model.HasSnippetMessage);
            Assert.AreEqual(invalidExternalFile, model.HasSnippetError, "Only an actual read error blocks editing.");
            Assert.AreEqual(!invalidExternalFile, model.CanAddSnippets);
            Assert.IsFalse(model.IsSnippetsFileUnavailable, "The current database exists even when its text cannot be read as snippets.");
            AssertRecoveryCopies(folder.Path, external, Canonical("!loaded=Loaded", "!local=Local"), error);
            if (invalidExternalFile)
            {
                StringAssert.Contains(model.SnippetError, string.Format(CultureInfo.CurrentCulture, English.Value["TextExpander_SnippetError_NotJson"], 1));
                Assert.AreEqual(English.Value["TextExpander_SnippetCountUnavailable"], model.SnippetCountText);
            }
            else
            {
                Assert.AreEqual("!later", model.Snippets.Single().Trigger);
                Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, English.Value["TextExpander_SnippetTotal_One"], 1), model.SnippetCountText);
            }

            model.LoadSnippets();
            Assert.IsFalse(model.SnippetError.Contains(error, StringComparison.Ordinal), "Explicit reload dismisses only the save warning.");
            Assert.AreEqual(invalidExternalFile, model.HasSnippetError, "A genuine read error must still be shown after the save warning is dismissed.");
            Assert.AreEqual(invalidExternalFile, model.HasSnippetMessage);
        }

        [TestMethod]
        public void RecoveryAdviceAllowsAnotherSaveAndItsSuccessDismissesTheAdvice()
        {
            using var folder = new TempFolder();
            var environment = new SaveEnvironment(folder.Path);
            string current = Path.Combine(folder.Path, Current);
            File.WriteAllBytes(current, Canonical("!loaded=Loaded"));
            using TextExpanderViewModel model = CreateViewModel(environment);
            byte[] external = Canonical("!external=External");
            environment.BeforeReplace = (target, _) => WriteExternal(target, external, replace: true);

            string error = model.SaveSnippet(null, "!local", "Local");
            Assert.IsNotNull(error);
            Assert.IsTrue(model.HasSnippetMessage);
            Assert.IsTrue(model.CanAddSnippets);
            environment.BeforeReplace = null;

            Assert.IsNull(model.SaveSnippet(null, "!next", "Next"));

            Assert.IsFalse(model.HasSnippetMessage);
            Assert.IsFalse(model.HasSnippetError);
            Assert.IsTrue(model.CanAddSnippets);
            CollectionAssert.AreEqual(Canonical("!loaded=Loaded", "!local=Local", "!next=Next"), File.ReadAllBytes(current));
            AssertRecoveryCopies(folder.Path, external, Canonical("!loaded=Loaded", "!local=Local"), error);
        }

        [TestMethod]
        public void AFileArrivingDuringDefaultFileRecreationIsKeptAsAConflict()
        {
            using var folder = new TempFolder();
            var environment = new SaveEnvironment(folder.Path);
            string current = Path.Combine(folder.Path, Current);
            File.WriteAllBytes(current, Canonical("!loaded=Loaded"));
            using TextExpanderViewModel model = CreateViewModel(environment);
            File.Delete(current);
            model.LoadSnippets();
            Assert.IsTrue(model.IsWaitingForSnippetsFile);
            byte[] arriving = Canonical("!arriving=Arriving");
            environment.AfterWrite = path =>
            {
                if (path.Contains(".tmp-", StringComparison.Ordinal))
                {
                    File.WriteAllBytes(current, arriving);
                }
            };

            string error = model.SaveSnippet(null, "!local", "Local");

            Assert.IsNotNull(error);
            AssertRecoveryCopies(folder.Path, arriving, Canonical("!loaded=Loaded", "!local=Local"), error);
            Assert.IsFalse(model.IsWaitingForSnippetsFile);
        }

        [TestMethod]
        public void AFileArrivingImmediatelyBeforeTheMoveIsNeverReplaced()
        {
            using var folder = new TempFolder();
            var environment = new SaveEnvironment(folder.Path);
            string current = Path.Combine(folder.Path, Current);
            File.WriteAllBytes(current, Canonical("!loaded=Loaded"));
            using TextExpanderViewModel model = CreateViewModel(environment);
            File.Delete(current);
            byte[] arriving = Canonical("!arriving=Arriving");
            environment.BeforeMove = (_, target) => File.WriteAllBytes(target, arriving);

            Assert.IsNotNull(model.SaveSnippet(null, "!local", "Local"));

            CollectionAssert.AreEqual(arriving, File.ReadAllBytes(current));
            Assert.AreEqual(0, environment.ReplaceCount, "A failed non-overwriting move must not retry by replacing the arriving file.");
            CollectionAssert.AreEquivalent(new[] { current }, Directory.GetFiles(folder.Path));
        }

        [TestMethod]
        public void AnUnreadableDisplacedVersionIsKeptAndReportedAsUnverified()
        {
            using var folder = new TempFolder();
            var environment = new SaveEnvironment(folder.Path);
            string current = Path.Combine(folder.Path, Current);
            byte[] loaded = Canonical("!loaded=Loaded");
            File.WriteAllBytes(current, loaded);
            using TextExpanderViewModel model = CreateViewModel(environment);
            FileStream heldBackup = null;
            environment.AfterReplace = (_, backup) => heldBackup = new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.None);
            string error;
            try
            {
                error = model.SaveSnippet(null, "!local", "Local");
            }
            finally
            {
                heldBackup?.Dispose();
            }

            StringAssert.Contains(error, "couldn't verify the version it replaced");
            AssertRecoveryCopies(folder.Path, loaded, Canonical("!loaded=Loaded", "!local=Local"), error);
            Assert.AreEqual(error, model.SnippetError);
        }

        [TestMethod]
        public void AFailedEditorRecoveryCopyStillKeepsAndReportsTheDisplacedFile()
        {
            using var folder = new TempFolder();
            var environment = new SaveEnvironment(folder.Path) { FailRecoveryWrite = true };
            string current = Path.Combine(folder.Path, Current);
            File.WriteAllBytes(current, Canonical("!loaded=Loaded"));
            using TextExpanderViewModel model = CreateViewModel(environment);
            byte[] external = Canonical("!external=External");
            environment.BeforeReplace = (target, _) => WriteExternal(target, external, replace: true);

            string error = model.SaveSnippet(null, "!local", "Local");

            StringAssert.Contains(error, "A recovery copy of your edited version couldn't be saved");
            string backup = Directory.GetFiles(folder.Path, Current + ".backup-*").Single();
            CollectionAssert.AreEqual(external, File.ReadAllBytes(backup));
            CollectionAssert.AreEqual(Canonical("!loaded=Loaded", "!local=Local"), File.ReadAllBytes(current));
            StringAssert.Contains(error, backup);
            StringAssert.Contains(error, environment.FailedRecoveryPath);
            Assert.AreEqual(0, Directory.GetFiles(folder.Path, Current + ".recovery-*").Length);
            Assert.AreEqual(error, model.SnippetError);
        }

        [TestMethod]
        public void CanonicalSavesDiscardOnlyTheirVerifiedBackupAndIgnoreTheirWatcherEvents()
        {
            using var folder = new TempFolder();
            var environment = new SaveEnvironment(folder.Path);
            string current = Path.Combine(folder.Path, Current);
            File.WriteAllBytes(current, Canonical("!loaded=Loaded"));
            using TextExpanderViewModel model = CreateViewModel(environment);
            model.Dispatch = action => action();

            Assert.IsNull(model.SaveSnippet(null, "!one", "One"));
            Assert.IsNull(model.SaveSnippet(null, "!two", "Two"));
            int revision = model.SnippetRevision;
            environment.NotifyFilesChanged();
            environment.NotifyFilesChanged();

            Assert.AreEqual(revision, model.SnippetRevision);
            Assert.IsFalse(model.HasSnippetError);
            Assert.IsFalse(model.HasSnippetMessage);
            Assert.IsNull(model.SaveSnippet(null, "!three", "Three"));
            CollectionAssert.AreEqual(Canonical("!loaded=Loaded", "!one=One", "!two=Two", "!three=Three"), File.ReadAllBytes(current));
            CollectionAssert.AreEquivalent(new[] { current }, Directory.GetFiles(folder.Path), "Normal canonical saves must not accumulate backups or recovery files.");
        }

        private static TextExpanderViewModel CreateViewModel(SaveEnvironment environment)
            => new(EditorSettings.Store(EditorSettings.For(string.Empty)).Object, isElevated: false, key => English.Value[key], environment);

        private static byte[] Canonical(params string[] snippets) => SnippetFile.Read(SnippetJson.Bytes(snippets)).ToBytes();

        private static void WriteExternal(string target, byte[] bytes, bool replace)
        {
            if (replace)
            {
                string temp = target + ".external-" + Guid.NewGuid().ToString("N");
                File.WriteAllBytes(temp, bytes);
                File.Replace(temp, target, destinationBackupFileName: null);
            }
            else
            {
                File.WriteAllBytes(target, bytes);
            }
        }

        private static void AssertRecoveryCopies(string folder, byte[] displaced, byte[] edited, string error)
        {
            string backup = Directory.GetFiles(folder, Current + ".backup-*").Single();
            string recovery = Directory.GetFiles(folder, Current + ".recovery-*").Single();
            CollectionAssert.AreEqual(displaced, File.ReadAllBytes(backup), "Keep the exact destination displaced by File.Replace, not an earlier read of it.");
            CollectionAssert.AreEqual(edited, File.ReadAllBytes(recovery), "The editor's payload must also survive if another writer replaces the active path.");
            StringAssert.Contains(error, backup);
            StringAssert.Contains(error, recovery);
            StringAssert.Contains(error, Path.Combine(folder, Current));
            Assert.AreEqual(0, Directory.GetFiles(folder, Current + ".tmp-*").Length);
        }

        private sealed class SaveEnvironment : ITextExpanderStorageEnvironment
        {
            private readonly TextExpanderStorageEnvironment _files = new();
            private Action _filesChanged;

            public SaveEnvironment(string folder)
            {
                DefaultSnippetsFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
            }

            public string DefaultSnippetsFolder { get; }

            public Action<string, string> BeforeReplace { get; set; }

            public Action<string, string> AfterReplace { get; set; }

            public Action<string, string> BeforeMove { get; set; }

            public Action<string> AfterWrite { get; set; }

            public bool FailRecoveryWrite { get; set; }

            public string FailedRecoveryPath { get; private set; }

            public int ReplaceCount { get; private set; }

            public string ExpandEnvironmentVariables(string value) => value;

            public bool FileExists(string path) => _files.FileExists(Guard(path));

            public void CreateDirectory(string path) => _files.CreateDirectory(Guard(path));

            public void WriteNewFile(string path, byte[] bytes)
            {
                Guard(path);
                if (FailRecoveryWrite && path.Contains(".recovery-", StringComparison.Ordinal))
                {
                    FailedRecoveryPath = path;
                    throw new IOException("The recovery copy could not be written.");
                }

                _files.WriteNewFile(path, bytes);
                AfterWrite?.Invoke(path);
            }

            public void MoveFile(string source, string destination)
            {
                Guard(source);
                Guard(destination);
                BeforeMove?.Invoke(source, destination);
                _files.MoveFile(source, destination);
            }

            public void ReplaceFile(string source, string destination, string backup)
            {
                Guard(source);
                Guard(destination);
                if (backup != null)
                {
                    Guard(backup);
                }

                ReplaceCount++;
                BeforeReplace?.Invoke(destination, backup);
                _files.ReplaceFile(source, destination, backup);
                AfterReplace?.Invoke(destination, backup);
            }

            public HandleBoundFileDeletionResult DeleteOwnedFile(string path, byte[] expectedBytes)
                => _files.DeleteOwnedFile(Guard(path), expectedBytes);

            public IDisposable WatchForFiles(string folder, IReadOnlyList<string> fileNames, Action changed)
            {
                Guard(folder);
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

            private string Guard(string path)
            {
                string full = Path.GetFullPath(path);
                Assert.IsTrue(
                    full.Equals(DefaultSnippetsFolder, StringComparison.OrdinalIgnoreCase)
                    || full.StartsWith(DefaultSnippetsFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
                    "The test touched a path outside its temporary folder: " + full);
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
