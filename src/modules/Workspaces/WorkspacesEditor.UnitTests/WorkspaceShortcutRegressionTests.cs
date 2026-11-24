// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;
using WorkspacesCsharpLibrary.Data;
using WorkspacesEditor.Messages;
using WorkspacesEditor.Models;
using WorkspacesEditor.Services;
using WorkspacesEditor.Telemetry;
using WorkspacesEditor.Utils;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor.UnitTests
{
    [STATestClass]
    [TestCategory("Regression.Shortcuts")]
    [DoNotParallelize]
    public partial class WorkspaceShortcutRegressionTests
    {
        [TestMethod]
        public void Rename_PreservesShortcutOwnedBySameNameWorkspace()
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Shared name");
            Project peer = fixture.Project(original.Name);
            fixture.WriteShortcut(peer);
            byte[] peerLink = File.ReadAllBytes(fixture.LinkPath(peer.Name));
            session.ViewModel.Workspaces.Add(original);
            session.ViewModel.Workspaces.Add(peer);
            Project draft = session.Edit(original);
            draft.Name = "Renamed workspace";
            draft.IsShortcutNeeded = false;

            session.Save(draft);

            Assert.AreEqual(1, session.SerializationCount);
            CollectionAssert.AreEqual(peerLink, File.ReadAllBytes(fixture.LinkPath(peer.Name)));
            Assert.IsTrue(fixture.Exists(peer), "Renaming one workspace must retain the other workspace's shortcut.");
            Assert.IsTrue(File.Exists(fixture.IconPath(peer)));
        }

        [TestMethod]
        public void Remove_PreservesForeignShortcutAndIcon()
        {
            using var fixture = new ShortcutFixture();
            Project project = fixture.Project("Foreign shortcut");
            string foreignTarget = Path.Combine(fixture.Root, "AnotherLauncher.exe");
            File.WriteAllBytes(foreignTarget, Array.Empty<byte>());
            fixture.WriteShortcut(project, targetPath: foreignTarget);
            byte[] link = File.ReadAllBytes(fixture.LinkPath(project.Name));
            byte[] icon = File.ReadAllBytes(fixture.IconPath(project));
            Assert.IsFalse(fixture.Exists(project), "Matching arguments alone do not establish ownership.");

            fixture.Remove(project);

            CollectionAssert.AreEqual(link, File.ReadAllBytes(fixture.LinkPath(project.Name)));
            CollectionAssert.AreEqual(icon, File.ReadAllBytes(fixture.IconPath(project)));
        }

        [TestMethod]
        public void Remove_PreservesUnreadableShortcutAndIcon()
        {
            using var fixture = new ShortcutFixture();
            Project project = fixture.Project("Unreadable shortcut");
            fixture.WriteShortcut(project);
            string path = fixture.LinkPath(project.Name);
            byte[] link = File.ReadAllBytes(path);
            byte[] icon = File.ReadAllBytes(fixture.IconPath(project));

            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.IsFalse(fixture.Exists(project));
                fixture.Remove(project);
            }

            CollectionAssert.AreEqual(link, File.ReadAllBytes(path));
            CollectionAssert.AreEqual(icon, File.ReadAllBytes(fixture.IconPath(project)));
        }

        [TestMethod]
        public void Remove_RetriesAfterTransientReaderReleasesShortcut()
        {
            using var fixture = new ShortcutFixture();
            Project project = fixture.Project("Read during deletion");
            fixture.WriteShortcut(project);
            string path = fixture.LinkPath(project.Name);
            Guid workspaceId = Guid.Parse(project.Id);

            ReleaseReaderOnFirstRetry(path, FileShare.Read, budget => WorkspaceShortcutFile.Remove(path, workspaceId, null, budget));

            Assert.IsFalse(File.Exists(path));
        }

        [TestMethod]
        public void Inspect_RetriesAfterExclusiveReaderReleasesShortcut()
        {
            using var fixture = new ShortcutFixture();
            Project project = fixture.Project("Read during inspection");
            fixture.WriteShortcut(project);
            string path = fixture.LinkPath(project.Name);
            Guid workspaceId = Guid.Parse(project.Id);
            (bool Owned, string IconPath) result = default;

            ReleaseReaderOnFirstRetry(path, FileShare.None, budget => result = WorkspaceShortcutFile.Inspect(path, workspaceId, budget));

            Assert.IsTrue(result.Owned);
            Assert.AreEqual(fixture.IconPath(project), result.IconPath);
        }

        [TestMethod]
        public void Remove_UnlinksNameWhileDeleteSharingObserverCanStillRead()
        {
            using var fixture = new ShortcutFixture();
            Project project = fixture.Project("Observed workspace");
            fixture.WriteShortcut(project);
            string path = fixture.LinkPath(project.Name);
            byte[] originalBytes = File.ReadAllBytes(path);
            using (var observer = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                const uint SupportsPosixUnlinkRename = 0x400;
                Assert.IsTrue(GetVolumeInformationByHandleW(observer.SafeFileHandle, IntPtr.Zero, 0, out _, out _, out uint flags, IntPtr.Zero, 0));

                fixture.Remove(project);

                if ((flags & SupportsPosixUnlinkRename) != 0)
                {
                    Assert.IsFalse(
                        Directory.EnumerateFiles(fixture.DesktopFolder).Contains(path, StringComparer.OrdinalIgnoreCase),
                        "A compatible observer must not retain the directory entry on a volume supporting POSIX unlink.");
                }

                byte[] observedBytes = new byte[originalBytes.Length];
                observer.ReadExactly(observedBytes);
                CollectionAssert.AreEqual(originalBytes, observedBytes);
            }

            Assert.IsFalse(File.Exists(path));
            Assert.IsFalse(File.Exists(fixture.IconPath(project)));
        }

        [TestMethod]
        public void ShortcutOwnership_UsesArgumentsInsteadOfStaleDescription()
        {
            using var fixture = new ShortcutFixture();
            Project original = fixture.Project("Shared name");
            Project peer = fixture.Project(original.Name);
            fixture.WriteShortcut(original, arguments: peer.Id + " 1");
            Assert.IsTrue(fixture.Exists(peer), "The arguments identify the peer despite the original workspace's description.");

            fixture.AssertUnownedShortcutPreserved(original);
        }

        [TestMethod]
        public void ShortcutOwnership_RejectsAmbiguousWorkspaceArguments()
        {
            using var fixture = new ShortcutFixture();
            Project project = fixture.Project("Ambiguous shortcut");
            fixture.WriteShortcut(project, arguments: project.Id + " 1 " + Guid.NewGuid().ToString("B"));

            fixture.AssertUnownedShortcutPreserved(project);
        }

        [TestMethod]
        public void ShortcutOwnership_PreservesMalformedLink()
        {
            using var fixture = new ShortcutFixture();
            Project project = fixture.Project("Malformed shortcut");
            fixture.WriteIcon(project);
            File.WriteAllBytes(fixture.LinkPath(project.Name), new byte[] { 1, 2, 3, 4 });

            fixture.AssertUnownedShortcutPreserved(project);
        }

        [TestMethod]
        public void ShortcutOwnership_PreservesOversizedLinkAndIcon()
        {
            using var fixture = new ShortcutFixture();
            Project project = fixture.Project("Oversized shortcut");
            fixture.WriteShortcut(project);
            Assert.IsTrue(fixture.Exists(project));

            // Even a valid Shell Link header does not establish ownership beyond the size limit.
            using (var file = File.OpenWrite(fixture.LinkPath(project.Name)))
            {
                file.SetLength((4 * 1024 * 1024) + 1);
            }

            fixture.AssertUnownedShortcutPreserved(project);
        }

        [TestMethod]
        public void ShortcutOwnership_AcceptsCanonicalGuidCaseAndPreviousInstallation()
        {
            using var fixture = new ShortcutFixture();
            Guid id = Guid.NewGuid();
            Project project = fixture.Project("Moved installation", id.ToString("D"));
            string previousLauncher = Path.Combine(fixture.Root, "Previous installation", "PowerToys.WorkspacesLauncher.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(previousLauncher));
            File.WriteAllBytes(previousLauncher, Array.Empty<byte>());
            fixture.WriteShortcut(project, previousLauncher, id.ToString("B").ToUpperInvariant() + " 1");
            File.Delete(previousLauncher);
            Assert.IsTrue(fixture.Exists(project), "GUID value and launcher identity survive casing and installation-directory changes.");

            fixture.Apply(project);

            fixture.AssertShortcut(project);
        }

        [TestMethod]
        [DataRow("D")]
        [DataRow("N")]
        [DataRow("P")]
        [DataRow("X")]
        public void ShortcutOwnership_PreservesNoncanonicalGuidArguments(string format)
        {
            using var fixture = new ShortcutFixture();
            Project project = fixture.Project("Noncanonical shortcut");
            fixture.WriteShortcut(project, arguments: Guid.Parse(project.Id).ToString(format) + " 1");

            fixture.AssertUnownedShortcutPreserved(project);
        }

        [TestMethod]
        public void UnicodeShortcut_PreservesForeignLinkAndStoresExactUnicodePaths()
        {
            using var fixture = new ShortcutFixture();
            Project project = fixture.Project("工作区 🖥");
            Project peer = fixture.Project("作業領域 🌙");
            fixture.WriteShortcut(peer);
            byte[] peerLink = File.ReadAllBytes(fixture.LinkPath(peer.Name));

            fixture.Apply(project);

            fixture.AssertShortcut(project);
            CollectionAssert.AreEqual(peerLink, File.ReadAllBytes(fixture.LinkPath(peer.Name)));
            Assert.AreEqual(2, Directory.EnumerateFiles(fixture.DesktopFolder, "*.lnk").Count());

            fixture.Remove(project);
            Assert.IsFalse(File.Exists(fixture.LinkPath(project.Name)));
            CollectionAssert.AreEqual(peerLink, File.ReadAllBytes(fixture.LinkPath(peer.Name)));
        }

        [TestMethod]
        public void ShortcutNameWithDirectorySeparatorPreservesSubfolderContents()
        {
            using var fixture = new ShortcutFixture();
            string subfolder = Path.Combine(fixture.DesktopFolder, "Category");
            Directory.CreateDirectory(subfolder);
            Project project = fixture.Project(Path.Combine("Category", "Workspace"));

            fixture.Apply(project);

            Assert.IsFalse(fixture.Exists(project));
            Assert.IsFalse(Directory.EnumerateFileSystemEntries(subfolder).Any());
            Assert.IsFalse(File.Exists(fixture.IconPath(project)));

            fixture.WriteShortcut(project);
            byte[] link = File.ReadAllBytes(fixture.LinkPath(project.Name));
            byte[] icon = File.ReadAllBytes(fixture.IconPath(project));
            fixture.Remove(project);

            CollectionAssert.AreEqual(link, File.ReadAllBytes(fixture.LinkPath(project.Name)));
            CollectionAssert.AreEqual(icon, File.ReadAllBytes(fixture.IconPath(project)));
        }

        [TestMethod]
        public void Remove_MissingShortcutDoesNotCreateIconDirectory()
        {
            using var fixture = new ShortcutFixture();
            Project project = fixture.Project("Missing shortcut");
            Assert.IsFalse(Directory.Exists(fixture.IconFolder));

            fixture.Remove(project);

            Assert.IsFalse(fixture.Exists(project));
            Assert.IsFalse(Directory.Exists(fixture.IconFolder));
        }

        [TestMethod]
        public void Rename_ReplacesOwnedShortcutAndRetainsWorkspaceIcon()
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Original workspace");
            fixture.WriteShortcut(original, hotkey: 0x064B, showCommand: 3); // Ctrl+Alt+K, maximized
            string oldPath = fixture.LinkPath(original.Name);
            string iconPath = fixture.IconPath(original);
            session.ViewModel.Workspaces.Add(original);
            Project draft = session.Edit(original);
            draft.Name = "Renamed workspace";

            session.Save(draft);

            Assert.AreEqual(1, session.SerializationCount);
            CollectionAssert.Contains(session.SerializedNames, draft.Name);
            Assert.IsFalse(File.Exists(oldPath));
            fixture.AssertShortcut(draft);
            fixture.AssertLaunchOptions(draft, 0x064B, 3);
            Assert.AreEqual(iconPath, fixture.IconPath(draft));
        }

        [TestMethod]
        public void Rename_ReadOnlyOldLinkDoesNotPreventPersistenceOrNewShortcut()
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Read only original");
            fixture.WriteShortcut(original);
            string oldPath = fixture.LinkPath(original.Name);
            File.SetAttributes(oldPath, File.GetAttributes(oldPath) | FileAttributes.ReadOnly);
            session.ViewModel.Workspaces.Add(original);
            Project draft = session.Edit(original);
            draft.Name = "Renamed workspace";
            Assert.IsTrue(draft.IsShortcutNeeded);

            session.Save(draft);

            Assert.AreEqual(1, session.SerializationCount);
            CollectionAssert.Contains(session.SerializedNames, draft.Name);
            Assert.IsTrue(File.Exists(oldPath), "The read-only original link should remain after cleanup fails.");
            fixture.AssertShortcut(draft);
            Assert.IsTrue(File.Exists(fixture.IconPath(draft)), "Both links still refer to the same workspace icon.");

            File.SetAttributes(oldPath, File.GetAttributes(oldPath) & ~FileAttributes.ReadOnly);
            fixture.OpenService().Remove(original);
            Assert.IsFalse(File.Exists(oldPath));
            Assert.IsFalse(File.Exists(fixture.LinkPath(draft.Name)));
            Assert.IsFalse(File.Exists(fixture.IconPath(original)));
        }

        [TestMethod]
        public void Rename_DestinationOwnedByAnotherWorkspacePreservesBothLinksAndIcons()
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Original workspace");
            Project peer = fixture.Project("Occupied name");
            fixture.WriteShortcut(original);
            fixture.WriteShortcut(peer);
            string oldPath = fixture.LinkPath(original.Name);
            byte[] oldLink = File.ReadAllBytes(oldPath);
            byte[] peerLink = File.ReadAllBytes(fixture.LinkPath(peer.Name));
            byte[] icon = File.ReadAllBytes(fixture.IconPath(original));
            session.ViewModel.Workspaces.Add(original);
            session.ViewModel.Workspaces.Add(peer);
            Project draft = session.Edit(original);
            draft.Name = peer.Name;

            session.Save(draft);

            Assert.AreEqual(1, session.SerializationCount);
            CollectionAssert.AreEqual(oldLink, File.ReadAllBytes(oldPath));
            CollectionAssert.AreEqual(peerLink, File.ReadAllBytes(fixture.LinkPath(peer.Name)));
            CollectionAssert.AreEqual(icon, File.ReadAllBytes(fixture.IconPath(original)));
            Assert.IsTrue(fixture.Exists(peer));

            var reopened = fixture.OpenService();
            Assert.IsTrue(reopened.Exists(original), "The old owned shortcut must remain discoverable after a restart.");
            reopened.Remove(original);
            Assert.IsFalse(File.Exists(oldPath));
            CollectionAssert.AreEqual(peerLink, File.ReadAllBytes(fixture.LinkPath(peer.Name)));
            Assert.IsTrue(fixture.Exists(peer));
        }

        [TestMethod]
        public void Rename_DestinationDirectoryPreservesOldLinkAndIcon()
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Original workspace");
            fixture.WriteShortcut(original);
            string oldPath = fixture.LinkPath(original.Name);
            byte[] oldLink = File.ReadAllBytes(oldPath);
            session.ViewModel.Workspaces.Add(original);
            Project draft = session.Edit(original);
            draft.Name = "Blocked destination";
            Directory.CreateDirectory(fixture.LinkPath(draft.Name));

            session.Save(draft);

            Assert.AreEqual(1, session.SerializationCount);
            CollectionAssert.AreEqual(oldLink, File.ReadAllBytes(oldPath));
            Assert.IsTrue(File.Exists(fixture.IconPath(original)));
            Assert.IsTrue(Directory.Exists(fixture.LinkPath(draft.Name)));
        }

        [TestMethod]
        public void Rename_NewLinkSaveFailureAfterPreflightPreservesOldLinkAndIcon()
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Original workspace");
            fixture.WriteShortcut(original);
            string oldPath = fixture.LinkPath(original.Name);
            byte[] oldLink = File.ReadAllBytes(oldPath);
            Project destination = fixture.Project("Renamed workspace", original.Id);
            fixture.WriteShortcut(destination);
            string newPath = fixture.LinkPath(destination.Name);
            byte[] destinationLink = File.ReadAllBytes(newPath);
            byte[] oldIcon = File.ReadAllBytes(fixture.IconPath(original));
            session.ViewModel.Workspaces.Add(original);
            Project draft = session.Edit(original);
            draft.Name = destination.Name;

            // Readers can establish ownership, but publishing a replacement is denied.
            using (var locked = new FileStream(newPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                session.Save(draft);
            }

            Assert.AreEqual(1, session.SerializationCount);
            CollectionAssert.Contains(session.SerializedNames, draft.Name);
            CollectionAssert.AreEqual(oldLink, File.ReadAllBytes(oldPath));
            CollectionAssert.AreEqual(destinationLink, File.ReadAllBytes(newPath));
            CollectionAssert.AreEqual(oldIcon, File.ReadAllBytes(fixture.IconPath(original)));
        }

        [TestMethod]
        public void Rename_ChangingOnlyCaseKeepsShortcutAndIcon()
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Case workspace");
            fixture.WriteShortcut(original);
            session.ViewModel.Workspaces.Add(original);
            Project draft = session.Edit(original);
            draft.Name = "CASE WORKSPACE";

            session.Save(draft);

            Assert.AreEqual(1, session.SerializationCount);
            fixture.AssertShortcut(draft);
            Assert.AreEqual(draft.Name + ".lnk", Path.GetFileName(Directory.EnumerateFiles(fixture.DesktopFolder, "*.lnk").Single()));
        }

        [TestMethod]
        public void Rename_ChangingOnlyCaseRetriesAfterTransientReaderReleasesShortcut()
        {
            using var fixture = new ShortcutFixture();
            Project project = fixture.Project("Read during rename");
            fixture.WriteShortcut(project);
            string originalPath = fixture.LinkPath(project.Name);
            project.Name = "READ DURING RENAME";
            string newPath = fixture.LinkPath(project.Name);
            Guid workspaceId = Guid.Parse(project.Id);
            string iconPath = fixture.IconPath(project);

            ReleaseReaderOnFirstRetry(originalPath, FileShare.Read, budget => WorkspaceShortcutFile.Update(newPath, fixture.LauncherPath, workspaceId, iconPath, () => { }, budget));

            Assert.AreEqual(project.Name + ".lnk", Path.GetFileName(Directory.EnumerateFiles(fixture.DesktopFolder, "*.lnk").Single()));
            fixture.AssertShortcut(project);
        }

        [TestMethod]
        public void Rename_IconCommitFailureRestoresOriginalLinkBytesAndCasing()
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Mixed case workspace");
            string previousLauncher = Path.Combine(fixture.Root, "Older installation", "PowerToys.WorkspacesLauncher.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(previousLauncher));
            File.WriteAllBytes(previousLauncher, Array.Empty<byte>());
            fixture.WriteShortcut(original, targetPath: previousLauncher, hotkey: 0x064B); // Ctrl+Alt+K
            string oldPath = fixture.LinkPath(original.Name);
            string iconPath = fixture.IconPath(original);
            byte[] oldLink = File.ReadAllBytes(oldPath);
            byte[] oldIcon = File.ReadAllBytes(iconPath);
            session.ViewModel.Workspaces.Add(original);
            Project draft = session.Edit(original);
            Assert.IsTrue(draft.IsShortcutNeeded);
            draft.Name = "MIXED CASE WORKSPACE";

            using (var lockedIcon = new FileStream(iconPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                session.Save(draft);
            }

            Assert.AreEqual(draft.Name, session.SerializedNames.Single());
            Assert.AreEqual(Path.GetFileName(oldPath), Path.GetFileName(Directory.EnumerateFiles(fixture.DesktopFolder, "*.lnk").Single()));
            CollectionAssert.AreEqual(oldLink, File.ReadAllBytes(oldPath));
            CollectionAssert.AreEqual(oldIcon, File.ReadAllBytes(iconPath));

            fixture.OpenService().Apply(original);
            fixture.AssertShortcut(original);
            Assert.AreEqual(draft.Name + ".lnk", Path.GetFileName(Directory.EnumerateFiles(fixture.DesktopFolder, "*.lnk").Single()));
        }

        [TestMethod]
        public void Rename_ToShortAliasKeepsSingleWorkingLink()
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Long workspace name with spaces");
            fixture.WriteShortcut(original);
            string originalPath = fixture.LinkPath(original.Name);
            var buffer = new StringBuilder(32768);
            uint length = GetShortPathName(originalPath, buffer, (uint)buffer.Capacity);
            Assert.IsTrue(length > 0 && length < buffer.Capacity);
            string alias = Path.GetFileNameWithoutExtension(buffer.ToString());
            if (string.Equals(original.Name, alias, StringComparison.OrdinalIgnoreCase))
            {
                Assert.Inconclusive("The fixture volume does not provide an 8.3 alias.");
            }

            session.ViewModel.Workspaces.Add(original);
            Project draft = session.Edit(original);
            draft.Name = alias;
            session.Save(draft);

            Assert.AreEqual(1, session.SerializationCount);
            fixture.AssertShortcut(draft);
            Assert.AreEqual(1, Directory.EnumerateFiles(fixture.DesktopFolder, "*.lnk").Count());
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Rename_WithShortcutDisabledRemovesOnlyOwnedFiles(bool ownsShortcut)
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Original workspace");
            Project owner = ownsShortcut ? original : fixture.Project(original.Name);
            fixture.WriteShortcut(owner);
            fixture.WriteIcon(original);
            string oldPath = fixture.LinkPath(original.Name);
            byte[] oldLink = File.ReadAllBytes(oldPath);
            session.ViewModel.Workspaces.Add(original);
            Project draft = session.Edit(original);
            draft.Name = "Renamed workspace";
            draft.IsShortcutNeeded = false;

            session.Save(draft);

            Assert.AreEqual(1, session.SerializationCount);
            Assert.IsFalse(original.IsShortcutNeeded);
            Assert.IsFalse(File.Exists(fixture.LinkPath(draft.Name)));
            Assert.AreEqual(!ownsShortcut, File.Exists(oldPath));
            Assert.AreEqual(!ownsShortcut, File.Exists(fixture.IconPath(original)));
            if (!ownsShortcut)
            {
                CollectionAssert.AreEqual(oldLink, File.ReadAllBytes(oldPath));
                Assert.IsTrue(fixture.Exists(owner));
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void DeleteProject_RemovesOnlyOwnedShortcutAndIcon(bool ownsShortcut)
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Deleted workspace");
            Project owner = ownsShortcut ? original : fixture.Project(original.Name);
            fixture.WriteShortcut(owner);
            fixture.WriteIcon(original);
            string path = fixture.LinkPath(original.Name);
            byte[] link = File.ReadAllBytes(path);
            session.ViewModel.Workspaces.Add(original);

            session.ViewModel.DeleteProject(original);

            Assert.AreEqual(1, session.SerializationCount);
            Assert.AreEqual(0, session.ViewModel.Workspaces.Count);
            Assert.AreEqual(!ownsShortcut, File.Exists(path));
            Assert.AreEqual(!ownsShortcut, File.Exists(fixture.IconPath(original)));
            if (!ownsShortcut)
            {
                CollectionAssert.AreEqual(link, File.ReadAllBytes(path));
                Assert.IsTrue(fixture.Exists(owner));
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Revert_PreservesUnsavedShortcutChoiceThroughSave(bool shortcutNeeded)
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Edited workspace");
            original.IsShortcutNeeded = !shortcutNeeded;
            if (original.IsShortcutNeeded)
            {
                fixture.WriteShortcut(original);
            }

            session.ViewModel.Workspaces.Add(original);
            Project draft = session.Edit(original);
            draft.IsShortcutNeeded = shortcutNeeded;
            session.CapturedShortcutChoice = !shortcutNeeded;
            await session.ViewModel.LaunchAndEditAsync(draft);
            await session.ViewModel.SnapWorkspaceAsync();
            Project recaptured = session.NavigatedProject;
            Assert.IsTrue(recaptured.IsRevertEnabled);

            session.ViewModel.RevertLaunch();

            Project restored = session.NavigatedProject;
            Assert.AreNotSame(recaptured, restored);
            Assert.AreEqual(draft.Applications[0].Id, restored.Applications[0].Id);
            Assert.AreEqual(shortcutNeeded, restored.IsShortcutNeeded);
            session.Save(restored);
            Assert.AreEqual(1, session.SerializationCount);
            Assert.AreEqual(shortcutNeeded, session.SerializedShortcutChoices.Single());
            Assert.AreEqual(shortcutNeeded, original.IsShortcutNeeded);
            Assert.AreEqual(shortcutNeeded, fixture.Exists(original));
            if (shortcutNeeded)
            {
                fixture.AssertShortcut(original);
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Recapture_PreservesUnsavedShortcutChoiceThroughSave(bool shortcutNeeded)
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Edited workspace");
            original.IsShortcutNeeded = !shortcutNeeded;
            if (original.IsShortcutNeeded)
            {
                fixture.WriteShortcut(original);
            }

            session.ViewModel.Workspaces.Add(original);
            Project draft = session.Edit(original);
            draft.IsShortcutNeeded = shortcutNeeded;
            session.CapturedShortcutChoice = !shortcutNeeded;

            await session.ViewModel.LaunchAndEditAsync(draft);
            await session.ViewModel.SnapWorkspaceAsync();

            Project recaptured = session.NavigatedProject;
            Assert.AreNotSame(draft, recaptured);
            Assert.AreNotEqual(draft.Applications[0].Id, recaptured.Applications[0].Id);
            Assert.AreEqual(draft.Id, recaptured.Id);
            Assert.AreEqual(draft.Name, recaptured.Name);
            Assert.IsTrue(recaptured.IsRevertEnabled);
            Assert.AreEqual(shortcutNeeded, recaptured.IsShortcutNeeded);
            session.Save(recaptured);
            Assert.AreEqual(1, session.SerializationCount);
            Assert.AreEqual(shortcutNeeded, session.SerializedShortcutChoices.Single());
            Assert.AreEqual(shortcutNeeded, original.IsShortcutNeeded);
            Assert.AreEqual(shortcutNeeded, fixture.Exists(original));
            if (shortcutNeeded)
            {
                fixture.AssertShortcut(original);
            }
        }

        [TestMethod]
        public void EditProject_InitializesShortcutChoiceFromDiskForExistingAndNewWorkspaces()
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Existing workspace");
            session.ViewModel.Workspaces.Add(original);

            session.ViewModel.EditProject(original);
            Assert.IsFalse(session.NavigatedProject.IsShortcutNeeded);
            Assert.IsFalse(Directory.Exists(fixture.IconFolder), "Checking presence must not create the icon directory.");

            fixture.WriteShortcut(original);
            session.ViewModel.EditProject(original);
            Assert.IsTrue(session.NavigatedProject.IsShortcutNeeded);

            File.Delete(fixture.LinkPath(original.Name));
            session.ViewModel.EditProject(original);
            Assert.IsFalse(session.NavigatedProject.IsShortcutNeeded, "Reopening editing must notice external shortcut deletion.");

            Project captured = fixture.Project("Captured workspace");
            session.ViewModel.EditProject(captured, true);
            Assert.IsFalse(session.NavigatedProject.IsShortcutNeeded);
            Assert.AreNotEqual("Captured workspace", session.NavigatedProject.Name);
            Assert.AreEqual(0, session.SerializationCount, "Opening editing must not save workspace data.");
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern uint GetShortPathName(string longPath, StringBuilder shortPath, uint bufferLength);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetVolumeInformationByHandleW(
            SafeFileHandle file,
            IntPtr volumeName,
            uint volumeNameCapacity,
            out uint serialNumber,
            out uint maximumComponentLength,
            out uint fileSystemFlags,
            IntPtr fileSystemName,
            uint fileSystemNameCapacity);

        private static void ReleaseReaderOnFirstRetry(string path, FileShare share, Action<WorkspaceShortcutFile.SharingRetryBudget> operation)
        {
            using var reader = new FileStream(path, FileMode.Open, FileAccess.Read, share);
            int waits = 0;
            var budget = new WorkspaceShortcutFile.SharingRetryBudget(_ =>
            {
                waits++;
                reader.Dispose();
            });

            operation(budget);
            Assert.AreEqual(1, waits, "The first sharing violation should be cleared on retry.");
        }

        private sealed class ViewModelSession : IDisposable
        {
            internal ViewModelSession(ShortcutFixture fixture)
            {
                string captureFile = Path.Combine(fixture.Root, "capture.json");
                var editorIO = new WorkspacesEditorIO(captureFile, (startInfo, timeout) =>
                {
                    if (Path.GetFileName(startInfo.FileName) == "PowerToys.WorkspacesSnapshotTool.exe")
                    {
                        string captureId = startInfo.Arguments.Split(' ')[0];
                        var capture = fixture.CreateProjectData("Captured workspace", captureId);
                        capture.IsShortcutNeeded = CapturedShortcutChoice;
                        File.WriteAllText(captureFile + "." + captureId, new ProjectData().Serialize(capture));
                    }

                    return Task.FromResult(true);
                });

                ViewModel = new MainViewModel(
                    editorIO,
                    new WorkspacesSettings(),
                    _ => { },
                    fixture.Service,
                    projects =>
                    {
                        if (FailSerialization)
                        {
                            throw new IOException("The fixture's workspace storage is unavailable.");
                        }

                        SerializationCount++;
                        SerializedNames = projects.Select(project => project.Name).ToArray();
                        SerializedShortcutChoices = projects.Select(project => project.IsShortcutNeeded).ToArray();
                    },
                    _ => { },
                    created => LastCreateEvent = created);
                StrongReferenceMessenger.Default.Register<NavigateToEditorMessage>(this, (recipient, message) => ((ViewModelSession)recipient).NavigatedProject = message.Project);
            }

            internal MainViewModel ViewModel { get; }

            internal bool CapturedShortcutChoice { get; set; }

            internal bool FailSerialization { get; set; }

            internal CreateEvent LastCreateEvent { get; private set; }

            internal int SerializationCount { get; private set; }

            internal string[] SerializedNames { get; private set; } = Array.Empty<string>();

            internal bool[] SerializedShortcutChoices { get; private set; } = Array.Empty<bool>();

            internal Project NavigatedProject { get; private set; }

            internal Project Edit(Project original)
            {
                ViewModel.EditProject(original);
                Assert.IsNotNull(NavigatedProject);
                Assert.AreNotSame(original, NavigatedProject);
                return NavigatedProject;
            }

            internal void Save(Project draft)
            {
                Assert.IsTrue(draft.CanBeSaved);
                ViewModel.SaveOrAddProject(draft);
            }

            public void Dispose()
            {
                StrongReferenceMessenger.Default.UnregisterAll(this);
                ViewModel.Dispose();
                GC.SuppressFinalize(this);
            }
        }

        private sealed class ShortcutFixture : IDisposable
        {
            private readonly string _temporaryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));

            internal ShortcutFixture()
            {
                Root = Path.Combine(_temporaryRoot, "PowerToys-ShortcutTests-" + Guid.NewGuid().ToString("N"));
                Assert.IsFalse(Directory.Exists(Root), "The fixture must not reuse a directory.");
                Directory.CreateDirectory(Root);
                DesktopFolder = Path.Combine(Root, "Desktop");
                Directory.CreateDirectory(DesktopFolder);
                IconFolder = Path.Combine(Root, "Icons with spaces, commas 图标 🎨");
                string launcherFolder = Path.Combine(Root, "Launcher 启动 🚀");
                Directory.CreateDirectory(launcherFolder);
                LauncherPath = Path.Combine(launcherFolder, "PowerToys.WorkspacesLauncher.exe");
                File.WriteAllBytes(LauncherPath, Array.Empty<byte>());
                Service = OpenService();
            }

            internal string Root { get; }

            internal string DesktopFolder { get; }

            internal string IconFolder { get; }

            internal string LauncherPath { get; }

            internal WorkspaceShortcutService Service { get; }

            internal WorkspaceShortcutService OpenService() => new(DesktopFolder, IconFolder, LauncherPath);

            internal string LinkPath(string name) => Path.Combine(DesktopFolder, name + ".lnk");

            internal string IconPath(Project project) => Path.Combine(IconFolder, project.Id + ".ico");

            internal bool Exists(Project project) => Service.Exists(project);

            internal void Remove(Project project) => Service.Remove(project);

            internal Project Project(string name, string id = null) => new(CreateProjectData(name, id));

            internal ProjectWrapper CreateProjectData(string name, string id = null) => new()
            {
                Id = id ?? Guid.NewGuid().ToString("B"),
                Name = name,
                IsShortcutNeeded = true,
                Applications = new List<ApplicationWrapper>
                {
                    new()
                    {
                        Id = Guid.NewGuid().ToString("B"),
                        Application = "Shortcut test fixture",
                        ApplicationPath = LauncherPath,
                        Title = string.Empty,
                        PackageFullName = string.Empty,
                        AppUserModelId = string.Empty,
                        PwaAppId = string.Empty,
                        CommandLineArguments = string.Empty,
                        Version = string.Empty,
                        Position = new ApplicationWrapper.WindowPositionWrapper { Width = 640, Height = 480 },
                        Monitor = 1,
                    },
                },
                MonitorConfiguration = new List<MonitorConfigurationWrapper>
                {
                    new()
                    {
                        Id = "Fixture display",
                        InstanceId = "Fixture display",
                        MonitorNumber = 1,
                        Dpi = 96,
                        MonitorRectDpiAware = new MonitorConfigurationWrapper.MonitorRectWrapper { Width = 1920, Height = 1080 },
                        MonitorRectDpiUnaware = new MonitorConfigurationWrapper.MonitorRectWrapper { Width = 1920, Height = 1080 },
                    },
                },
            };

            internal void WriteIcon(Project project)
            {
                Directory.CreateDirectory(IconFolder);
                File.WriteAllBytes(IconPath(project), new byte[] { 1, 2, 3, 4 });
            }

            internal void WriteShortcut(Project project, string targetPath = null, string arguments = null, short hotkey = 0, int showCommand = 1, string iconPath = null)
            {
                WriteIcon(project);
                WithShortcut(LinkPath(project.Name), write: true, shortcut =>
                {
                    shortcut.SetPath(targetPath ?? LauncherPath);
                    shortcut.SetWorkingDirectory(Path.GetDirectoryName(targetPath ?? LauncherPath));
                    shortcut.SetArguments(arguments ?? project.Id + " 1");
                    shortcut.SetDescription("Project Launcher " + project.Id);
                    shortcut.SetHotkey(hotkey);
                    shortcut.SetShowCmd(showCommand);
                    shortcut.SetIconLocation(iconPath ?? IconPath(project), 0);
                });
                Assert.IsTrue(File.Exists(LinkPath(project.Name)), "The real shortcut fixture was not created.");
            }

            internal void Apply(Project project) => Service.Apply(project);

            internal void AssertLaunchOptions(Project project, short expectedHotkey, int expectedShowCommand)
            {
                WithShortcut(LinkPath(project.Name), write: false, shortcut =>
                {
                    shortcut.GetHotkey(out short hotkey);
                    shortcut.GetShowCmd(out int showCommand);
                    Assert.AreEqual(expectedHotkey, hotkey);
                    Assert.AreEqual(expectedShowCommand, showCommand);
                });
            }

            internal void AssertUnownedShortcutPreserved(Project project)
            {
                string path = LinkPath(project.Name);
                byte[] link = File.ReadAllBytes(path);
                byte[] icon = File.ReadAllBytes(IconPath(project));
                Assert.IsFalse(Exists(project));

                Apply(project);
                Remove(project);

                Assert.IsTrue(File.Exists(path), "An unowned or unreadable shortcut must not be deleted.");
                CollectionAssert.AreEqual(link, File.ReadAllBytes(path));
                CollectionAssert.AreEqual(icon, File.ReadAllBytes(IconPath(project)));
            }

            internal void AssertShortcut(Project project)
            {
                Assert.IsTrue(File.Exists(LinkPath(project.Name)), "The shortcut was not created.");
                Assert.IsTrue(File.Exists(IconPath(project)), "The workspace icon was not retained.");
                Assert.IsTrue(Exists(project), "The shortcut does not belong to this workspace.");
                WithShortcut(LinkPath(project.Name), write: false, shortcut =>
                {
                    const int RawPath = 4;
                    var value = new StringBuilder(32768);
                    shortcut.GetPath(value, value.Capacity, IntPtr.Zero, RawPath);
                    Assert.IsTrue(string.Equals(LauncherPath, value.ToString(), StringComparison.OrdinalIgnoreCase), $"Unexpected shortcut target '{value}'.");
                    value.Clear();
                    shortcut.GetWorkingDirectory(value, value.Capacity);
                    Assert.IsTrue(string.Equals(Path.GetDirectoryName(LauncherPath), value.ToString(), StringComparison.OrdinalIgnoreCase), $"Unexpected working directory '{value}'.");
                    value.Clear();
                    shortcut.GetArguments(value, value.Capacity);
                    Assert.AreEqual(Guid.Parse(project.Id).ToString("B") + " 1", value.ToString());
                    value.Clear();
                    shortcut.GetIconLocation(value, value.Capacity, out int index);
                    Assert.IsTrue(string.Equals(IconPath(project), value.ToString(), StringComparison.OrdinalIgnoreCase), $"Unexpected icon path '{value}'.");
                    Assert.AreEqual(0, index);
                });
            }

            public void Dispose()
            {
                Assert.IsTrue(string.Equals(Path.GetDirectoryName(Path.GetFullPath(Root)), _temporaryRoot, StringComparison.OrdinalIgnoreCase));
                DeleteDirectory(Root);
                GC.SuppressFinalize(this);
            }

            // Read the persisted Unicode fields without running the editor's shortcut service.
            private static void WithShortcut(string path, bool write, Action<WorkspaceShellLink.IShellLinkW> action)
            {
                var shortcut = (WorkspaceShellLink.IShellLinkW)new WorkspaceShellLink.ShellLink();
                try
                {
                    var file = (IPersistFile)shortcut;
                    if (!write)
                    {
                        file.Load(path, 0);
                    }

                    action(shortcut);
                    if (write)
                    {
                        file.Save(path, true);
                    }
                }
                finally
                {
                    Marshal.FinalReleaseComObject(shortcut);
                }
            }

            private void DeleteDirectory(string path)
            {
                string absolutePath = Path.GetFullPath(path);
                Assert.IsTrue(string.Equals(absolutePath, Root, StringComparison.OrdinalIgnoreCase) || absolutePath.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                Assert.IsFalse(File.GetAttributes(absolutePath).HasFlag(FileAttributes.ReparsePoint), "Refusing cleanup through a reparse point.");
                foreach (string file in Directory.EnumerateFiles(absolutePath))
                {
                    Assert.IsFalse(File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint));
                    File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
                    DeleteFile(file);
                }

                foreach (string directory in Directory.EnumerateDirectories(absolutePath))
                {
                    DeleteDirectory(directory);
                }

                Directory.Delete(absolutePath, recursive: false);
            }

            private static void DeleteFile(string path)
            {
                const int RetryCount = 50;
                const int RetryDelayMilliseconds = 20;
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        File.Delete(path);
                        return;
                    }
                    catch (IOException ex) when (attempt < RetryCount && (ex.HResult & 0xFFFF) is 32 or 33)
                    {
                        Thread.Sleep(RetryDelayMilliseconds);
                    }
                }
            }
        }
    }
}
