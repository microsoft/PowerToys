// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesCsharpLibrary.Data;
using WorkspacesEditor.Models;

namespace WorkspacesEditor.UnitTests
{
    [TestClass]
    public class ProjectRecaptureTests
    {
        private const string SavedProjectId = "{278ac530-f8ed-4bab-9bee-302c67f37650}";
        private const string SnapshotProjectId = "{3b450a72-db59-4a46-8048-b1cd61201267}";
        private static readonly Guid FirstAppId = new("a8599738-4046-4c13-a105-1e742866af13");

        [TestMethod]
        [TestCategory("Model.Project")]
        public void UpdateAfterLaunchAndEdit_PreservesSettingsAndCapturedLayout()
        {
            var projectBefore = CreateProject(SavedProjectId, "My workspace", 100, 200, [CreateMonitor(1, 0)], new ApplicationWrapper
            {
                Id = FirstAppId.ToString("D"),
                Application = "Browser",
                ApplicationPath = @"C:\Browser.exe",
                CommandLineArguments = "--profile-directory=Work",
                IsElevated = false,
                Position = new ApplicationWrapper.WindowPositionWrapper { X = 10, Y = 20, Width = 640, Height = 480 },
                Monitor = 1,
            });
            projectBefore.MoveExistingWindows = true;
            projectBefore.IsShortcutNeeded = true;

            var captured = CreateProject(SnapshotProjectId, string.Empty, 300, 0, [CreateMonitor(1, 0), CreateMonitor(2, 1920)], new ApplicationWrapper
            {
                Id = FirstAppId.ToString("B").ToUpperInvariant(),
                Application = "Browser",
                ApplicationPath = @"C:\Browser.exe",
                CommandLineArguments = string.Empty,
                IsElevated = true,
                Maximized = true,
                Position = new ApplicationWrapper.WindowPositionWrapper { X = 1920, Y = 30, Width = 1000, Height = 700 },
                Monitor = 2,
            });

            captured.UpdateAfterLaunchAndEdit(projectBefore);

            Assert.AreEqual(projectBefore.Id, captured.Id);
            Assert.AreEqual("My workspace", captured.Name);
            Assert.IsTrue(captured.MoveExistingWindows);
            Assert.IsTrue(captured.IsShortcutNeeded);
            Assert.IsTrue(captured.IsRevertEnabled);
            Assert.IsTrue(captured.CanBeSaved);
            Assert.AreEqual(FirstAppId, Guid.Parse(captured.Applications[0].Id));
            Assert.AreEqual("--profile-directory=Work", captured.Applications[0].CommandLineArguments);
            Assert.IsFalse(captured.Applications[0].IsElevated);
            Assert.AreEqual(new Application.WindowPosition { X = 1920, Y = 30, Width = 1000, Height = 700 }, captured.Applications[0].Position);
            Assert.AreEqual(2, captured.Applications[0].MonitorNumber);
            Assert.IsTrue(captured.Applications[0].Maximized);
            Assert.AreEqual(300L, captured.CreationTime);
            Assert.AreEqual(0L, captured.LastLaunchedTime);
        }

        [TestMethod]
        [TestCategory("Model.Project")]
        public void UpdateAfterLaunchAndEdit_MatchesIdsAndKeepsNewCapturedApplications()
        {
            Guid secondAppId = new("4a1f8170-4b6a-4b49-ba37-f55380bea461");
            Guid newAppId = new("9b833110-44a2-46c9-872d-d6668c9cc4d3");
            var projectBefore = CreateProject(
                SavedProjectId,
                "My workspace",
                100,
                200,
                [CreateMonitor(1, 0)],
                CreateApp(FirstAppId.ToString("D"), "--first", true),
                CreateApp(secondAppId.ToString("B"), "--second", false),
                CreateApp("{1bf80372-956e-405a-86b4-3818f59bc726}", "--removed", true));
            var captured = CreateProject(
                SnapshotProjectId,
                string.Empty,
                300,
                0,
                [CreateMonitor(1, 0)],
                CreateApp(secondAppId.ToString("D").ToUpperInvariant(), string.Empty, true),
                CreateApp(FirstAppId.ToString("B").ToUpperInvariant(), string.Empty, false),
                CreateApp(newAppId.ToString("B"), "--captured", true));
            Application newApplication = captured.Applications[2];

            captured.UpdateAfterLaunchAndEdit(projectBefore);

            Assert.AreEqual(3, captured.Applications.Count);
            Assert.AreEqual(secondAppId, Guid.Parse(captured.Applications[0].Id));
            Assert.AreEqual("--second", captured.Applications[0].CommandLineArguments);
            Assert.IsFalse(captured.Applications[0].IsElevated);
            Assert.AreEqual(FirstAppId, Guid.Parse(captured.Applications[1].Id));
            Assert.AreEqual("--first", captured.Applications[1].CommandLineArguments);
            Assert.IsTrue(captured.Applications[1].IsElevated);
            Assert.AreSame(newApplication, captured.Applications[2]);
            Assert.AreEqual(newAppId, Guid.Parse(newApplication.Id));
            Assert.AreEqual("--captured", newApplication.CommandLineArguments);
            Assert.IsTrue(newApplication.IsElevated);
            Assert.AreEqual("--removed", projectBefore.Applications[2].CommandLineArguments);
        }

        private static ApplicationWrapper CreateApp(string id, string arguments, bool isElevated)
        {
            return new ApplicationWrapper
            {
                Id = id,
                Application = "Browser",
                ApplicationPath = @"C:\Browser.exe",
                CommandLineArguments = arguments,
                IsElevated = isElevated,
                Position = new ApplicationWrapper.WindowPositionWrapper { Width = 640, Height = 480 },
                Monitor = 1,
            };
        }

        private static MonitorConfigurationWrapper CreateMonitor(int number, int left)
        {
            var bounds = new MonitorConfigurationWrapper.MonitorRectWrapper { Left = left, Width = 1920, Height = 1080 };
            return new MonitorConfigurationWrapper
            {
                MonitorNumber = number,
                Dpi = 96,
                MonitorRectDpiAware = bounds,
                MonitorRectDpiUnaware = bounds,
            };
        }

        private static Project CreateProject(
            string id,
            string name,
            long creationTime,
            long lastLaunchedTime,
            List<MonitorConfigurationWrapper> monitors,
            params ApplicationWrapper[] applications)
        {
            return new Project(new ProjectWrapper
            {
                Id = id,
                Name = name,
                CreationTime = creationTime,
                LastLaunchedTime = lastLaunchedTime,
                Applications = [.. applications],
                MonitorConfiguration = monitors,
            });
        }
    }
}
