// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesCsharpLibrary.Data;
using WorkspacesEditor.Messages;
using WorkspacesEditor.Models;
using WorkspacesEditor.Telemetry;
using WorkspacesEditor.Utils;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor.UnitTests
{
    [TestClass]
    [TestCategory("Regression.Shortcuts")]
    [DoNotParallelize]
    public class WorkspaceShortcutRegressionTests
    {
        [TestMethod]
        public void Rename_PreservesShortcutOwnedBySameNameWorkspace() => RunSta(() =>
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Shared name");
            Project peer = fixture.Project(original.Name);
            fixture.WriteShortcut(peer);
            byte[] peerLink = File.ReadAllBytes(fixture.LinkPath(peer.Name));
            session.ViewModel.Workspaces.Add(original);
            session.ViewModel.Workspaces.Add(peer);
            Project draft = new(original) { Name = "Renamed workspace", IsShortcutNeeded = false };

            session.Save(original, draft);

            Assert.AreEqual(1, session.SerializationCount);
            CollectionAssert.AreEqual(peerLink, File.ReadAllBytes(fixture.LinkPath(peer.Name)));
            Assert.IsTrue(fixture.Exists(peer), "Renaming one workspace must retain the other workspace's shortcut.");
            Assert.IsTrue(File.Exists(fixture.IconPath(peer)));
        });

        [TestMethod]
        public void Remove_PreservesForeignShortcutAndIcon() => RunSta(() =>
        {
            using var fixture = new ShortcutFixture();
            Project project = fixture.Project("Foreign shortcut");
            fixture.WriteShortcut(project, targetPath: Path.Combine(fixture.Root, "AnotherLauncher.exe"));
            byte[] link = File.ReadAllBytes(fixture.LinkPath(project.Name));
            byte[] icon = File.ReadAllBytes(fixture.IconPath(project));
            Assert.IsFalse(fixture.Exists(project), "Matching arguments alone do not establish ownership.");

            fixture.Remove(project);

            CollectionAssert.AreEqual(link, File.ReadAllBytes(fixture.LinkPath(project.Name)));
            CollectionAssert.AreEqual(icon, File.ReadAllBytes(fixture.IconPath(project)));
        });

        [TestMethod]
        public void Remove_PreservesUnreadableShortcutAndIcon() => RunSta(() =>
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
        });

        [TestMethod]
        public void ShortcutOwnership_UsesArgumentsInsteadOfStaleDescription() => RunSta(() =>
        {
            using var fixture = new ShortcutFixture();
            Project original = fixture.Project("Shared name");
            Project peer = fixture.Project(original.Name);
            fixture.WriteShortcut(original, arguments: peer.Id + " 1");
            Assert.IsTrue(fixture.Exists(peer), "The arguments identify the peer despite the original workspace's description.");

            fixture.AssertUnownedShortcutPreserved(original);
        });

        [TestMethod]
        public void ShortcutOwnership_RejectsAmbiguousWorkspaceArguments() => RunSta(() =>
        {
            using var fixture = new ShortcutFixture();
            Project project = fixture.Project("Ambiguous shortcut");
            fixture.WriteShortcut(project, arguments: project.Id + " 1 " + Guid.NewGuid().ToString("B"));

            fixture.AssertUnownedShortcutPreserved(project);
        });

        [TestMethod]
        public void ShortcutOwnership_PreservesMalformedLink() => RunSta(() =>
        {
            using var fixture = new ShortcutFixture();
            Project project = fixture.Project("Malformed shortcut");
            fixture.WriteIcon(project);
            File.WriteAllBytes(fixture.LinkPath(project.Name), new byte[] { 1, 2, 3, 4 });

            fixture.AssertUnownedShortcutPreserved(project);
        });

        [TestMethod]
        public void ShortcutOwnership_AcceptsCanonicalGuidCaseAndPreviousInstallation() => RunSta(() =>
        {
            using var fixture = new ShortcutFixture();
            Guid id = Guid.NewGuid();
            Project project = fixture.Project("Moved installation", id.ToString("D"));
            string previousLauncher = Path.Combine(fixture.Root, "Previous installation", "PowerToys.WorkspacesLauncher.exe");
            fixture.WriteShortcut(project, previousLauncher, id.ToString("B").ToUpperInvariant() + " 1");
            Assert.IsTrue(fixture.Exists(project), "GUID value and launcher identity survive casing and installation-directory changes.");

            fixture.Apply(project);

            fixture.AssertShortcut(project);
        });

        [TestMethod]
        [DataRow("D")]
        [DataRow("N")]
        [DataRow("P")]
        [DataRow("X")]
        public void ShortcutOwnership_PreservesNoncanonicalGuidArguments(string format) => RunSta(() =>
        {
            using var fixture = new ShortcutFixture();
            Project project = fixture.Project("Noncanonical shortcut");
            fixture.WriteShortcut(project, arguments: Guid.Parse(project.Id).ToString(format) + " 1");

            fixture.AssertUnownedShortcutPreserved(project);
        });

        [TestMethod]
        public void Remove_MissingShortcutDoesNotCreateIconDirectory() => RunSta(() =>
        {
            using var fixture = new ShortcutFixture();
            Project project = fixture.Project("Missing shortcut");
            Assert.IsFalse(Directory.Exists(fixture.IconFolder));

            fixture.Remove(project);

            Assert.IsFalse(fixture.Exists(project));
            Assert.IsFalse(Directory.Exists(fixture.IconFolder));
        });

        [TestMethod]
        public void Rename_ReplacesOwnedShortcutAndRetainsWorkspaceIcon() => RunSta(() =>
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Original workspace");
            fixture.WriteShortcut(original);
            string oldPath = fixture.LinkPath(original.Name);
            string iconPath = fixture.IconPath(original);
            session.ViewModel.Workspaces.Add(original);
            Project draft = new(original) { Name = "Renamed workspace" };

            session.Save(original, draft);

            Assert.AreEqual(1, session.SerializationCount);
            CollectionAssert.Contains(session.SerializedNames, draft.Name);
            Assert.IsFalse(File.Exists(oldPath));
            fixture.AssertShortcut(draft);
            Assert.AreEqual(iconPath, fixture.IconPath(draft));
        });

        [TestMethod]
        public void Rename_ReadOnlyOldLinkDoesNotPreventPersistenceOrNewShortcut() => RunSta(() =>
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Read only original");
            fixture.WriteShortcut(original);
            string oldPath = fixture.LinkPath(original.Name);
            File.SetAttributes(oldPath, File.GetAttributes(oldPath) | FileAttributes.ReadOnly);
            session.ViewModel.Workspaces.Add(original);
            Project draft = new(original) { Name = "Renamed workspace" };
            Assert.IsTrue(draft.IsShortcutNeeded);

            session.Save(original, draft);

            Assert.AreEqual(1, session.SerializationCount);
            CollectionAssert.Contains(session.SerializedNames, draft.Name);
            Assert.IsTrue(File.Exists(oldPath), "The read-only original link should remain after cleanup fails.");
            fixture.AssertShortcut(draft);
            Assert.IsTrue(File.Exists(fixture.IconPath(draft)), "Both links still refer to the same workspace icon.");
        });

        [TestMethod]
        public void Rename_DestinationOwnedByAnotherWorkspacePreservesBothLinksAndIcons() => RunSta(() =>
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
            Project draft = new(original) { Name = peer.Name };

            session.Save(original, draft);

            Assert.AreEqual(1, session.SerializationCount);
            CollectionAssert.AreEqual(oldLink, File.ReadAllBytes(oldPath));
            CollectionAssert.AreEqual(peerLink, File.ReadAllBytes(fixture.LinkPath(peer.Name)));
            CollectionAssert.AreEqual(icon, File.ReadAllBytes(fixture.IconPath(original)));
            Assert.IsTrue(fixture.Exists(peer));
        });

        [TestMethod]
        public void Rename_DestinationDirectoryPreservesOldLinkAndIcon() => RunSta(() =>
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Original workspace");
            fixture.WriteShortcut(original);
            string oldPath = fixture.LinkPath(original.Name);
            byte[] oldLink = File.ReadAllBytes(oldPath);
            session.ViewModel.Workspaces.Add(original);
            Project draft = new(original) { Name = "Blocked destination" };
            Directory.CreateDirectory(fixture.LinkPath(draft.Name));

            session.Save(original, draft);

            Assert.AreEqual(1, session.SerializationCount);
            CollectionAssert.AreEqual(oldLink, File.ReadAllBytes(oldPath));
            Assert.IsTrue(File.Exists(fixture.IconPath(original)));
            Assert.IsTrue(Directory.Exists(fixture.LinkPath(draft.Name)));
        });

        [TestMethod]
        public void Rename_NewLinkSaveFailureAfterPreflightPreservesOldLinkAndIcon() => RunSta(() =>
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Original workspace");
            fixture.WriteShortcut(original);
            string oldPath = fixture.LinkPath(original.Name);
            byte[] oldLink = File.ReadAllBytes(oldPath);
            session.ViewModel.Workspaces.Add(original);
            Project draft = new(original) { Name = Path.Combine("Missing folder", "New name") };
            string newPath = fixture.LinkPath(draft.Name);
            Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(newPath)), "The destination must pass the missing-file probe but fail when its link is saved.");

            session.Save(original, draft);

            Assert.AreEqual(1, session.SerializationCount);
            CollectionAssert.Contains(session.SerializedNames, draft.Name);
            CollectionAssert.AreEqual(oldLink, File.ReadAllBytes(oldPath));
            Assert.IsTrue(File.Exists(fixture.IconPath(original)));
            Assert.IsFalse(File.Exists(newPath));
            Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(newPath)));
        });

        [TestMethod]
        public void Rename_ChangingOnlyCaseKeepsShortcutAndIcon() => RunSta(() =>
        {
            using var fixture = new ShortcutFixture();
            using var session = new ViewModelSession(fixture);
            Project original = fixture.Project("Case workspace");
            fixture.WriteShortcut(original);
            session.ViewModel.Workspaces.Add(original);
            Project draft = new(original) { Name = "CASE WORKSPACE" };

            session.Save(original, draft);

            Assert.AreEqual(1, session.SerializationCount);
            fixture.AssertShortcut(draft);
        });

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Rename_WithShortcutDisabledRemovesOnlyOwnedFiles(bool ownsShortcut) => RunSta(() =>
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
            Project draft = new(original) { Name = "Renamed workspace", IsShortcutNeeded = false };

            session.Save(original, draft);

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
        });

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void DeleteProject_RemovesOnlyOwnedShortcutAndIcon(bool ownsShortcut) => RunSta(() =>
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
        });

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Revert_PreservesUnsavedShortcutChoiceThroughSave(bool shortcutNeeded) => RunSta(() =>
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
            Project draft = new(original) { IsShortcutNeeded = shortcutNeeded };
            Project beforeLaunch = new(draft);
            Assert.AreEqual(shortcutNeeded, beforeLaunch.IsShortcutNeeded);
            session.SetProjectBeforeLaunch(beforeLaunch);

            session.Revert();

            Assert.AreSame(beforeLaunch, session.NavigatedProject);
            Assert.AreEqual(shortcutNeeded, beforeLaunch.IsShortcutNeeded);
            session.Save(original, beforeLaunch);
            Assert.AreEqual(1, session.SerializationCount);
            Assert.AreEqual(shortcutNeeded, session.SerializedShortcutChoices.Single());
            Assert.AreEqual(shortcutNeeded, original.IsShortcutNeeded);
            Assert.AreEqual(shortcutNeeded, fixture.Exists(original));
            if (shortcutNeeded)
            {
                fixture.AssertShortcut(original);
            }
        });

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Recapture_PreservesUnsavedShortcutChoiceThroughSave(bool shortcutNeeded) => RunSta(() =>
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
            Project draft = new(original) { IsShortcutNeeded = shortcutNeeded };
            Project beforeLaunch = new(draft);
            Assert.AreEqual(shortcutNeeded, beforeLaunch.IsShortcutNeeded);
            Project recaptured = fixture.Project("Captured workspace");
            recaptured.IsShortcutNeeded = !shortcutNeeded;

            session.ProcessCapturedProject(beforeLaunch, recaptured);

            Assert.AreSame(recaptured, session.NavigatedProject);
            Assert.AreEqual(beforeLaunch.Id, recaptured.Id);
            Assert.AreEqual(beforeLaunch.Name, recaptured.Name);
            Assert.IsTrue(recaptured.IsRevertEnabled);
            Assert.AreEqual(shortcutNeeded, recaptured.IsShortcutNeeded);
            session.Save(original, recaptured);
            Assert.AreEqual(1, session.SerializationCount);
            Assert.AreEqual(shortcutNeeded, session.SerializedShortcutChoices.Single());
            Assert.AreEqual(shortcutNeeded, original.IsShortcutNeeded);
            Assert.AreEqual(shortcutNeeded, fixture.Exists(original));
            if (shortcutNeeded)
            {
                fixture.AssertShortcut(original);
            }
        });

        [TestMethod]
        public void EditProject_InitializesShortcutChoiceFromDiskForExistingAndNewWorkspaces() => RunSta(() =>
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
        });

        private static void RunSta(Action action)
        {
            Exception error = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (error != null)
            {
                ExceptionDispatchInfo.Capture(error).Throw();
            }
        }

        private static object Invoke(object instance, string method, params object[] arguments)
        {
            try
            {
                return instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(instance, arguments);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        private sealed class ViewModelSession : IDisposable
        {
            internal ViewModelSession(ShortcutFixture fixture)
            {
                ConstructorInfo constructor = typeof(MainViewModel).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single(candidate => candidate.GetParameters().Length == 6);
                ViewModel = (MainViewModel)constructor.Invoke(new object[]
                {
                    new WorkspacesEditorIO(),
                    new WorkspacesSettings(),
                    new Action<WorkspacesSettings>(_ => { }),
                    fixture.Service,
                    new Action<List<Project>>(projects =>
                    {
                        SerializationCount++;
                        SerializedNames = projects.Select(project => project.Name).ToArray();
                        SerializedShortcutChoices = projects.Select(project => project.IsShortcutNeeded).ToArray();
                    }),
                    new Action<EditEvent>(_ => { }),
                });
                StrongReferenceMessenger.Default.Register<NavigateToEditorMessage>(this, (recipient, message) => ((ViewModelSession)recipient).NavigatedProject = message.Project);
            }

            internal MainViewModel ViewModel { get; }

            internal int SerializationCount { get; private set; }

            internal string[] SerializedNames { get; private set; } = Array.Empty<string>();

            internal bool[] SerializedShortcutChoices { get; private set; } = Array.Empty<bool>();

            internal Project NavigatedProject { get; private set; }

            internal void Save(Project original, Project draft)
            {
                Assert.IsTrue(draft.CanBeSaved);
                typeof(MainViewModel).GetField("_editedProject", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ViewModel, original);
                ViewModel.SaveProject(draft);
            }

            internal void SetProjectBeforeLaunch(Project project) =>
                typeof(MainViewModel).GetField("_projectBeforeLaunch", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ViewModel, project);

            internal void Revert() => Invoke(ViewModel, "RevertLaunch");

            internal void ProcessCapturedProject(Project beforeLaunch, Project captured)
            {
                SetProjectBeforeLaunch(beforeLaunch);
                typeof(MainViewModel).GetField("_isExistingProjectLaunched", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ViewModel, true);
                Invoke(ViewModel, "ProcessCapturedProject", captured);
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
                IconFolder = Path.Combine(Root, "Icons with spaces, commas");
                LauncherPath = Path.Combine(Root, "PowerToys.WorkspacesLauncher.exe");
                File.WriteAllBytes(LauncherPath, Array.Empty<byte>());
                Type serviceType = typeof(MainViewModel).Assembly.GetType("WorkspacesEditor.Services.WorkspaceShortcutService", throwOnError: true);
                ConstructorInfo constructor = serviceType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(string), typeof(string), typeof(string) }, null);
                Assert.IsNotNull(constructor, "The service must support isolated shortcut directories.");
                Service = constructor.Invoke(new object[] { DesktopFolder, IconFolder, LauncherPath });
            }

            internal string Root { get; }

            internal string DesktopFolder { get; }

            internal string IconFolder { get; }

            internal string LauncherPath { get; }

            internal object Service { get; }

            internal string LinkPath(string name) => Path.Combine(DesktopFolder, name + ".lnk");

            internal string IconPath(Project project) => Path.Combine(IconFolder, project.Id + ".ico");

            internal bool Exists(Project project) => (bool)Invoke(Service, "Exists", project);

            internal void Remove(Project project) => Invoke(Service, "Remove", project);

            internal Project Project(string name, string id = null) => new(new ProjectWrapper
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
                    },
                },
                MonitorConfiguration = new List<MonitorConfigurationWrapper>(),
            });

            internal void WriteIcon(Project project)
            {
                Directory.CreateDirectory(IconFolder);
                File.WriteAllBytes(IconPath(project), new byte[] { 1, 2, 3, 4 });
            }

            internal void WriteShortcut(Project project, string targetPath = null, string arguments = null)
            {
                WriteIcon(project);
                WithShortcut(LinkPath(project.Name), shortcut =>
                {
                    shortcut.TargetPath = targetPath ?? LauncherPath;
                    shortcut.Arguments = arguments ?? project.Id + " 1";
                    shortcut.Description = "Project Launcher " + project.Id;
                    shortcut.IconLocation = IconPath(project) + ",0";
                    shortcut.Save();
                });
                Assert.IsTrue(File.Exists(LinkPath(project.Name)), "The real shortcut fixture was not created.");
            }

            internal void Apply(Project project) => Invoke(Service, "Apply", project, null);

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
                WithShortcut(LinkPath(project.Name), shortcut =>
                {
                    Assert.IsTrue(string.Equals(LauncherPath, (string)shortcut.TargetPath, StringComparison.OrdinalIgnoreCase));
                    Assert.AreEqual(Guid.Parse(project.Id).ToString("B") + " 1", (string)shortcut.Arguments);
                    string icon = shortcut.IconLocation;
                    string iconPath = (icon.EndsWith(",0", StringComparison.Ordinal) ? icon.Substring(0, icon.Length - 2) : icon).Trim().Trim('"');
                    Assert.IsTrue(string.Equals(IconPath(project), iconPath, StringComparison.OrdinalIgnoreCase));
                });
            }

            public void Dispose()
            {
                Assert.IsTrue(string.Equals(Path.GetDirectoryName(Path.GetFullPath(Root)), _temporaryRoot, StringComparison.OrdinalIgnoreCase));
                DeleteDirectory(Root);
                GC.SuppressFinalize(this);
            }

            private static void WithShortcut(string path, Action<dynamic> action)
            {
                object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", throwOnError: true));
                object shortcut = null;
                try
                {
                    shortcut = ((dynamic)shell).CreateShortcut(path);
                    action(shortcut);
                }
                finally
                {
                    if (shortcut != null)
                    {
                        Marshal.FinalReleaseComObject(shortcut);
                    }

                    Marshal.FinalReleaseComObject(shell);
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
