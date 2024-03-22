// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using static FancyZonesEditorCommon.Data.AppliedLayouts;
using static FancyZonesEditorCommon.Data.Constants;
using static FancyZonesEditorCommon.Data.DefaultLayouts;
using static FancyZonesEditorCommon.Data.EditorParameters;
using static FancyZonesEditorCommon.Data.LayoutTemplates;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorSession;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class TemplateLayoutsTests
    {
        private static readonly TemplateLayoutsListWrapper Layouts = new TemplateLayoutsListWrapper
        {
            LayoutTemplates = new List<TemplateLayoutWrapper>
            {
                new TemplateLayoutWrapper
                {
                    Type = TemplateLayoutJsonTags[TemplateLayout.Empty],
                },
                new TemplateLayoutWrapper
                {
                    Type = TemplateLayoutJsonTags[TemplateLayout.Focus],
                    ZoneCount = 10,
                },
                new TemplateLayoutWrapper
                {
                    Type = TemplateLayoutJsonTags[TemplateLayout.Rows],
                    ZoneCount = 2,
                    ShowSpacing = true,
                    Spacing = 10,
                    SensitivityRadius = 10,
                },
                new TemplateLayoutWrapper
                {
                    Type = TemplateLayoutJsonTags[TemplateLayout.Columns],
                    ZoneCount = 2,
                    ShowSpacing = true,
                    Spacing = 20,
                    SensitivityRadius = 20,
                },
                new TemplateLayoutWrapper
                {
                    Type = TemplateLayoutJsonTags[TemplateLayout.Grid],
                    ZoneCount = 4,
                    ShowSpacing = false,
                    Spacing = 10,
                    SensitivityRadius = 30,
                },
                new TemplateLayoutWrapper
                {
                    Type = TemplateLayoutJsonTags[TemplateLayout.PriorityGrid],
                    ZoneCount = 3,
                    ShowSpacing = true,
                    Spacing = 1,
                    SensitivityRadius = 40,
                },
            },
        };

        private static TestContext? _context;
        private static FancyZonesEditorSession? _session;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            _context = testContext;
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
            LayoutTemplates layoutTemplates = new LayoutTemplates();
            FancyZonesEditorSession.Files.LayoutTemplatesIOHelper.WriteData(layoutTemplates.Serialize(Layouts));

            // Default layouts should match templates
            DefaultLayouts defaultLayouts = new DefaultLayouts();
            DefaultLayoutsListWrapper defaultLayoutsList = new DefaultLayoutsListWrapper
            {
                DefaultLayouts = new List<DefaultLayoutWrapper>
                {
                    new DefaultLayoutWrapper
                    {
                        MonitorConfiguration = MonitorConfigurationTypeEnumExtensions.MonitorConfigurationTypeToString(MonitorConfigurationType.Vertical),
                        Layout = new DefaultLayoutWrapper.LayoutWrapper
                        {
                            Type = TemplateLayoutJsonTags[TemplateLayout.Rows],
                            ZoneCount = 2,
                            ShowSpacing = true,
                            Spacing = 10,
                            SensitivityRadius = 10,
                        },
                    },
                    new DefaultLayoutWrapper
                    {
                        MonitorConfiguration = MonitorConfigurationTypeEnumExtensions.MonitorConfigurationTypeToString(MonitorConfigurationType.Horizontal),
                        Layout = new DefaultLayoutWrapper.LayoutWrapper
                        {
                            Type = TemplateLayoutJsonTags[TemplateLayout.PriorityGrid],
                            ZoneCount = 3,
                            ShowSpacing = true,
                            Spacing = 1,
                            SensitivityRadius = 40,
                        },
                    },
                },
            };
            FancyZonesEditorSession.Files.DefaultLayoutsIOHelper.WriteData(defaultLayouts.Serialize(defaultLayoutsList));

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
            FancyZonesEditorSession.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

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
                            Type = TemplateLayoutJsonTags[TemplateLayout.PriorityGrid],
                            ZoneCount = 3,
                            ShowSpacing = true,
                            Spacing = 1,
                            SensitivityRadius = 40,
                        },
                    },
                },
            };
            FancyZonesEditorSession.Files.AppliedLayoutsIOHelper.WriteData(appliedLayouts.Serialize(appliedLayoutsList));

            CustomLayouts customLayouts = new CustomLayouts();
            CustomLayouts.CustomLayoutListWrapper customLayoutListWrapper = new CustomLayouts.CustomLayoutListWrapper
            {
                CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper> { },
            };
            FancyZonesEditorSession.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));

            LayoutHotkeys layoutHotkeys = new LayoutHotkeys();
            LayoutHotkeys.LayoutHotkeysWrapper layoutHotkeysWrapper = new LayoutHotkeys.LayoutHotkeysWrapper
            {
                LayoutHotkeys = new List<LayoutHotkeys.LayoutHotkeyWrapper> { },
            };
            FancyZonesEditorSession.Files.LayoutHotkeysIOHelper.WriteData(layoutHotkeys.Serialize(layoutHotkeysWrapper));

            _session = new FancyZonesEditorSession(_context!);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _session?.Close();
            FancyZonesEditorSession.Files.Restore();
        }

        [TestMethod]
        public void ZoneNumber_Initialize()
        {
            foreach (var (key, name) in TestConstants.TemplateLayoutNames)
            {
                if (key == TemplateLayout.Empty)
                {
                    continue;
                }

                _session?.ClickEditLayout(name);

                var slider = _session?.FindByAccessibilityId(AccessibilityId.TemplateZoneSlider);
                Assert.IsNotNull(slider);
                var expected = Layouts.LayoutTemplates.Find(x => x.Type == TemplateLayoutJsonTags[key]).ZoneCount;
                Assert.AreEqual($"{expected}", slider.Text);

                _session?.Click(ElementName.Cancel);
                _session?.WaitUntilHidden(slider); // let the dialog window close
            }
        }

        [TestMethod]
        public void ZoneNumber_Save()
        {
            var type = TemplateLayout.Columns;
            var layout = Layouts.LayoutTemplates.Find(x => x.Type == TemplateLayoutJsonTags[type]);
            var value = layout.ZoneCount;
            var expected = value - 1;
            _session?.ClickEditLayout(TestConstants.TemplateLayoutNames[type]);

            var slider = _session?.FindByAccessibilityId(AccessibilityId.TemplateZoneSlider);
            Assert.IsNotNull(slider);
            slider.SendKeys(Keys.Left);
            Assert.AreEqual($"{expected}", slider.Text);

            _session?.Click(ElementName.Save);
            _session?.WaitUntilHidden(slider); // let the dialog window close

            // verify the file
            var templateLayouts = new LayoutTemplates();
            var data = templateLayouts.Read(templateLayouts.File);
            var actual = data.LayoutTemplates.Find(x => x.Type == TemplateLayoutJsonTags[type]).ZoneCount;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void ZoneNumber_Cancel()
        {
            var type = TemplateLayout.Rows;
            var layout = Layouts.LayoutTemplates.Find(x => x.Type == TemplateLayoutJsonTags[type]);
            var expected = layout.ZoneCount;
            _session?.ClickEditLayout(TestConstants.TemplateLayoutNames[type]);

            var slider = _session?.FindByAccessibilityId(AccessibilityId.TemplateZoneSlider);
            Assert.IsNotNull(slider);
            slider.SendKeys(Keys.Left);

            _session?.Click(ElementName.Cancel);
            _session?.WaitUntilHidden(slider); // let the dialog window close

            // verify the file
            var templateLayouts = new LayoutTemplates();
            var data = templateLayouts.Read(templateLayouts.File);
            var actual = data.LayoutTemplates.Find(x => x.Type == TemplateLayoutJsonTags[type]).ZoneCount;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void HighlightDistance_Initialize()
        {
            foreach (var (key, name) in TestConstants.TemplateLayoutNames)
            {
                if (key == TemplateLayout.Empty)
                {
                    continue;
                }

                _session?.ClickEditLayout(name);

                var slider = _session?.FindByAccessibilityId(AccessibilityId.SensitivitySlider);
                Assert.IsNotNull(slider);
                var expected = Layouts.LayoutTemplates.Find(x => x.Type == TemplateLayoutJsonTags[key]).SensitivityRadius;
                Assert.AreEqual($"{expected}", slider.Text);

                _session?.Click(ElementName.Cancel);
                _session?.WaitUntilHidden(slider); // let the dialog window close
            }
        }

        [TestMethod]
        public void HighlightDistance_Save()
        {
            var type = TemplateLayout.Focus;
            var layout = Layouts.LayoutTemplates.Find(x => x.Type == TemplateLayoutJsonTags[type]);
            var value = layout.SensitivityRadius;
            _session?.ClickEditLayout(TestConstants.TemplateLayoutNames[type]);

            var slider = _session?.FindByAccessibilityId(AccessibilityId.SensitivitySlider);
            Assert.IsNotNull(slider);
            slider.SendKeys(Keys.Right);

            var expected = value + 1; // one step right
            Assert.AreEqual($"{expected}", slider.Text);

            _session?.Click(ElementName.Save);
            _session?.WaitUntilHidden(slider); // let the dialog window close

            // verify the file
            var templateLayouts = new LayoutTemplates();
            var data = templateLayouts.Read(templateLayouts.File);
            var actual = data.LayoutTemplates.Find(x => x.Type == TemplateLayoutJsonTags[type]).SensitivityRadius;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void HighlightDistance_Cancel()
        {
            var type = TemplateLayout.Focus;
            var layout = Layouts.LayoutTemplates.Find(x => x.Type == TemplateLayoutJsonTags[type]);
            var expected = layout.SensitivityRadius;
            _session?.ClickEditLayout(TestConstants.TemplateLayoutNames[type]);

            var slider = _session?.FindByAccessibilityId(AccessibilityId.SensitivitySlider);
            Assert.IsNotNull(slider);
            slider.SendKeys(Keys.Right);
            _session?.Click(ElementName.Cancel);
            _session?.WaitUntilHidden(slider); // let the dialog window close

            // verify the file
            var templateLayouts = new LayoutTemplates();
            var data = templateLayouts.Read(templateLayouts.File);
            var actual = data.LayoutTemplates.Find(x => x.Type == TemplateLayoutJsonTags[type]).SensitivityRadius;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SpaceAroundZones_Initialize()
        {
            foreach (var (key, name) in TestConstants.TemplateLayoutNames)
            {
                if (key == TemplateLayout.Empty || key == TemplateLayout.Focus)
                {
                    // only for grid layouts
                    continue;
                }

                _session?.ClickEditLayout(name);

                var slider = _session?.FindByAccessibilityId(AccessibilityId.SpacingSlider);
                Assert.IsNotNull(slider);

                var spacingEnabled = Layouts.LayoutTemplates.Find(x => x.Type == TemplateLayoutJsonTags[key]).ShowSpacing;
                Assert.AreEqual(spacingEnabled, slider.Enabled);

                var expected = Layouts.LayoutTemplates.Find(x => x.Type == TemplateLayoutJsonTags[key]).Spacing;
                Assert.AreEqual($"{expected}", slider.Text);

                _session?.Click(ElementName.Cancel);
                _session?.WaitUntilHidden(slider); // let the dialog window close
            }
        }

        [TestMethod]
        public void SpaceAroundZones_Slider_Save()
        {
            var type = TemplateLayout.PriorityGrid;
            var layout = Layouts.LayoutTemplates.Find(x => x.Type == TemplateLayoutJsonTags[type]);
            var expected = layout.Spacing + 1;
            _session?.ClickEditLayout(TestConstants.TemplateLayoutNames[type]);

            var slider = _session?.FindByAccessibilityId(AccessibilityId.SpacingSlider);
            Assert.IsNotNull(slider);
            slider.SendKeys(Keys.Right);
            Assert.AreEqual($"{expected}", slider.Text);

            _session?.Click(ElementName.Save);
            _session?.WaitUntilHidden(slider); // let the dialog window close

            // verify the file
            var templateLayouts = new LayoutTemplates();
            var data = templateLayouts.Read(templateLayouts.File);
            var actual = data.LayoutTemplates.Find(x => x.Type == TemplateLayoutJsonTags[type]).Spacing;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SpaceAroundZones_Slider_Cancel()
        {
            var type = TemplateLayout.PriorityGrid;
            var layout = Layouts.LayoutTemplates.Find(x => x.Type == TemplateLayoutJsonTags[type]);
            var expected = layout.Spacing;
            _session?.ClickEditLayout(TestConstants.TemplateLayoutNames[type]);

            var slider = _session?.FindByAccessibilityId(AccessibilityId.SpacingSlider);
            Assert.IsNotNull(slider);
            slider.SendKeys(Keys.Right);
            Assert.AreEqual($"{expected + 1}", slider.Text);

            _session?.Click(ElementName.Cancel);
            _session?.WaitUntilHidden(slider); // let the dialog window close

            // verify the file
            var templateLayouts = new LayoutTemplates();
            var data = templateLayouts.Read(templateLayouts.File);
            var actual = data.LayoutTemplates.Find(x => x.Type == TemplateLayoutJsonTags[type]).Spacing;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SpaceAroundZones_Toggle_Save()
        {
            var type = TemplateLayout.PriorityGrid;
            var layout = Layouts.LayoutTemplates.Find(x => x.Type == TemplateLayoutJsonTags[type]);
            var expected = !layout.ShowSpacing;
            _session?.ClickEditLayout(TestConstants.TemplateLayoutNames[type]);

            var toggle = _session?.FindByAccessibilityId(AccessibilityId.SpacingToggle);
            Assert.IsNotNull(toggle);
            toggle.Click();
            Assert.AreEqual(expected, toggle.Selected);
            Assert.AreEqual(expected, _session?.FindByAccessibilityId(AccessibilityId.SpacingSlider)?.Enabled);

            _session?.Click(ElementName.Save);
            _session?.WaitUntilHidden(toggle); // let the dialog window close

            // verify the file
            var templateLayouts = new LayoutTemplates();
            var data = templateLayouts.Read(templateLayouts.File);
            var actual = data.LayoutTemplates.Find(x => x.Type == TemplateLayoutJsonTags[type]).ShowSpacing;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SpaceAroundZones_Toggle_Cancel()
        {
            var type = TemplateLayout.PriorityGrid;
            var layout = Layouts.LayoutTemplates.Find(x => x.Type == TemplateLayoutJsonTags[type]);
            var expected = layout.ShowSpacing;
            _session?.ClickEditLayout(TestConstants.TemplateLayoutNames[type]);

            var toggle = _session?.FindByAccessibilityId(AccessibilityId.SpacingToggle);
            Assert.IsNotNull(toggle);
            toggle.Click();
            Assert.AreNotEqual(expected, toggle.Selected);
            Assert.AreNotEqual(expected, _session?.FindByAccessibilityId(AccessibilityId.SpacingSlider)?.Enabled);

            _session?.Click(ElementName.Cancel);
            _session?.WaitUntilHidden(toggle); // let the dialog window close

            // verify the file
            var templateLayouts = new LayoutTemplates();
            var data = templateLayouts.Read(templateLayouts.File);
            var actual = data.LayoutTemplates.Find(x => x.Type == TemplateLayoutJsonTags[type]).ShowSpacing;
            Assert.AreEqual(expected, actual);
        }
    }
}
