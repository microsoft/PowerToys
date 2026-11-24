// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesCsharpLibrary.Data;
using WorkspacesEditor.Models;
using WorkspacesEditor.Telemetry;
using WorkspacesEditor.Utils;

namespace WorkspacesEditor.UnitTests
{
    [TestClass]
    public class WorkspaceDraftModelRegressionTests
    {
        [TestMethod]
        public void Recapture_AssignsFreshIdentityToAnAdditionalWindowWithTheSameStamp()
        {
            Project previous = TestHelpers.CreateProject("Work", 0, 0, "Browser");
            Application original = previous.Applications[0];
            original.CommandLineArguments = "--profile=work";
            original.IsElevated = true;

            Project captured = TestHelpers.CreateProject("Capture", 0, 0, "Browser", "Browser", "New app");
            Application retained = captured.Applications[0];
            retained.Id = original.Id;
            Application additional = captured.Applications[1];
            additional.Id = Guid.Parse(original.Id).ToString("D");
            additional.CommandLineArguments = "--captured";
            string newAppId = captured.Applications[2].Id;

            captured.UpdateAfterLaunchAndEdit(previous);

            Assert.AreEqual(original.Id, retained.Id);
            Assert.AreEqual("--profile=work", retained.CommandLineArguments);
            Assert.IsTrue(retained.IsElevated);
            Assert.IsTrue(Guid.TryParseExact(additional.Id, "B", out Guid newId));
            Assert.AreNotEqual(Guid.Parse(original.Id), newId);
            Assert.AreNotEqual(Guid.Parse(newAppId), newId);
            Assert.AreEqual("--captured", additional.CommandLineArguments);
            Assert.IsFalse(additional.IsElevated);
            Assert.AreEqual(newAppId, captured.Applications[2].Id);
        }

        [TestMethod]
        public void DuplicateStamps_MatchEachPriorRowOnlyOnceInRecaptureAndTelemetry()
        {
            Project previous = TestHelpers.CreateProject("Work", 0, 0, "Browser", "Browser");
            string stamp = previous.Applications[0].Id;
            previous.Applications[1].Id = stamp;
            previous.Applications[1].CommandLineArguments = "--second";
            previous.Applications[1].IsElevated = true;
            Project captured = TestHelpers.CreateProject("Capture", 0, 0, "Browser", "Browser", "Browser");
            foreach (Application app in captured.Applications)
            {
                app.Id = stamp;
            }

            captured.Applications[0].CommandLineArguments = "--first";
            captured.Applications[0].IsElevated = true;
            EditEvent edit = EditEventFactory.Create(previous, captured);
            Assert.AreEqual(1, edit.AppsAdded);
            Assert.AreEqual(0, edit.AppsRemoved);
            Assert.AreEqual(1, edit.CliAdded);
            Assert.AreEqual(1, edit.CliRemoved);
            Assert.AreEqual(1, edit.AdminAdded);
            Assert.AreEqual(1, edit.AdminRemoved);
            Assert.AreEqual(1, EditEventFactory.Create(captured, previous).AppsRemoved);

            captured.UpdateAfterLaunchAndEdit(previous);

            Assert.AreEqual(string.Empty, captured.Applications[0].CommandLineArguments);
            Assert.IsFalse(captured.Applications[0].IsElevated);
            Assert.AreEqual("--second", captured.Applications[1].CommandLineArguments);
            Assert.IsTrue(captured.Applications[1].IsElevated);
            Assert.AreEqual(string.Empty, captured.Applications[2].CommandLineArguments);
            Assert.IsFalse(captured.Applications[2].IsElevated);
            Assert.AreNotEqual(captured.Applications[1].Id, captured.Applications[2].Id);
        }

