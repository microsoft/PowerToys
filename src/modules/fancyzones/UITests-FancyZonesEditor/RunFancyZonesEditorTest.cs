// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class RunFancyZonesEditorTest
    {
        private static FancyZonesEditorSession? _session;
        private static TestContext? _context;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            _context = testContext;

            // prepare test editor parameters with 2 monitors before launching the editor
            EditorParameters editorParameters = new EditorParameters();
            EditorParameters.ParamsWrapper parameters = new EditorParameters.ParamsWrapper
            {
                ProcessId = 1,
                SpanZonesAcrossMonitors = false,
                Monitors = new List<EditorParameters.NativeMonitorDataWrapper>
                {
                    new EditorParameters.NativeMonitorDataWrapper
                    {
                        Monitor = "monitor-1",
                        MonitorInstanceId = "instance-id-1",
                        MonitorSerialNumber = "serial-number-1",
                        MonitorNumber = 1,
                        VirtualDesktop = "{FF34D993-73F3-4B8C-AA03-73730A01D6A8}",
                        Dpi = 96,
                        LeftCoordinate = 0,
                        TopCoordinate = 0,
                        WorkAreaHeight = 1040,
                        WorkAreaWidth = 1920,
                        MonitorHeight = 1080,
                        MonitorWidth = 1920,
                        IsSelected = true,
                    },
                    new EditorParameters.NativeMonitorDataWrapper
                    {
                        Monitor = "monitor-2",
                        MonitorInstanceId = "instance-id-2",
                        MonitorSerialNumber = "serial-number-2",
                        MonitorNumber = 2,
                        VirtualDesktop = "{FF34D993-73F3-4B8C-AA03-73730A01D6A8}",
                        Dpi = 96,
                        LeftCoordinate = 1920,
                        TopCoordinate = 0,
                        WorkAreaHeight = 1040,
                        WorkAreaWidth = 1920,
                        MonitorHeight = 1080,
                        MonitorWidth = 1920,
                        IsSelected = false,
                    },
                },
            };
            FancyZonesEditorSession.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            LayoutTemplates layoutTemplates = new LayoutTemplates();
            LayoutTemplates.TemplateLayoutsListWrapper templateLayoutsListWrapper = new LayoutTemplates.TemplateLayoutsListWrapper
            {
                LayoutTemplates = new List<LayoutTemplates.TemplateLayoutWrapper>
                {
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Empty],
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Focus],
                        ZoneCount = 10,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Rows],
                        ZoneCount = 2,
                        ShowSpacing = true,
                        Spacing = 10,
                        SensitivityRadius = 10,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Columns],
                        ZoneCount = 2,
                        ShowSpacing = true,
                        Spacing = 20,
                        SensitivityRadius = 20,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Grid],
                        ZoneCount = 4,
                        ShowSpacing = false,
                        Spacing = 10,
                        SensitivityRadius = 30,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.PriorityGrid],
                        ZoneCount = 3,
                        ShowSpacing = true,
                        Spacing = 1,
                        SensitivityRadius = 40,
                    },
                },
            };
            FancyZonesEditorSession.Files.LayoutTemplatesIOHelper.WriteData(layoutTemplates.Serialize(templateLayoutsListWrapper));

            CustomLayouts customLayouts = new CustomLayouts();
            CustomLayouts.CustomLayoutListWrapper customLayoutListWrapper = new CustomLayouts.CustomLayoutListWrapper
            {
                CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper>
                {
                    new CustomLayouts.CustomLayoutWrapper
                    {
                        Uuid = "{E7807D0D-6223-4883-B15B-1F3883944C09}",
                        Type = Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Canvas],
                        Name = "Custom layout",
                        Info = new CustomLayouts().ToJsonElement(new CustomLayouts.CanvasInfoWrapper
                        {
                            RefHeight = 952,
                            RefWidth = 1500,
                            SensitivityRadius = 10,
                            Zones = new List<CustomLayouts.CanvasInfoWrapper.CanvasZoneWrapper> { },
                        }),
                    },
                },
            };
            FancyZonesEditorSession.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));

            DefaultLayouts defaultLayouts = new DefaultLayouts();
            DefaultLayouts.DefaultLayoutsListWrapper defaultLayoutsListWrapper = new DefaultLayouts.DefaultLayoutsListWrapper
            {
                DefaultLayouts = new List<DefaultLayouts.DefaultLayoutWrapper> { },
            };
            FancyZonesEditorSession.Files.DefaultLayoutsIOHelper.WriteData(defaultLayouts.Serialize(defaultLayoutsListWrapper));

            LayoutHotkeys layoutHotkeys = new LayoutHotkeys();
            LayoutHotkeys.LayoutHotkeysWrapper layoutHotkeysWrapper = new LayoutHotkeys.LayoutHotkeysWrapper
            {
                LayoutHotkeys = new List<LayoutHotkeys.LayoutHotkeyWrapper> { },
            };
            FancyZonesEditorSession.Files.LayoutHotkeysIOHelper.WriteData(layoutHotkeys.Serialize(layoutHotkeysWrapper));

            AppliedLayouts appliedLayouts = new AppliedLayouts();
            AppliedLayouts.AppliedLayoutsListWrapper appliedLayoutsWrapper = new AppliedLayouts.AppliedLayoutsListWrapper
            {
                AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper> { },
            };
            FancyZonesEditorSession.Files.AppliedLayoutsIOHelper.WriteData(appliedLayouts.Serialize(appliedLayoutsWrapper));
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            FancyZonesEditorSession.Files.Restore();
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
            _session?.Close();
        }

        [TestMethod]
        public void OpenEditorWindow() // verify the session is initialized
        {
            Assert.IsNotNull(_session?.Session);
        }

        [TestMethod]
        public void OpenNewLayoutDialog() // verify the new layout dialog is opened
        {
            _session?.ClickCreateNewLayout();
            Assert.IsNotNull(_session?.Session?.FindElementsByName("Choose layout type")); // check the pane header
        }

        [TestMethod]
        public void OpenEditLayoutDialog() // verify the edit layout dialog is opened
        {
            _session?.Click_EditLayout(TestConstants.TemplateLayoutNames[Constants.TemplateLayout.Grid]);
            Assert.IsNotNull(_session?.Session?.FindElementByAccessibilityId("EditLayoutDialogTitle")); // check the pane header
            Assert.IsNotNull(_session?.Session?.FindElementsByName("Edit 'Grid'")); // verify it's opened for the correct layout
        }

        [TestMethod]
        public void OpenEditLayoutDialog_ByContextMenu_TemplateLayout() // verify the edit layout dialog is opened
        {
            _session?.ClickContextMenuItem(Constants.TemplateLayoutNames[Constants.TemplateLayouts.Grid], "Edit");
            Assert.IsNotNull(_session?.Session?.FindElementByAccessibilityId("EditLayoutDialogTitle")); // check the pane header
            Assert.IsNotNull(_session?.Session?.FindElementsByName("Edit 'Grid'")); // verify it's opened for the correct layout
        }

        [TestMethod]
        public void OpenEditLayoutDialog_ByContextMenu_CustomLayout() // verify the edit layout dialog is opened
        {
            _session?.ClickContextMenuItem("Custom layout", "Edit");
            Assert.IsNotNull(_session?.Session?.FindElementByAccessibilityId("EditLayoutDialogTitle")); // check the pane header
            Assert.IsNotNull(_session?.Session?.FindElementsByName("Edit 'Grid'")); // verify it's opened for the correct layout
        }

        [TestMethod]
        public void OpenContextMenu() // verify the context menu is opened
        {
            Assert.IsNotNull(_session?.OpenContextMenu(TestConstants.TemplateLayoutNames[Constants.TemplateLayout.Columns]));
        }

        [TestMethod]
        public void ClickMonitor()
        {
            Assert.IsNotNull(_session?.GetMonitorItem(1));
            Assert.IsNotNull(_session?.GetMonitorItem(2));

            // verify that the monitor 1 is selected initially
            Assert.IsTrue(_session?.GetMonitorItem(1)?.Selected);
            Assert.IsFalse(_session?.GetMonitorItem(2)?.Selected);

            _session?.ClickMonitor(2);

            // verify that the monitor 2 is selected after click
            Assert.IsFalse(_session?.GetMonitorItem(1)?.Selected);
            Assert.IsTrue(_session?.GetMonitorItem(2)?.Selected);
        }
    }
}
