// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernWpf.Controls;
using OpenQA.Selenium;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorHelper;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class ApplyLayoutTests : UITestBase
    {
        public ApplyLayoutTests()
            : base(PowerToysModule.FancyZone)
        {
        }

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

        [TestInitialize]
        public void TestInitialize()
        {
            EditorParameters editorParameters = new EditorParameters();
            FancyZonesEditorHelper.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(Parameters));

            LayoutTemplates layoutTemplates = new LayoutTemplates();
            FancyZonesEditorHelper.Files.LayoutTemplatesIOHelper.WriteData(layoutTemplates.Serialize(TemplateLayoutsList));

            CustomLayouts customLayouts = new CustomLayouts();
            FancyZonesEditorHelper.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(CustomLayoutsList));

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
        public void ApplyCustomLayout()
        {
            var layout = CustomLayoutsList.CustomLayouts[0];
            Assert.IsFalse(Session.Find<Element>(layout.Name).Selected);
            Session.Find<Element>(layout.Name).Click();

            Assert.IsTrue(Session.Find<Element>(layout.Name).Selected);

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
            Assert.IsFalse(Session.Find<Element>(layout).Selected);
            Session.Find<Element>(layout).Click();

            Assert.IsTrue(Session.Find<Element>(layout).Selected);

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
            Session.Find<Element>(firstLayoutName).Click();
            Assert.IsTrue(Session.Find<Element>(firstLayoutName)!.Selected);

            // apply the layout on the second monitor
            Session.Find<Element>(PowerToys.UITest.By.AccessibilityId("Monitors")).Find<Element>("Monitor 2").Click();
            var secondLayout = CustomLayoutsList.CustomLayouts[0];
            Session.Find<Element>(secondLayout.Name).Click();
            Assert.IsTrue(Session.Find<Element>(secondLayout.Name)!.Selected);

            // verify the layout on the first monitor wasn't changed
            Session.Find<Element>(PowerToys.UITest.By.AccessibilityId("Monitors")).Find<Element>("Monitor 1").Click();
            Assert.IsTrue(Session.Find<Element>(firstLayoutName)!.Selected);

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
            Session.Find<Element>(layoutName).Click();
            Session.Find<Element>(layoutName).Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            var slider = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.TemplateZoneSlider));
            Assert.IsNotNull(slider);
            slider.SendKeys(Keys.Right);
            slider.SendKeys(Keys.Right);
            var expectedFirstLayoutZoneCount = int.Parse(slider.Text!, CultureInfo.InvariantCulture);
            Session.Find<Button>(ElementName.Save).Click();

            // apply the layout on the second monitor, set different parameters
            Session.Find<Element>(PowerToys.UITest.By.AccessibilityId("Monitors")).Find<Element>("Monitor 2").Click();
            Session.Find<Element>(layoutName).Click();
            Session.Find<Element>(layoutName).Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            slider = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.TemplateZoneSlider));
            Assert.IsNotNull(slider);
            slider.SendKeys(Keys.Left);
            var expectedSecondLayoutZoneCount = int.Parse(slider.Text!, CultureInfo.InvariantCulture);
            Session.Find<Button>(ElementName.Save).Click();

            // verify the layout on the first monitor wasn't changed
            Session.Find<Element>(PowerToys.UITest.By.AccessibilityId("Monitors")).Find<Element>("Monitor 1").Click();
            Session.Find<Element>(layoutName).Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            slider = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.TemplateZoneSlider));
            Assert.IsNotNull(slider);
            Assert.AreEqual(expectedFirstLayoutZoneCount, int.Parse(slider.Text!, CultureInfo.InvariantCulture));
            Session.Find<Button>(ElementName.Cancel).Click();

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
