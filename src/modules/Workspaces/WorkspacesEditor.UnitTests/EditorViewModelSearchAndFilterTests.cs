// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesEditor.Models;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor.UnitTests
{
    /// <summary>
    /// Tests for MainViewModel search and filter logic.
    /// The search filters workspaces by name and app name (case-insensitive, partial match).
    /// This behavior must be preserved after the WinUI migration.
    /// </summary>
    [TestClass]
    public class EditorViewModelSearchAndFilterTests
    {
        [TestMethod]
        [TestCategory("ViewModel.Search")]
        public void SearchTerm_Empty_ReturnsAllWorkspaces()
        {
            var vm = TestHelpers.CreateViewModel();
            vm.Workspaces = new ObservableCollection<Project>
            {
                TestHelpers.CreateProject("DevSetup", 0, 0, "VS Code", "Terminal"),
                TestHelpers.CreateProject("Browsing", 0, 0, "Edge", "Notepad"),
            };

            vm.SearchTerm = string.Empty;
            Assert.AreEqual(2, vm.WorkspacesView.Count());
        }

        [TestMethod]
        [TestCategory("ViewModel.Search")]
        public void SearchTerm_Null_ReturnsAllWorkspaces()
        {
            var vm = TestHelpers.CreateViewModel();
            vm.Workspaces = new ObservableCollection<Project>
            {
                TestHelpers.CreateProject("DevSetup", 0, 0, "VS Code"),
                TestHelpers.CreateProject("Browsing", 0, 0, "Edge"),
            };

            vm.SearchTerm = null;
            Assert.AreEqual(2, vm.WorkspacesView.Count());
        }

        [TestMethod]
        [TestCategory("ViewModel.Search")]
        public void SearchTerm_MatchesWorkspaceName_ReturnsMatching()
        {
            var vm = TestHelpers.CreateViewModel();
            vm.Workspaces = new ObservableCollection<Project>
            {
                TestHelpers.CreateProject("DevSetup", 0, 0, "VS Code"),
                TestHelpers.CreateProject("Browsing", 0, 0, "Edge"),
                TestHelpers.CreateProject("DesignWork", 0, 0, "Figma"),
            };

            vm.SearchTerm = "Dev";
            var results = vm.WorkspacesView.ToList();

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("DevSetup", results[0].Name);
        }

        [TestMethod]
        [TestCategory("ViewModel.Search")]
        public void SearchTerm_MatchesAppName_ReturnsWorkspaceContainingApp()
        {
            var vm = TestHelpers.CreateViewModel();
            vm.Workspaces = new ObservableCollection<Project>
            {
                TestHelpers.CreateProject("DevSetup", 0, 0, "VS Code", "Terminal"),
                TestHelpers.CreateProject("Browsing", 0, 0, "Edge", "Notepad"),
            };

            vm.SearchTerm = "Terminal";
            var results = vm.WorkspacesView.ToList();

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("DevSetup", results[0].Name);
        }

        [TestMethod]
        [TestCategory("ViewModel.Search")]
        public void SearchTerm_CaseInsensitive_MatchesRegardlessOfCase()
        {
            var vm = TestHelpers.CreateViewModel();
            vm.Workspaces = new ObservableCollection<Project>
            {
                TestHelpers.CreateProject("DevSetup", 0, 0, "VS Code"),
                TestHelpers.CreateProject("Browsing", 0, 0, "Edge"),
            };

            vm.SearchTerm = "devsetup";
            Assert.AreEqual(1, vm.WorkspacesView.Count());
        }

        [TestMethod]
        [TestCategory("ViewModel.Search")]
        public void SearchTerm_NoMatch_ReturnsEmpty()
        {
            var vm = TestHelpers.CreateViewModel();
            vm.Workspaces = new ObservableCollection<Project>
            {
                TestHelpers.CreateProject("DevSetup", 0, 0, "VS Code"),
                TestHelpers.CreateProject("Browsing", 0, 0, "Edge"),
            };

            vm.SearchTerm = "NonExistent";
            Assert.AreEqual(0, vm.WorkspacesView.Count());
        }

        [TestMethod]
        [TestCategory("ViewModel.Search")]
        public void SearchTerm_PartialMatch_MatchesSubstring()
        {
            var vm = TestHelpers.CreateViewModel();
            vm.Workspaces = new ObservableCollection<Project>
            {
                TestHelpers.CreateProject("MyDevelopmentSetup", 0, 0, "VS Code"),
                TestHelpers.CreateProject("Browsing", 0, 0, "Edge"),
            };

            vm.SearchTerm = "Develop";
            Assert.AreEqual(1, vm.WorkspacesView.Count());
        }

        [TestMethod]
        [TestCategory("ViewModel.Search")]
        public void SearchTerm_MatchesMultiple_ReturnsAll()
        {
            var vm = TestHelpers.CreateViewModel();
            vm.Workspaces = new ObservableCollection<Project>
            {
                TestHelpers.CreateProject("DevSetup1", 0, 0, "VS Code"),
                TestHelpers.CreateProject("DevSetup2", 0, 0, "Terminal"),
                TestHelpers.CreateProject("Browsing", 0, 0, "Edge"),
            };

            vm.SearchTerm = "Dev";
            Assert.AreEqual(2, vm.WorkspacesView.Count());
        }

        [TestMethod]
        [TestCategory("ViewModel.Search")]
        public void SearchTerm_Changed_RaisesPropertyChangedForWorkspacesView()
        {
            var vm = TestHelpers.CreateViewModel();
            vm.Workspaces = new ObservableCollection<Project>
            {
                TestHelpers.CreateProject("Test", 0, 0, "App"),
            };

            string changedProp = null;
            vm.PropertyChanged += (s, e) => changedProp = e.PropertyName;

            vm.SearchTerm = "Test";
            Assert.AreEqual("WorkspacesView", changedProp);
        }

        [TestMethod]
        [TestCategory("ViewModel.Search")]
        public void SearchTerm_EmptyCollection_ReturnsEmptyAndSetsFlag()
        {
            var vm = TestHelpers.CreateViewModel();
            vm.Workspaces = new ObservableCollection<Project>();

            vm.SearchTerm = "anything";
            Assert.AreEqual(0, vm.WorkspacesView.Count());
            Assert.IsTrue(vm.IsWorkspacesViewEmpty);
        }

        [TestMethod]
        [TestCategory("ViewModel.Search")]
        public void SearchTerm_MatchesAppNameCaseInsensitive_ReturnsWorkspace()
        {
            var vm = TestHelpers.CreateViewModel();
            vm.Workspaces = new ObservableCollection<Project>
            {
                TestHelpers.CreateProject("MySetup", 0, 0, "Visual Studio Code"),
            };

            vm.SearchTerm = "visual studio";
            Assert.AreEqual(1, vm.WorkspacesView.Count());
        }
    }
}
