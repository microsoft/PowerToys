// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesEditor.Models;

namespace WorkspacesEditor.UnitTests
{
    /// <summary>
    /// Tests for Project model validation, computed properties, and state management.
    /// </summary>
    [TestClass]
    public class ProjectModelValidationTests
    {
        [TestMethod]
        [TestCategory("Model.Project")]
        public void CanBeSaved_NameAndAppsPresent_ReturnsTrue()
        {
            var project = TestHelpers.CreateProject("My Workspace", 0, 0, "Notepad");
            Assert.IsTrue(project.CanBeSaved);
        }

        [TestMethod]
        [TestCategory("Model.Project")]
        public void CanBeSaved_EmptyName_ReturnsFalse()
        {
            var project = TestHelpers.CreateProject(string.Empty, 0, 0, "Notepad");
            Assert.IsFalse(project.CanBeSaved);
        }

        [TestMethod]
        [TestCategory("Model.Project")]
        public void CanBeSaved_NoApps_ReturnsFalse()
        {
            var project = TestHelpers.CreateProject("Test Workspace");
            Assert.IsFalse(project.CanBeSaved);
        }

        [TestMethod]
        [TestCategory("Model.Project")]
        public void Name_SetValue_RaisesPropertyChanged()
        {
            var project = TestHelpers.CreateProject("Initial", 0, 0, "App");

            var changedProps = new List<string>();
            project.PropertyChanged += (s, e) => changedProps.Add(e.PropertyName);

            project.Name = "Changed";

            Assert.IsTrue(changedProps.Contains("Name"));
            Assert.IsTrue(changedProps.Contains("CanBeSaved"));
        }

        [TestMethod]
        [TestCategory("Model.Project")]
        public void AppsCountString_SingleApp_ContainsOne()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "App1");
            Assert.IsTrue(project.AppsCountString.StartsWith('1'));
        }

        [TestMethod]
        [TestCategory("Model.Project")]
        public void AppsCountString_MultipleApps_ContainsCount()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "App1", "App2", "App3");
            Assert.IsTrue(project.AppsCountString.StartsWith('3'));
        }

        [TestMethod]
        [TestCategory("Model.Project")]
        public void LastLaunched_NeverLaunched_ReturnsNonEmptyString()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "App");
            Assert.IsTrue(project.LastLaunched.Length > 0);
        }

        [TestMethod]
        [TestCategory("Model.Project")]
        public void IsRevertEnabled_SetTrue_RaisesPropertyChanged()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "App");

            string changedProp = null;
            project.PropertyChanged += (s, e) => changedProp = e.PropertyName;

            project.IsRevertEnabled = true;
            Assert.AreEqual("IsRevertEnabled", changedProp);
        }

        [TestMethod]
        [TestCategory("Model.Project")]
        public void IsPopupVisible_SetTrue_RaisesPropertyChanged()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "App");

            string changedProp = null;
            project.PropertyChanged += (s, e) => changedProp = e.PropertyName;

            project.IsPopupVisible = true;
            Assert.AreEqual("IsPopupVisible", changedProp);
        }

        [TestMethod]
        [TestCategory("Model.Project")]
        public void Name_Changed_UpdatesCanBeSaved()
        {
            var project = TestHelpers.CreateProject("Valid", 0, 0, "App");
            Assert.IsTrue(project.CanBeSaved);

            project.Name = string.Empty;
            Assert.IsFalse(project.CanBeSaved);

            project.Name = "Valid Again";
            Assert.IsTrue(project.CanBeSaved);
        }

        [TestMethod]
        [TestCategory("Model.Project")]
        public void MoveExistingWindows_DefaultFalse_CanBeSet()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "App");
            Assert.IsFalse(project.MoveExistingWindows);

            project.MoveExistingWindows = true;
            Assert.IsTrue(project.MoveExistingWindows);
        }

        [TestMethod]
        [TestCategory("Model.Project")]
        public void IsShortcutNeeded_DefaultFalse_CanBeSet()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "App");
            Assert.IsFalse(project.IsShortcutNeeded);

            project.IsShortcutNeeded = true;
            Assert.IsTrue(project.IsShortcutNeeded);
        }
    }
}
