// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Workspaces.UITests
{
    [TestClass]
    public sealed class WorkspaceProfileSnapshotTests
    {
        private DirectoryInfo temporaryDirectory = null!;
        private string profile = null!;

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
                Assert.IsTrue(File.Exists(Path.Combine(profile + ".UITestRecovery", "original.zip")));
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
            Assert.IsFalse(File.Exists(Path.Combine(profile + ".UITestRecovery", "original.zip")));
        }

        [TestMethod]
        public void NextRunRestoresInterruptedOriginalsAndKeepsIntermediateDataRecoverable()
        {
            var recovery = profile + ".UITestRecovery";
            var workspace = Path.Combine(profile, "workspaces.json");
            var draft = Path.Combine(profile, "temp-workspaces.json");
            File.WriteAllText(workspace, "original workspace");
            using (var previous = new WorkspaceProfileSnapshot(profile))
            {
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
            Assert.IsTrue(File.Exists(Path.Combine(recovery, "original.zip")));
            using (var snapshot = new WorkspaceProfileSnapshot(profile))
            {
                snapshot.Isolate();
                Assert.IsFalse(File.Exists(workspace));
                Assert.IsFalse(File.Exists(draft));
                File.WriteAllText(workspace, "subsequent run");
            }

            Assert.AreEqual("original workspace", File.ReadAllText(workspace));
            Assert.IsFalse(File.Exists(draft));
            Assert.IsFalse(File.Exists(Path.Combine(recovery, "original.zip")));
            var archives = Directory.GetFiles(recovery, "interrupted-*.zip");
            Assert.HasCount(1, archives);
            using var interrupted = ZipFile.OpenRead(archives[0]);
            Assert.AreEqual("interrupted workspace", ReadEntry(interrupted, "workspaces.json"));
            Assert.AreEqual("interrupted draft", ReadEntry(interrupted, "temp-workspaces.json"));
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
        public void FailedCaptureKeepsOriginalsInPlaceAndCanBeRetried()
        {
            var workspace = Path.Combine(profile, "workspaces.json");
            var placement = Path.Combine(profile, "editor-window.json");
            File.WriteAllText(workspace, "original workspace");
            File.WriteAllText(placement, "original placement");
            using (var snapshot = new WorkspaceProfileSnapshot(profile))
            {
                using (File.Open(placement, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    Assert.Throws<IOException>(() => snapshot.Isolate());
                }

                Assert.AreEqual("original workspace", File.ReadAllText(workspace));
                Assert.AreEqual("original placement", File.ReadAllText(placement));
                Assert.IsFalse(File.Exists(Path.Combine(profile + ".UITestRecovery", "original.zip")));
                snapshot.Isolate();
                Assert.IsFalse(File.Exists(placement));
            }

            Assert.AreEqual("original placement", File.ReadAllText(placement));
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
            using var original = ZipFile.OpenRead(Path.Combine(profile + ".UITestRecovery", "original.zip"));
            Assert.AreEqual("original", ReadEntry(original, "workspaces.json"));
        }

        private static string ReadEntry(ZipArchive archive, string name)
        {
            var entry = archive.GetEntry(name);
            Assert.IsNotNull(entry);
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        }
    }
}