        [TestMethod]
        public void Recapture_PrefersTheWindowWithTheSameTitleEvenWhenItMovedBehindANewWindow()
        {
            Project previous = TestHelpers.CreateProject("Work", 0, 0, "Editor");
            Application original = previous.Applications[0];
            original.AppTitle = "Original document";
            original.CommandLineArguments = "--original";
            original.IsElevated = true;

            Project captured = TestHelpers.CreateProject("Capture", 0, 0, "Editor", "Editor");
            Application extra = captured.Applications[0];
            extra.Id = original.Id;
            extra.AppTitle = "New document";
            extra.CommandLineArguments = "--captured";
            Application retained = captured.Applications[1];
            retained.Id = original.Id;
            retained.AppTitle = original.AppTitle;
            retained.Position = new Application.WindowPosition { X = 100, Y = 200, Width = 900, Height = 600 };

            var edit = EditEventFactory.Create(previous, captured);
            Assert.AreEqual(1, edit.AppsAdded);
            Assert.AreEqual(1, edit.CliRemoved);
            captured.UpdateAfterLaunchAndEdit(previous);

            Assert.AreEqual(original.Id, retained.Id);
            Assert.AreEqual("--original", retained.CommandLineArguments);
            Assert.IsTrue(retained.IsElevated);
            Assert.AreNotEqual(original.Id, extra.Id);
            Assert.AreEqual("--captured", extra.CommandLineArguments);
            Assert.IsFalse(extra.IsElevated);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Recapture_AssignsNewIdentitiesWhenNoWindowMatchesThePreviousStamp(bool reverse)
        {
            Project previous = TestHelpers.CreateProject("Work", 0, 0, "Editor");
            var original = previous.Applications[0];
            original.AppTitle = "Original document";
            original.IsElevated = true;
            original.CommandLineArguments = "--original";
            Project captured = TestHelpers.CreateProject("Capture", 0, 0, "Editor", "Editor");
            for (int i = 0; i < captured.Applications.Count; i++)
            {
                var app = captured.Applications[i];
                app.Id = original.Id;
                app.AppTitle = i == 0 ? "Notes" : "Report";
                app.Position = new Application.WindowPosition { X = i + 100, Width = 1000, Height = 500 };
            }

            if (reverse)
            {
                captured.Applications.Reverse();
            }

            captured.UpdateAfterLaunchAndEdit(previous);

            Assert.IsTrue(captured.Applications.TrueForAll(app => !app.IsElevated && string.IsNullOrEmpty(app.CommandLineArguments)));
            Guid reservedId = Guid.Parse(original.Id);
            foreach (var window in captured.Applications)
            {
                Assert.IsTrue(Guid.TryParseExact(window.Id, "B", out var id));
                Assert.AreNotEqual(reservedId, id);
            }

            Assert.AreNotEqual(captured.Applications[0].Id, captured.Applications[1].Id);
            var telemetry = EditEventFactory.Create(previous, captured);
            Assert.AreEqual(2, telemetry.AppsAdded);
            Assert.AreEqual(1, telemetry.AppsRemoved);
            Assert.AreEqual(0, telemetry.AdminRemoved);
            Assert.AreEqual(0, telemetry.CliRemoved);
        }

        [TestMethod]
        public void Recapture_NewGroupWithNoPreviousStampKeepsItsFirstIdentity()
        {
            Project previous = TestHelpers.CreateProject("Work", 0, 0, "Old app");
            Project captured = TestHelpers.CreateProject("Capture", 0, 0, "Browser", "Browser");
            string newStamp = captured.Applications[0].Id;
            captured.Applications[1].Id = newStamp;

            captured.UpdateAfterLaunchAndEdit(previous);

            Assert.AreEqual(newStamp, captured.Applications[0].Id);
            Assert.AreNotEqual(newStamp, captured.Applications[1].Id);
        }

        [TestMethod]
        public void Telemetry_PairsBothDocumentsWhenTheirEnumerationOrderReverses()
        {
            Project previous = TestHelpers.CreateProject("Work", 0, 0, "Editor", "Editor");
            string stamp = previous.Applications[0].Id;
            previous.Applications[0].AppTitle = "Elevated document";
            previous.Applications[0].IsElevated = true;
            previous.Applications[0].CommandLineArguments = "--elevated";
            previous.Applications[1].Id = stamp;
            previous.Applications[1].AppTitle = "Ordinary document";
            Project captured = new(previous);
            captured.Applications.Reverse();

            var edit = EditEventFactory.Create(previous, captured);

            Assert.AreEqual(0, edit.AppsAdded);
            Assert.AreEqual(0, edit.AppsRemoved);
            Assert.AreEqual(0, edit.CliAdded);
            Assert.AreEqual(0, edit.CliRemoved);
            Assert.AreEqual(0, edit.AdminAdded);
            Assert.AreEqual(0, edit.AdminRemoved);
        }

        [TestMethod]
        public void StoredRowsRepairDuplicateIdsWhileCaptureRetainsOriginalStamps()
        {
            Guid stamp = Guid.NewGuid();
            ProjectWrapper wrapper = new()
            {
                Id = Guid.NewGuid().ToString("B"),
                Applications =
                [
                    new() { Id = stamp.ToString("B"), CommandLineArguments = "--first" },
                    new() { Id = stamp.ToString("D"), CommandLineArguments = "--second" },
                    new() { Id = "malformed" },
                ],
                MonitorConfiguration = [],
            };

            Project stored = Project.FromStorage(wrapper);
            Project rawCapture = new(wrapper);

            Assert.IsTrue(stored.NeedsApplicationIdRepair);
            Assert.IsFalse(rawCapture.NeedsApplicationIdRepair);
            Assert.AreEqual(stamp.ToString("B"), stored.Applications[0].Id);
            Assert.AreEqual(3, stored.Applications.Select(app => Guid.ParseExact(app.Id, "B")).Distinct().Count());
            Assert.AreEqual("--second", stored.Applications[1].CommandLineArguments);
            Assert.AreEqual(stamp, Guid.Parse(rawCapture.Applications[0].Id));
            Assert.AreEqual(stamp, Guid.Parse(rawCapture.Applications[1].Id));

            wrapper.Applications.RemoveRange(1, 2);
            Assert.IsFalse(Project.FromStorage(wrapper).NeedsApplicationIdRepair);
        }

        [TestMethod]
        public void Recapture_PreservesOnlyLayoutFieldsEditedWhileTheToolWasRunning()
        {
            Project beforeCapture = TestHelpers.CreateProject("Before", 0, 0, "Editor");
            Project current = new(beforeCapture) { Name = "Renamed", MoveExistingWindows = true, IsShortcutNeeded = true };
            var edited = current.Applications[0];
            edited.UpdatePositionY("75");
            edited.IsIncluded = false;
            edited.CommandLineArguments = "--late";
            edited.IsElevated = true;
            edited.Minimized = true;
            Project captured = TestHelpers.CreateProject("Capture", 0, 0, "Editor");
            var window = captured.Applications[0];
            window.Id = edited.Id;
            window.Position = new Application.WindowPosition { X = 25, Y = 55, Width = 900, Height = 700 };

            captured.UpdateAfterLaunchAndEdit(beforeCapture, current);

            Assert.AreEqual("Renamed", captured.Name);
            Assert.IsTrue(captured.IsShortcutNeeded);
            Assert.IsTrue(captured.MoveExistingWindows);
            Assert.IsTrue(captured.IsPositionChangedManually);
            Assert.AreEqual("--late", window.CommandLineArguments);
            Assert.IsTrue(window.IsElevated);
            Assert.IsFalse(window.IsIncluded);
            Assert.IsTrue(window.Minimized);
            Assert.AreEqual(new Application.WindowPosition { X = 25, Y = 75, Width = 900, Height = 700 }, window.Position);
        }

        [TestMethod]
        public void Recapture_AlreadyExcludedWindowStaysOutOfTheSavedWorkspace()
        {
            Project original = TestHelpers.CreateProject("Work", 0, 0, "Editor", "Browser");
            Project draft = new(original);
            draft.Applications[1].IsIncluded = false;
            var launched = new Project(draft);
            Project capture = TestHelpers.CreateProject("Capture", 0, 0, "Editor", "Browser");
            capture.Applications[0].Id = draft.Applications[0].Id;
            capture.Applications[1].Id = draft.Applications[1].Id;
            capture.UpdateAfterLaunchAndEdit(launched, draft);
            Assert.AreEqual(2, capture.Applications.Count);
            Assert.IsFalse(capture.Applications[1].IsIncluded);

            var folder = Directory.CreateTempSubdirectory("PowerToys-Recapture-");
            try
            {
                var storage = new WorkspacesEditorIO(Path.Combine(folder.FullName, "temp-workspaces.json"), (_, _) => throw new InvalidOperationException());
                storage.SerializeWorkspaces([original]);
                var saved = new Project(capture, original);
                saved.Applications.RemoveAll(app => !app.IsIncluded);
                storage.PersistChange(original, saved);
                var persisted = new WorkspacesData().Read(Path.Combine(folder.FullName, "workspaces.json")).Workspaces.Single();
                Assert.AreEqual(draft.Applications[0].Id, persisted.Applications.Single().Id);
            }
            finally
            {
                folder.Delete(recursive: true);
            }
        }

        [TestMethod]
        public void RevertCopy_RetainsTitleHistoryAndManualAdjustments()
        {
            Project draft = TestHelpers.CreateProject("Draft", 123, 456, "Browser");
            draft.EditorWindowTitle = "Create workspace";
            draft.IsPositionChangedManually = true;
            draft.IsRevertEnabled = true;

            Project copy = new(draft);

            Assert.AreEqual(draft.Id, copy.Id);
            Assert.AreEqual(draft.EditorWindowTitle, copy.EditorWindowTitle);
            Assert.AreEqual(draft.CreationTime, copy.CreationTime);
            Assert.AreEqual(draft.LastLaunchedTime, copy.LastLaunchedTime);
            Assert.IsTrue(copy.IsPositionChangedManually);
            Assert.IsFalse(copy.IsRevertEnabled);
            Assert.AreNotSame(draft.Applications[0], copy.Applications[0]);
            Assert.AreSame(copy, copy.Applications[0].Parent);
        }

        [TestMethod]
        public void SaveCandidate_UsesDraftLayoutAndOriginalHistory()
        {
            Project previous = TestHelpers.CreateProject("Before", 123, 456, "Browser");
            Project captured = TestHelpers.CreateProject("Updated", 789, 0, "Editor");
            captured.IsPositionChangedManually = true;
            captured.Monitors.Add(new MonitorSetup("Second display", string.Empty, 2, 96, default, default));
            captured.Applications[0].MonitorNumber = 2;

            Project candidate = new(captured, previous);

            Assert.AreEqual(previous.Id, candidate.Id);
            Assert.AreEqual(previous.CreationTime, candidate.CreationTime);
            Assert.AreEqual(previous.LastLaunchedTime, candidate.LastLaunchedTime);
            Assert.AreEqual(captured.Name, candidate.Name);
            Assert.AreEqual("Editor", candidate.Applications[0].AppName);
            Assert.AreEqual(2, candidate.Applications[0].MonitorSetup.MonitorNumber);
            Assert.IsFalse(new Project(candidate).IsPositionChangedManually);
            Assert.IsFalse(EditEventFactory.Create(candidate, new Project(candidate)).PixelAdjustmentsUsed);
            Assert.IsFalse(previous.Monitors.Exists(monitor => monitor.MonitorNumber == 2));
        }

        [TestMethod]
        public void MissingMonitorBlocksSavingUntilTheUnresolvedRowIsExcludedOrRecaptured()
        {
            Project draft = TestHelpers.CreateProject("Work", 0, 0, "Editor", "Browser");
            var moved = draft.Applications[1];
            Assert.IsFalse(draft.HasUnresolvedMonitors);
            Assert.IsTrue(draft.CanBeSaved);

            moved.MonitorNumber = 3;
            Assert.IsTrue(draft.HasUnresolvedMonitors);
            Assert.IsFalse(draft.CanBeSaved);
            Assert.AreEqual(3, moved.MonitorSetup.MonitorNumber);
            Assert.AreEqual(0d, moved.MonitorSetup.MonitorDpiAwareBounds.Width);

            var notifications = new List<string>();
            draft.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
            moved.IsIncluded = false;
            Assert.IsFalse(draft.HasUnresolvedMonitors);
            Assert.IsTrue(draft.CanBeSaved);
            CollectionAssert.Contains(notifications, nameof(Project.HasUnresolvedMonitors));
            CollectionAssert.Contains(notifications, nameof(Project.CanBeSaved));
        }

        [TestMethod]
        [DataRow(0, 96)]
        [DataRow(1, 0)]
        [DataRow(1, -1)]
        public void InvalidMonitorTableRequiresRecaptureEvenWhenAllIncludedAppsHaveAMonitor(int number, int dpi)
        {
            Project draft = TestHelpers.CreateProject("Work", 0, 0, "Editor");
            draft.Monitors.Add(new MonitorSetup("Second", "Second", number, dpi, default, default));

            Assert.IsTrue(draft.HasUnresolvedMonitors);
            Assert.IsFalse(draft.CanBeSaved);

            Project captured = TestHelpers.CreateProject("Capture", 0, 0, "Editor");
            captured.Applications[0].Id = draft.Applications[0].Id;
            captured.UpdateAfterLaunchAndEdit(draft);
            Assert.IsFalse(captured.HasUnresolvedMonitors);
            Assert.IsTrue(captured.CanBeSaved);
        }

        [TestMethod]
        public void CommittedLaunchTimeNotifiesSortAndDisplayOnce()
        {
            Project project = TestHelpers.CreateProject("Work", 0, 12, "Editor");
            var notifications = new List<string>();
            project.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

            project.UpdateLastLaunchedTime(42);
            project.UpdateLastLaunchedTime(42);

            Assert.AreEqual(42L, project.LastLaunchedTime);
            CollectionAssert.AreEqual(new List<string> { nameof(Project.LastLaunchedTime), nameof(Project.LastLaunched) }, notifications);
        }

        [TestMethod]
        public void CanBeSaved_TracksIncludedApplicationsAndBothDimensions()
        {
            Project draft = TestHelpers.CreateProject("Work", 0, 0, "Browser", "Editor");
            Application first = draft.Applications[0];
            Application second = draft.Applications[1];
            second.IsIncluded = false;
            var notifications = new List<bool>();
            draft.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Project.CanBeSaved))
                {
                    notifications.Add(draft.CanBeSaved);
                }
            };

            Assert.IsTrue(draft.CanBeSaved);
            first.SwitchDeletion();
            first.SwitchDeletion();
            first.UpdatePositionWidth("0");
            second.UpdatePositionHeight("-1");
            first.UpdatePositionWidth("640");
            second.SwitchDeletion();
            second.UpdatePositionHeight("480");

            CollectionAssert.AreEqual(new List<bool> { false, true, false, true, false, true }, notifications);
        }
    }
}
