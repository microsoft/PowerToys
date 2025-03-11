// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UITests;
using Microsoft.FancyZonesEditor.UITests.Utils;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using Windows.UI;
using static FancyZonesEditorCommon.Data.AppliedLayouts;
using static FancyZonesEditorCommon.Data.DefaultLayouts;
using static FancyZonesEditorCommon.Data.EditorParameters;
using static FancyZonesEditorCommon.Data.LayoutTemplates;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorHelper;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class TemplateLayoutsTests : UITestBase
    {
        private static readonly TemplateLayoutsListWrapper Layouts = new TemplateLayoutsListWrapper
        {
            LayoutTemplates = new List<TemplateLayoutWrapper>
            {
                new TemplateLayoutWrapper
                {
                    Type = LayoutType.Blank.TypeToString(),
                },
                new TemplateLayoutWrapper
                {
                    Type = LayoutType.Focus.TypeToString(),
                    ZoneCount = 10,
                },
                new TemplateLayoutWrapper
                {
                    Type = LayoutType.Rows.TypeToString(),
                    ZoneCount = 2,
                    ShowSpacing = true,
                    Spacing = 10,
                    SensitivityRadius = 10,
                },
                new TemplateLayoutWrapper
                {
                    Type = LayoutType.Columns.TypeToString(),
                    ZoneCount = 2,
                    ShowSpacing = true,
                    Spacing = 20,
                    SensitivityRadius = 20,
                },
                new TemplateLayoutWrapper
                {
                    Type = LayoutType.Grid.TypeToString(),
                    ZoneCount = 4,
                    ShowSpacing = false,
                    Spacing = 10,
                    SensitivityRadius = 30,
                },
                new TemplateLayoutWrapper
                {
                    Type = LayoutType.PriorityGrid.TypeToString(),
                    ZoneCount = 3,
                    ShowSpacing = true,
                    Spacing = 1,
                    SensitivityRadius = 40,
                },
            },
        };

        public TemplateLayoutsTests()
            : base(PowerToysModule.FancyZone)
        {
        }

        [TestInitialize]
        public void TestInitialize()
        {
            EditorParameters editorParameters = new EditorParameters();
            ParamsWrapper parameters = new ParamsWrapper
            {
                ProcessId = 1,
                SpanZonesAcrossMonitors = false,
                Monitors = new List<NativeMonitorDataWrapper>
                {
                    new NativeMonitorDataWrapper
                    {
                        Monitor = "monitor-1",
                        MonitorInstanceId = "instance-id-1",
                        MonitorSerialNumber = "serial-number-1",
                        MonitorNumber = 1,
                        VirtualDesktop = "{FF34D993-73F3-4B8C-AA03-73730A01D6A8}",
                        Dpi = 192,
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
            FancyZonesEditorHelper.Files.LayoutTemplatesIOHelper.WriteData(layoutTemplates.Serialize(Layouts));

            // Default layouts should match templates
            DefaultLayouts defaultLayouts = new DefaultLayouts();
            DefaultLayoutsListWrapper defaultLayoutsList = new DefaultLayoutsListWrapper
            {
                DefaultLayouts = new List<DefaultLayoutWrapper>
                {
                    new DefaultLayoutWrapper
                    {
                        MonitorConfiguration = MonitorConfigurationType.Vertical.ToString(),
                        Layout = new DefaultLayoutWrapper.LayoutWrapper
                        {
                            Type = LayoutType.Rows.TypeToString(),
                            ZoneCount = 2,
                            ShowSpacing = true,
                            Spacing = 10,
                            SensitivityRadius = 10,
                        },
                    },
                    new DefaultLayoutWrapper
                    {
                        MonitorConfiguration = MonitorConfigurationType.Horizontal.ToString(),
                        Layout = new DefaultLayoutWrapper.LayoutWrapper
                        {
                            Type = LayoutType.PriorityGrid.TypeToString(),
                            ZoneCount = 3,
                            ShowSpacing = true,
                            Spacing = 1,
                            SensitivityRadius = 40,
                        },
                    },
                },
            };
            FancyZonesEditorHelper.Files.DefaultLayoutsIOHelper.WriteData(defaultLayouts.Serialize(defaultLayoutsList));

            // Make sure applied layouts don't replate template settings
            AppliedLayouts appliedLayouts = new AppliedLayouts();
            AppliedLayoutsListWrapper appliedLayoutsList = new AppliedLayoutsListWrapper
            {
                AppliedLayouts = new List<AppliedLayoutWrapper>
                {
                    new AppliedLayoutWrapper
                    {
                        Device = new AppliedLayoutWrapper.DeviceIdWrapper
                        {
                            Monitor = "monitor-1",
                            MonitorInstance = "instance-id-1",
                            MonitorNumber = 1,
                            SerialNumber = "serial-number-1",
                            VirtualDesktop = "{FF34D993-73F3-4B8C-AA03-73730A01D6A8}",
                        },
                        AppliedLayout = new AppliedLayoutWrapper.LayoutWrapper
                        {
                            Uuid = "{72409DFC-2B87-469B-AAC4-557273791C26}",
                            Type = LayoutType.PriorityGrid.TypeToString(),
                            ZoneCount = 3,
                            ShowSpacing = true,
                            Spacing = 1,
                            SensitivityRadius = 40,
                        },
                    },
                },
            };
            FancyZonesEditorHelper.Files.AppliedLayoutsIOHelper.WriteData(appliedLayouts.Serialize(appliedLayoutsList));

            CustomLayouts customLayouts = new CustomLayouts();
            CustomLayouts.CustomLayoutListWrapper customLayoutListWrapper = new CustomLayouts.CustomLayoutListWrapper
            {
                CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper> { },
            };
            FancyZonesEditorHelper.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));

            LayoutHotkeys layoutHotkeys = new LayoutHotkeys();
            LayoutHotkeys.LayoutHotkeysWrapper layoutHotkeysWrapper = new LayoutHotkeys.LayoutHotkeysWrapper
            {
                LayoutHotkeys = new List<LayoutHotkeys.LayoutHotkeyWrapper> { },
            };
            FancyZonesEditorHelper.Files.LayoutHotkeysIOHelper.WriteData(layoutHotkeys.Serialize(layoutHotkeysWrapper));

            this.RestartScopeExe();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            FancyZonesEditorHelper.Files.Restore();
        }

        [TestMethod]
        public void ZoneNumber_Cancel()
        {
            var type = LayoutType.Rows;
            var layout = Layouts.LayoutTemplates.Find(x => x.Type == type.TypeToString());
            var expected = layout.ZoneCount;
            Session.Find<Button>(TestConstants.TemplateLayoutNames[type]).Click();

            var slider = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.TemplateZoneSlider));
            Assert.IsNotNull(slider);
            slider.SendKeys(Keys.Left);

            Session.Find<Button>(ElementName.Cancel).Click();

            // verify the file
            var templateLayouts = new LayoutTemplates();
            var data = templateLayouts.Read(templateLayouts.File);
            var actual = data.LayoutTemplates.Find(x => x.Type == type.TypeToString()).ZoneCount;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void HighlightDistance_Initialize()
        {
            foreach (var (type, name) in TestConstants.TemplateLayoutNames)
            {
                if (type == LayoutType.Blank)
                {
                    continue;
                }

                Session.Find<Button>(name).Click();

                var slider = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SensitivitySlider));
                Assert.IsNotNull(slider);
                var expected = Layouts.LayoutTemplates.Find(x => x.Type == type.TypeToString()).SensitivityRadius;
                Assert.AreEqual($"{expected}", slider.Text);

                Session.Find<Button>(ElementName.Cancel).Click();
            }
        }

        [TestMethod]
        public void HighlightDistance_Save()
        {
            var type = LayoutType.Focus;
            var layout = Layouts.LayoutTemplates.Find(x => x.Type == type.TypeToString());
            var value = layout.SensitivityRadius;
            Session.Find<Button>(TestConstants.TemplateLayoutNames[type]).Click();

            var slider = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SensitivitySlider));
            Assert.IsNotNull(slider);
            slider.SendKeys(Keys.Right);

            var expected = value + 1; // one step right
            Assert.AreEqual($"{expected}", slider.Text);

            Session.Find<Button>(ElementName.Save).Click();

            // verify the file
            var templateLayouts = new LayoutTemplates();
            var data = templateLayouts.Read(templateLayouts.File);
            var actual = data.LayoutTemplates.Find(x => x.Type == type.TypeToString()).SensitivityRadius;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void HighlightDistance_Cancel()
        {
            var type = LayoutType.Focus;
            var layout = Layouts.LayoutTemplates.Find(x => x.Type == type.TypeToString());
            var expected = layout.SensitivityRadius;
            Session.Find<Button>(TestConstants.TemplateLayoutNames[type]).Click();

            var slider = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SensitivitySlider));
            Assert.IsNotNull(slider);
            slider.SendKeys(Keys.Right);
            Session.Find<Button>(ElementName.Cancel).Click();

            // verify the file
            var templateLayouts = new LayoutTemplates();
            var data = templateLayouts.Read(templateLayouts.File);
            var actual = data.LayoutTemplates.Find(x => x.Type == type.TypeToString()).SensitivityRadius;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SpaceAroundZones_Initialize()
        {
            foreach (var (type, name) in TestConstants.TemplateLayoutNames)
            {
                if (type == LayoutType.Blank || type == LayoutType.Focus)
                {
                    // only for grid layouts
                    continue;
                }

                Session.Find<Button>(name).Click();

                var slider = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SpacingSlider));
                Assert.IsNotNull(slider);

                var spacingEnabled = Layouts.LayoutTemplates.Find(x => x.Type == type.TypeToString()).ShowSpacing;
                Assert.AreEqual(spacingEnabled, slider.Enabled);

                var expected = Layouts.LayoutTemplates.Find(x => x.Type == type.TypeToString()).Spacing;
                Assert.AreEqual($"{expected}", slider.Text);

                Session.Find<Button>(ElementName.Cancel).Click();
            }
        }

        [TestMethod]
        public void SpaceAroundZones_Slider_Save()
        {
            var type = LayoutType.PriorityGrid;
            var layout = Layouts.LayoutTemplates.Find(x => x.Type == type.TypeToString());
            var expected = layout.Spacing + 1;
            Session.Find<Button>(TestConstants.TemplateLayoutNames[type]).Click();

            var slider = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SpacingSlider));
            Assert.IsNotNull(slider);
            slider.SendKeys(Keys.Right);
            Assert.AreEqual($"{expected}", slider.Text);

            Session.Find<Button>(ElementName.Save).Click();

            // verify the file
            var templateLayouts = new LayoutTemplates();
            var data = templateLayouts.Read(templateLayouts.File);
            var actual = data.LayoutTemplates.Find(x => x.Type == type.TypeToString()).Spacing;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SpaceAroundZones_Slider_Cancel()
        {
            var type = LayoutType.PriorityGrid;
            var layout = Layouts.LayoutTemplates.Find(x => x.Type == type.TypeToString());
            var expected = layout.Spacing;
            Session.Find<Button>(TestConstants.TemplateLayoutNames[type]).Click();

            var slider = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SpacingSlider));
            Assert.IsNotNull(slider);
            slider.SendKeys(Keys.Right);
            Assert.AreEqual($"{expected + 1}", slider.Text);

            Session.Find<Button>(ElementName.Cancel).Click();

            // verify the file
            var templateLayouts = new LayoutTemplates();
            var data = templateLayouts.Read(templateLayouts.File);
            var actual = data.LayoutTemplates.Find(x => x.Type == type.TypeToString()).Spacing;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SpaceAroundZones_Toggle_Save()
        {
            var type = LayoutType.PriorityGrid;
            var layout = Layouts.LayoutTemplates.Find(x => x.Type == type.TypeToString());
            var expected = !layout.ShowSpacing;
            Session.Find<Button>(TestConstants.TemplateLayoutNames[type]).Click();

            var toggle = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SpacingToggle));
            Assert.IsNotNull(toggle);
            toggle.Click();
            Assert.AreEqual(expected, toggle.Selected);
            Assert.AreEqual(expected, Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SpacingSlider))?.Enabled);

            Session.Find<Button>(ElementName.Save).Click();

            // verify the file
            var templateLayouts = new LayoutTemplates();
            var data = templateLayouts.Read(templateLayouts.File);
            var actual = data.LayoutTemplates.Find(x => x.Type == type.TypeToString()).ShowSpacing;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SpaceAroundZones_Toggle_Cancel()
        {
            var type = LayoutType.PriorityGrid;
            var layout = Layouts.LayoutTemplates.Find(x => x.Type == type.TypeToString());
            var expected = layout.ShowSpacing;
            Session.Find<Button>(TestConstants.TemplateLayoutNames[type]).Click();

            var toggle = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SpacingToggle));
            Assert.IsNotNull(toggle);
            toggle.Click();
            Assert.AreNotEqual(expected, toggle.Selected);
            Assert.AreNotEqual(expected, Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SpacingSlider))?.Enabled);

            Session.Find<Button>(ElementName.Cancel).Click();

            // verify the file
            var templateLayouts = new LayoutTemplates();
            var data = templateLayouts.Read(templateLayouts.File);
            var actual = data.LayoutTemplates.Find(x => x.Type == type.TypeToString()).ShowSpacing;
            Assert.AreEqual(expected, actual);
        }
    }
}
