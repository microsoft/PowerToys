// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.TextExpander.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class TextExpanderSettingsUtilsTests
    {
        [TestMethod]
        public void SaveSettingsOrThrowRestoresExistingFileWhenPublicationThrowsAfterCommit()
        {
            var mockFileSystem = new MockFileSystem();
            var settingsUtils = new ThrowAfterPublicationSettingsUtils(mockFileSystem);
            const string module = "AtomicExisting";
            const string original = "{\"value\":\"original\"}";
            const string replacement = "{\"value\":\"replacement\"}";
            settingsUtils.SaveSettings(original, module);
            string settingsPath = settingsUtils.GetSettingsFilePath(module);
            byte[] originalBytes = mockFileSystem.File.ReadAllBytes(settingsPath);

            AtomicSettingsPublicationException error = Assert.ThrowsException<AtomicSettingsPublicationException>(
                () => settingsUtils.SaveSettingsOrThrow(replacement, module));

            Assert.AreEqual(AtomicSettingsPublicationOutcome.PreviousSettingsVerified, error.Outcome);
            Assert.AreEqual("Injected failure after atomic settings publication.", error.PublicationException.Message);
            Assert.AreEqual(replacement, settingsUtils.StagedContents);
            CollectionAssert.AreEqual(originalBytes, mockFileSystem.File.ReadAllBytes(settingsPath));
            Assert.AreEqual(1, mockFileSystem.Directory.GetFiles(Path.GetDirectoryName(settingsPath)!).Length);
        }

        [TestMethod]
        public void SaveSettingsOrThrowRemovesFirstFileWhenPublicationThrowsAfterCommit()
        {
            var mockFileSystem = new MockFileSystem();
            var settingsUtils = new ThrowAfterPublicationSettingsUtils(mockFileSystem);
            const string module = "AtomicFirst";
            const string replacement = "{\"value\":\"replacement\"}";
            string settingsPath = settingsUtils.GetSettingsFilePath(module);

            AtomicSettingsPublicationException error = Assert.ThrowsException<AtomicSettingsPublicationException>(
                () => settingsUtils.SaveSettingsOrThrow(replacement, module));

            Assert.AreEqual(AtomicSettingsPublicationOutcome.PreviousSettingsVerified, error.Outcome);
            Assert.AreEqual("Injected failure after atomic settings publication.", error.PublicationException.Message);
            Assert.AreEqual(replacement, settingsUtils.StagedContents);
            Assert.IsFalse(mockFileSystem.File.Exists(settingsPath));
            Assert.AreEqual(0, mockFileSystem.Directory.GetFiles(Path.GetDirectoryName(settingsPath)!).Length);
        }

        [TestMethod]
        public void SaveSettingsOrThrowSerializesCompensationBeforeNewerExistingFileSave()
        {
            var fileSystem = new MockFileSystem();
            string module = "AtomicExistingConcurrent-" + Guid.NewGuid().ToString("N");
            const string original = "{\"value\":\"original\"}";
            const string firstPayload = "{\"value\":\"first\"}";
            const string secondPayload = "{\"value\":\"second\"}";
            var firstWriter = new BlockingThrowAfterPublicationSettingsUtils(fileSystem);
            var secondWriter = new MockFileSystemSettingsUtils(fileSystem);
            firstWriter.SaveSettings(original, module);
            string settingsPath = firstWriter.GetSettingsFilePath(module);
            Exception firstError = null;

            Task first = Task.Run(() =>
            {
                try
                {
                    firstWriter.SaveSettingsOrThrow(firstPayload, module);
                }
                catch (Exception ex)
                {
                    firstError = ex;
                }
            });

            Assert.IsTrue(firstWriter.Published.Wait(TimeSpan.FromSeconds(5)));
            using var secondStarted = new ManualResetEventSlim(false);
            Task second = Task.Run(() =>
            {
                secondStarted.Set();
                secondWriter.SaveSettingsOrThrow(secondPayload, module);
            });

            try
            {
                Assert.IsTrue(secondStarted.Wait(TimeSpan.FromSeconds(5)));
                Assert.IsFalse(second.Wait(TimeSpan.FromMilliseconds(100)), "The newer writer must wait until compensation releases the path claim.");
            }
            finally
            {
                firstWriter.Release.Set();
            }

            Assert.IsTrue(Task.WaitAll(new[] { first, second }, TimeSpan.FromSeconds(5)));
            Assert.IsInstanceOfType<AtomicSettingsPublicationException>(firstError);
            Assert.AreEqual(
                AtomicSettingsPublicationOutcome.PreviousSettingsVerified,
                ((AtomicSettingsPublicationException)firstError).Outcome);
            Assert.AreEqual(secondPayload, fileSystem.File.ReadAllText(settingsPath));
            Assert.AreEqual(1, fileSystem.Directory.GetFiles(Path.GetDirectoryName(settingsPath)!).Length);
        }

        [TestMethod]
        public void SaveSettingsOrThrowSerializesCompensationBeforeNewerFirstFileSave()
        {
            var fileSystem = new MockFileSystem();
            string module = "AtomicFirstConcurrent-" + Guid.NewGuid().ToString("N");
            const string firstPayload = "{\"value\":\"first\"}";
            const string secondPayload = "{\"value\":\"second\"}";
            var firstWriter = new BlockingThrowAfterPublicationSettingsUtils(fileSystem);
            var secondWriter = new MockFileSystemSettingsUtils(fileSystem);
            string settingsPath = firstWriter.GetSettingsFilePath(module);
            Exception firstError = null;

            Task first = Task.Run(() =>
            {
                try
                {
                    firstWriter.SaveSettingsOrThrow(firstPayload, module);
                }
                catch (Exception ex)
                {
                    firstError = ex;
                }
            });

            Assert.IsTrue(firstWriter.Published.Wait(TimeSpan.FromSeconds(5)));
            using var secondStarted = new ManualResetEventSlim(false);
            Task second = Task.Run(() =>
            {
                secondStarted.Set();
                secondWriter.SaveSettingsOrThrow(secondPayload, module);
            });

            try
            {
                Assert.IsTrue(secondStarted.Wait(TimeSpan.FromSeconds(5)));
                Assert.IsFalse(second.Wait(TimeSpan.FromMilliseconds(100)), "The newer writer must wait until first-file compensation releases the path claim.");
            }
            finally
            {
                firstWriter.Release.Set();
            }

            Assert.IsTrue(Task.WaitAll(new[] { first, second }, TimeSpan.FromSeconds(5)));
            Assert.IsInstanceOfType<AtomicSettingsPublicationException>(firstError);
            Assert.AreEqual(
                AtomicSettingsPublicationOutcome.PreviousSettingsVerified,
                ((AtomicSettingsPublicationException)firstError).Outcome);
            Assert.AreEqual(secondPayload, fileSystem.File.ReadAllText(settingsPath));
            Assert.AreEqual(1, fileSystem.Directory.GetFiles(Path.GetDirectoryName(settingsPath)!).Length);
        }

        [TestMethod]
        public void SaveSettingsOrThrowPreservesNonParticipantExistingFileMutationDuringCompensation()
        {
            var fileSystem = new MockFileSystem();
            string module = "AtomicExistingExternal-" + Guid.NewGuid().ToString("N");
            const string original = "{\"value\":\"original\"}";
            const string attempted = "{\"value\":\"attempted\"}";
            const string external = "{\"value\":\"external\"}";
            var settingsUtils = new MutateBeforeCompensationSettingsUtils(fileSystem, external);
            settingsUtils.SaveSettings(original, module);
            string settingsPath = settingsUtils.GetSettingsFilePath(module);

            AtomicSettingsPublicationException error = Assert.ThrowsException<AtomicSettingsPublicationException>(
                () => settingsUtils.SaveSettingsOrThrow(attempted, module));

            Assert.AreEqual(AtomicSettingsPublicationOutcome.StateUncertain, error.Outcome);
            Assert.AreEqual(external, fileSystem.File.ReadAllText(settingsPath));
            Assert.IsTrue(error.RecoveryPaths.Any(path => fileSystem.File.ReadAllText(path) == original));
            Assert.IsTrue(error.RecoveryPaths.Any(path => fileSystem.File.ReadAllText(path) == attempted));
        }

        [TestMethod]
        public void SaveSettingsOrThrowPreservesNonParticipantFirstFileMutationDuringCompensation()
        {
            var fileSystem = new MockFileSystem();
            string module = "AtomicFirstExternal-" + Guid.NewGuid().ToString("N");
            const string attempted = "{\"value\":\"attempted\"}";
            const string external = "{\"value\":\"external\"}";
            var settingsUtils = new MutateBeforeCompensationSettingsUtils(fileSystem, external);
            string settingsPath = settingsUtils.GetSettingsFilePath(module);

            AtomicSettingsPublicationException error = Assert.ThrowsException<AtomicSettingsPublicationException>(
                () => settingsUtils.SaveSettingsOrThrow(attempted, module));

            Assert.AreEqual(AtomicSettingsPublicationOutcome.StateUncertain, error.Outcome);
            Assert.AreEqual(external, fileSystem.File.ReadAllText(settingsPath));
            Assert.IsTrue(error.RecoveryPaths.Any(path => fileSystem.File.ReadAllText(path) == attempted));
        }

        [TestMethod]
        public void SaveSettingsOrThrowPreservesExistingFileArrivingAtCompensationQuarantineBoundary()
        {
            var fileSystem = new MockFileSystem();
            string module = "AtomicExistingQuarantine-" + Guid.NewGuid().ToString("N");
            const string original = "{\"value\":\"original\"}";
            const string attempted = "{\"value\":\"attempted\"}";
            const string external = "{\"value\":\"external\"}";
            var settingsUtils = new MutateAtCompensationQuarantineSettingsUtils(fileSystem, external);
            settingsUtils.SaveSettings(original, module);
            string settingsPath = settingsUtils.GetSettingsFilePath(module);

            AtomicSettingsPublicationException error = Assert.ThrowsException<AtomicSettingsPublicationException>(
                () => settingsUtils.SaveSettingsOrThrow(attempted, module));

            Assert.AreEqual(AtomicSettingsPublicationOutcome.StateUncertain, error.Outcome);
            Assert.AreEqual(external, fileSystem.File.ReadAllText(settingsPath));
            Assert.IsTrue(error.RecoveryPaths.Any(path => fileSystem.File.ReadAllText(path) == original));
            Assert.IsTrue(error.RecoveryPaths.Any(path => fileSystem.File.ReadAllText(path) == attempted));
            Assert.IsFalse(error.RecoveryPaths.Any(path => path.EndsWith(".quarantine", StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        public void SaveSettingsOrThrowPreservesFirstFileArrivingAtCompensationQuarantineBoundary()
        {
            var fileSystem = new MockFileSystem();
            string module = "AtomicFirstQuarantine-" + Guid.NewGuid().ToString("N");
            const string attempted = "{\"value\":\"attempted\"}";
            const string external = "{\"value\":\"external\"}";
            var settingsUtils = new MutateAtCompensationQuarantineSettingsUtils(fileSystem, external);
            string settingsPath = settingsUtils.GetSettingsFilePath(module);

            AtomicSettingsPublicationException error = Assert.ThrowsException<AtomicSettingsPublicationException>(
                () => settingsUtils.SaveSettingsOrThrow(attempted, module));

            Assert.AreEqual(AtomicSettingsPublicationOutcome.StateUncertain, error.Outcome);
            Assert.AreEqual(external, fileSystem.File.ReadAllText(settingsPath));
            Assert.IsTrue(error.RecoveryPaths.Any(path => fileSystem.File.ReadAllText(path) == attempted));
            Assert.IsFalse(error.RecoveryPaths.Any(path => path.EndsWith(".quarantine", StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        public void SaveSettingsOrThrowSurfacesExistingFileQuarantineWhenUnexpectedRestorationFails()
        {
            var fileSystem = new MockFileSystem();
            string module = "AtomicExistingQuarantineFailure-" + Guid.NewGuid().ToString("N");
            const string original = "{\"value\":\"original\"}";
            const string attempted = "{\"value\":\"attempted\"}";
            const string external = "{\"value\":\"external\"}";
            const string blocker = "{\"value\":\"newer\"}";
            var settingsUtils = new MutateAtCompensationQuarantineSettingsUtils(fileSystem, external, blocker);
            settingsUtils.SaveSettings(original, module);
            string settingsPath = settingsUtils.GetSettingsFilePath(module);

            AtomicSettingsPublicationException error = Assert.ThrowsException<AtomicSettingsPublicationException>(
                () => settingsUtils.SaveSettingsOrThrow(attempted, module));

            Assert.AreEqual(AtomicSettingsPublicationOutcome.StateUncertain, error.Outcome);
            Assert.AreEqual(blocker, fileSystem.File.ReadAllText(settingsPath));
            Assert.IsTrue(error.RecoveryPaths.Any(path => fileSystem.File.ReadAllText(path) == original));
            Assert.IsTrue(error.RecoveryPaths.Any(path => fileSystem.File.ReadAllText(path) == attempted));
            Assert.IsTrue(error.RecoveryPaths.Any(path =>
                path.EndsWith(".quarantine", StringComparison.OrdinalIgnoreCase)
                && fileSystem.File.ReadAllText(path) == external));
        }

        [TestMethod]
        public void SaveSettingsOrThrowSurfacesFirstFileQuarantineWhenUnexpectedRestorationFails()
        {
            var fileSystem = new MockFileSystem();
            string module = "AtomicFirstQuarantineFailure-" + Guid.NewGuid().ToString("N");
            const string attempted = "{\"value\":\"attempted\"}";
            const string external = "{\"value\":\"external\"}";
            const string blocker = "{\"value\":\"newer\"}";
            var settingsUtils = new MutateAtCompensationQuarantineSettingsUtils(fileSystem, external, blocker);
            string settingsPath = settingsUtils.GetSettingsFilePath(module);

            AtomicSettingsPublicationException error = Assert.ThrowsException<AtomicSettingsPublicationException>(
                () => settingsUtils.SaveSettingsOrThrow(attempted, module));

            Assert.AreEqual(AtomicSettingsPublicationOutcome.StateUncertain, error.Outcome);
            Assert.AreEqual(blocker, fileSystem.File.ReadAllText(settingsPath));
            Assert.IsTrue(error.RecoveryPaths.Any(path => fileSystem.File.ReadAllText(path) == attempted));
            Assert.IsTrue(error.RecoveryPaths.Any(path =>
                path.EndsWith(".quarantine", StringComparison.OrdinalIgnoreCase)
                && fileSystem.File.ReadAllText(path) == external));
        }

        [TestMethod]
        public void SaveSettingsOrThrowReportsVerifiedNewCommitWhenCompensationFails()
        {
            var fileSystem = new MockFileSystem();
            string module = "AtomicVerifiedNew-" + Guid.NewGuid().ToString("N");
            const string original = "{\"value\":\"original\"}";
            const string replacement = "{\"value\":\"replacement\"}";
            var settingsUtils = new FailCompensationSettingsUtils(fileSystem);
            settingsUtils.SaveSettings(original, module);
            string settingsPath = settingsUtils.GetSettingsFilePath(module);

            AtomicSettingsPublicationException error = Assert.ThrowsException<AtomicSettingsPublicationException>(
                () => settingsUtils.SaveSettingsOrThrow(replacement, module));

            Assert.AreEqual(AtomicSettingsPublicationOutcome.NewSettingsVerified, error.Outcome);
            Assert.AreEqual(replacement, fileSystem.File.ReadAllText(settingsPath));
            Assert.IsTrue(error.RecoveryPaths.Any(path => fileSystem.File.ReadAllText(path) == original));
            Assert.AreEqual("Injected settings compensation failure.", error.CompensationException?.Message);
        }

        [TestMethod]
        public void SaveSettingsOrThrowReportsVerifiedPreviousStateWhenCompensationThrowsLate()
        {
            var fileSystem = new MockFileSystem();
            string module = "AtomicVerifiedPrevious-" + Guid.NewGuid().ToString("N");
            const string original = "{\"value\":\"original\"}";
            const string replacement = "{\"value\":\"replacement\"}";
            var settingsUtils = new ThrowAfterCompensationSettingsUtils(fileSystem);
            settingsUtils.SaveSettings(original, module);
            string settingsPath = settingsUtils.GetSettingsFilePath(module);

            AtomicSettingsPublicationException error = Assert.ThrowsException<AtomicSettingsPublicationException>(
                () => settingsUtils.SaveSettingsOrThrow(replacement, module));

            Assert.AreEqual(AtomicSettingsPublicationOutcome.PreviousSettingsVerified, error.Outcome);
            Assert.AreEqual(original, fileSystem.File.ReadAllText(settingsPath));
            Assert.AreEqual("Injected failure after settings restoration.", error.CompensationException?.Message);
            Assert.AreEqual(1, fileSystem.Directory.GetFiles(Path.GetDirectoryName(settingsPath)!).Length);
        }

        [TestMethod]
        public void HandleBoundCleanupDeletesOrdinaryOwnedFile()
        {
            string root = CreateCleanupTestDirectory();
            try
            {
                string path = Path.Combine(root, ".settings.json.owned.quarantine");
                byte[] ownedBytes = System.Text.Encoding.UTF8.GetBytes("{\"value\":\"owned\"}");
                File.WriteAllBytes(path, ownedBytes);
                var settingsUtils = new ExposedHandleBoundCleanupSettingsUtils();

                HandleBoundFileDeletionResult result = settingsUtils.DeleteOwnedArtifact(path, ownedBytes);

                Assert.IsTrue(result.Succeeded);
                Assert.AreEqual(HandleBoundFileDeletionStatus.Deleted, result.Status);
                Assert.IsFalse(File.Exists(path));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [TestMethod]
        public void SettingsQuarantineReplacementAtHandleBoundCleanupSurvivesAndIsSurfaced()
        {
            string root = CreateCleanupTestDirectory();
            try
            {
                string path = Path.Combine(root, ".settings.json.owned.quarantine");
                string ownedObjectPath = path + ".owned";
                byte[] ownedBytes = System.Text.Encoding.UTF8.GetBytes("{\"value\":\"owned\"}");
                byte[] replacementBytes = System.Text.Encoding.UTF8.GetBytes("{\"value\":\"replacement\"}");
                File.WriteAllBytes(path, ownedBytes);
                var settingsUtils = new ExposedHandleBoundCleanupSettingsUtils(cleanupPath =>
                {
                    File.Move(cleanupPath, ownedObjectPath);
                    File.WriteAllBytes(cleanupPath, replacementBytes);
                });

                HandleBoundFileDeletionResult result = settingsUtils.DeleteOwnedArtifact(path, ownedBytes);

                Assert.IsFalse(result.Succeeded);
                Assert.IsTrue(result.OwnedObjectDeleted);
                Assert.AreEqual(HandleBoundFileDeletionStatus.PathStillPresent, result.Status);
                Assert.AreEqual(path, result.Path);
                StringAssert.Contains(result.Error?.Message, path);
                CollectionAssert.AreEqual(replacementBytes, File.ReadAllBytes(path));
                Assert.IsFalse(File.Exists(ownedObjectPath), "Disposition must delete the verified quarantine object after its rename.");
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [TestMethod]
        public void HandleBoundCleanupReportsHandleAcquisitionFailure()
        {
            string root = CreateCleanupTestDirectory();
            try
            {
                string path = Path.Combine(root, "blocked.tmp");
                byte[] ownedBytes = System.Text.Encoding.UTF8.GetBytes("owned");
                File.WriteAllBytes(path, ownedBytes);
                using var blocker = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

                HandleBoundFileDeletionResult result = HandleBoundFileDeletion.TryDelete(path, ownedBytes);

                Assert.IsFalse(result.Succeeded);
                Assert.AreEqual(HandleBoundFileDeletionStatus.HandleAcquisitionFailed, result.Status);
                Assert.AreEqual(path, result.Path);
                Assert.IsTrue(File.Exists(path));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [TestMethod]
        public void HandleBoundCleanupPreservesIdentityMismatch()
        {
            string root = CreateCleanupTestDirectory();
            try
            {
                string path = Path.Combine(root, "different.tmp");
                byte[] actualBytes = System.Text.Encoding.UTF8.GetBytes("different");
                File.WriteAllBytes(path, actualBytes);

                HandleBoundFileDeletionResult result = HandleBoundFileDeletion.TryDelete(
                    path,
                    System.Text.Encoding.UTF8.GetBytes("expected"));

                Assert.IsFalse(result.Succeeded);
                Assert.AreEqual(HandleBoundFileDeletionStatus.IdentityMismatch, result.Status);
                CollectionAssert.AreEqual(actualBytes, File.ReadAllBytes(path));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [TestMethod]
        public void HandleBoundCleanupSupportsExtendedLengthPaths()
        {
            string root = CreateCleanupTestDirectory();
            try
            {
                string folder = root;
                while (folder.Length < 280)
                {
                    folder = Path.Combine(folder, new string('a', 40));
                }

                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "owned.tmp");
                byte[] ownedBytes = System.Text.Encoding.UTF8.GetBytes("owned");
                File.WriteAllBytes(path, ownedBytes);
                Assert.IsTrue(path.Length > 260);

                HandleBoundFileDeletionResult result = HandleBoundFileDeletion.TryDelete(path, ownedBytes);

                Assert.IsTrue(result.Succeeded);
                Assert.AreEqual(HandleBoundFileDeletionStatus.Deleted, result.Status);
                Assert.IsFalse(File.Exists(path));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [TestMethod]
        public void HandleBoundCleanupReportsDispositionFailure()
        {
            string root = CreateCleanupTestDirectory();
            string path = Path.Combine(root, "readonly.bak");
            try
            {
                byte[] ownedBytes = System.Text.Encoding.UTF8.GetBytes("owned");
                File.WriteAllBytes(path, ownedBytes);
                File.SetAttributes(path, FileAttributes.ReadOnly);

                HandleBoundFileDeletionResult result = HandleBoundFileDeletion.TryDelete(path, ownedBytes);

                Assert.IsFalse(result.Succeeded);
                Assert.AreEqual(HandleBoundFileDeletionStatus.DispositionFailed, result.Status);
                Assert.AreEqual(path, result.Path);
                Assert.IsTrue(File.Exists(path));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                }

                Directory.Delete(root, recursive: true);
            }
        }

        [TestMethod]
        public void SuccessfulSettingsPublicationSurfacesBackupCleanupFailure()
        {
            var fileSystem = new MockFileSystem();
            string module = "AtomicCleanupFailure-" + Guid.NewGuid().ToString("N");
            const string original = "{\"value\":\"original\"}";
            const string replacement = "{\"value\":\"replacement\"}";
            var settingsUtils = new FailBackupCleanupSettingsUtils(fileSystem);
            settingsUtils.SaveSettings(original, module);
            string settingsPath = settingsUtils.GetSettingsFilePath(module);

            AtomicSettingsPublicationException error = Assert.ThrowsException<AtomicSettingsPublicationException>(
                () => settingsUtils.SaveSettingsOrThrow(replacement, module));

            Assert.AreEqual(AtomicSettingsPublicationOutcome.NewSettingsVerified, error.Outcome);
            Assert.AreEqual(replacement, fileSystem.File.ReadAllText(settingsPath));
            Assert.AreEqual(1, error.RecoveryPaths.Count);
            StringAssert.EndsWith(error.RecoveryPaths[0], ".bak");
            Assert.AreEqual(original, fileSystem.File.ReadAllText(error.RecoveryPaths[0]));
            StringAssert.Contains(error.Message, error.RecoveryPaths[0]);
        }

        private static string CreateCleanupTestDirectory()
        {
            string root = Path.Combine(
                AppContext.BaseDirectory,
                ".settings-cleanup-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private class MockFileSystemSettingsUtils : TextExpanderSettingsUtils
        {
            public MockFileSystemSettingsUtils(MockFileSystem fileSystem)
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

            public ThrowAfterPublicationSettingsUtils(MockFileSystem fileSystem)
                : base(fileSystem)
            {
                _fileSystem = fileSystem;
            }

            public string StagedContents { get; private set; }

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

        private sealed class ExposedHandleBoundCleanupSettingsUtils : TextExpanderSettingsUtils
        {
            private readonly Action<string> _beforeDisposition;

            public ExposedHandleBoundCleanupSettingsUtils(Action<string> beforeDisposition = null)
                : base(new FileSystem())
            {
                _beforeDisposition = beforeDisposition;
            }

            public HandleBoundFileDeletionResult DeleteOwnedArtifact(string path, byte[] ownedBytes)
                => TryDeleteOwnedAtomicSaveArtifact(path, ownedBytes);

            protected override void BeforeOwnedAtomicSaveArtifactDisposition(string path)
                => _beforeDisposition?.Invoke(path);
        }

        private sealed class FailBackupCleanupSettingsUtils : MockFileSystemSettingsUtils
        {
            public FailBackupCleanupSettingsUtils(MockFileSystem fileSystem)
                : base(fileSystem)
            {
            }

            protected override HandleBoundFileDeletionResult TryDeleteOwnedAtomicSaveArtifact(
                string path,
                byte[] ownedBytes)
            {
                if (path.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                {
                    return new HandleBoundFileDeletionResult(
                        HandleBoundFileDeletionStatus.PathStillPresent,
                        path,
                        new IOException($"Injected backup cleanup failure at '{path}'."));
                }

                return base.TryDeleteOwnedAtomicSaveArtifact(path, ownedBytes);
            }
        }

        private sealed class BlockingThrowAfterPublicationSettingsUtils : MockFileSystemSettingsUtils
        {
            public BlockingThrowAfterPublicationSettingsUtils(MockFileSystem fileSystem)
                : base(fileSystem)
            {
            }

            public ManualResetEventSlim Published { get; } = new(false);

            public ManualResetEventSlim Release { get; } = new(false);

            protected override void PublishStagedSettingsFile(
                string stagingPath,
                string settingsPath,
                string backupPath,
                bool destinationExisted)
            {
                base.PublishStagedSettingsFile(stagingPath, settingsPath, backupPath, destinationExisted);
                Published.Set();
                if (!Release.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new IOException("The concurrent settings test gate timed out.");
                }

                throw new IOException("Injected failure after atomic settings publication.");
            }
        }

        private sealed class MutateBeforeCompensationSettingsUtils : MockFileSystemSettingsUtils
        {
            private readonly MockFileSystem _fileSystem;
            private readonly string _externalContents;

            public MutateBeforeCompensationSettingsUtils(MockFileSystem fileSystem, string externalContents)
                : base(fileSystem)
            {
                _fileSystem = fileSystem;
                _externalContents = externalContents;
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

            protected override void BeforeSettingsPublicationCompensation(
                string stagingPath,
                string settingsPath,
                string backupPath,
                bool destinationExisted)
            {
                _fileSystem.File.WriteAllText(settingsPath, _externalContents);
            }
        }

        private sealed class MutateAtCompensationQuarantineSettingsUtils : MockFileSystemSettingsUtils
        {
            private readonly MockFileSystem _fileSystem;
            private readonly string _externalContents;
            private readonly string _restorationBlocker;

            public MutateAtCompensationQuarantineSettingsUtils(
                MockFileSystem fileSystem,
                string externalContents,
                string restorationBlocker = null)
                : base(fileSystem)
            {
                _fileSystem = fileSystem;
                _externalContents = externalContents;
                _restorationBlocker = restorationBlocker;
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

            protected override void BeforeSettingsCompensationQuarantine(
                string settingsPath,
                string quarantinePath,
                bool destinationExisted)
            {
                _fileSystem.File.WriteAllText(settingsPath, _externalContents);
            }

            protected override void BeforeUnexpectedSettingsRestore(
                string quarantinePath,
                string settingsPath,
                bool destinationExisted)
            {
                if (_restorationBlocker is not null)
                {
                    _fileSystem.File.WriteAllText(settingsPath, _restorationBlocker);
                }
            }
        }

        private sealed class FailCompensationSettingsUtils : MockFileSystemSettingsUtils
        {
            public FailCompensationSettingsUtils(MockFileSystem fileSystem)
                : base(fileSystem)
            {
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

            protected override void CompensateFailedSettingsPublication(
                string stagingPath,
                string settingsPath,
                string backupPath,
                string displacedPath,
                bool destinationExisted,
                byte[] originalBytes,
                byte[] publishedBytes)
            {
                throw new IOException("Injected settings compensation failure.");
            }
        }

        private sealed class ThrowAfterCompensationSettingsUtils : MockFileSystemSettingsUtils
        {
            public ThrowAfterCompensationSettingsUtils(MockFileSystem fileSystem)
                : base(fileSystem)
            {
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
    }
}
