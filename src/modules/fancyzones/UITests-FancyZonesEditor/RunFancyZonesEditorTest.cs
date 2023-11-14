// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UITests_FancyZonesEditor
{
    [TestClass]
    public class RunFancyZonesEditorTest
    {
        private static FancyZonesEditorSession? _session;
        private static TestContext? _context;

        private enum Layouts
        {
            Empty,
            Focus,
            Rows,
            Columns,
            Grid,
            PriorityGrid,
        }

        private static readonly Dictionary<Layouts, string> LayoutNames = new Dictionary<Layouts, string>()
        {
            { Layouts.Empty, "No layout" },
            { Layouts.Focus, "Focus" },
            { Layouts.Rows, "Rows" },
            { Layouts.Columns, "Columns" },
            { Layouts.Grid, "Grid" },
            { Layouts.PriorityGrid, "PriorityGrid" },
        };

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            _context = testContext;
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _context = null;
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _session = new FancyZonesEditorSession(_context!);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _session?.Close(_context!);
        }

        [TestMethod]
        public void OpenEditorWindow() // verify the session is initialized
        {
            Assert.IsNotNull(_session?.Session);
        }

        [TestMethod]
        public void OpenNewLayoutDialog() // verify the new layout dialog is opened
        {
            _session?.Click_CreateNewLayout();
            Assert.IsNotNull(_session?.Session?.FindElementsByName("Choose layout type")); // check the pane header
        }

        [TestMethod]
        public void OpenEditLayoutDialog() // verify the edit layout dialog is opened
        {
            _session?.Click_EditLayout(LayoutNames[Layouts.Grid]);
            Assert.IsNotNull(_session?.Session?.FindElementByAccessibilityId("EditLayoutDialogTitle")); // check the pane header
            Assert.IsNotNull(_session?.Session?.FindElementsByName("Edit 'Grid'")); // verify it's opened for the correct layout
        }

        [TestMethod]
        public void OpenContextMenu() // verify the context menu is opened
        {
            Assert.IsNotNull(_session?.OpenContextMenu(LayoutNames[Layouts.Columns]));
        }
    }
}
