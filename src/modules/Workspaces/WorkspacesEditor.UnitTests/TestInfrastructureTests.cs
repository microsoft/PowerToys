// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesEditor.Models;

namespace WorkspacesEditor.UnitTests
{
    /// <summary>
    /// Smoke test to verify the test infrastructure compiles and Project/Application
    /// objects can be created for testing.
    /// </summary>
    [TestClass]
    public class TestInfrastructureTests
    {
        [TestMethod]
        [TestCategory("Infrastructure")]
        public void CreateProject_WithApps_ReturnsValidProject()
        {
            var project = TestHelpers.CreateProject("TestWorkspace", 0, 0, "Notepad", "VS Code");

            Assert.IsNotNull(project);
            Assert.AreEqual("TestWorkspace", project.Name);
            Assert.AreEqual(2, project.Applications.Count);
        }

        [TestMethod]
        [TestCategory("Infrastructure")]
        public void CreateProject_ApplicationNames_AreCorrect()
        {
            var project = TestHelpers.CreateProject("Test", 0, 0, "App1", "App2", "App3");

            Assert.AreEqual("App1", project.Applications[0].AppName);
            Assert.AreEqual("App2", project.Applications[1].AppName);
            Assert.AreEqual("App3", project.Applications[2].AppName);
        }

        [TestMethod]
        [TestCategory("Infrastructure")]
        public void CreateProject_NoApps_ReturnsEmptyApplicationsList()
        {
            var project = TestHelpers.CreateProject("EmptyWorkspace");

            Assert.IsNotNull(project.Applications);
            Assert.AreEqual(0, project.Applications.Count);
        }
    }
}
