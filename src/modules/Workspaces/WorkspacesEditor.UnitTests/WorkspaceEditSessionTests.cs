// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesCsharpLibrary.Data;
using WorkspacesEditor.Messages;
using WorkspacesEditor.Models;
using WorkspacesEditor.Services;
using WorkspacesEditor.Utils;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor.UnitTests;

[STATestClass]
[DoNotParallelize]
public class WorkspaceEditSessionTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task NavigatingDuringLaunchOrCaptureCannotReplaceTheNextDraft(bool waitDuringCapture)
    {
        using var session = new Session();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Run = start =>
        {
            if (Path.GetFileName(start.FileName) == "PowerToys.WorkspacesLauncher.exe")
            {
                return waitDuringCapture ? Task.FromResult(true) : completion.Task;
            }

            session.WriteCapture(start);
            return completion.Task;
        };
        var first = session.OpenExisting("First");
        var staleDraft = session.Draft;
        Task pending = session.ViewModel.LaunchAndEditAsync(staleDraft);
        if (waitDuringCapture)
        {
            await pending;
            pending = session.ViewModel.SnapWorkspaceAsync();
        }

        session.ViewModel.SwitchToMainView();
        var second = session.OpenExisting("Second");
        var current = session.Draft;
        completion.SetResult(true);
        await pending;
        session.ViewModel.SaveOrAddProject(staleDraft);
        Assert.AreSame(current, session.Draft);
        Assert.AreEqual(0, session.Writes.Count);
        Assert.AreEqual("First", first.Name);
        Assert.AreEqual("Second", second.Name);
        Assert.IsFalse(File.Exists(session.TempPath));
        Assert.AreEqual(0, Directory.GetFiles(session.Folder.FullName).Length);

        current.Name = "Edited second";
        session.ViewModel.SaveOrAddProject(current);
        Assert.AreEqual("Edited second", second.Name);
        Assert.AreEqual(1, session.Writes.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LaunchAndEditUsesCurrentApplicationIdsAndArguments(bool isNew)
    {
        using var session = new Session();
        var original = TestHelpers.CreateProject("Original", 123, 45, "Existing app");
        if (!isNew)
        {
            session.ViewModel.Workspaces.Add(original);
        }

        session.ViewModel.EditProject(original, isNew);
        var edited = session.Draft;
        edited.Applications[0].CommandLineArguments = "--edited";
        edited.MoveExistingWindows = true;
        string id = edited.Applications[0].Id;
        session.Run = start =>
        {
            if (Path.GetFileName(start.FileName) == "PowerToys.WorkspacesLauncher.exe")
            {
                Assert.IsFalse(File.Exists(session.TempPath));
                var draft = new ProjectData().Read(session.LaunchInputPath(start));
                Assert.AreEqual(original.Id, draft.Id);
                Assert.AreEqual(id, draft.Applications.Single().Id);
                Assert.AreEqual("--edited", draft.Applications[0].CommandLineArguments);
                Assert.IsTrue(draft.MoveExistingWindows);
            }
            else
            {
                session.WriteCapture(start, appId: id);
            }

            return Task.FromResult(true);
        };
        await session.ViewModel.LaunchAndEditAsync(edited);
        await session.ViewModel.SnapWorkspaceAsync();
        Assert.AreEqual(id, session.Draft.Applications[0].Id);
        Assert.AreEqual("--edited", session.Draft.Applications[0].CommandLineArguments);
        Assert.AreEqual(2, session.Draft.Monitors.Single().MonitorNumber);
        session.ViewModel.SaveOrAddProject(session.Draft);
        var saved = session.Writes.Single().Single();
        Assert.AreEqual(2, saved.Monitors.Single().MonitorNumber);
        Assert.AreEqual(2, saved.Applications.Single().MonitorNumber);
        if (!isNew)
        {
            Assert.AreEqual(123L, saved.CreationTime);
            Assert.AreEqual(45L, saved.LastLaunchedTime);
            Assert.AreEqual(2, original.Monitors.Single().MonitorNumber);
            Assert.AreSame(original, original.Applications.Single().Parent);
        }

        Assert.IsFalse(File.Exists(session.TempPath));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RejectedSnapshotDoesNotReadPreviousDraftOrNavigate(bool processSucceeded)
    {
        using var session = new Session();
        session.OpenExisting("Original");
        var edited = session.Draft;
        var previousSnapshot = new ProjectWrapper { Id = Guid.NewGuid().ToString("B"), Applications = [], MonitorConfiguration = [] };
        File.WriteAllText(session.TempPath, new ProjectData().Serialize(previousSnapshot));
        session.Run = start =>
        {
            if (Path.GetFileName(start.FileName) == "PowerToys.WorkspacesLauncher.exe")
            {
                return Task.FromResult(true);
            }

            session.WriteCapture(start, projectId: Guid.NewGuid().ToString("B"));
            return Task.FromResult(processSucceeded);
        };

        await session.ViewModel.LaunchAndEditAsync(edited);
        await session.ViewModel.SnapWorkspaceAsync();
        Assert.AreSame(edited, session.Draft);
        Assert.AreEqual(previousSnapshot.Id, new ProjectData().Read(session.TempPath).Id);
        Assert.IsFalse(session.ViewModel.IsLoading);
        Assert.AreEqual(0, session.Writes.Count);
        session.ViewModel.SwitchToMainView();
        CollectionAssert.AreEqual(new[] { session.TempPath }, Directory.GetFiles(session.Folder.FullName));
    }

    [TestMethod]
    public async Task EditsDuringCaptureSurviveAcceptanceAndRevert()
    {
        using var session = new Session();
        session.OpenExisting("Original");
        var source = session.Draft;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Run = start =>
        {
            if (Path.GetFileName(start.FileName) == "PowerToys.WorkspacesLauncher.exe")
            {
                return Task.FromResult(true);
            }

            session.WriteCapture(start, appId: source.Applications[0].Id);
            return completion.Task;
        };
        await session.ViewModel.LaunchAndEditAsync(source);
        Task pending = session.ViewModel.SnapWorkspaceAsync();
        Assert.IsFalse(session.ViewModel.CanLaunchAndEdit);
        source.Name = "During capture";
        source.MoveExistingWindows = true;
        source.IsShortcutNeeded = true;
        source.Applications[0].CommandLineTextChanged("--latest");
        source.Applications[0].IsElevated = true;
        source.Applications[0].UpdatePositionX("177");
        completion.SetResult(true);
        await pending;

        Project captured = session.Draft;
        Assert.IsTrue(captured.IsRevertEnabled);
        Assert.AreEqual("During capture", captured.Name);
        Assert.IsTrue(captured.MoveExistingWindows);
        Assert.IsTrue(captured.IsShortcutNeeded);
        Assert.AreEqual("--latest", captured.Applications[0].CommandLineArguments);
        Assert.IsTrue(captured.Applications[0].IsElevated);
        Assert.AreEqual(177, captured.Applications[0].Position.X);
        Assert.AreEqual(2, captured.Applications[0].MonitorNumber);
        session.ViewModel.RevertLaunch();
        Assert.AreEqual("During capture", session.Draft.Name);
        Assert.AreEqual("--latest", session.Draft.Applications[0].CommandLineArguments);
        Assert.AreEqual(177, session.Draft.Applications[0].Position.X);
        Assert.AreEqual(source.Applications[0].MonitorNumber, session.Draft.Applications[0].MonitorNumber);
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, false)]
    [DataRow(false, true)]
    public async Task VerifiedCaptureIsAdoptedWithoutDeletingAnotherWritersMirror(bool lockExistingMirror, bool reuseCapturedId)
    {
        using var session = new Session();
        string unrelatedId = Guid.NewGuid().ToString("B");
        string unrelated = new ProjectData().Serialize(new ProjectWrapper { Id = unrelatedId, Applications = [], MonitorConfiguration = [] });
        File.WriteAllText(session.TempPath, unrelated);
        session.Run = start =>
        {
            session.WriteCapture(start);
            return Task.FromResult(true);
        };
        session.ViewModel.EnterSnapshotMode(false);
        using (var locked = lockExistingMirror ? File.Open(session.TempPath, FileMode.Open, FileAccess.Read, FileShare.Read) : null)
        {
            await session.ViewModel.SnapWorkspaceAsync();
            Assert.AreEqual("Captured app", session.Draft.Applications.Single().AppName);
        }

        if (lockExistingMirror)
        {
            Assert.AreEqual(unrelatedId, new ProjectData().Read(session.TempPath).Id);
        }
        else
        {
            Assert.AreEqual(session.Draft.Id, new ProjectData().Read(session.TempPath).Id);
            if (reuseCapturedId)
            {
                var otherDraft = new ProjectData().Read(session.TempPath);
                otherDraft.Name = "Edited by another observer";
                unrelated = new ProjectData().Serialize(otherDraft);
            }

            File.WriteAllText(session.TempPath, unrelated);
        }

        session.ViewModel.SwitchToMainView();
        Assert.AreEqual(unrelated, File.ReadAllText(session.TempPath));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CancelledOrFailedNewCaptureRefreshesDeferredOverview(bool cancel)
    {
        using var session = new Session();
        var original = session.OpenExisting("Before launch");
        session.ViewModel.SwitchToMainView();
        var stored = new WorkspacesData.WorkspacesListWrapper
        {
            Workspaces = [new ProjectWrapper { Id = original.Id, Name = "Updated on disk", Applications = [], MonitorConfiguration = [] }],
        };
        File.WriteAllText(Path.Combine(session.Folder.FullName, "workspaces.json"), new WorkspacesData().Serialize(stored));
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Run = start => Path.GetFileName(start.FileName) == "PowerToys.WorkspacesLauncher.exe" ? completion.Task : Task.FromResult(false);
        var launch = session.ViewModel.LaunchProjectAsync(original);
        session.ViewModel.EnterSnapshotMode(false);
        completion.SetResult(true);
        await launch;
        Assert.AreEqual("Before launch", session.ViewModel.Workspaces.Single().Name);
        if (cancel)
        {
            session.ViewModel.CancelSnapshot();
        }
        else
        {
            await session.ViewModel.SnapWorkspaceAsync();
        }

        Assert.AreEqual("Updated on disk", session.ViewModel.Workspaces.Single().Name);
    }

    [TestMethod]
    public async Task InvalidSavedMonitorCanBeRepairedWithoutReplayingTheWorkspace()
    {
        using var session = new Session();
        Project original = session.OpenExisting("Old layout");
        original.Applications[0].MonitorNumber = 99;
        session.ViewModel.EditProject(original);
        var legacy = session.Draft;
        bool ranSnapshot = false;
        session.Run = start =>
        {
            Assert.AreEqual("PowerToys.WorkspacesSnapshotTool.exe", Path.GetFileName(start.FileName));
            session.WriteCapture(start, appId: legacy.Applications[0].Id);
            ranSnapshot = true;
            return Task.FromResult(true);
        };
        Assert.IsFalse(session.ViewModel.CanLaunchAndEdit);
        Assert.IsTrue(session.ViewModel.CanRecaptureCurrentLayout);
        await session.ViewModel.LaunchAndEditAsync(legacy);
        Assert.AreEqual(0, session.SnapshotRequests);
        session.ViewModel.RecaptureCurrentLayout(legacy);
        Assert.IsFalse(session.ViewModel.CanRecaptureCurrentLayout);
        Assert.AreEqual(1, session.SnapshotRequests);
        await session.ViewModel.SnapWorkspaceAsync();
        Assert.IsTrue(ranSnapshot);
        Assert.IsTrue(session.ViewModel.CanLaunchAndEdit);
        Assert.IsFalse(session.ViewModel.CanRecaptureCurrentLayout);
        session.ViewModel.SaveOrAddProject(session.Draft);
        Assert.AreEqual(2, original.Applications.Single().MonitorNumber);
        Assert.AreEqual(2, original.Monitors.Single().MonitorNumber);
    }

    [TestMethod]
    public async Task FirstOverviewLaunchCommitsRequiredLegacyIdRepairs()
    {
        using var session = new Session(physicalStorage: true);
        var old = TestHelpers.CreateProject("Old", 1, 0, "One", "Two");
        old.Applications[1].Id = old.Applications[0].Id;
        session.Storage.SerializeWorkspaces([old]);
        Assert.IsTrue(session.Storage.ParseWorkspaces(session.ViewModel).Result);
        var repaired = session.ViewModel.Workspaces.Single();
        Assert.IsTrue(repaired.NeedsApplicationIdRepair);
        Assert.AreNotEqual(repaired.Applications[0].Id, repaired.Applications[1].Id);
        int launches = 0;
        session.Run = start =>
        {
            launches++;
            Assert.AreEqual("PowerToys.WorkspacesLauncher.exe", Path.GetFileName(start.FileName));
            Assert.IsFalse(start.Arguments.Contains("--editor-draft", StringComparison.Ordinal));
            var stored = new WorkspacesData().Read(Path.Combine(session.Folder.FullName, "workspaces.json")).Workspaces.Single();
            CollectionAssert.AreEqual(repaired.Applications.Select(app => app.Id).ToArray(), stored.Applications.Select(app => app.Id).ToArray());
            return Task.FromResult(false);
        };
        await session.ViewModel.LaunchProjectAsync(repaired);
        Assert.AreEqual(1, launches);
        Assert.IsFalse(repaired.NeedsApplicationIdRepair);
        Assert.IsFalse(session.ViewModel.SaveFailed);
    }

    [TestMethod]
    public async Task LaunchAvailabilityNotifiesOnDraftAndOperationChanges()
    {
        using var session = new Session();
        session.OpenExisting("Ready");
        var changes = new List<bool>();
        session.ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CanLaunchAndEdit))
            {
                changes.Add(session.ViewModel.CanLaunchAndEdit);
            }
        };
        session.Draft.Name = string.Empty;
        Assert.IsFalse(changes.Last());
        session.Draft.Name = "Ready";
        Assert.IsTrue(changes.Last());
        var waiting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Run = _ => waiting.Task;
        var launch = session.ViewModel.LaunchAndEditAsync(session.Draft);
        Assert.IsFalse(changes.Last());
        waiting.SetResult(false);
        await launch;
        Assert.IsTrue(changes.Last());
    }

    [TestMethod]
    public async Task RepeatedLaunchClickStartsOneLauncherAndKeepsLaterEdits()
    {
        using var session = new Session();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int launches = 0;
        session.Run = start =>
        {
            if (Path.GetFileName(start.FileName) == "PowerToys.WorkspacesLauncher.exe")
            {
                launches++;
                return completion.Task;
            }

            session.WriteCapture(start, appId: session.Draft.Applications[0].Id);
            return Task.FromResult(true);
        };
        session.OpenExisting("First");
        var draft = session.Draft;
        var initial = session.ViewModel.LaunchAndEditAsync(draft);
        await session.ViewModel.LaunchAndEditAsync(draft);
        draft.Name = "Edited while launching";
        draft.Applications[0].IsIncluded = false;
        completion.SetResult(true);
        await initial;
        await session.ViewModel.SnapWorkspaceAsync();
        Assert.AreEqual(1, launches);
        Assert.AreEqual("Edited while launching", session.Draft.Name);
        Assert.IsFalse(session.Draft.Applications[0].IsIncluded, "A removed row must stay excluded even if its window was already launched.");
    }

    [TestMethod]
    public async Task UnsuccessfulLaunchDoesNotEnterCaptureMode()
    {
        using var session = new Session();
        var original = session.OpenExisting("Original");
        session.FailWrite = true;
        session.ViewModel.SaveOrAddProject(session.Draft);
        Assert.IsTrue(session.ViewModel.SaveFailed);
        session.Run = _ => Task.FromResult(false);
        await session.ViewModel.LaunchAndEditAsync(session.Draft);
        await session.ViewModel.SnapWorkspaceAsync();
        Assert.AreEqual(0, session.SnapshotRequests);
        Assert.AreEqual(original.Id, session.Draft.Id);
        Assert.IsFalse(File.Exists(session.TempPath));
        Assert.IsFalse(session.ViewModel.SaveFailed);
    }

    [TestMethod]
    public void StartupCleanupDeletesOnlyOldRequestFiles()
    {
        using var session = new Session();
        var storage = new WorkspacesEditorIO(session.TempPath, (_, _) => Task.FromResult(false));
        string orphan = storage.RequestPath(Guid.NewGuid());
        string fresh = storage.RequestPath(Guid.NewGuid());
        string unrelated = session.TempPath + ".{unrelated}";
        string otherExtension = orphan + ".backup";
        foreach (string path in new[] { orphan, session.TempPath, unrelated, otherExtension })
        {
            File.WriteAllText(path, string.Empty);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-1));
        }

        File.WriteAllText(fresh, string.Empty);
        storage.CleanupOrphanedRequests();
        Assert.IsFalse(File.Exists(orphan));
        Assert.IsTrue(File.Exists(fresh));
        Assert.IsTrue(File.Exists(session.TempPath));
        Assert.IsTrue(File.Exists(unrelated));
        Assert.IsTrue(File.Exists(otherExtension));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ShutdownNeverPublishesAnInFlightLaunchOrCapture(bool captureInProgress)
    {
        using var session = new Session();
        session.OpenExisting("Shutdown");
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        string requestPath = null;
        session.Run = start =>
        {
            if (Path.GetFileName(start.FileName) == "PowerToys.WorkspacesLauncher.exe")
            {
                requestPath = session.LaunchInputPath(start);
                return captureInProgress ? Task.FromResult(true) : completion.Task;
            }

            requestPath = session.WriteCapture(start);
            return completion.Task;
        };
        Task operation = session.ViewModel.LaunchAndEditAsync(session.Draft);
        if (captureInProgress)
        {
            await operation;
            operation = session.ViewModel.SnapWorkspaceAsync();
        }

        Assert.IsTrue(File.Exists(requestPath));
        Assert.IsFalse(File.Exists(session.TempPath));
        session.ViewModel.Dispose();
        Assert.IsTrue(File.Exists(requestPath));
        completion.SetResult(true);
        await operation;
        Assert.IsFalse(File.Exists(session.TempPath));
        Assert.IsFalse(File.Exists(requestPath));
    }

    [TestMethod]
    public async Task ConcurrentEditSessionsGiveEachLauncherItsOwnDraft()
    {
        using var session = new Session();
        var waiting = new List<TaskCompletionSource<bool>>();
        var inputs = new List<string>();
        session.Run = start =>
        {
            inputs.Add(session.LaunchInputPath(start));
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            waiting.Add(completion);
            return completion.Task;
        };
        var first = session.OpenExisting("First");
        Task firstLaunch = session.ViewModel.LaunchAndEditAsync(session.Draft);
        session.ViewModel.SwitchToMainView();
        var second = session.OpenExisting("Second");
        Task secondLaunch = session.ViewModel.LaunchAndEditAsync(session.Draft);

        Assert.AreEqual(2, inputs.Count);
        Assert.AreNotEqual(inputs[0], inputs[1]);
        Assert.AreEqual(first.Id, new ProjectData().Read(inputs[0]).Id);
        Assert.AreEqual(second.Id, new ProjectData().Read(inputs[1]).Id);
        Assert.IsFalse(File.Exists(session.TempPath));
        waiting[1].SetResult(true);
        await secondLaunch;
        Assert.IsFalse(File.Exists(inputs[1]));
        Assert.IsTrue(File.Exists(inputs[0]));
        waiting[0].SetResult(true);
        await firstLaunch;
        Assert.IsFalse(File.Exists(inputs[0]));
        Assert.AreEqual(1, session.SnapshotRequests);
    }

    [TestMethod]
    public void FailedSaveAddAndDeleteRetainTheDraftAndCanBeRetried()
    {
        using var session = new Session();
        var original = session.OpenExisting("Original");
        session.FailWrite = true;
        var edited = session.Draft;
        edited.Name = "Renamed";
        edited.Applications[0].AppName = "Changed application";
        session.ViewModel.SaveOrAddProject(edited);
        Assert.IsTrue(session.ViewModel.SaveFailed);
        Assert.IsFalse(session.ViewModel.WorkspaceSaveConflict);
        Assert.AreEqual("Original", original.Name);
        Assert.AreEqual("App", original.Applications[0].AppName);
        Assert.AreEqual(0, session.ShortcutChanges);
        Assert.AreEqual(0, session.EditEvents);
        Assert.AreEqual(0, session.BackNavigations);

        session.ViewModel.DeleteProjectById(original.Id);
        Assert.AreSame(original, session.ViewModel.Workspaces.Single());
        Assert.AreEqual(0, session.ShortcutChanges);
        Assert.AreEqual(0, session.BackNavigations);
        session.FailWrite = false;
        session.ViewModel.SaveOrAddProject(edited);
        Assert.IsFalse(session.ViewModel.SaveFailed);
        Assert.AreEqual("Renamed", original.Name);
        Assert.AreEqual(1, session.ShortcutChanges);
        Assert.AreEqual(1, session.EditEvents);
        Assert.AreEqual(1, session.BackNavigations);

        session.ViewModel.EditProject(TestHelpers.CreateProject("New", 0, 0, "New app"), true);
        var newDraft = session.Draft;
        session.FailWrite = true;
        session.ViewModel.SaveOrAddProject(newDraft);
        Assert.IsTrue(session.ViewModel.SaveFailed);
        Assert.AreEqual(1, session.ViewModel.Workspaces.Count);
        Assert.AreEqual(1, session.ShortcutChanges);
        Assert.AreEqual(1, session.BackNavigations);
        session.FailWrite = false;
        session.ViewModel.SaveOrAddProject(newDraft);
        Assert.IsFalse(session.ViewModel.SaveFailed);
        Assert.AreEqual(2, session.ViewModel.Workspaces.Count);
        Assert.AreEqual(2, session.ShortcutChanges);
        Assert.AreEqual(2, session.BackNavigations);
    }

    private sealed class Session : IWorkspaceShortcutService, IDisposable
    {
        internal Session(bool physicalStorage = false)
        {
            TempPath = Path.Combine(Folder.FullName, "temp-workspaces.json");
            Storage = new WorkspacesEditorIO(TempPath, (start, _) => Run(start));
            ViewModel = new MainViewModel(
                Storage,
                new WorkspacesSettings(),
                _ => { },
                this,
                physicalStorage ? null : RecordWrite,
                _ => EditEvents++,
                _ => { });
            StrongReferenceMessenger.Default.Register<NavigateToEditorMessage>(this, (r, m) => ((Session)r).Draft = m.Project);
            StrongReferenceMessenger.Default.Register<GoBackMessage>(this, (r, _) => ((Session)r).BackNavigations++);
            StrongReferenceMessenger.Default.Register<ShowSnapshotWindowMessage>(this, (r, _) => ((Session)r).SnapshotRequests++);
        }

        internal DirectoryInfo Folder { get; } = Directory.CreateTempSubdirectory("PowerToys-EditSession-");

        internal string TempPath { get; }

        internal WorkspacesEditorIO Storage { get; }

        internal MainViewModel ViewModel { get; }

        internal Project Draft { get; private set; }

        internal Func<ProcessStartInfo, Task<bool>> Run { get; set; }

        internal List<List<Project>> Writes { get; } = [];

        internal bool FailWrite { get; set; }

        internal int ShortcutChanges { get; private set; }

        internal int EditEvents { get; private set; }

        internal int BackNavigations { get; private set; }

        internal int SnapshotRequests { get; private set; }

        internal Project OpenExisting(string name)
        {
            var original = TestHelpers.CreateProject(name, 0, 0, "App");
            ViewModel.Workspaces.Add(original);
            ViewModel.EditProject(original);
            return original;
        }

        internal string LaunchInputPath(ProcessStartInfo start)
        {
            const string prefix = "--editor-draft=";
            string[] arguments = start.Arguments.Split(' ');
            Assert.AreEqual(3, arguments.Length);
            StringAssert.StartsWith(arguments[2], prefix);
            string token = arguments[2][prefix.Length..];
            Assert.IsTrue(Guid.TryParseExact(token, "B", out _));
            return TempPath + "." + token;
        }

        internal string WriteCapture(ProcessStartInfo start, string projectId = null, string appId = null)
        {
            string requested = start.Arguments.Split(' ')[0];
            var rect = new MonitorConfigurationWrapper.MonitorRectWrapper { Width = 1920, Height = 1080 };
            ProjectWrapper data = new()
            {
                Id = projectId ?? requested,
                Name = string.Empty,
                Applications =
                [
                    new ApplicationWrapper
                    {
                        Id = appId ?? Guid.NewGuid().ToString("B"),
                        Application = "Captured app",
                        ApplicationPath = Environment.ProcessPath,
                        CommandLineArguments = string.Empty,
                        Position = new ApplicationWrapper.WindowPositionWrapper { Width = 640, Height = 480 },
                        Monitor = 2,
                    },
                ],
                MonitorConfiguration =
                [
                    new MonitorConfigurationWrapper
                    {
                        Id = "screen",
                        InstanceId = "screen",
                        Dpi = 96,
                        MonitorNumber = 2,
                        MonitorRectDpiAware = rect,
                        MonitorRectDpiUnaware = rect,
                    },
                ],
            };
            string path = TempPath + "." + requested;
            File.WriteAllText(path, new ProjectData().Serialize(data));
            return path;
        }

        public bool Exists(Project project) => false;

        public void Apply(Project project, string previousName = null) => ShortcutChanges++;

        public void Remove(Project project) => ShortcutChanges++;

        public void Dispose()
        {
            StrongReferenceMessenger.Default.UnregisterAll(this);
            ViewModel.Dispose();
            Folder.Delete(recursive: true);
            GC.SuppressFinalize(this);
        }

        private void RecordWrite(List<Project> projects)
        {
            if (FailWrite)
            {
                throw new IOException("Simulated storage failure.");
            }

            Writes.Add(projects);
        }
    }
}
