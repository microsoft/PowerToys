// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorHelper;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class CreateLayoutTests : UITestBase
    {
        public CreateLayoutTests()
            : base(PowerToysModule.FancyZone)
        {
        }

        [TestInitialize]
        public void TestInitialize()
        {
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
                CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper> { },
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

        [TestCleanup]
        public void TestCleanup()
        {
            FancyZonesEditorHelper.Files.Restore();
        }

        [TestMethod]
        public void CreateWithDefaultName()
        {
            string name = "Custom layout 1";
            Session.Find<Element>(By.AccessibilityId(AccessibilityId.NewLayoutButton)).Click();
            Session.Find<Element>(By.AccessibilityId(AccessibilityId.PrimaryButton)).Click();
            Session.Find<Button>(ElementName.Save).Click();

            // verify new layout presented
            Assert.IsNotNull(Session.Find<Element>(name));

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(1, data.CustomLayouts.Count);
            Assert.IsTrue(data.CustomLayouts.Exists(x => x.Name == name));
        }

        [TestMethod]
        public void CreateWithCustomName()
        {
            string name = "Layout Name";
            Session.Find<Element>(By.AccessibilityId(AccessibilityId.NewLayoutButton)).Click();
            var input = Session.Find<TextBox>(By.ClassName(ClassName.TextBox));
            Assert.IsNotNull(input);
            input.SetText(name, true);
            Session.Find<Element>(By.AccessibilityId(AccessibilityId.PrimaryButton)).Click();
            Session.Find<Button>(ElementName.Save).Click();

            // verify new layout presented
            Assert.IsNotNull(Session.Find<Element>(name));

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(1, data.CustomLayouts.Count);
            Assert.IsTrue(data.CustomLayouts.Exists(x => x.Name == name));
        }

        [TestMethod]
        public void CreateGrid()
        {
            CustomLayout type = CustomLayout.Grid;
            Session.Find<Element>(By.AccessibilityId(AccessibilityId.NewLayoutButton)).Click();
            Session.Find<Element>(By.AccessibilityId(AccessibilityId.GridRadioButton)).Click();
            Session.Find<Element>(By.AccessibilityId(AccessibilityId.PrimaryButton)).Click();
            Session.Find<Button>(ElementName.Save).Click();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(1, data.CustomLayouts.Count);
            Assert.IsTrue(data.CustomLayouts.Exists(x => x.Type == type.TypeToString()));
        }

        [TestMethod]
        public void CreateCanvas()
        {
            CustomLayout type = CustomLayout.Canvas;
            Session.Find<Element>(By.AccessibilityId(AccessibilityId.NewLayoutButton)).Click();
            Session.Find<Element>(By.AccessibilityId(AccessibilityId.CanvasRadioButton)).Click();
            Session.Find<Element>(By.AccessibilityId(AccessibilityId.PrimaryButton)).Click();
            Session.Find<Button>(ElementName.Save).Click();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(1, data.CustomLayouts.Count);
            Assert.IsTrue(data.CustomLayouts.Exists(x => x.Type == type.TypeToString()));
        }

        [TestMethod]
        public void CancelGridCreation()
        {
            Session.Find<Element>(By.AccessibilityId(AccessibilityId.NewLayoutButton)).Click();
            Session.Find<Element>(By.AccessibilityId(AccessibilityId.GridRadioButton)).Click();
            Session.Find<Element>(By.AccessibilityId(AccessibilityId.PrimaryButton)).Click();
            Session.Find<Button>(ElementName.Cancel).Click();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(0, data.CustomLayouts.Count);
        }

        [TestMethod]
        public void CancelCanvasCreation()
        {
            Session.Find<Element>(By.AccessibilityId(AccessibilityId.NewLayoutButton)).Click();
            Session.Find<Element>(By.AccessibilityId(AccessibilityId.CanvasRadioButton)).Click();
            Session.Find<Element>(By.AccessibilityId(AccessibilityId.PrimaryButton)).Click();
            Session.Find<Button>(ElementName.Cancel).Click();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(0, data.CustomLayouts.Count);
        }
    }
}
