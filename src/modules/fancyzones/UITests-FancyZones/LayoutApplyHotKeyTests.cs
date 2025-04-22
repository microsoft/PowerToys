// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Xml.Linq;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernWpf.Controls;
using OpenQA.Selenium;
using static FancyZonesEditorCommon.Data.CustomLayouts;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorHelper;
using NavigationViewItem = Microsoft.PowerToys.UITest.NavigationViewItem;
using ToggleSwitch = Microsoft.PowerToys.UITest.ToggleSwitch;

namespace Microsoft.FancyZones.UITests
{
    [TestClass]
    public class LayoutApplyHotKeyTests : UITestBase
    {
        public LayoutApplyHotKeyTests()
            : base(PowerToysModule.PowerToysSettings)
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
                new CustomLayoutWrapper
                {
                    Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                    Type = CustomLayout.Grid.TypeToString(),
                    Name = "Grid custom layout",
                    Info = new CustomLayouts().ToJsonElement(new GridInfoWrapper
                    {
                        Rows = 2,
                        Columns = 2,
                        RowsPercentage = new List<int> { 5000, 5000 },
                        ColumnsPercentage = new List<int> { 5000, 5000 },
                        CellChildMap = new int[][] { [0, 1], [2, 3] },
                        SensitivityRadius = 30,
                        Spacing = 26,
                        ShowSpacing = false,
                    }),
                },
                new CustomLayoutWrapper
                {
                    Uuid = "{0EB9BF3E-010E-46D7-8681-1879D1E111E1}",
                    Type = CustomLayout.Grid.TypeToString(),
                    Name = "Grid-9",
                    Info = new CustomLayouts().ToJsonElement(new GridInfoWrapper
                    {
                        Rows = 3,
                        Columns = 3,
                        RowsPercentage = new List<int> { 2333, 3333, 4334 },
                        ColumnsPercentage = new List<int> { 2333, 3333, 4334 },
                        CellChildMap = new int[][] { [0, 1, 2], [3, 4, 5], [6, 7, 8] },
                        SensitivityRadius = 20,
                        Spacing = 3,
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
                        RefHeight = 1040,
                        RefWidth = 1920,
                        SensitivityRadius = 10,
                        Zones = new List<CanvasInfoWrapper.CanvasZoneWrapper>
                        {
                            new CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 0,
                                Y = 0,
                                Width = 500,
                                Height = 250,
                            },
                            new CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 500,
                                Y = 0,
                                Width = 1420,
                                Height = 500,
                            },
                            new CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 0,
                                Y = 250,
                                Width = 1920,
                                Height = 500,
                            },
                        },
                    }),
                },
            },
        };

        private static readonly LayoutHotkeys.LayoutHotkeysWrapper LayoutHotkeysList = new LayoutHotkeys.LayoutHotkeysWrapper
        {
            LayoutHotkeys = new List<LayoutHotkeys.LayoutHotkeyWrapper>
            {
                new LayoutHotkeys.LayoutHotkeyWrapper
                {
                    Key = 0,
                    LayoutId = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                },
                new LayoutHotkeys.LayoutHotkeyWrapper
                {
                    Key = 1,
                    LayoutId = "{0EB9BF3E-010E-46D7-8681-1879D1E111E1}",
                },
                new LayoutHotkeys.LayoutHotkeyWrapper
                {
                    Key = 2,
                    LayoutId = "{E7807D0D-6223-4883-B15B-1F3883944C09}",
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
            FancyZonesEditorHelper.Files.Restore();
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
            FancyZonesEditorHelper.Files.CustomLayoutsIOHelper.WriteData(layoutHotkeys.Serialize(LayoutHotkeysList));

            AppliedLayouts appliedLayouts = new AppliedLayouts();
            AppliedLayouts.AppliedLayoutsListWrapper appliedLayoutsWrapper = new AppliedLayouts.AppliedLayoutsListWrapper
            {
                AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper> { },
            };
            FancyZonesEditorHelper.Files.AppliedLayoutsIOHelper.WriteData(appliedLayouts.Serialize(appliedLayoutsWrapper));

            this.RestartScopeExe();
        }

        [TestMethod]
        public void TestApplyHotKey()
        {
            this.OpenFancyZonesPanel();

            this.Session.SetMainWindowSize(WindowSize.Large_Vertical);
            int tries = 10;
            Pull(tries, "down"); // Pull the setting page up to make sure the setting is visible
            this.Find<ToggleSwitch>("Enable quick layout switch").Toggle(true);

            tries = 10;
            Pull(tries, "up");
            this.Find<Button>("Launch layout editor").Click();

            Task.Delay(1000).Wait();
            this.Session.Attach(PowerToysModule.FancyZone);
            SendKeys(Key.Win, Key.Ctrl, Key.Alt, Key.Num0);
            var element = this.Find<Element>("Grid custom layout");
            Assert.IsTrue(element.Selected, "Grid custom layout is not visible");

            SendKeys(Key.Win, Key.Ctrl, Key.Alt, Key.Num1);
            element = this.Find<Element>("Grid-9");
            Assert.IsTrue(element.Selected, "Grid-9 is not visible");

            SendKeys(Key.Win, Key.Ctrl, Key.Alt, Key.Num2);
            element = this.Find<Element>("Canvas custom layout");
            Assert.IsTrue(element.Selected, "Canvas custom layout is not visible");
        }

        private void OpenFancyZonesPanel(bool launchAsAdmin = false)
        {
            // Goto FancyZones Editor setting page
            if (this.FindAll<NavigationViewItem>("FancyZones").Count == 0)
            {
                // Expand Advanced list-group if needed
                this.Find<NavigationViewItem>("Windowing & Layouts").Click();
            }

            this.Find<NavigationViewItem>("FancyZones").Click();
            this.Find<ToggleSwitch>("Enable FancyZones").Toggle(true);
            this.Session.SetMainWindowSize(WindowSize.Large_Vertical);
        }

        private void Pull(int tries = 5, string direction = "up")
        {
            Key keyToSend = direction == "up" ? Key.Up : Key.Down;
            for (int i = 0; i < tries; i++)
            {
                SendKeys(keyToSend);
            }
        }
    }
}
