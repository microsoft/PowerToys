// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.TextExpander.Editor.UnitTests;
using Microsoft.PowerToys.TextExpander.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class TextExpanderSettingsUtilsTests
    {
        private const string Original = "{\"value\":\"original\"}";
        private const string Attempted = "{\"value\":\"attempted\"}";
        private const string External = "{\"value\":\"external\"}";
        private const string Newer = "{\"value\":\"newer\"}";

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void SaveSettingsOrThrowUndoesAPublicationThatThrowsAfterCommit(bool existing)
        {
            var store = new MockSettingsStore { ThrowAfterPublication = true };
            (string module, string settingsPath) = Prepare(store, existing);
            byte[] originalBytes = existing ? store.FileSystem.File.ReadAllBytes(settingsPath) : null;

            AtomicSettingsPublicationException error = Save(store, module);

            Assert.AreEqual(AtomicSettingsPublicationOutcome.PreviousSettingsVerified, error.Outcome);
            Assert.AreEqual("Injected failure after atomic settings publication.", error.PublicationException.Message);
            Assert.AreEqual(Attempted, store.StagedContents);
            if (existing)
            {
                CollectionAssert.AreEqual(originalBytes, store.FileSystem.File.ReadAllBytes(settingsPath));
            }
            else
            {
                Assert.IsFalse(store.FileSystem.File.Exists(settingsPath));
            }

            Assert.AreEqual(existing ? 1 : 0, FilesBeside(store.FileSystem, settingsPath));
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void SaveSettingsOrThrowSerializesCompensationBeforeANewerSave(bool existing)
        {
            var fileSystem = new MockFileSystem();
            using var published = new ManualResetEventSlim(false);
            using var release = new ManualResetEventSlim(false);
            var firstWriter = new MockSettingsStore(fileSystem)
            {
                ThrowAfterPublication = true,
                AfterPublication = () =>
                {
                    published.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(5)))
                    {
                        throw new IOException("The concurrent settings test gate timed out.");
                    }
                },
            };
            var secondWriter = new MockSettingsStore(fileSystem);
            (string module, string settingsPath) = Prepare(firstWriter, existing);

            Task<AtomicSettingsPublicationException> first = Task.Run(() => Save(firstWriter, module));
            Assert.IsTrue(published.Wait(TimeSpan.FromSeconds(5)));
            using var secondStarted = new ManualResetEventSlim(false);
            Task second = Task.Run(() =>
            {
                secondStarted.Set();
                secondWriter.SaveSettingsOrThrow(Newer, module);
            });

            try
            {
                Assert.IsTrue(secondStarted.Wait(TimeSpan.FromSeconds(5)));
                Assert.IsFalse(second.Wait(TimeSpan.FromMilliseconds(100)), "The newer writer must wait until compensation releases the path claim.");
            }
            finally
            {
                release.Set();
            }

            Assert.IsTrue(Task.WaitAll(new[] { first, second }, TimeSpan.FromSeconds(5)));
            Assert.AreEqual(AtomicSettingsPublicationOutcome.PreviousSettingsVerified, first.Result.Outcome);
            Assert.AreEqual(Newer, fileSystem.File.ReadAllText(settingsPath));
            Assert.AreEqual(1, FilesBeside(fileSystem, settingsPath));
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void SaveSettingsOrThrowPreservesANonParticipantWriteDuringCompensation(bool existing)
        {
            var store = new MockSettingsStore { ThrowAfterPublication = true, WrittenBeforeCompensation = External };
            (string module, string settingsPath) = Prepare(store, existing);

            AtomicSettingsPublicationException error = Save(store, module);

            Assert.AreEqual(AtomicSettingsPublicationOutcome.StateUncertain, error.Outcome);
            Assert.AreEqual(External, store.FileSystem.File.ReadAllText(settingsPath));
            AssertRecoveryCopies(store, error, existing);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void SaveSettingsOrThrowPreservesAWriteArrivingAtTheCompensationQuarantine(bool existing)
        {
            var store = new MockSettingsStore { ThrowAfterPublication = true, WrittenBeforeQuarantine = External };
            (string module, string settingsPath) = Prepare(store, existing);

            AtomicSettingsPublicationException error = Save(store, module);

            Assert.AreEqual(AtomicSettingsPublicationOutcome.StateUncertain, error.Outcome);
            Assert.AreEqual(External, store.FileSystem.File.ReadAllText(settingsPath));
            AssertRecoveryCopies(store, error, existing);
            Assert.IsFalse(error.RecoveryPaths.Any(path => path.EndsWith(".quarantine", StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void SaveSettingsOrThrowSurfacesTheQuarantineWhenRestoringItFails(bool existing)
        {
            var store = new MockSettingsStore { ThrowAfterPublication = true, WrittenBeforeQuarantine = External, WrittenBeforeRestore = Newer };
            (string module, string settingsPath) = Prepare(store, existing);

            AtomicSettingsPublicationException error = Save(store, module);

            Assert.AreEqual(AtomicSettingsPublicationOutcome.StateUncertain, error.Outcome);
            Assert.AreEqual(Newer, store.FileSystem.File.ReadAllText(settingsPath));
            AssertRecoveryCopies(store, error, existing);
            Assert.IsTrue(error.RecoveryPaths.Any(path =>
                path.EndsWith(".quarantine", StringComparison.OrdinalIgnoreCase)
                && store.FileSystem.File.ReadAllText(path) == External));
        }

        [TestMethod]
        public void SaveSettingsOrThrowReportsVerifiedNewCommitWhenCompensationFails()
        {
            var store = new MockSettingsStore { ThrowAfterPublication = true, FailCompensation = true };
            (string module, string settingsPath) = Prepare(store, existing: true);

            AtomicSettingsPublicationException error = Save(store, module);

            Assert.AreEqual(AtomicSettingsPublicationOutcome.NewSettingsVerified, error.Outcome);
            Assert.AreEqual(Attempted, store.FileSystem.File.ReadAllText(settingsPath));
            Assert.IsTrue(error.RecoveryPaths.Any(path => store.FileSystem.File.ReadAllText(path) == Original));
            Assert.AreEqual("Injected settings compensation failure.", error.CompensationException?.Message);
        }

        [TestMethod]
        public void SaveSettingsOrThrowReportsVerifiedPreviousStateWhenCompensationThrowsLate()
        {
            var store = new MockSettingsStore { ThrowAfterPublication = true, FailAfterCompensation = true };
            (string module, string settingsPath) = Prepare(store, existing: true);

            AtomicSettingsPublicationException error = Save(store, module);

            Assert.AreEqual(AtomicSettingsPublicationOutcome.PreviousSettingsVerified, error.Outcome);
            Assert.AreEqual(Original, store.FileSystem.File.ReadAllText(settingsPath));
            Assert.AreEqual("Injected failure after settings restoration.", error.CompensationException?.Message);
            Assert.AreEqual(1, FilesBeside(store.FileSystem, settingsPath));
        }

        [TestMethod]
        public void SuccessfulSettingsPublicationSurfacesBackupCleanupFailure()
        {
            var store = new MockSettingsStore { FailBackupCleanup = true };
            (string module, string settingsPath) = Prepare(store, existing: true);

            AtomicSettingsPublicationException error = Save(store, module);

            Assert.AreEqual(AtomicSettingsPublicationOutcome.NewSettingsVerified, error.Outcome);
            Assert.AreEqual(Attempted, store.FileSystem.File.ReadAllText(settingsPath));
            Assert.AreEqual(1, error.RecoveryPaths.Count);
            StringAssert.EndsWith(error.RecoveryPaths[0], ".bak");
            Assert.AreEqual(Original, store.FileSystem.File.ReadAllText(error.RecoveryPaths[0]));
            StringAssert.Contains(error.Message, error.RecoveryPaths[0]);
        }

        [TestMethod]
        public void HandleBoundCleanupDeletesOrdinaryOwnedFile()
        {
            using var folder = new TempFolder();
            string path = Path.Combine(folder.Path, ".settings.json.owned.quarantine");
            byte[] ownedBytes = Encoding.UTF8.GetBytes("{\"value\":\"owned\"}");
            File.WriteAllBytes(path, ownedBytes);

            HandleBoundFileDeletionResult result = new RealCleanupStore().DeleteOwnedArtifact(path, ownedBytes);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(HandleBoundFileDeletionStatus.Deleted, result.Status);
            Assert.IsFalse(File.Exists(path));
        }

        [TestMethod]
        public void SettingsQuarantineReplacementAtHandleBoundCleanupSurvivesAndIsSurfaced()
        {
            using var folder = new TempFolder();
            string path = Path.Combine(folder.Path, ".settings.json.owned.quarantine");
            string ownedObjectPath = path + ".owned";
            byte[] ownedBytes = Encoding.UTF8.GetBytes("{\"value\":\"owned\"}");
            byte[] replacementBytes = Encoding.UTF8.GetBytes("{\"value\":\"replacement\"}");
            File.WriteAllBytes(path, ownedBytes);
            var store = new RealCleanupStore(cleanupPath =>
            {
                File.Move(cleanupPath, ownedObjectPath);
                File.WriteAllBytes(cleanupPath, replacementBytes);
            });

            HandleBoundFileDeletionResult result = store.DeleteOwnedArtifact(path, ownedBytes);

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.OwnedObjectDeleted);
            Assert.AreEqual(HandleBoundFileDeletionStatus.PathStillPresent, result.Status);
            Assert.AreEqual(path, result.Path);
            StringAssert.Contains(result.Error?.Message, path);
            CollectionAssert.AreEqual(replacementBytes, File.ReadAllBytes(path));
            Assert.IsFalse(File.Exists(ownedObjectPath));
        }

        [TestMethod]
        public void HandleBoundCleanupReportsHandleAcquisitionFailure()
        {
            using var folder = new TempFolder();
            string path = Path.Combine(folder.Path, "blocked.tmp");
            byte[] ownedBytes = Encoding.UTF8.GetBytes("owned");
            File.WriteAllBytes(path, ownedBytes);
            using var blocker = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

            HandleBoundFileDeletionResult result = HandleBoundFileDeletion.TryDelete(path, ownedBytes);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(HandleBoundFileDeletionStatus.HandleAcquisitionFailed, result.Status);
            Assert.AreEqual(path, result.Path);
            Assert.IsTrue(File.Exists(path));
        }

        [TestMethod]
        public void HandleBoundCleanupPreservesIdentityMismatch()
        {
            using var folder = new TempFolder();
            string path = Path.Combine(folder.Path, "different.tmp");
            byte[] actualBytes = Encoding.UTF8.GetBytes("different");
            File.WriteAllBytes(path, actualBytes);

            HandleBoundFileDeletionResult result = HandleBoundFileDeletion.TryDelete(path, Encoding.UTF8.GetBytes("expected"));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(HandleBoundFileDeletionStatus.IdentityMismatch, result.Status);
            CollectionAssert.AreEqual(actualBytes, File.ReadAllBytes(path));
        }

        [TestMethod]
        public void HandleBoundCleanupSupportsExtendedLengthPaths()
        {
            using var folder = new TempFolder();
            string directory = folder.Path;
            while (directory.Length < 280)
            {
                directory = Path.Combine(directory, new string('a', 40));
            }

            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "owned.tmp");
            byte[] ownedBytes = Encoding.UTF8.GetBytes("owned");
            File.WriteAllBytes(path, ownedBytes);
            Assert.IsTrue(path.Length > 260);

            HandleBoundFileDeletionResult result = HandleBoundFileDeletion.TryDelete(path, ownedBytes);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(HandleBoundFileDeletionStatus.Deleted, result.Status);
            Assert.IsFalse(File.Exists(path));
        }

        [TestMethod]
        public void HandleBoundCleanupReportsDispositionFailure()
        {
            using var folder = new TempFolder();
            string path = Path.Combine(folder.Path, "readonly.bak");
            byte[] ownedBytes = Encoding.UTF8.GetBytes("owned");
            File.WriteAllBytes(path, ownedBytes);
            File.SetAttributes(path, FileAttributes.ReadOnly);

            HandleBoundFileDeletionResult result = HandleBoundFileDeletion.TryDelete(path, ownedBytes);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(HandleBoundFileDeletionStatus.DispositionFailed, result.Status);
            Assert.AreEqual(path, result.Path);
            Assert.IsTrue(File.Exists(path));
        }

        private static (string Module, string SettingsPath) Prepare(MockSettingsStore store, bool existing)
        {
            string module = "TextExpanderSettingsTest-" + Guid.NewGuid().ToString("N");
            if (existing)
            {
                store.SaveSettings(Original, module);
            }

            return (module, store.GetSettingsFilePath(module));
        }

        private static AtomicSettingsPublicationException Save(MockSettingsStore store, string module)
            => Assert.ThrowsException<AtomicSettingsPublicationException>(() => store.SaveSettingsOrThrow(Attempted, module));

        private static int FilesBeside(MockFileSystem fileSystem, string settingsPath)
            => fileSystem.Directory.GetFiles(Path.GetDirectoryName(settingsPath)).Length;

        /// <summary>The attempted settings, and the original ones when there were any, are kept for recovery.</summary>
        private static void AssertRecoveryCopies(MockSettingsStore store, AtomicSettingsPublicationException error, bool existing)
        {
            if (existing)
            {
                Assert.IsTrue(error.RecoveryPaths.Any(path => store.FileSystem.File.ReadAllText(path) == Original), "The original settings are kept.");
            }

            Assert.IsTrue(error.RecoveryPaths.Any(path => store.FileSystem.File.ReadAllText(path) == Attempted), "The attempted settings are kept.");
        }

        private sealed class RealCleanupStore : TextExpanderSettingsUtils
        {
            private readonly Action<string> _beforeDisposition;

            public RealCleanupStore(Action<string> beforeDisposition = null)
                : base(new FileSystem())
            {
                _beforeDisposition = beforeDisposition;
            }

            public HandleBoundFileDeletionResult DeleteOwnedArtifact(string path, byte[] ownedBytes)
                => TryDeleteOwnedAtomicSaveArtifact(path, ownedBytes);

            protected override void BeforeOwnedAtomicSaveArtifactDisposition(string path)
                => _beforeDisposition?.Invoke(path);
        }
    }
}
