// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernWpf.Controls;
using OpenQA.Selenium;
using static FancyZonesEditorCommon.Data.CustomLayouts;
using static FancyZonesEditorCommon.Data.DefaultLayouts;
using static FancyZonesEditorCommon.Data.EditorParameters;
using static FancyZonesEditorCommon.Data.LayoutHotkeys;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorHelper;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class DeleteLayoutTests : UITestBase
    {
        public DeleteLayoutTests()
            : base(PowerToysModule.FancyZone)
        {
        }

        private static readonly CustomLayoutListWrapper CustomLayouts = new CustomLayoutListWrapper
        {
            CustomLayouts = new List<CustomLayoutWrapper>
            {
                new CustomLayoutWrapper
                {
                    Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                    Type = CustomLayout.Grid.TypeToString(),
                    Name = "Custom layout 1",
                    Info = new CustomLayouts().ToJsonElement(new CustomLayouts.GridInfoWrapper
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
                    Name = "Custom layout 2",
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
            },
        };

        private static readonly DefaultLayouts.DefaultLayoutsListWrapper DefaultLayoutsList = new DefaultLayouts.DefaultLayoutsListWrapper
        {
            DefaultLayouts = new List<DefaultLayouts.DefaultLayoutWrapper>
            {
                new DefaultLayoutWrapper
                {
                    MonitorConfiguration = MonitorConfigurationType.Horizontal.TypeToString(),
                    Layout = new DefaultLayoutWrapper.LayoutWrapper
                    {
                        Type = LayoutType.Custom.TypeToString(),
                        Uuid = CustomLayouts.CustomLayouts[1].Uuid,
                    },
                },
            },
        };

        private static readonly LayoutHotkeys.LayoutHotkeysWrapper Hotkeys = new LayoutHotkeys.LayoutHotkeysWrapper
        {
            LayoutHotkeys = new List<LayoutHotkeys.LayoutHotkeyWrapper>
            {
                new LayoutHotkeyWrapper
                {
                    LayoutId = CustomLayouts.CustomLayouts[1].Uuid,
                    Key = 0,
                },
            },
        };

        private static readonly ParamsWrapper Parameters = new ParamsWrapper
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

        [TestInitialize]
        public void TestInitialize()
        {
            EditorParameters editorParameters = new EditorParameters();
            FancyZonesEditorHelper.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(Parameters));

            CustomLayouts customLayouts = new CustomLayouts();
            FancyZonesEditorHelper.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(CustomLayouts));

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
            FancyZonesEditorHelper.Files.DefaultLayoutsIOHelper.WriteData(defaultLayouts.Serialize(DefaultLayoutsList));

            LayoutHotkeys layoutHotkeys = new LayoutHotkeys();
            FancyZonesEditorHelper.Files.LayoutHotkeysIOHelper.WriteData(layoutHotkeys.Serialize(Hotkeys));

            AppliedLayouts appliedLayouts = new AppliedLayouts();
            AppliedLayouts.AppliedLayoutsListWrapper appliedLayoutsWrapper = new AppliedLayouts.AppliedLayoutsListWrapper
            {
                AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper> { },
            };
            FancyZonesEditorHelper.Files.AppliedLayoutsIOHelper.WriteData(appliedLayouts.Serialize(appliedLayoutsWrapper));

            this.RestartScopeExe();
            Session.Find<Element>(CustomLayouts.CustomLayouts[0].Name).Click();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            FancyZonesEditorHelper.Files.Restore();
        }

        [TestMethod]
        public void DeleteNotAppliedLayout()
        {
            var deletedLayout = CustomLayouts.CustomLayouts[1].Name;
            Session.Find<Element>(deletedLayout).Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            Session.Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.DeleteLayoutButton)).Click();
            Session.KeyboardAction(Keys.Tab, Keys.Enter);

            // verify the layout is removed
            Assert.IsTrue(Session.FindAll<Element>(deletedLayout).Count == 0);

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(CustomLayouts.CustomLayouts.Count - 1, data.CustomLayouts.Count);
            Assert.IsFalse(data.CustomLayouts.Exists(x => x.Name == deletedLayout));
        }

        [TestMethod]
        public void DeleteAppliedLayout()
        {
            var deletedLayout = CustomLayouts.CustomLayouts[0].Name;
            Session.Find<Element>(deletedLayout).Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            Session.Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.DeleteLayoutButton)).Click();
            Session.KeyboardAction(Keys.Tab, Keys.Enter);

            // verify the layout is removed
            Assert.IsTrue(Session.FindAll<Element>(deletedLayout).Count == 0);

            // verify the empty layout is selected
            Assert.IsTrue(Session.Find<Element>(TestConstants.TemplateLayoutNames[LayoutType.Blank])!.Selected);

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(CustomLayouts.CustomLayouts.Count - 1, data.CustomLayouts.Count);
            Assert.IsFalse(data.CustomLayouts.Exists(x => x.Name == deletedLayout));

            var appliedLayouts = new AppliedLayouts();
            var appliedLayoutsData = appliedLayouts.Read(appliedLayouts.File);
            Assert.AreEqual(LayoutType.Blank.TypeToString(), appliedLayoutsData.AppliedLayouts.Find(x => x.Device.Monitor == Parameters.Monitors[0].Monitor).AppliedLayout.Type);
        }

        [TestMethod]
        public void CancelDeletion()
        {
            var deletedLayout = CustomLayouts.CustomLayouts[1].Name;
            Session.Find<Element>(deletedLayout).Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            Session.Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.DeleteLayoutButton)).Click();
            Session.KeyboardAction(Keys.Tab, Keys.Tab, Keys.Enter);

            // verify the layout is not removed
            Assert.IsNotNull(Session.Find<Element>(deletedLayout));

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(CustomLayouts.CustomLayouts.Count, data.CustomLayouts.Count);
            Assert.IsTrue(data.CustomLayouts.Exists(x => x.Name == deletedLayout));
        }

        [TestMethod]
        public void DeleteFromContextMenu()
        {
            var deletedLayout = CustomLayouts.CustomLayouts[1].Name;
            FancyZonesEditorHelper.ClickContextMenuItem(Session, deletedLayout, FancyZonesEditorHelper.ElementName.Delete);
            Session.KeyboardAction(Keys.Tab, Keys.Enter);

            // verify the layout is removed
            Assert.IsTrue(Session.FindAll<Element>(deletedLayout).Count == 0);

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            Assert.AreEqual(CustomLayouts.CustomLayouts.Count - 1, data.CustomLayouts.Count);
            Assert.IsFalse(data.CustomLayouts.Exists(x => x.Name == deletedLayout));
        }

        [TestMethod]
        public void DeleteDefaultLayout()
        {
            var deletedLayout = CustomLayouts.CustomLayouts[1].Name;
            FancyZonesEditorHelper.ClickContextMenuItem(Session, deletedLayout, FancyZonesEditorHelper.ElementName.Delete);
            Session.KeyboardAction(Keys.Tab, Keys.Enter);

            // verify the default layout is reset to the "default" default
            Session.Find<Element>(TestConstants.TemplateLayoutNames[LayoutType.PriorityGrid]).Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            Assert.IsNotNull(Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.HorizontalDefaultButtonChecked)));
            Session.Find<Button>(ElementName.Cancel).Click();

            // check the file
            var defaultLayouts = new DefaultLayouts();
            var data = defaultLayouts.Read(defaultLayouts.File);
            string configuration = MonitorConfigurationType.Horizontal.TypeToString();
            Assert.AreEqual(LayoutType.PriorityGrid.TypeToString(), data.DefaultLayouts.Find(x => x.MonitorConfiguration == configuration).Layout.Type);
        }

        [TestMethod]
        public void DeleteLayoutWithHotkey()
        {
            var deletedLayout = CustomLayouts.CustomLayouts[1].Name;
            FancyZonesEditorHelper.ClickContextMenuItem(Session, deletedLayout, FancyZonesEditorHelper.ElementName.Delete);
            Session.KeyboardAction(Keys.Tab, Keys.Enter);

            // verify the hotkey is available
            Session.Find<Element>(CustomLayouts.CustomLayouts[0].Name).Find<Button>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            var hotkeyComboBox = Session.Find<Element>(PowerToys.UITest.By.AccessibilityId(AccessibilityId.HotkeyComboBox));
            Assert.IsNotNull(hotkeyComboBox);
            hotkeyComboBox.Click();

            var popup = Session.Find<Element>(PowerToys.UITest.By.ClassName(ClassName.Popup));
            Assert.IsNotNull(popup);
            try
            {
                for (int i = 0; i < 10; i++)
                {
                    popup.Find<Element>($"{i}");
                }
            }
            catch
            {
                Assert.Fail("Hotkey not found");
            }

            Session.Find<Button>(ElementName.Cancel).DoubleClick();

            // check the file
            var hotkeys = new LayoutHotkeys();
            var data = hotkeys.Read(hotkeys.File);
            int layoutHotkeyCount = 0;
            foreach (var layout in data.LayoutHotkeys)
            {
                if (layout.Key != -1)
                {
                    layoutHotkeyCount++;
                }
            }

            Assert.AreEqual(0, layoutHotkeyCount);
        }
    }
}
