// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesCsharpLibrary.Data;
using WorkspacesEditor.Models;

namespace WorkspacesEditor.UnitTests
{
    [TestClass]
    public class ProjectRecaptureTests
    {
        [TestMethod]
        [TestCategory("Model.Project")]
        public void UpdateAfterLaunchAndEdit_PreservesSettingsAndCapturedLayout()
        {
            const string appId = "{A8599738-4046-4C13-A105-1E742866AF13}";
            var projectBefore = CreateProject("original", "My workspace", 100, 200, new ApplicationWrapper
            {
                Id = appId,
                Application = "Browser",
                ApplicationPath = @"C:\Browser.exe",
                CommandLineArguments = "--profile-directory=Work",
                IsElevated = false,
                Position = new ApplicationWrapper.WindowPositionWrapper { X = 10, Y = 20, Width = 640, Height = 480 },
                Monitor = 1,
            });
            projectBefore.MoveExistingWindows = true;
            projectBefore.IsShortcutNeeded = true;

            var captured = CreateProject("snapshot", string.Empty, 300, 0, new ApplicationWrapper
            {
                Id = appId.ToLowerInvariant(),
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
            var projectBefore = CreateProject(
                "original",
                "My workspace",
                100,
                200,
                CreateApp("first", "--first", true),
                CreateApp("second", "--second", false),
                CreateApp("removed", "--removed", true));
            var captured = CreateProject(
                "snapshot",
                string.Empty,
                300,
                0,
                CreateApp("SECOND", string.Empty, true),
                CreateApp("FIRST", string.Empty, false),
                CreateApp("new", "--captured", true));
            Application newApplication = captured.Applications[2];

            captured.UpdateAfterLaunchAndEdit(projectBefore);

            Assert.AreEqual(3, captured.Applications.Count);
            Assert.AreEqual("--second", captured.Applications[0].CommandLineArguments);
            Assert.IsFalse(captured.Applications[0].IsElevated);
            Assert.AreEqual("--first", captured.Applications[1].CommandLineArguments);
            Assert.IsTrue(captured.Applications[1].IsElevated);
            Assert.AreSame(newApplication, captured.Applications[2]);
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
            };
        }

        private static Project CreateProject(string id, string name, long creationTime, long lastLaunchedTime, params ApplicationWrapper[] applications)
        {
            return new Project(new ProjectWrapper
            {
                Id = id,
                Name = name,
                CreationTime = creationTime,
                LastLaunchedTime = lastLaunchedTime,
                Applications = [.. applications],
                MonitorConfiguration = [],
            });
        }
    }
}
