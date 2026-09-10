// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesEditor.Models;

namespace WorkspacesEditor.UnitTests
{
    /// <summary>
    /// Tests for the Application model: state toggles, computed properties,
    /// position management, and copy semantics.
    /// </summary>
    [TestClass]
    public class ApplicationModelTests
    {
        [TestMethod]
        [TestCategory("Model.Application")]
        public void SwitchDeletion_InitiallyIncluded_TogglesOff()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "Notepad");
            var app = project.Applications[0];
            app.IsIncluded = true;

            app.IsIncluded = !app.IsIncluded;

            Assert.IsFalse(app.IsIncluded);
        }

        [TestMethod]
        [TestCategory("Model.Application")]
        public void SwitchDeletion_InitiallyExcluded_TogglesOn()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "Notepad");
            var app = project.Applications[0];
            app.IsIncluded = false;

            app.IsIncluded = !app.IsIncluded;

            Assert.IsTrue(app.IsIncluded);
        }

        [TestMethod]
        [TestCategory("Model.Application")]
        public void SwitchDeletion_DoubleToggle_ReturnsToOriginal()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "Notepad");
            var app = project.Applications[0];
            app.IsIncluded = true;

            app.IsIncluded = !app.IsIncluded;
            app.IsIncluded = !app.IsIncluded;

            Assert.IsTrue(app.IsIncluded);
        }

        [TestMethod]
        [TestCategory("Model.Application")]
        public void AppMainParams_NotElevatedNoArgs_ReturnsEmpty()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "Notepad");
            var app = project.Applications[0];
            app.IsElevated = false;
            app.CommandLineArguments = string.Empty;

            Assert.AreEqual(string.Empty, app.AppMainParams);
        }

        [TestMethod]
        [TestCategory("Model.Application")]
        public void AppMainParams_ElevatedNoArgs_ContainsText()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "Regedit");
            var app = project.Applications[0];
            app.IsElevated = true;
            app.CommandLineArguments = string.Empty;

            Assert.IsTrue(app.AppMainParams.Length > 0);
        }

        [TestMethod]
        [TestCategory("Model.Application")]
        public void AppMainParams_NotElevatedWithArgs_ContainsArgs()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "Code");
            var app = project.Applications[0];
            app.IsElevated = false;
            app.CommandLineArguments = "--new-window";

            Assert.IsTrue(app.AppMainParams.Contains("--new-window", System.StringComparison.Ordinal));
        }

        [TestMethod]
        [TestCategory("Model.Application")]
        public void AppMainParams_ElevatedWithArgs_ContainsBoth()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "Code");
            var app = project.Applications[0];
            app.IsElevated = true;
            app.CommandLineArguments = "--reuse-window";

            var result = app.AppMainParams;
            Assert.IsTrue(result.Contains("--reuse-window", System.StringComparison.Ordinal));
            Assert.IsTrue(result.Contains('|'), "Should have separator between admin and args");
        }

        [TestMethod]
        [TestCategory("Model.Application")]
        public void PositionComboboxIndex_Custom_ReturnsZero()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "App");
            var app = project.Applications[0];
            app.Minimized = false;
            app.Maximized = false;

            Assert.AreEqual(0, app.PositionComboboxIndex);
        }

        [TestMethod]
        [TestCategory("Model.Application")]
        public void PositionComboboxIndex_Maximized_ReturnsOne()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "App");
            var app = project.Applications[0];
            app.Minimized = false;
            app.Maximized = true;

            Assert.AreEqual(1, app.PositionComboboxIndex);
        }

        [TestMethod]
        [TestCategory("Model.Application")]
        public void PositionComboboxIndex_Minimized_ReturnsTwo()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "App");
            var app = project.Applications[0];
            app.Minimized = true;
            app.Maximized = false;

            Assert.AreEqual(2, app.PositionComboboxIndex);
        }

        [TestMethod]
        [TestCategory("Model.Application")]
        public void EditPositionEnabled_CustomPosition_ReturnsTrue()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "App");
            var app = project.Applications[0];
            app.Minimized = false;
            app.Maximized = false;

            Assert.IsTrue(app.EditPositionEnabled);
        }

        [TestMethod]
        [TestCategory("Model.Application")]
        public void EditPositionEnabled_Maximized_ReturnsFalse()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "App");
            var app = project.Applications[0];
            app.Maximized = true;

            Assert.IsFalse(app.EditPositionEnabled);
        }

        [TestMethod]
        [TestCategory("Model.Application")]
        public void EditPositionEnabled_Minimized_ReturnsFalse()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "App");
            var app = project.Applications[0];
            app.Minimized = true;

            Assert.IsFalse(app.EditPositionEnabled);
        }

        [TestMethod]
        [TestCategory("Model.Application")]
        public void RepeatIndexString_IndexZeroOrOne_ReturnsEmpty()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "App");
            var app = project.Applications[0];

            app.RepeatIndex = 0;
            Assert.AreEqual(string.Empty, app.RepeatIndexString);

            app.RepeatIndex = 1;
            Assert.AreEqual(string.Empty, app.RepeatIndexString);
        }

        [TestMethod]
        [TestCategory("Model.Application")]
        public void RepeatIndexString_IndexGreaterThanOne_ReturnsNumber()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "App");
            var app = project.Applications[0];

            app.RepeatIndex = 2;
            Assert.AreEqual("2", app.RepeatIndexString);

            app.RepeatIndex = 5;
            Assert.AreEqual("5", app.RepeatIndexString);
        }

        [TestMethod]
        [TestCategory("Model.Application")]
        public void WindowPosition_Equality_SameValues_ReturnsTrue()
        {
            var pos1 = new Application.WindowPosition { X = 100, Y = 200, Width = 800, Height = 600 };
            var pos2 = new Application.WindowPosition { X = 100, Y = 200, Width = 800, Height = 600 };

            Assert.IsTrue(pos1 == pos2);
        }

        [TestMethod]
        [TestCategory("Model.Application")]
        public void WindowPosition_Inequality_DifferentValues_ReturnsTrue()
        {
            var pos1 = new Application.WindowPosition { X = 0, Y = 0, Width = 1920, Height = 1080 };
            var pos2 = new Application.WindowPosition { X = 960, Y = 0, Width = 960, Height = 1080 };

            Assert.IsTrue(pos1 != pos2);
        }

        [TestMethod]
        [TestCategory("Model.Application")]
        public void CopyConstructor_CopiesAllFields()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "VS Code");
            var original = project.Applications[0];
            original.CommandLineArguments = "--new-window";
            original.IsElevated = true;
            original.Maximized = true;
            original.MonitorNumber = 2;
            original.RepeatIndex = 3;

            var copy = new Application(original);

            Assert.AreEqual(original.AppName, copy.AppName);
            Assert.AreEqual(original.CommandLineArguments, copy.CommandLineArguments);
            Assert.AreEqual(original.IsElevated, copy.IsElevated);
            Assert.AreEqual(original.Maximized, copy.Maximized);
            Assert.AreEqual(original.MonitorNumber, copy.MonitorNumber);
            Assert.AreEqual(original.RepeatIndex, copy.RepeatIndex);
        }

        [TestMethod]
        [TestCategory("Model.Application")]
        public void IsAppMainParamVisible_EmptyParams_ReturnsFalse()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "App");
            var app = project.Applications[0];
            app.IsElevated = false;
            app.CommandLineArguments = string.Empty;

            _ = app.AppMainParams;
            Assert.IsFalse(app.IsAppMainParamVisible);
        }

        [TestMethod]
        [TestCategory("Model.Application")]
        public void IsAppMainParamVisible_HasParams_ReturnsTrue()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "App");
            var app = project.Applications[0];
            app.IsElevated = true;

            _ = app.AppMainParams;
            Assert.IsTrue(app.IsAppMainParamVisible);
        }
    }
}
