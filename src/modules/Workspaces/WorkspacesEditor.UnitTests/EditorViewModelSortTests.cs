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
    /// Tests for MainViewModel sort logic.
    /// Sorting affects the order of WorkspacesView: by name, creation time, or last-launched.
    /// </summary>
    [TestClass]
    public class EditorViewModelSortTests
    {
        [TestMethod]
        [TestCategory("ViewModel.Sort")]
        public void Sort_ByName_ReturnsAlphabeticalOrder()
        {
            var vm = TestHelpers.CreateViewModel();
            vm.Workspaces = new ObservableCollection<Project>
            {
                TestHelpers.CreateProject("Zebra", 0, 0, "App"),
                TestHelpers.CreateProject("Alpha", 0, 0, "App"),
                TestHelpers.CreateProject("Middle", 0, 0, "App"),
            };

            vm.OrderByIndex = 2; // Name
            var results = vm.WorkspacesView.ToList();

            Assert.AreEqual("Alpha", results[0].Name);
            Assert.AreEqual("Middle", results[1].Name);
            Assert.AreEqual("Zebra", results[2].Name);
        }

        [TestMethod]
        [TestCategory("ViewModel.Sort")]
        public void Sort_ByCreated_ReturnsNewestFirst()
        {
            var vm = TestHelpers.CreateViewModel();
            vm.Workspaces = new ObservableCollection<Project>
            {
                TestHelpers.CreateProject("Oldest", 1000, 0, "App"),
                TestHelpers.CreateProject("Newest", 3000, 0, "App"),
                TestHelpers.CreateProject("Middle", 2000, 0, "App"),
            };

            vm.OrderByIndex = 1; // Created (descending)
            var results = vm.WorkspacesView.ToList();

            Assert.AreEqual("Newest", results[0].Name);
            Assert.AreEqual("Middle", results[1].Name);
            Assert.AreEqual("Oldest", results[2].Name);
        }

        [TestMethod]
        [TestCategory("ViewModel.Sort")]
        public void Sort_ByLastViewed_ReturnsMostRecentFirst()
        {
            var vm = TestHelpers.CreateViewModel();
            vm.Workspaces = new ObservableCollection<Project>
            {
                TestHelpers.CreateProject("LeastRecent", 0, 1000, "App"),
                TestHelpers.CreateProject("MostRecent", 0, 3000, "App"),
                TestHelpers.CreateProject("Middle", 0, 2000, "App"),
            };

            vm.OrderByIndex = 0; // LastViewed (descending)
            var results = vm.WorkspacesView.ToList();

            Assert.AreEqual("MostRecent", results[0].Name);
            Assert.AreEqual("Middle", results[1].Name);
            Assert.AreEqual("LeastRecent", results[2].Name);
        }

        [TestMethod]
        [TestCategory("ViewModel.Sort")]
        public void Sort_OrderByIndex_RaisesPropertyChanged()
        {
            var vm = TestHelpers.CreateViewModel();
            vm.Workspaces = new ObservableCollection<Project>();

            string changedProp = null;
            vm.PropertyChanged += (s, e) => changedProp = e.PropertyName;

            vm.OrderByIndex = 1;
            Assert.AreEqual("WorkspacesView", changedProp);
        }

        [TestMethod]
        [TestCategory("ViewModel.Sort")]
        public void Sort_CombinedWithFilter_FilteredResultsAreSorted()
        {
            var vm = TestHelpers.CreateViewModel();
            vm.Workspaces = new ObservableCollection<Project>
            {
                TestHelpers.CreateProject("Z Dev", 0, 0, "VS Code"),
                TestHelpers.CreateProject("A Dev", 0, 0, "Terminal"),
                TestHelpers.CreateProject("Browsing", 0, 0, "Edge"),
            };

            vm.OrderByIndex = 2; // Name
            vm.SearchTerm = "Dev";
            var results = vm.WorkspacesView.ToList();

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual("A Dev", results[0].Name);
            Assert.AreEqual("Z Dev", results[1].Name);
        }
    }
}
