// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UITests;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium.Windows;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorHelper;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class RunFancyZonesEditorTest : UITestBase
    {
        public RunFancyZonesEditorTest()
            : base(PowerToysModule.FancyZone, WindowSize.UnSpecified)
        {
        }

        [TestInitialize]
        public void TestInitialize()
        {
            FancyZonesEditorHelper.Files.Restore();

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
            FancyZonesEditorHelper.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            LayoutTemplates layoutTemplates = new LayoutTemplates();
            LayoutTemplates.TemplateLayoutsListWrapper templateLayoutsListWrapper = new LayoutTemplates.TemplateLayoutsListWrapper
            {
                LayoutTemplates = new List<LayoutTemplates.TemplateLayoutWrapper>
                {
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = LayoutType.Blank.TypeToString(),
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = LayoutType.Focus.TypeToString(),
                        ZoneCount = 10,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = LayoutType.Rows.TypeToString(),
                        ZoneCount = 2,
                        ShowSpacing = true,
                        Spacing = 10,
                        SensitivityRadius = 10,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = LayoutType.Columns.TypeToString(),
                        ZoneCount = 2,
                        ShowSpacing = true,
                        Spacing = 20,
                        SensitivityRadius = 20,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = LayoutType.Grid.TypeToString(),
                        ZoneCount = 4,
                        ShowSpacing = false,
                        Spacing = 10,
                        SensitivityRadius = 30,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = LayoutType.PriorityGrid.TypeToString(),
                        ZoneCount = 3,
                        ShowSpacing = true,
                        Spacing = 1,
                        SensitivityRadius = 40,
                    },
                },
            };
            FancyZonesEditorHelper.Files.LayoutTemplatesIOHelper.WriteData(layoutTemplates.Serialize(templateLayoutsListWrapper));

            CustomLayouts customLayouts = new CustomLayouts();
            CustomLayouts.CustomLayoutListWrapper customLayoutListWrapper = new CustomLayouts.CustomLayoutListWrapper
            {
                CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper>
                {
                    new CustomLayouts.CustomLayoutWrapper
                    {
                        Uuid = "{E7807D0D-6223-4883-B15B-1F3883944C09}",
                        Type = CustomLayout.Canvas.TypeToString(),
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
            FancyZonesEditorHelper.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));

            DefaultLayouts defaultLayouts = new DefaultLayouts();
            DefaultLayouts.DefaultLayoutsListWrapper defaultLayoutsListWrapper = new DefaultLayouts.DefaultLayoutsListWrapper
            {
                DefaultLayouts = new List<DefaultLayouts.DefaultLayoutWrapper> { },
            };
            FancyZonesEditorHelper.Files.DefaultLayoutsIOHelper.WriteData(defaultLayouts.Serialize(defaultLayoutsListWrapper));

            LayoutHotkeys layoutHotkeys = new LayoutHotkeys();
            LayoutHotkeys.LayoutHotkeysWrapper layoutHotkeysWrapper = new LayoutHotkeys.LayoutHotkeysWrapper
            {
                LayoutHotkeys = new List<LayoutHotkeys.LayoutHotkeyWrapper> { },
            };
            FancyZonesEditorHelper.Files.LayoutHotkeysIOHelper.WriteData(layoutHotkeys.Serialize(layoutHotkeysWrapper));

            AppliedLayouts appliedLayouts = new AppliedLayouts();
            AppliedLayouts.AppliedLayoutsListWrapper appliedLayoutsWrapper = new AppliedLayouts.AppliedLayoutsListWrapper
            {
                AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper> { },
            };
            FancyZonesEditorHelper.Files.AppliedLayoutsIOHelper.WriteData(appliedLayouts.Serialize(appliedLayoutsWrapper));

            this.RestartScopeExe();
        }

        [TestMethod]
        public void OpenNewLayoutDialog() // verify the new layout dialog is opened
        {
            Session.Find<Button>(By.AccessibilityId(AccessibilityId.NewLayoutButton)).Click();
            Assert.IsNotNull(Session.Find<Element>("Choose layout type")); // check the pane header
        }

        [TestMethod]
        public void OpenEditLayoutDialog() // verify the edit layout dialog is opened
        {
            Session.Find<Button>(TestConstants.TemplateLayoutNames[LayoutType.Grid]).Click();
            Assert.IsNotNull(Session.Find<Element>(By.AccessibilityId(FancyZonesEditorHelper.AccessibilityId.DialogTitle))); // check the pane header
            Assert.IsNotNull(Session.Find<Element>($"Edit '{TestConstants.TemplateLayoutNames[LayoutType.Grid]}'")); // verify it's opened for the correct layout
        }

        [TestMethod]
        public void OpenEditLayoutDialog_ByContextMenu_TemplateLayout() // verify the edit layout dialog is opened
        {
            Session.Find<Button>(TestConstants.TemplateLayoutNames[LayoutType.Grid]).Click(true);
            var menu = Session.Find<Element>(By.ClassName(ClassName.ContextMenu));
            menu.Find<Element>(FancyZonesEditorHelper.ElementName.Edit).Click();

            Assert.IsNotNull(Session.Find<Element>(By.AccessibilityId(FancyZonesEditorHelper.AccessibilityId.DialogTitle))); // check the pane header
            Assert.IsNotNull(Session.Find<Element>($"Edit '{TestConstants.TemplateLayoutNames[LayoutType.Grid]}'")); // verify it's opened for the correct layout
        }

        [TestMethod]
        public void OpenEditLayoutDialog_ByContextMenu_CustomLayout() // verify the edit layout dialog is opened
        {
            string layoutName = "Custom layout";
            Session.Find<Button>(layoutName).Click(true);
            var menu = Session.Find<Element>(By.ClassName(ClassName.ContextMenu));
            menu.Find<Element>(FancyZonesEditorHelper.ElementName.Edit).Click();

            Assert.IsNotNull(Session.Find<Element>(By.AccessibilityId(FancyZonesEditorHelper.AccessibilityId.DialogTitle))); // check the pane header
            Assert.IsNotNull(Session.Find<Element>($"Edit '{layoutName}'")); // verify it's opened for the correct layout
        }

        [TestMethod]
        public void OpenContextMenu() // verify the context menu is opened
        {
            Session.Find<Button>(TestConstants.TemplateLayoutNames[LayoutType.Columns]).Click(true);
            Assert.IsNotNull(Session.Find<Element>(By.ClassName(ClassName.ContextMenu)));
        }

        [TestMethod]
        public void ClickMonitor()
        {
            Assert.IsNotNull(Session.Find<Element>("Monitor 1"));
            Assert.IsNotNull(Session.Find<Element>("Monitor 2"));

            // verify that the monitor 1 is selected initially
            Assert.IsTrue(Session.Find<Element>("Monitor 1").Selected);
            Assert.IsFalse(Session.Find<Element>("Monitor 2").Selected);

            Session.Find<Element>("Monitor 2").Click();

            // verify that the monitor 2 is selected after click
            Assert.IsFalse(Session.Find<Element>("Monitor 1").Selected);
            Assert.IsTrue(Session.Find<Element>("Monitor 2").Selected);
        }
    }
}
