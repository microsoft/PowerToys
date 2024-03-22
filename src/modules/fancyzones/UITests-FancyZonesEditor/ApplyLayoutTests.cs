// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorSession;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class ApplyLayoutTests
    {
        private static readonly EditorParameters.ParamsWrapper Parameters = new EditorParameters.ParamsWrapper
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

        private static readonly CustomLayouts.CustomLayoutListWrapper CustomLayoutsList = new CustomLayouts.CustomLayoutListWrapper
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

        private static readonly LayoutTemplates.TemplateLayoutsListWrapper TemplateLayoutsList = new LayoutTemplates.TemplateLayoutsListWrapper
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

        private static FancyZonesEditorSession? _session;
        private static TestContext? _context;

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
            EditorParameters editorParameters = new EditorParameters();
            FancyZonesEditorSession.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(Parameters));

            LayoutTemplates layoutTemplates = new LayoutTemplates();
            FancyZonesEditorSession.Files.LayoutTemplatesIOHelper.WriteData(layoutTemplates.Serialize(TemplateLayoutsList));

            CustomLayouts customLayouts = new CustomLayouts();
            FancyZonesEditorSession.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(CustomLayoutsList));

            DefaultLayouts defaultLayouts = new DefaultLayouts();
            DefaultLayouts.DefaultLayoutsListWrapper defaultLayoutsListWrapper = new DefaultLayouts.DefaultLayoutsListWrapper
            {
                DefaultLayouts = new List<DefaultLayouts.DefaultLayoutWrapper>
                {
                    new DefaultLayouts.DefaultLayoutWrapper
                    {
                        MonitorConfiguration = MonitorConfigurationType.Horizontal.TypeToString(),
                        Layout = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
                        {
                            Type = LayoutType.Focus.TypeToString(),
                            ZoneCount = 4,
                            ShowSpacing = true,
                            Spacing = 5,
                            SensitivityRadius = 20,
                        },
                    },
                    new DefaultLayouts.DefaultLayoutWrapper
                    {
                        MonitorConfiguration = MonitorConfigurationType.Vertical.TypeToString(),
                        Layout = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
                        {
                            Type = LayoutType.Custom.TypeToString(),
                            Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                            ZoneCount = 0,
                            ShowSpacing = false,
                            Spacing = 0,
                            SensitivityRadius = 0,
                        },
                    },
                },
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

            _session = new FancyZonesEditorSession(_context!);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _session?.Close();
            FancyZonesEditorSession.Files.Restore();
        }

        [TestMethod]
        public void ApplyCustomLayout()
        {
            var layout = CustomLayoutsList.CustomLayouts[0];
            Assert.IsFalse(_session?.GetLayout(layout.Name)!.Selected);
            _session?.Click(_session?.GetLayout(layout.Name)!);

            Assert.IsTrue(_session?.GetLayout(layout.Name)!.Selected);

            AppliedLayouts appliedLayouts = new AppliedLayouts();
            var data = appliedLayouts.Read(appliedLayouts.File);
            Assert.AreEqual(Parameters.Monitors.Count, data.AppliedLayouts.Count);
            Assert.AreEqual(layout.Uuid, data.AppliedLayouts[0].AppliedLayout.Uuid);
            Assert.AreEqual(Parameters.Monitors[0].MonitorNumber, data.AppliedLayouts[0].Device.MonitorNumber);
        }

        [TestMethod]
        public void ApplyTemplateLayout()
        {
            var layoutType = LayoutType.Columns;
            var layout = TestConstants.TemplateLayoutNames[layoutType];
            Assert.IsFalse(_session?.GetLayout(layout)!.Selected);
            _session?.Click(_session?.GetLayout(layout)!);

            Assert.IsTrue(_session?.GetLayout(layout)!.Selected);

            AppliedLayouts appliedLayouts = new AppliedLayouts();
            var data = appliedLayouts.Read(appliedLayouts.File);
            Assert.AreEqual(Parameters.Monitors.Count, data.AppliedLayouts.Count);
            Assert.AreEqual(layoutType.TypeToString(), data.AppliedLayouts[0].AppliedLayout.Type);
            Assert.AreEqual(Parameters.Monitors[0].MonitorNumber, data.AppliedLayouts[0].Device.MonitorNumber);
        }

        [TestMethod]
        public void ApplyLayoutsOnEachMonitor()
        {
            // apply the layout on the first monitor
            var firstLayoutType = LayoutType.Columns;
            var firstLayoutName = TestConstants.TemplateLayoutNames[firstLayoutType];
            _session?.Click(_session?.GetLayout(firstLayoutName)!);
            Assert.IsTrue(_session?.GetLayout(firstLayoutName)!.Selected);

            // apply the layout on the second monitor
            _session?.ClickMonitor(2);
            var secondLayout = CustomLayoutsList.CustomLayouts[0];
            _session?.Click(_session?.GetLayout(secondLayout.Name)!);
            Assert.IsTrue(_session?.GetLayout(secondLayout.Name)!.Selected);

            // verify the layout on the first monitor wasn't changed
            _session?.ClickMonitor(1);
            Assert.IsTrue(_session?.GetLayout(firstLayoutName)!.Selected);

            // verify the file
            var appliedLayouts = new AppliedLayouts();
            var data = appliedLayouts.Read(appliedLayouts.File);
            Assert.AreEqual(Parameters.Monitors.Count, data.AppliedLayouts.Count);
            Assert.AreEqual(firstLayoutType.TypeToString(), data.AppliedLayouts.Find(x => x.Device.MonitorNumber == 1).AppliedLayout.Type);
            Assert.AreEqual(secondLayout.Uuid, data.AppliedLayouts.Find(x => x.Device.MonitorNumber == 2).AppliedLayout.Uuid);
        }

        [TestMethod]
        public void ApplyTemplateWithDifferentParametersOnEachMonitor()
        {
            var layoutType = LayoutType.Columns;
            var layoutName = TestConstants.TemplateLayoutNames[layoutType];

            // apply the layout on the first monitor, set parameters
            _session?.Click(_session?.GetLayout(layoutName)!);
            _session?.ClickEditLayout(layoutName);
            var slider = _session?.FindByAccessibilityId(AccessibilityId.TemplateZoneSlider);
            Assert.IsNotNull(slider);
            slider.SendKeys(Keys.Right);
            slider.SendKeys(Keys.Right);
            var expectedFirstLayoutZoneCount = int.Parse(slider.Text!, CultureInfo.InvariantCulture);
            _session?.Click(ElementName.Save);
            _session?.WaitUntilHidden(slider); // let the dialog window close

            // apply the layout on the second monitor, set different parameters
            _session?.ClickMonitor(2);
            _session?.Click(_session?.GetLayout(layoutName)!);
            _session?.ClickEditLayout(layoutName);
            slider = _session?.FindByAccessibilityId(AccessibilityId.TemplateZoneSlider);
            Assert.IsNotNull(slider);
            slider.SendKeys(Keys.Left);
            var expectedSecondLayoutZoneCount = int.Parse(slider.Text!, CultureInfo.InvariantCulture);
            _session?.Click(ElementName.Save);
            _session?.WaitUntilHidden(slider); // let the dialog window close

            // verify the layout on the first monitor wasn't changed
            _session?.ClickMonitor(1);
            _session?.ClickEditLayout(layoutName);
            slider = _session?.FindByAccessibilityId(AccessibilityId.TemplateZoneSlider);
            Assert.IsNotNull(slider);
            Assert.AreEqual(expectedFirstLayoutZoneCount, int.Parse(slider.Text!, CultureInfo.InvariantCulture));
            _session?.Click(ElementName.Cancel);
            _session?.WaitUntilHidden(slider); // let the dialog window close

            // check the file
            var appliedLayouts = new AppliedLayouts();
            var data = appliedLayouts.Read(appliedLayouts.File);
            Assert.AreEqual(Parameters.Monitors.Count, data.AppliedLayouts.Count);
            Assert.AreEqual(layoutType.TypeToString(), data.AppliedLayouts.Find(x => x.Device.MonitorNumber == 1).AppliedLayout.Type);
            Assert.AreEqual(expectedFirstLayoutZoneCount, data.AppliedLayouts.Find(x => x.Device.MonitorNumber == 1).AppliedLayout.ZoneCount);
            Assert.AreEqual(layoutType.TypeToString(), data.AppliedLayouts.Find(x => x.Device.MonitorNumber == 2).AppliedLayout.Type);
            Assert.AreEqual(expectedSecondLayoutZoneCount, data.AppliedLayouts.Find(x => x.Device.MonitorNumber == 2).AppliedLayout.ZoneCount);
        }
    }
}
