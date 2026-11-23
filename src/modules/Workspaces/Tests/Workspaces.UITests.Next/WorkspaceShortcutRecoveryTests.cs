// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Workspaces.UITests
{
    [TestClass]
    public sealed class WorkspaceShortcutRecoveryTests
    {
        private DirectoryInfo root = null!;
        private string profile = null!;
        private string desktop = null!;
        private string icons = null!;

        [TestInitialize]
        public void SetUp()
        {
            root = Directory.CreateTempSubdirectory("WorkspacesShortcutRecoveryTests-");
            profile = root.CreateSubdirectory("Workspaces").FullName;
            desktop = root.CreateSubdirectory("Desktop").FullName;
            icons = Directory.CreateDirectory(Path.Combine(profile, "WorkspacesIcons")).FullName;
        }

        [TestCleanup]
        public void TearDown() => root.Delete(recursive: true);

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void CleanupRetiresOnlyTheRegisteredWorkspaceShortcutFiles(bool interrupt)
        {
            string id = Guid.NewGuid().ToString("B");
            string name = $"PTUITest-{Guid.NewGuid():N}-workspace";
            string link = Path.Combine(desktop, name + ".lnk");
            string icon = Path.Combine(icons, id + ".ico");
            string journal = Path.Combine(icons, id + ".links.json");
            string untrackedName = name + "-untracked";
            string untrackedLink = Path.Combine(desktop, untrackedName + ".lnk");
            string untrackedIcon = Path.Combine(icons, $"{Guid.NewGuid():B}.ico");
            string unrelatedJson = Path.Combine(icons, "notes.links.json");
            File.WriteAllText(untrackedIcon, "existing icon");
            File.WriteAllText(unrelatedJson, "existing notes");
            WriteShortcut(untrackedLink, id, untrackedIcon);
            byte[] untrackedLinkBytes = File.ReadAllBytes(untrackedLink);

            using (var snapshot = new WorkspaceProfileSnapshot(profile, desktop))
            {
                snapshot.Isolate();
                snapshot.TrackProjectShortcutFiles(id);
                snapshot.TrackDesktopShortcut(name);
                File.WriteAllText(icon, "test icon");
                WriteJournal(journal, id, name);
                WriteShortcut(link, id, icon);
                if (interrupt)
                {
                    snapshot.LeaveForRecovery();
                }
            }

            if (interrupt)
            {
                Assert.IsTrue(File.Exists(link) && File.Exists(icon) && File.Exists(journal));
                using var next = new WorkspaceProfileSnapshot(profile, desktop);
                next.Isolate();
            }

            Assert.IsFalse(File.Exists(link));
            Assert.IsFalse(File.Exists(icon));
            Assert.IsFalse(File.Exists(journal));
            CollectionAssert.AreEqual(untrackedLinkBytes, File.ReadAllBytes(untrackedLink));
            Assert.AreEqual("existing icon", File.ReadAllText(untrackedIcon));
            Assert.AreEqual("existing notes", File.ReadAllText(unrelatedJson));
        }

        [TestMethod]
        public void RecoveryRestoresOriginalWorkspaceDataBeforeExposingItsShortcutJournal()
        {
            string id = Guid.NewGuid().ToString("B");
            string name = "Original workspace";
            string workspace = Path.Combine(profile, "workspaces.json");
            string journal = Path.Combine(icons, id + ".links.json");
            string saved = JsonSerializer.Serialize(new { workspaces = new[] { new { id, name } } });
            File.WriteAllText(workspace, saved);
            WriteJournal(journal, id, name);
            byte[] originalJournal = File.ReadAllBytes(journal);

            using (var snapshot = new WorkspaceProfileSnapshot(profile, desktop))
            {
                snapshot.Isolate();
                Assert.IsFalse(File.Exists(workspace));
                Assert.IsFalse(File.Exists(journal));
                File.WriteAllText(workspace, """{"workspaces":[]}""");
                using var reader = File.Open(workspace, FileMode.Open, FileAccess.Read, FileShare.Read);
                Assert.Throws<IOException>(() => snapshot.Dispose());
                Assert.IsFalse(File.Exists(journal), "The editor could recover the user's shortcut against the test workspace list.");
            }

            using (var next = new WorkspaceProfileSnapshot(profile, desktop))
            {
                next.Isolate();
                Assert.IsFalse(File.Exists(journal));
            }

            Assert.AreEqual(saved, File.ReadAllText(workspace));
            CollectionAssert.AreEqual(originalJournal, File.ReadAllBytes(journal));
        }

        [TestMethod]
        public void DeleteSharingReaderCanRemainOpenDuringProfileIsolationAndRestore()
        {
            string id = Guid.NewGuid().ToString("B");
            string name = "Original workspace";
            string workspace = Path.Combine(profile, "workspaces.json");
            string journal = Path.Combine(icons, id + ".links.json");
            string saved = JsonSerializer.Serialize(new { workspaces = new[] { new { id, name } } });
            File.WriteAllText(workspace, saved);
            WriteJournal(journal, id, name);
            byte[] expected = File.ReadAllBytes(journal);
            string recovery = profile + ".UITestRecovery";

            using (var reader = File.Open(journal, FileMode.Open, FileAccess.Read, FileShare.Delete))
            {
                using (var snapshot = new WorkspaceProfileSnapshot(profile, desktop))
                {
                    snapshot.Isolate();
                    Assert.IsFalse(File.Exists(journal));
                    Assert.IsFalse(File.Exists(workspace));
                    Assert.IsFalse(Directory.Exists(Path.Combine(recovery, "capturing")));
                    Assert.IsTrue(Directory.Exists(Path.Combine(recovery, "original")));
                    File.WriteAllText(workspace, """{"workspaces":[]}""");
                }

                Assert.AreEqual(saved, File.ReadAllText(workspace));
                Assert.IsTrue(File.Exists(journal));
                Assert.IsFalse(Directory.Exists(Path.Combine(recovery, "original")));
                using var contents = new MemoryStream();
                reader.CopyTo(contents);
                CollectionAssert.AreEqual(expected, contents.ToArray());
            }

            CollectionAssert.AreEqual(expected, File.ReadAllBytes(journal));
        }

        [TestMethod]
        public void RegistrationRejectsExistingDesktopLinksAndProjectIcons()
        {
            string id = Guid.NewGuid().ToString("B");
            string icon = Path.Combine(icons, id + ".ico");
            File.WriteAllText(icon, "existing icon");
            string name = $"PTUITest-{Guid.NewGuid():N}-collision";
            string link = Path.Combine(desktop, name + ".lnk");
            WriteShortcut(link, id, icon);
            byte[] originalLink = File.ReadAllBytes(link);

            using (var snapshot = new WorkspaceProfileSnapshot(profile, desktop))
            {
                snapshot.Isolate();
                Assert.Throws<IOException>(() => snapshot.TrackProjectShortcutFiles(id));
                Assert.Throws<IOException>(() => snapshot.TrackDesktopShortcut(name));
            }

            Assert.AreEqual("existing icon", File.ReadAllText(icon));
            CollectionAssert.AreEqual(originalLink, File.ReadAllBytes(link));
        }

        [TestMethod]
        public void InterruptedCleanupPreservesARegisteredLinkReplacedByAnotherWorkspace()
        {
            string id = Guid.NewGuid().ToString("B");
            string name = $"PTUITest-{Guid.NewGuid():N}-replaced";
            string link = Path.Combine(desktop, name + ".lnk");
            string icon = Path.Combine(icons, id + ".ico");
            using (var snapshot = new WorkspaceProfileSnapshot(profile, desktop))
            {
                snapshot.Isolate();
                snapshot.TrackProjectShortcutFiles(id);
                snapshot.TrackDesktopShortcut(name);
                File.WriteAllText(icon, "test icon");
                WriteJournal(Path.Combine(icons, id + ".links.json"), id, name);
                WriteShortcut(link, id, icon);
                snapshot.LeaveForRecovery();
            }

            string otherId = Guid.NewGuid().ToString("B");
            string otherIcon = Path.Combine(icons, otherId + ".ico");
            File.WriteAllText(otherIcon, "replacement icon");
            File.Delete(link);
            WriteShortcut(link, otherId, otherIcon);
            byte[] replacementLink = File.ReadAllBytes(link);
            using (var next = new WorkspaceProfileSnapshot(profile, desktop))
            {
                next.Isolate();
            }

            CollectionAssert.AreEqual(replacementLink, File.ReadAllBytes(link));
            Assert.AreEqual("replacement icon", File.ReadAllText(otherIcon));
        }

        [TestMethod]
        public void InterruptedCleanupMatchesEachRegisteredLinkToItsOwnWorkspaceJournal()
        {
            string firstId = Guid.NewGuid().ToString("B");
            string secondId = Guid.NewGuid().ToString("B");
            string prefix = $"PTUITest-{Guid.NewGuid():N}";
            string firstName = prefix + "-first";
            string secondName = prefix + "-second";
            string firstLink = Path.Combine(desktop, firstName + ".lnk");
            string secondLink = Path.Combine(desktop, secondName + ".lnk");
            string firstIcon = Path.Combine(icons, firstId + ".ico");
            string secondIcon = Path.Combine(icons, secondId + ".ico");
            using (var snapshot = new WorkspaceProfileSnapshot(profile, desktop))
            {
                snapshot.Isolate();
                snapshot.TrackProjectShortcutFiles(firstId);
                snapshot.TrackProjectShortcutFiles(secondId);
                snapshot.TrackDesktopShortcut(firstName);
                snapshot.TrackDesktopShortcut(secondName);
                File.WriteAllText(firstIcon, "first icon");
                File.WriteAllText(secondIcon, "second icon");
                WriteJournal(Path.Combine(icons, firstId + ".links.json"), firstId, firstName);
                WriteJournal(Path.Combine(icons, secondId + ".links.json"), secondId, secondName);
                WriteShortcut(firstLink, firstId, firstIcon);
                WriteShortcut(secondLink, secondId, secondIcon);
                snapshot.LeaveForRecovery();
            }

            File.Delete(firstLink);
            WriteShortcut(firstLink, secondId, secondIcon);
            byte[] replacement = File.ReadAllBytes(firstLink);
            Assert.IsTrue(File.Exists(secondLink), "The second workspace should still have its original link before recovery.");
            using (var next = new WorkspaceProfileSnapshot(profile, desktop))
            {
                next.Isolate();
            }

            CollectionAssert.AreEqual(replacement, File.ReadAllBytes(firstLink), "The second workspace's journal does not claim the first link's name.");
            Assert.IsFalse(File.Exists(secondLink), "The second link's name and target both match its workspace journal.");
            Assert.AreEqual("second icon", File.ReadAllText(secondIcon), "The replacement link still uses the second workspace's icon.");
        }

        private static void WriteJournal(string path, string id, string name)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new { ProjectId = id, Names = new[] { name }, PendingRemoval = false }));
        }

        private void WriteShortcut(string path, string id, string icon)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell", throwOnError: true)!;
            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic shortcut = shell.CreateShortcut(path);
                try
                {
                    shortcut.TargetPath = Path.Combine(root.FullName, "PowerToys.WorkspacesLauncher.exe");
                    shortcut.Arguments = $"{id} 1";
                    shortcut.Description = $"Project Launcher {id}";
                    shortcut.IconLocation = icon + ",0";
                    shortcut.Save();
                }
                finally
                {
                    Marshal.FinalReleaseComObject(shortcut);
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
