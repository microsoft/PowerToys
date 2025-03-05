// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using static FancyZonesEditorCommon.Data.CustomLayouts;
using static FancyZonesEditorCommon.Data.EditorParameters;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorHelper;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class CustomLayoutsTests : UITestBase
    {
        public CustomLayoutsTests()
            : base(PowerToysModule.FancyZone)
        {
        }

        private static readonly CustomLayoutListWrapper Layouts = new CustomLayoutListWrapper
        {
            CustomLayouts = new List<CustomLayoutWrapper>
            {
                new CustomLayoutWrapper
                {
                    Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                    Type = CustomLayout.Grid.TypeToString(),
                    Name = "Grid custom layout",
                    Info = new CustomLayouts().ToJsonElement(new GridInfoWrapper
                    {
                        Rows = 2,
                        Columns = 3,
                        RowsPercentage = new List<int> { 2967, 7033 },
                        ColumnsPercentage = new List<int> { 2410, 6040, 1550 },
                        CellChildMap = new int[][] { [0, 1, 1], [0, 2, 3] },
                        SensitivityRadius = 30,
                        Spacing = 26,
                        ShowSpacing = false,
                    }),
                },
                new CustomLayoutWrapper
                {
                    Uuid = "{E7807D0D-6223-4883-B15B-1F3883944C09}",
                    Type = CustomLayout.Canvas.TypeToString(),
                    Name = "Canvas custom layout",
                    Info = new CustomLayouts().ToJsonElement(new CanvasInfoWrapper
                    {
                        RefHeight = 952,
                        RefWidth = 1500,
                        SensitivityRadius = 10,
                        Zones = new List<CanvasInfoWrapper.CanvasZoneWrapper>
                        {
                            new CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 0,
                                Y = 0,
                                Width = 900,
                                Height = 522,
                            },
                            new CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 900,
                                Y = 0,
                                Width = 600,
                                Height = 750,
                            },
                            new CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 0,
                                Y = 522,
                                Width = 1500,
                                Height = 430,
                            },
                        },
                    }),
                },
                new CustomLayoutWrapper
                {
                    Uuid = "{F1A94F38-82B6-4876-A653-70D0E882DE2A}",
                    Type = CustomLayout.Grid.TypeToString(),
                    Name = "Grid custom layout spacing enabled",
                    Info = new CustomLayouts().ToJsonElement(new GridInfoWrapper
                    {
                        Rows = 2,
                        Columns = 3,
                        RowsPercentage = new List<int> { 2967, 7033 },
                        ColumnsPercentage = new List<int> { 2410, 6040, 1550 },
                        CellChildMap = new int[][] { [0, 1, 1], [0, 2, 3] },
                        SensitivityRadius = 30,
                        Spacing = 10,
                        ShowSpacing = true,
                    }),
                },
            },
        };

        [TestInitialize]
        public void TestInitialize()
        {
            CustomLayouts customLayouts = new CustomLayouts();
            FancyZonesEditorHelper.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(Layouts));

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
        public void Name_Initialize()
        {
            // verify all custom layouts are presented
            foreach (var layout in Layouts.CustomLayouts)
            {
                Assert.IsNotNull(Session.Find<Element>(layout.Name));
            }
        }

        [TestMethod]
        public void Rename_Save()
        {
            string newName = "New layout name";
            var oldName = Layouts.CustomLayouts[0].Name;

            // rename the layout
            Session.Find<Element>(oldName).Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            var input = Session.Find<TextBox>(PowerToys.UITest.By.ClassName(ClassName.TextBox));
            Assert.IsNotNull(input);
            input.SetText(newName, true);

            // verify new name
            Session.Find<Button>(ElementName.Save).Click();
            if (Session.FindAll<Element>(oldName).Count != 0)
            {
                Assert.Fail("Error : previous name isn't presented"); // previous name isn't presented
            }

            if (Session.FindAll<Element>(newName).Count == 0)
            {
                Assert.Fail("Error : new name is presented"); // new name is presented
            }
        }

        [TestMethod]
        public void Rename_Cancel()
        {
            string newName = "New layout name";
            var oldName = Layouts.CustomLayouts[0].Name;

            // rename the layout
            Session.Find<Element>(oldName).Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            var input = Session.Find<TextBox>(PowerToys.UITest.By.ClassName(ClassName.TextBox));
            Assert.IsNotNull(input);
            input.SetText(newName, true);

            // verify new name
            Session.Find<Button>(ElementName.Cancel).Click();
            Assert.IsTrue(Session.FindAll<Element>(oldName).Count == 0);
            Assert.IsTrue(Session.FindAll<Element>(newName).Count != 0);
        }

        [TestMethod]
        public void HighlightDistance_Initialize()
        {
            foreach (var layout in Layouts.CustomLayouts)
            {
                Session.Find<Element>(layout.Name).Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();

                var slider = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SensitivitySlider));
                Assert.IsNotNull(slider);
                var expected = layout.Type == CustomLayout.Canvas.TypeToString() ?
                    new CustomLayouts().CanvasFromJsonElement(layout.Info.GetRawText()).SensitivityRadius :
                    new CustomLayouts().GridFromJsonElement(layout.Info.GetRawText()).SensitivityRadius;
                Assert.AreEqual($"{expected}", slider.Text);

                Session.Find<Button>(ElementName.Cancel).Click();
            }
        }

        [TestMethod]
        public void HighlightDistance_Save()
        {
            var layout = Layouts.CustomLayouts[0];
            var type = layout.Type;
            Session.Find<Element>(layout.Name).Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();

            var slider = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SensitivitySlider));
            Assert.IsNotNull(slider);
            slider.SendKeys(Keys.Right);

            var value = type == CustomLayout.Canvas.TypeToString() ?
                    new CustomLayouts().CanvasFromJsonElement(layout.Info.GetRawText()).SensitivityRadius :
                    new CustomLayouts().GridFromJsonElement(layout.Info.GetRawText()).SensitivityRadius;
            var expected = value + 1; // one step right

            Assert.AreEqual($"{expected}", slider.Text);

            Session.Find<Button>(ElementName.Save).Click();

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var actual = type == CustomLayout.Canvas.TypeToString() ?
                new CustomLayouts().CanvasFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == layout.Uuid).Info.GetRawText()).SensitivityRadius :
                new CustomLayouts().GridFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == layout.Uuid).Info.GetRawText()).SensitivityRadius;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void HighlightDistance_Cancel()
        {
            var layout = Layouts.CustomLayouts[0];
            var type = layout.Type;
            Session.Find<Element>(layout.Name).Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();

            var slider = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SensitivitySlider));
            Assert.IsNotNull(slider);
            slider.SendKeys(Keys.Right);

            var expected = type == CustomLayout.Canvas.TypeToString() ?
                    new CustomLayouts().CanvasFromJsonElement(layout.Info.GetRawText()).SensitivityRadius :
                    new CustomLayouts().GridFromJsonElement(layout.Info.GetRawText()).SensitivityRadius;

            Session.Find<Button>(ElementName.Cancel).Click();

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var actual = type == CustomLayout.Canvas.TypeToString() ?
                new CustomLayouts().CanvasFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == layout.Uuid).Info.GetRawText()).SensitivityRadius :
                new CustomLayouts().GridFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == layout.Uuid).Info.GetRawText()).SensitivityRadius;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SpaceAroundZones_Initialize()
        {
            foreach (var layout in Layouts.CustomLayouts)
            {
                if (layout.Type != CustomLayout.Grid.TypeToString())
                {
                    // only for grid layouts
                    continue;
                }

                Session.Find<Element>(layout.Name).Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();

                var toggle = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SpacingToggle));
                Assert.IsNotNull(toggle);
                var slider = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SpacingSlider));
                Assert.IsNotNull(slider);

                var spacingEnabled = new CustomLayouts().GridFromJsonElement(layout.Info.GetRawText()).ShowSpacing;
                Assert.AreEqual(spacingEnabled, slider.Enabled);
                Assert.AreEqual(spacingEnabled, toggle.Selected);

                var expected = new CustomLayouts().GridFromJsonElement(layout.Info.GetRawText()).Spacing;
                Assert.AreEqual($"{expected}", slider.Text);

                Session.Find<Button>(ElementName.Cancel).Click();
            }
        }

        [TestMethod]
        public void SpaceAroundZones_Slider_Save()
        {
            var layout = Layouts.CustomLayouts.Find(x => x.Type == CustomLayout.Grid.TypeToString() && new CustomLayouts().GridFromJsonElement(x.Info.GetRawText()).ShowSpacing);
            var expected = new CustomLayouts().GridFromJsonElement(layout.Info.GetRawText()).Spacing + 1; // one step right
            Session.Find<Element>(layout.Name).Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();

            var slider = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SpacingSlider));
            Assert.IsNotNull(slider);
            slider.SendKeys(Keys.Right);
            Assert.AreEqual($"{expected}", slider.Text);

            Session.Find<Button>(ElementName.Save).Click();

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var actual = new CustomLayouts().GridFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == layout.Uuid).Info.GetRawText()).Spacing;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SpaceAroundZones_Slider_Cancel()
        {
            var layout = Layouts.CustomLayouts.Find(x => x.Type == CustomLayout.Grid.TypeToString() && new CustomLayouts().GridFromJsonElement(x.Info.GetRawText()).ShowSpacing);
            Session.Find<Element>(layout.Name).Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            var expected = new CustomLayouts().GridFromJsonElement(layout.Info.GetRawText()).Spacing;

            var slider = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SpacingSlider));
            Assert.IsNotNull(slider);
            slider.SendKeys(Keys.Right);
            Session.Find<Button>(ElementName.Cancel).Click();

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var actual = new CustomLayouts().GridFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == layout.Uuid).Info.GetRawText()).Spacing;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SpaceAroundZones_Toggle_Save()
        {
            var layout = Layouts.CustomLayouts.Find(x => x.Type == CustomLayout.Grid.TypeToString());
            var value = new CustomLayouts().GridFromJsonElement(layout.Info.GetRawText()).ShowSpacing;
            var expected = !value;
            Session.Find<Element>(layout.Name).Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();

            var toggle = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SpacingToggle));
            Assert.IsNotNull(toggle);
            toggle.Click();
            Assert.AreEqual(expected, toggle.Selected, "Toggle value not changed");
            Assert.AreEqual(expected, Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SpacingSlider))?.Enabled);

            Session.Find<Button>(ElementName.Save).Click();

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var actual = new CustomLayouts().GridFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == layout.Uuid).Info.GetRawText()).ShowSpacing;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SpaceAroundZones_Toggle_Cancel()
        {
            var layout = Layouts.CustomLayouts.Find(x => x.Type == CustomLayout.Grid.TypeToString());
            var expected = new CustomLayouts().GridFromJsonElement(layout.Info.GetRawText()).ShowSpacing;
            Session.Find<Element>(layout.Name).Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();

            var toggle = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SpacingToggle));
            Assert.IsNotNull(toggle);
            toggle.Click();
            Assert.AreNotEqual(expected, toggle.Selected, "Toggle value not changed");
            Assert.AreNotEqual(expected, Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.SpacingSlider))?.Enabled);

            Session.Find<Button>(ElementName.Cancel).Click();

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var actual = new CustomLayouts().GridFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == layout.Uuid).Info.GetRawText()).ShowSpacing;
            Assert.AreEqual(expected, actual);
        }
    }
}
