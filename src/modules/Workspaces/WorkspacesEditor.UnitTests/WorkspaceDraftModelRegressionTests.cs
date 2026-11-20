// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesEditor.Models;
using WorkspacesEditor.Telemetry;

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
            captured.Monitors.Add(new MonitorSetup("Second display", string.Empty, 2, 96, default, default));
            captured.Applications[0].MonitorNumber = 2;

            Project candidate = new(captured, previous);

            Assert.AreEqual(previous.Id, candidate.Id);
            Assert.AreEqual(previous.CreationTime, candidate.CreationTime);
            Assert.AreEqual(previous.LastLaunchedTime, candidate.LastLaunchedTime);
            Assert.AreEqual(captured.Name, candidate.Name);
            Assert.AreEqual("Editor", candidate.Applications[0].AppName);
            Assert.AreEqual(2, candidate.Applications[0].MonitorSetup.MonitorNumber);
            Assert.AreEqual(0, previous.Monitors.Count);
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
