// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesEditor.Models;
using WorkspacesEditor.Services;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor.UnitTests
{
    public partial class WorkspaceShortcutRegressionTests
    {
        [TestMethod]
        [DataRow("", false)]
        [DataRow("{", true)]
        [DataRow("null", false)]
        [DataRow("{\"Names\":42,\"PendingRemoval\":true}", true)]
        [DataRow("{\"ProjectId\":\"{00000000-0000-0000-0000-000000000000}\",\"Names\":[\"Other desktop name\"],\"PendingRemoval\":true}", false)]
        public void InvalidJournal_IsPreservedAndOnlyTheKnownOwnedLinkIsRecovered(string invalid, bool delete)
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Before recovery");
            fixture.Apply(original);
            fixture.WriteShortcut(original, hotkey: 0x064B, showCommand: 3);
            string oldPath = fixture.LinkPath(original.Name);
            string oldIcon = fixture.IconPath(original);
            byte[] oldIconBytes = File.ReadAllBytes(oldIcon);
            string journal = Directory.GetFiles(fixture.IconFolder, "*.links.json").Single();
            byte[] damaged = Encoding.UTF8.GetBytes(invalid);
            File.WriteAllBytes(journal, damaged);

            Project other = fixture.Project("Other desktop name");
            fixture.WriteShortcut(other, iconPath: oldIcon);
            string otherPath = fixture.LinkPath(other.Name);
            byte[] otherBytes = File.ReadAllBytes(otherPath);
            session.ViewModel.Workspaces.Add(original);
            if (delete)
            {
                session.ViewModel.DeleteProject(original);
                Assert.AreEqual(0, session.ViewModel.Workspaces.Count);
                CollectionAssert.AreEqual(oldIconBytes, File.ReadAllBytes(oldIcon), "Unknown historical links may still reference the ICO.");
            }
            else
            {
                Project draft = session.Edit(original);
                draft.Name = "Recovered workspace";
                session.Save(draft);
                Assert.AreEqual(draft.Name, original.Name);
                fixture.AssertShortcut(original);
                fixture.AssertLaunchOptions(original, 0x064B, 3);
            }

            Assert.AreEqual(1, session.SerializationCount);
            Assert.IsFalse(File.Exists(oldPath));
            Assert.IsFalse(session.ViewModel.HasShortcutNotice);
            string recoveryCopy = Directory.GetFiles(fixture.IconFolder, Path.GetFileName(journal) + ".corrupt*").Single();
            CollectionAssert.AreEqual(damaged, File.ReadAllBytes(recoveryCopy));
            CollectionAssert.AreEqual(otherBytes, File.ReadAllBytes(otherPath));
        }

        [TestMethod]
        public void DeletedWorkspace_RestartsPreserveTheUnregisteredShortcutAndSingleRecoveryCopy()
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project project = fixture.Project("Registered workspace");
            fixture.Apply(project);

            string registeredPath = fixture.LinkPath(project.Name);
            string customPath = fixture.LinkPath("My own launcher");

            // Copying the link retains the workspace ID and icon without registering this name.
            File.Copy(registeredPath, customPath);
            byte[] customLink = File.ReadAllBytes(customPath);
            string iconPath = fixture.IconPath(project);
            byte[] icon = File.ReadAllBytes(iconPath);
            string journal = Directory.GetFiles(fixture.IconFolder, "*.links.json").Single();
            byte[] damaged = Encoding.UTF8.GetBytes("{");
            File.WriteAllBytes(journal, damaged);

            session.ViewModel.Workspaces.Add(project);
            session.ViewModel.DeleteProject(project);
            Assert.AreEqual(0, session.ViewModel.Workspaces.Count);
            Assert.IsFalse(File.Exists(registeredPath));
            string pattern = Path.GetFileName(journal) + ".corrupt*";
            string backup = Directory.GetFiles(fixture.IconFolder, pattern).Single();
            for (int restart = 0; restart < 2; restart++)
            {
                var service = fixture.OpenService();
                Assert.IsTrue(service.GetPresence(project) is false, "The custom desktop name must not be adopted.");
                Assert.IsFalse(service.RetryPendingRemovals([]));
                CollectionAssert.AreEqual(new[] { backup }, Directory.GetFiles(fixture.IconFolder, pattern));
                CollectionAssert.AreEqual(damaged, File.ReadAllBytes(backup));
                CollectionAssert.AreEqual(customLink, File.ReadAllBytes(customPath));
                CollectionAssert.AreEqual(icon, File.ReadAllBytes(iconPath));
            }
        }

        [TestMethod]
        public void LockedInvalidJournal_IsRecoveredOnlyAfterTheReaderAllowsIt()
        {
            using var fixture = new ShortcutFixture();
            Project project = fixture.Project("Invalid record in use");
            fixture.Apply(project);
            string journal = Directory.GetFiles(fixture.IconFolder, "*.links.json").Single();
            byte[] damaged = Encoding.UTF8.GetBytes("[");
            File.WriteAllBytes(journal, damaged);
            byte[] link = File.ReadAllBytes(fixture.LinkPath(project.Name));
            using (var locked = File.OpenRead(journal))
            {
                Assert.IsFalse(fixture.Service.TryPrepareChange(project, deleting: true));
                CollectionAssert.AreEqual(damaged, File.ReadAllBytes(journal));
                CollectionAssert.AreEqual(link, File.ReadAllBytes(fixture.LinkPath(project.Name)));
                Assert.AreEqual(0, Directory.GetFiles(fixture.IconFolder, "*.corrupt").Length);
            }

            Assert.IsTrue(fixture.Service.TryPrepareChange(project, deleting: true));
            Assert.AreEqual(WorkspaceShortcutStatus.Success, fixture.Service.RemoveWithResult(project).Status);
            Assert.IsFalse(File.Exists(fixture.LinkPath(project.Name)));
            CollectionAssert.AreEqual(damaged, File.ReadAllBytes(Directory.GetFiles(fixture.IconFolder, "*.corrupt").Single()));
        }

        [TestMethod]
        [DataRow(4)]
        [DataRow(128)]
        [DataRow((4 * 1024 * 1024) + 1)]
        public void UnverifiableDesktopFile_IsRetainedWithoutSchedulingCleanup(int length)
        {
            using var fixture = new ShortcutFixture();
            Project project = fixture.Project("Unverifiable desktop file");
            fixture.Apply(project);
            string path = fixture.LinkPath(project.Name);
            if (length <= 128)
            {
                File.WriteAllBytes(path, new byte[length]);
            }
            else
            {
                // A genuine link followed by additional data still exceeds the inspection limit.
                using var oversized = File.OpenWrite(path);
                oversized.SetLength(length);
            }

            byte[] bytes = File.ReadAllBytes(path);
            byte[] icon = File.ReadAllBytes(fixture.IconPath(project));
            Assert.AreEqual(WorkspaceShortcutStatus.NameConflict, fixture.Service.ApplyWithResult(project).Status);
            Assert.AreEqual(WorkspaceShortcutStatus.Success, fixture.Service.RemoveWithResult(project).Status);
            Assert.IsFalse(fixture.OpenService().RetryPendingRemovals([]));
            Assert.IsFalse(fixture.OpenService().RetryPendingRemovals([]));
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));
            CollectionAssert.AreEqual(icon, File.ReadAllBytes(fixture.IconPath(project)));

            // Once the file can prove ownership, an explicit sweep can finish the recorded removal.
            File.Delete(path);
            fixture.WriteShortcut(project);
            Assert.IsFalse(fixture.OpenService().RetryPendingRemovals([]));
            Assert.IsFalse(File.Exists(path));
            Assert.IsFalse(File.Exists(fixture.IconPath(project)));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void UnreadableAtOpen_RechecksOwnershipBeforeSavingAnUnrelatedEdit(bool storedChoice)
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Previously unreadable");
            original.IsShortcutNeeded = storedChoice;
            fixture.WriteShortcut(original, hotkey: 0x064B, showCommand: 3);
            session.ViewModel.Workspaces.Add(original);

            Project draft;
            using (var locked = new FileStream(fixture.LinkPath(original.Name), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.IsNull(fixture.Service.GetPresence(original));
                draft = session.Edit(original);
                Assert.AreEqual(storedChoice, draft.IsShortcutNeeded);
                draft.MoveExistingWindows = !original.MoveExistingWindows;
            }

            session.Save(draft);

            Assert.AreEqual(1, session.SerializationCount);
            Assert.AreEqual(draft.MoveExistingWindows, original.MoveExistingWindows);
            Assert.IsTrue(session.SerializedShortcutChoices.Single());
            Assert.IsFalse(session.ViewModel.HasShortcutNotice);
            fixture.AssertShortcut(original);
            fixture.AssertLaunchOptions(original, 0x064B, 3);
        }

        [TestMethod]
        public void UnreadableThroughSave_PreservesLinkAndStoredChoiceAndReportsRetry()
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Still unreadable");
            original.IsShortcutNeeded = false;
            fixture.WriteShortcut(original);
            string linkPath = fixture.LinkPath(original.Name);
            byte[] link = File.ReadAllBytes(linkPath);
            byte[] icon = File.ReadAllBytes(fixture.IconPath(original));
            session.ViewModel.Workspaces.Add(original);

            using (var locked = new FileStream(linkPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Project draft = session.Edit(original);
                Assert.IsFalse(draft.IsShortcutNeeded);
                draft.MoveExistingWindows = true;
                session.Save(draft);
            }

            Assert.AreEqual(1, session.SerializationCount);
            Assert.IsFalse(session.SerializedShortcutChoices.Single());
            Assert.IsTrue(original.MoveExistingWindows);
            AssertShortcutNotice(session.ViewModel, "ShortcutUpdateFailedAfterSave");
            CollectionAssert.AreEqual(link, File.ReadAllBytes(linkPath));
            CollectionAssert.AreEqual(icon, File.ReadAllBytes(fixture.IconPath(original)));
        }

        [TestMethod]
        public void LockedTrackingFile_BlocksAnUntrackedRenameUntilItCanBeRetried()
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Untracked old name");
            Project historical = fixture.Project("Previously tracked name", original.Id);
            fixture.Apply(historical);
            fixture.WriteShortcut(original);
            string journal = Directory.EnumerateFiles(fixture.IconFolder, "*.links.json").Single();
            byte[] oldLink = File.ReadAllBytes(fixture.LinkPath(original.Name));
            byte[] oldIcon = File.ReadAllBytes(fixture.IconPath(original));
            session.ViewModel.Workspaces.Add(original);
            Project draft = session.Edit(original);
            draft.Name = "New name after retry";

            using (var locked = File.OpenRead(journal))
            {
                session.Save(draft);
            }

            Assert.AreEqual(0, session.SerializationCount);
            Assert.AreSame(draft, session.NavigatedProject);
            Assert.AreEqual("Untracked old name", original.Name);
            AssertShortcutNotice(session.ViewModel, "ShortcutTrackingFailedBeforeSave");
            CollectionAssert.AreEqual(oldLink, File.ReadAllBytes(fixture.LinkPath(original.Name)));
            CollectionAssert.AreEqual(oldIcon, File.ReadAllBytes(fixture.IconPath(original)));
            Assert.IsFalse(File.Exists(fixture.LinkPath(draft.Name)));

            string oldPath = fixture.LinkPath(original.Name);
            session.Save(draft);
            Assert.AreEqual(1, session.SerializationCount);
            Assert.AreEqual(draft.Name, original.Name);
            Assert.IsFalse(File.Exists(oldPath));
            Assert.IsFalse(File.Exists(fixture.LinkPath(historical.Name)));
            fixture.AssertShortcut(original);
        }

        [TestMethod]
        public void RepeatedRenamesDuringTrackingFailure_PreserveTheOldNameForARestartedService()
        {
            using var fixture = new ShortcutFixture();
            Project original = fixture.Project("Initially tracked");
            fixture.Apply(original);
            string journal = Directory.EnumerateFiles(fixture.IconFolder, "*.links.json").Single();
            string oldPath = fixture.LinkPath(original.Name);
            byte[] oldLink = File.ReadAllBytes(oldPath);
            byte[] originalIcon = File.ReadAllBytes(fixture.IconPath(original));
            Project intermediate = fixture.Project("Intermediate name", original.Id);
            Project final = fixture.Project("Final name", original.Id);

            using (var locked = File.OpenRead(journal))
            {
                var first = fixture.Service.ApplyWithResult(intermediate, original.Name);
                var second = fixture.OpenService().ApplyWithResult(final, intermediate.Name);

                Assert.AreEqual(WorkspaceShortcutStatus.Failed, first.Status);
                Assert.IsFalse(first.Created);
                Assert.AreEqual(WorkspaceShortcutStatus.Failed, second.Status);
                Assert.IsFalse(second.Created);
                Assert.IsFalse(File.Exists(fixture.LinkPath(intermediate.Name)));
                Assert.IsFalse(File.Exists(fixture.LinkPath(final.Name)));
                CollectionAssert.AreEqual(oldLink, File.ReadAllBytes(oldPath));
                CollectionAssert.AreEqual(originalIcon, File.ReadAllBytes(fixture.IconPath(original)));
            }

            var reopened = fixture.OpenService();
            Assert.IsTrue(reopened.GetPresence(final) is true);
            var retried = reopened.ApplyWithResult(final, intermediate.Name);
            Assert.AreEqual(WorkspaceShortcutStatus.Success, retried.Status);
            Assert.IsTrue(retried.Created);
            Assert.IsFalse(File.Exists(oldPath));
            Assert.IsFalse(File.Exists(fixture.LinkPath(intermediate.Name)));
            fixture.AssertShortcut(final);
        }

        [TestMethod]
        public void DeletedWorkspace_RestartRemovesDeniedLinkWithoutTouchingActiveWorkspace()
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project deleted = fixture.Project("Deleted workspace");
            Project active = fixture.Project("Still active");
            fixture.Apply(deleted);
            fixture.Apply(active);
            byte[] activeLink = File.ReadAllBytes(fixture.LinkPath(active.Name));
            byte[] activeIcon = File.ReadAllBytes(fixture.IconPath(active));
            string deletedPath = fixture.LinkPath(deleted.Name);
            session.ViewModel.Workspaces.Add(deleted);
            session.ViewModel.Workspaces.Add(active);

            using (var locked = File.OpenRead(deletedPath))
            {
                session.ViewModel.DeleteProject(deleted);

                Assert.AreEqual(1, session.SerializationCount);
                Assert.AreEqual(active.Name, session.SerializedNames.Single());
                AssertShortcutNotice(session.ViewModel, "ShortcutCleanupPendingAfterDelete");
                Assert.IsTrue(fixture.OpenService().RetryPendingRemovals([active.Id]));
                Assert.IsTrue(File.Exists(deletedPath));
                Assert.IsTrue(File.Exists(fixture.IconPath(deleted)));
            }

            Assert.IsFalse(fixture.OpenService().RetryPendingRemovals([active.Id]));
            Assert.IsFalse(File.Exists(deletedPath));
            Assert.IsFalse(File.Exists(fixture.IconPath(deleted)));
            CollectionAssert.AreEqual(activeLink, File.ReadAllBytes(fixture.LinkPath(active.Name)));
            CollectionAssert.AreEqual(activeIcon, File.ReadAllBytes(fixture.IconPath(active)));
        }

        [TestMethod]
        public void FailedWorkspaceDeletion_RestartLeavesTheStillActiveWorkspaceAlone()
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project project = fixture.Project("Database write failure");
            fixture.WriteShortcut(project);
            string linkPath = fixture.LinkPath(project.Name);
            byte[] link = File.ReadAllBytes(linkPath);
            byte[] icon = File.ReadAllBytes(fixture.IconPath(project));
            session.ViewModel.Workspaces.Add(project);
            session.FailSerialization = true;

            session.ViewModel.DeleteProject(project);

            Assert.AreEqual(0, session.SerializationCount);
            Assert.AreSame(project, session.ViewModel.Workspaces.Single());
            Assert.IsTrue(session.ViewModel.SaveFailed);
            Assert.IsFalse(fixture.OpenService().RetryPendingRemovals([project.Id]));
            CollectionAssert.AreEqual(link, File.ReadAllBytes(linkPath));
            CollectionAssert.AreEqual(icon, File.ReadAllBytes(fixture.IconPath(project)));

            session.FailSerialization = false;
            session.ViewModel.DeleteProject(project);
            Assert.AreEqual(1, session.SerializationCount);
            Assert.IsFalse(File.Exists(linkPath));
            Assert.IsFalse(File.Exists(fixture.IconPath(project)));
        }

        [TestMethod]
        [DataRow("direct")]
        [DataRow("parent")]
        [DataRow("short")]
        public void ForeignHistoricalLink_RetainsTheReferencedIconAcrossSavesAndRemoval(string pathForm)
        {
            using var fixture = new ShortcutFixture();
            Project original = fixture.Project("Old workspace name");
            fixture.Apply(original);
            string iconPath = fixture.IconPath(original);
            string referencedIcon = pathForm switch
            {
                "direct" => iconPath,
                "parent" => Path.Combine(fixture.IconFolder, "..", Path.GetFileName(fixture.IconFolder), Path.GetFileName(iconPath)),
                "short" => Path.Combine(GetShortIconDirectory(fixture.IconFolder), Path.GetFileName(iconPath)),
                _ => throw new ArgumentOutOfRangeException(nameof(pathForm)),
            };
            Project foreign = fixture.Project(original.Name);
            fixture.WriteShortcut(foreign, iconPath: referencedIcon);
            string foreignPath = fixture.LinkPath(foreign.Name);
            byte[] foreignLink = File.ReadAllBytes(foreignPath);
            var storedLink = WorkspaceShellLink.Inspect(foreignLink, Guid.Parse(original.Id));
            Assert.IsFalse(storedLink.Owned);
            Assert.IsTrue(string.Equals(referencedIcon, storedLink.IconPath, StringComparison.OrdinalIgnoreCase), $"Unexpected persisted icon path '{storedLink.IconPath}'.");
            CollectionAssert.AreEqual(File.ReadAllBytes(iconPath), File.ReadAllBytes(storedLink.IconPath));

            Project renamed = fixture.Project("Renamed workspace", original.Id);
            var rename = fixture.Service.ApplyWithResult(renamed, original.Name);
            Assert.AreEqual(WorkspaceShortcutStatus.Success, rename.Status);
            Assert.IsTrue(rename.Created);
            fixture.Apply(renamed);
            fixture.OpenService().Apply(renamed);
            byte[] protectedIcon = File.ReadAllBytes(iconPath);

            fixture.OpenService().RemoveWithResult(renamed);
            fixture.OpenService().RetryPendingRemovals([foreign.Id]);

            Assert.IsFalse(File.Exists(fixture.LinkPath(renamed.Name)));
            CollectionAssert.AreEqual(foreignLink, File.ReadAllBytes(foreignPath));
            CollectionAssert.AreEqual(protectedIcon, File.ReadAllBytes(iconPath));

            fixture.WriteShortcut(foreign);
            byte[] retargetedLink = File.ReadAllBytes(foreignPath);
            string foreignIcon = fixture.IconPath(foreign);

            // Equal image data in two separate ICO files still permits removing the original.
            File.Copy(iconPath, foreignIcon, overwrite: true);
            byte[] foreignIconBytes = File.ReadAllBytes(foreignIcon);
            Assert.IsFalse(fixture.OpenService().RetryPendingRemovals([foreign.Id]));
            Assert.IsFalse(File.Exists(iconPath), "The deleted workspace's icon should be released once the foreign shortcut points elsewhere.");
            CollectionAssert.AreEqual(retargetedLink, File.ReadAllBytes(foreignPath));
            CollectionAssert.AreEqual(foreignIconBytes, File.ReadAllBytes(foreignIcon));
            fixture.AssertShortcut(foreign);
        }

        [TestMethod]
        public void LegacyNameArray_RestartFindsTheOldLinkAndMigratesItsLaunchOptionsOnRename()
        {
            using var fixture = new ShortcutFixture();
            Guid workspaceId = Guid.NewGuid();
            Project historical = fixture.Project("Legacy desktop name", workspaceId.ToString("D"));
            fixture.WriteShortcut(historical, arguments: $"{workspaceId:B} 1", hotkey: 0x064B, showCommand: 3);
            string journal = Path.Combine(fixture.IconFolder, $"{workspaceId:B}.links.json");
            string legacy = JsonSerializer.Serialize(new[] { historical.Name });
            File.WriteAllText(journal, legacy);

            Project current = fixture.Project("Already saved workspace name", historical.Id);
            var restarted = fixture.OpenService();
            Assert.IsFalse(File.Exists(fixture.LinkPath(current.Name)));
            Assert.IsTrue(restarted.GetPresence(current) is true);
            Assert.AreEqual(legacy, File.ReadAllText(journal));

            Project renamed = fixture.Project("Renamed after restart", historical.Id);
            var result = restarted.ApplyWithResult(renamed, current.Name);
            Assert.AreEqual(WorkspaceShortcutStatus.Success, result.Status);
            Assert.IsTrue(result.Created);
            Assert.IsFalse(File.Exists(fixture.LinkPath(historical.Name)));
            fixture.AssertShortcut(renamed);
            fixture.AssertLaunchOptions(renamed, 0x064B, 3);

            using var migrated = JsonDocument.Parse(File.ReadAllText(journal));
            JsonElement record = migrated.RootElement;
            Assert.AreEqual(JsonValueKind.Object, record.ValueKind);
            Assert.AreEqual(historical.Id, record.GetProperty("ProjectId").GetString());
            Assert.AreEqual(renamed.Name, record.GetProperty("Names").EnumerateArray().Single().GetString());
            Assert.IsFalse(record.GetProperty("PendingRemoval").GetBoolean());

            Assert.AreEqual(WorkspaceShortcutStatus.Success, fixture.OpenService().RemoveWithResult(renamed).Status);
            Assert.IsFalse(File.Exists(fixture.LinkPath(renamed.Name)));
            Assert.IsFalse(File.Exists(fixture.IconPath(renamed)));
            Assert.IsFalse(File.Exists(journal));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void CreateWorkspace_ReportsWhetherTheRequestedShortcutWasCreated(bool occupied)
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project peer = fixture.Project("Requested shortcut name");
            byte[] peerLink = null;
            if (occupied)
            {
                fixture.WriteShortcut(peer);
                peerLink = File.ReadAllBytes(fixture.LinkPath(peer.Name));
                session.ViewModel.Workspaces.Add(peer);
            }

            session.ViewModel.EditProject(fixture.Project("New capture"), isNewlyCreated: true);
            Project draft = session.NavigatedProject;
            draft.Name = peer.Name;
            draft.IsShortcutNeeded = true;
            session.Save(draft);

            Project saved = session.ViewModel.Workspaces.Single(project => project.Id == draft.Id);
            Assert.AreEqual(1, session.SerializationCount);
            Assert.IsNotNull(session.LastCreateEvent);
            Assert.AreEqual(!occupied, session.LastCreateEvent.ShortcutCreated);
            if (occupied)
            {
                AssertShortcutNotice(session.ViewModel, "ShortcutNameConflictAfterSave");
                var result = fixture.Service.ApplyWithResult(draft);
                Assert.AreEqual(WorkspaceShortcutStatus.NameConflict, result.Status);
                Assert.IsFalse(result.Created);
                CollectionAssert.AreEqual(peerLink, File.ReadAllBytes(fixture.LinkPath(peer.Name)));
                Assert.IsFalse(File.Exists(fixture.IconPath(saved)));
            }
            else
            {
                Assert.IsFalse(session.ViewModel.HasShortcutNotice);
                fixture.AssertShortcut(saved);
                var update = fixture.Service.ApplyWithResult(saved);
                Assert.AreEqual(WorkspaceShortcutStatus.Success, update.Status);
                Assert.IsFalse(update.Created, "Updating an existing link is not a new shortcut creation.");
            }

            Assert.AreEqual(!occupied, session.Edit(saved).IsShortcutNeeded);
        }

        private static string GetShortIconDirectory(string directory)
        {
            var buffer = new StringBuilder(32768);
            uint length = GetShortPathName(directory, buffer, (uint)buffer.Capacity);
            Assert.IsTrue(length > 0 && length < buffer.Capacity, "GetShortPathNameW failed for the existing icon directory.");
            string shortDirectory = buffer.ToString();
            if (string.Equals(directory, shortDirectory, StringComparison.OrdinalIgnoreCase))
            {
                Assert.Inconclusive("The fixture volume does not provide an 8.3 alias for the icon directory or its ancestors.");
            }

            return shortDirectory;
        }

        private static void AssertShortcutNotice(MainViewModel viewModel, string key)
        {
            Assert.IsFalse(viewModel.SaveFailed);
            Assert.IsTrue(viewModel.HasShortcutNotice);
            Assert.AreEqual(ResourceLoaderInstance.ResourceLoader?.GetString(key) ?? key, viewModel.ShortcutNotice);
        }
    }
}
