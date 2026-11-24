// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Workspaces.UITests
{
    [TestClass]
    public sealed class WorkspaceProfileSnapshotTests
    {
        private DirectoryInfo temporaryDirectory = null!;
        private string profile = null!;

        private string RecoveryDirectory => profile + ".UITestRecovery";

        private string OriginalDirectory => Path.Combine(RecoveryDirectory, "original");

        [TestInitialize]
        public void SetUp()
        {
            temporaryDirectory = Directory.CreateTempSubdirectory("WorkspacesProfileTests-");
            profile = temporaryDirectory.CreateSubdirectory("Workspaces").FullName;
        }

        [TestCleanup]
        public void TearDown()
        {
            temporaryDirectory.Delete(recursive: true);
        }

        [TestMethod]
        public void DisposeRestoresOriginalBytesAndAbsenceWithoutRevertingOtherFiles()
        {
            var originals = new Dictionary<string, byte[]?>
            {
                ["workspaces.json"] = [0xef, 0xbb, 0xbf, 0x7b, 0x7d, 0x0d, 0x0a],
                ["editor-window.json"] = [0xff, 0x00],
                ["settings.json"] = [],
                ["temp-workspaces.json"] = null,
            };
            foreach (var (name, bytes) in originals)
            {
                if (bytes is not null)
                {
                    File.WriteAllBytes(Path.Combine(profile, name), bytes);
                }
            }

            var unrelated = Path.Combine(profile, "notes.txt");
            File.WriteAllText(unrelated, "before");
            var workspace = Path.Combine(profile, "workspaces.json");
            File.WriteAllText(workspace, "editor is still running");
            using (var snapshot = new WorkspaceProfileSnapshot(profile))
            {
                // Simulate the editor's last write after the test claims the lease.
                File.WriteAllBytes(workspace, originals["workspaces.json"]!);
                snapshot.Isolate();
                Assert.IsTrue(Directory.Exists(OriginalDirectory));
                Assert.AreEqual("before", File.ReadAllText(unrelated));
                foreach (var name in originals.Keys)
                {
                    var path = Path.Combine(profile, name);
                    Assert.IsFalse(File.Exists(path), name);
                    File.WriteAllText(path, "test data");
                }

                File.WriteAllText(unrelated, "during");
                snapshot.Isolate();
                Assert.AreEqual("test data", File.ReadAllText(workspace));
            }

            foreach (var (name, bytes) in originals)
            {
                var path = Path.Combine(profile, name);
                if (bytes is null)
                {
                    Assert.IsFalse(File.Exists(path), name);
                }
                else
                {
                    CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path), name);
                }
            }

            Assert.AreEqual("during", File.ReadAllText(unrelated));
            Assert.IsFalse(Directory.Exists(OriginalDirectory));
        }

        [TestMethod]
        public void IsolateRestoresExistingBracedGuidRequestsWithTheirOriginalWriteTime()
        {
            const string prefix = "temp-workspaces.json.";
            var id = Guid.NewGuid();
            var request = Path.Combine(profile, $"{prefix}{id:B}");
            var laterRequest = Path.Combine(profile, $"{prefix}{Guid.NewGuid():B}");
            string[] unrelated = [$"{prefix}{{invalid}}", $"{prefix}{id:D}", $"{prefix}{id:B}.backup"];
            byte[] original = [0xff, 0x00, 0x0d, 0x0a];
            File.WriteAllBytes(request, original);
            File.SetLastWriteTimeUtc(request, new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1_234_567));
            var originalWriteTime = File.GetLastWriteTimeUtc(request);
            foreach (var name in unrelated)
            {
                File.WriteAllText(Path.Combine(profile, name), name);
            }

            using (var snapshot = new WorkspaceProfileSnapshot(profile))
            {
                snapshot.Isolate();
                Assert.IsFalse(File.Exists(request));
                foreach (var name in unrelated)
                {
                    Assert.AreEqual(name, File.ReadAllText(Path.Combine(profile, name)));
                }

                File.WriteAllText(laterRequest, "new request");
                File.WriteAllText(request, "later version of saved request");
            }

            CollectionAssert.AreEqual(original, File.ReadAllBytes(request));
            Assert.AreEqual(originalWriteTime, File.GetLastWriteTimeUtc(request));
            Assert.AreEqual("new request", File.ReadAllText(laterRequest));
            Assert.AreEqual("later version of saved request", File.ReadAllText(InterruptedFile(Path.GetFileName(request))));
            foreach (var name in unrelated)
            {
                Assert.AreEqual(name, File.ReadAllText(Path.Combine(profile, name)));
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void NextRunRestoresInterruptedOriginalsAndKeepsIntermediateDataRecoverable(bool versionOneBackup)
        {
            var workspace = Path.Combine(profile, "workspaces.json");
            var draft = Path.Combine(profile, "temp-workspaces.json");
            var oldArchive = Path.Combine(RecoveryDirectory, "original.zip");
            File.WriteAllText(workspace, "original workspace");
            if (versionOneBackup)
            {
                Directory.CreateDirectory(RecoveryDirectory);
                using var archive = ZipFile.Open(oldArchive, ZipArchiveMode.Create);
                archive.CreateEntry("PowerToys.Workspaces.UITests.Next.v1");
                archive.CreateEntryFromFile(workspace, "workspaces.json");
            }
            else
            {
                using var previous = new WorkspaceProfileSnapshot(profile);
                previous.Isolate();
                previous.LeaveForRecovery();
            }

            File.WriteAllText(workspace, "interrupted workspace");
            File.WriteAllText(draft, "interrupted draft");

            // Acquiring and releasing the lease before the product has stopped must leave even a pending recovery alone.
            using (new WorkspaceProfileSnapshot(profile))
            {
                Assert.AreEqual("interrupted workspace", File.ReadAllText(workspace));
                Assert.AreEqual("interrupted draft", File.ReadAllText(draft));
            }

            Assert.AreEqual("interrupted workspace", File.ReadAllText(workspace));
            Assert.IsTrue(versionOneBackup ? File.Exists(oldArchive) : Directory.Exists(OriginalDirectory));
            using (var snapshot = new WorkspaceProfileSnapshot(profile))
            {
                snapshot.Isolate();
                Assert.IsFalse(File.Exists(workspace));
                Assert.IsFalse(File.Exists(draft));
                File.WriteAllText(workspace, "subsequent run");
            }

            Assert.AreEqual("original workspace", File.ReadAllText(workspace));
            Assert.IsFalse(File.Exists(draft));
            Assert.IsFalse(Directory.Exists(OriginalDirectory));
            Assert.IsFalse(File.Exists(oldArchive));
            Assert.AreEqual("interrupted workspace", File.ReadAllText(InterruptedFile("workspaces.json")));
            Assert.AreEqual("interrupted draft", File.ReadAllText(InterruptedFile("temp-workspaces.json")));
        }

        [TestMethod]
        [DataRow("workspaces.json")]
        [DataRow("temp-workspaces.json.{32f60caf-0800-40b3-906e-399c9963b0bb}")]
        public void VersionOneBackupKeepsMatchingOriginalFileMetadata(string name)
        {
            var path = Path.Combine(profile, name);
            File.WriteAllText(path, "original");
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);
            File.SetLastWriteTimeUtc(path, new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1_234_567));
            var modified = File.GetLastWriteTimeUtc(path);
            Directory.CreateDirectory(RecoveryDirectory);
            using (var archive = ZipFile.Open(Path.Combine(RecoveryDirectory, "original.zip"), ZipArchiveMode.Create))
            {
                archive.CreateEntry("PowerToys.Workspaces.UITests.Next.v1");
                var entry = archive.CreateEntryFromFile(path, name);
                if (name != "workspaces.json")
                {
                    entry.Comment = modified.ToString("O", CultureInfo.InvariantCulture);
                }
            }

            using (var next = new WorkspaceProfileSnapshot(profile))
            {
                next.Isolate();
                Assert.IsFalse(File.Exists(path));
            }

            Assert.AreEqual("original", File.ReadAllText(path));
            Assert.IsTrue(File.GetAttributes(path).HasFlag(FileAttributes.Hidden));
            Assert.AreEqual(modified, File.GetLastWriteTimeUtc(path));
        }

        [TestMethod]
        public void SecondSnapshotCannotClaimTheSameProfile()
        {
            var workspace = Path.Combine(profile, "workspaces.json");
            File.WriteAllText(workspace, "original");
            using (var snapshot = new WorkspaceProfileSnapshot(profile))
            {
                snapshot.Isolate();
                File.WriteAllText(workspace, "in use");
                Assert.Throws<IOException>(() =>
                {
                    using var second = new WorkspaceProfileSnapshot(profile);
                });
                Assert.AreEqual("in use", File.ReadAllText(workspace));
            }

            Assert.AreEqual("original", File.ReadAllText(workspace));
        }

        [TestMethod]
        public void ProductWorkspaceLockProtectsIsolationAndRestoreButAllowsTestWrites()
        {
            var workspace = Path.Combine(profile, "workspaces.json");
            var lockPath = workspace + ".lock";
            File.WriteAllText(workspace, "original");
            using (var snapshot = new WorkspaceProfileSnapshot(profile))
            {
                using (File.Open(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    Assert.Throws<IOException>(() => snapshot.Isolate());
                    Assert.AreEqual("original", File.ReadAllText(workspace));
                }

                snapshot.Isolate();
                using (File.Open(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    File.WriteAllText(workspace, "product write");
                    Assert.Throws<IOException>(() => snapshot.Dispose());
                    Assert.AreEqual("product write", File.ReadAllText(workspace));
                    Assert.AreEqual("original", File.ReadAllText(Path.Combine(OriginalDirectory, "workspaces.json")));
                }
            }

            using (var next = new WorkspaceProfileSnapshot(profile))
            {
                next.Isolate();
            }

            Assert.AreEqual("original", File.ReadAllText(workspace));
        }

        [TestMethod]
        [DataRow("workspaces.json")]
        [DataRow("editor-window.json")]
        public void FailedIsolationOnlyRollsBackMovedFilesAndPreservesLaterEdits(string blockedName)
        {
            var workspace = Path.Combine(profile, "workspaces.json");
            var placement = Path.Combine(profile, "editor-window.json");
            var settings = Path.Combine(profile, "settings.json");
            var blocked = Path.Combine(profile, blockedName);
            File.WriteAllText(workspace, "original workspace");
            File.WriteAllText(placement, "original placement");
            File.WriteAllText(settings, "unchanged settings");
            using (var snapshot = new WorkspaceProfileSnapshot(profile))
            {
                // Block the first move or a later move, while permitting ordinary reads.
                // The subsequent settings file was never moved and must not be replaced on rollback.
                using (File.Open(settings, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (File.Open(blocked, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Assert.Throws<IOException>(() => snapshot.Isolate());
                    Assert.AreEqual("original workspace", File.ReadAllText(workspace));
                    Assert.AreEqual("original placement", File.ReadAllText(placement));
                    Assert.AreEqual("unchanged settings", File.ReadAllText(settings));
                    Assert.IsFalse(Directory.Exists(OriginalDirectory));
                    Assert.IsFalse(Directory.Exists(Path.Combine(RecoveryDirectory, "capturing")));
                }
            }

            File.WriteAllText(blocked, "user edit after failure");
            using (var retry = new WorkspaceProfileSnapshot(profile))
            {
                retry.Isolate();
                Assert.IsFalse(File.Exists(workspace));
            }

            Assert.AreEqual("user edit after failure", File.ReadAllText(blocked));
            Assert.AreEqual("unchanged settings", File.ReadAllText(settings));
        }

        [TestMethod]
        public void InterruptedCaptureRestoresMovedFilesAndKeepsNewFilesAtTheirOriginalPaths()
        {
            var workspace = Path.Combine(profile, "workspaces.json");
            var placement = Path.Combine(profile, "editor-window.json");
            File.WriteAllText(workspace, "original");
            using (var prior = new WorkspaceProfileSnapshot(profile))
            {
                prior.Isolate();
                prior.LeaveForRecovery();
            }

            // Recreate the state immediately before the final directory rename. Placement
            // was absent when capture started, so there is no moved placement file to undo.
            Directory.Move(OriginalDirectory, Path.Combine(RecoveryDirectory, "capturing"));
            File.WriteAllText(placement, "created after the interruption");
            using (var next = new WorkspaceProfileSnapshot(profile))
            {
                next.Isolate();
            }

            Assert.AreEqual("original", File.ReadAllText(workspace));
            Assert.AreEqual("created after the interruption", File.ReadAllText(placement));
        }

        [TestMethod]
        public void FailedRestorePreservesTheConflictingDirectoryAndTheOriginalArchive()
        {
            var workspace = Path.Combine(profile, "workspaces.json");
            var unrelated = Path.Combine(workspace, "keep.txt");
            File.WriteAllText(workspace, "original");
            Assert.Throws<IOException>(() =>
            {
                using var snapshot = new WorkspaceProfileSnapshot(profile);
                snapshot.Isolate();
                Directory.CreateDirectory(workspace);
                File.WriteAllText(unrelated, "keep");
            });

            Assert.AreEqual("keep", File.ReadAllText(unrelated));
            Assert.AreEqual("original", File.ReadAllText(Path.Combine(OriginalDirectory, "workspaces.json")));
        }

        [TestMethod]
        public void DisposeRestoresOriginalNtfsMetadataWhenReplacementContentIsIdentical()
        {
            if (!string.Equals(new DriveInfo(Path.GetPathRoot(profile)!).DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Inconclusive("This test requires an NTFS temporary directory.");
            }

            var file = new FileInfo(Path.Combine(profile, "workspaces.json"));
            var streamPath = file.FullName + ":powertoys-test";
            byte[] content = [0x00, 0x7b, 0x7d, 0xff];
            File.WriteAllBytes(file.FullName, content);
            File.WriteAllText(streamPath, "original stream");
            var access = System.Security.AccessControl.AccessControlSections.Access;
            var permissions = file.GetAccessControl(access);

            // Protect the DACL but copy its inherited permissions so access remains the same.
            permissions.SetAccessRuleProtection(isProtected: true, preserveInheritance: true);
            file.SetAccessControl(permissions);
            var originalDacl = file.GetAccessControl(access).GetSecurityDescriptorSddlForm(access);
            File.SetAttributes(file.FullName, File.GetAttributes(file.FullName) | FileAttributes.Hidden);
            var originalTime = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1_234_567);
            File.SetLastWriteTimeUtc(file.FullName, originalTime);

            using (var snapshot = new WorkspaceProfileSnapshot(profile))
            {
                snapshot.Isolate();
                Assert.IsFalse(File.Exists(file.FullName));
                File.WriteAllBytes(file.FullName, content);
                Assert.AreNotEqual(originalDacl, file.GetAccessControl(access).GetSecurityDescriptorSddlForm(access));
            }

            CollectionAssert.AreEqual(content, File.ReadAllBytes(file.FullName));
            Assert.IsTrue(File.GetAttributes(file.FullName).HasFlag(FileAttributes.Hidden));
            Assert.AreEqual(originalDacl, file.GetAccessControl(access).GetSecurityDescriptorSddlForm(access));
            Assert.AreEqual("original stream", File.ReadAllText(streamPath));
            Assert.AreEqual(originalTime, File.GetLastWriteTimeUtc(file.FullName));
        }

        private string InterruptedFile(string name) =>
            Path.Combine(Directory.GetDirectories(RecoveryDirectory, "interrupted-*").Single(), name);
    }
}
