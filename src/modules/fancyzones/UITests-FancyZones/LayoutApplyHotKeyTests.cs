// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UITests;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FancyZonesEditorCommon.Data.CustomLayouts;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorHelper;

namespace Microsoft.FancyZones.UITests
{
    [TestClass]
    public class LayoutApplyHotKeyTests : UITestBase
    {
        private static readonly string WindowName = "Windows (C:) - File Explorer"; // set launch explorer window name

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
            FancyZonesEditorHelper.Files.LayoutHotkeysIOHelper.WriteData(layoutHotkeys.Serialize(LayoutHotkeysList));

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
            this.ControlQuickLayoutSwitch(true);

            SendKeys(Key.Win, Key.Ctrl, Key.Alt, Key.Num0);
            this.AttachFancyZonesEditor();
            var element = this.Find<Element>("Grid custom layout");
            Assert.IsTrue(element.Selected, $"{element.Selected} Grid custom layout is not visible");
            this.CloseFancyZonesEditor();
            this.AttachPowertoySetting();

            SendKeys(Key.Win, Key.Ctrl, Key.Alt, Key.Num1);
            this.AttachFancyZonesEditor();
            element = this.Find<Element>("Grid-9");
            Assert.IsTrue(element.Selected, $"{element.Selected} Grid-9 is not visible");
            this.CloseFancyZonesEditor();
            this.AttachPowertoySetting();

            SendKeys(Key.Win, Key.Ctrl, Key.Alt, Key.Num2);
            this.AttachFancyZonesEditor();
            element = this.Find<Element>("Canvas custom layout");
            Assert.IsTrue(element.Selected, $"{element.Selected} Canvas custom layout is not visible");
            this.CloseFancyZonesEditor();
            this.AttachPowertoySetting();
        }

        [TestMethod]
        public void TestDragShiftHotKey()
        {
            this.OpenFancyZonesPanel();
            this.ControlQuickLayoutSwitch(true);

            int screenWidth = 1920;  // default 1920

            int targetX = screenWidth / 2 / 3;
            int targetY = screenWidth / 2 / 2;

            // assert the AppZoneHistory layout is set
            Session.KillAllProcessesByName("explorer");
            Session.StartExe("explorer.exe", "C:\\");

            Session.Attach(WindowName, WindowSize.UnSpecified);
            var tabView = Find<Element>(PowerToys.UITest.By.AccessibilityId("TabView"));
            tabView.DoubleClick(); // maximize the window
            tabView.HoldShiftToDrag(Key.Shift, targetX, targetY);
            SendKeys(Key.Num0);
            tabView.ReleaseAction();
            tabView.ReleaseKey(Key.Shift);

            // Attach FancyZones Editor
            this.AttachPowertoySetting();
            this.AttachFancyZonesEditor();
            var element = this.Find<Element>("Grid custom layout");
            Assert.IsTrue(element.Selected, "Grid custom layout is not visible");
            this.CloseFancyZonesEditor();

            Session.Attach(WindowName, WindowSize.UnSpecified);
            tabView = Find<Element>(PowerToys.UITest.By.AccessibilityId("TabView"));
            tabView.DoubleClick(); // maximize the window
            tabView.HoldShiftToDrag(Key.Shift, targetX, targetY);
            SendKeys(Key.Num1);
            tabView.ReleaseAction();
            tabView.ReleaseKey(Key.Shift);

            // Attach FancyZones Editor
            this.AttachPowertoySetting();
            this.AttachFancyZonesEditor();
            element = this.Find<Element>("Grid-9");
            Assert.IsTrue(element.Selected, "Grid-9 is not visible");
            this.CloseFancyZonesEditor();

            Session.Attach(WindowName, WindowSize.UnSpecified);
            tabView = Find<Element>(PowerToys.UITest.By.AccessibilityId("TabView"));
            tabView.DoubleClick(); // maximize the window
            tabView.HoldShiftToDrag(Key.Shift, targetX, targetY);
            SendKeys(Key.Num2);
            tabView.ReleaseAction();
            tabView.ReleaseKey(Key.Shift);

            // Attach FancyZones Editor
            this.AttachPowertoySetting();
            this.AttachFancyZonesEditor();
            element = this.Find<Element>("Canvas custom layout");
            Assert.IsTrue(element.Selected, "Canvas custom layout is not visible");
            this.CloseFancyZonesEditor();
            this.AttachPowertoySetting();

            // Clean
            Session.KillAllProcessesByName("explorer");
        }

        [TestMethod]
        public void HotKeyWindowFlashTest()
        {
            this.OpenFancyZonesPanel();
            this.ControlQuickLayoutSwitch(true);

            int tries = 16;
            Pull(tries, "down");
            this.Find<Element>("Enable quick layout switch").Click();
            var checkbox1 = this.Find<Element>("Flash zones when switching layout");
            if (checkbox1.GetAttribute("TogglePattern.ToggleState") == "False")
            {
                checkbox1.Click();
            }

            this.Session.PressKey(Key.Win);
            this.Session.PressKey(Key.Ctrl);
            this.Session.PressKey(Key.Alt);
            this.Session.PressKey(Key.Num0);
            bool res = this.Session.IsWindowOpen("FancyZones_ZonesOverlay");
            Assert.IsTrue(res, $"==={res}===");
            this.Session.ReleaseKey(Key.Win);
            this.Session.ReleaseKey(Key.Ctrl);
            this.Session.ReleaseKey(Key.Alt);
            this.Session.ReleaseKey(Key.Num0);

            var checkbox2 = this.Find<Element>("Flash zones when switching layout");
            if (checkbox2.GetAttribute("TogglePattern.ToggleState") == "True")
            {
                checkbox2.Click();
            }

            this.Session.PressKey(Key.Win);
            this.Session.PressKey(Key.Ctrl);
            this.Session.PressKey(Key.Alt);
            this.Session.PressKey(Key.Num0);
            res = this.Session.IsWindowOpen("FancyZones_ZonesOverlay");
            Assert.IsFalse(res, $"==={res}===");
            this.Session.ReleaseKey(Key.Win);
            this.Session.ReleaseKey(Key.Ctrl);
            this.Session.ReleaseKey(Key.Alt);
            this.Session.ReleaseKey(Key.Num0);
        }

        [TestMethod]
        public void TestDisableApplyHotKey()
        {
            this.OpenFancyZonesPanel();
            this.AttachFancyZonesEditor();
            this.ControlQuickLayoutSwitch(false);
            this.CloseFancyZonesEditor();

            SendKeys(Key.Win, Key.Ctrl, Key.Alt, Key.Num0);
            this.AttachFancyZonesEditor();
            var element = this.Find<Element>("Grid custom layout");
            Assert.IsTrue(element.Selected, $"{element.Selected} Grid custom layout is not visible");
            this.CloseFancyZonesEditor();
            this.AttachPowertoySetting();

            SendKeys(Key.Win, Key.Ctrl, Key.Alt, Key.Num1);
            this.AttachFancyZonesEditor();
            element = this.Find<Element>("Grid-9");
            Assert.IsTrue(element.Selected, $"{element.Selected} Grid-9 is not visible");
            this.CloseFancyZonesEditor();
            this.AttachPowertoySetting();

            SendKeys(Key.Win, Key.Ctrl, Key.Alt, Key.Num2);
            this.AttachFancyZonesEditor();
            element = this.Find<Element>("Canvas custom layout");
            Assert.IsTrue(element.Selected, $"{element.Selected} Canvas custom layout is not visible");
            this.CloseFancyZonesEditor();
            this.AttachPowertoySetting();
        }

        [TestMethod]
        public void TestVirtualDesktopLayout()
        {
            this.OpenFancyZonesPanel();

            this.AttachFancyZonesEditor();
            var element = this.Find<Element>("Grid custom layout");
            element.Click();
            this.CloseFancyZonesEditor();
            this.ExitScopeExe();

            // Add virtual desktop
            SendKeys(Key.Ctrl, Key.Win, Key.D);
            this.RestartScopeExe();
            this.OpenFancyZonesPanel();
            this.AttachFancyZonesEditor();
            element = this.Find<Element>("Grid custom layout");
            Assert.IsTrue(element.Selected, $"{element.Selected} Canvas custom layout is not visible");
            this.CloseFancyZonesEditor();

            // close the virtual desktop
            SendKeys(Key.Ctrl, Key.Win, Key.Right);
            Task.Delay(500).Wait(); // Optional: Wait for a moment to ensure window switch
            SendKeys(Key.Ctrl, Key.Win, Key.F4);
            Task.Delay(500).Wait(); // Optional: Wait for a moment to ensure window switch
        }

        [TestMethod]
        public void TestVirtualDesktopLayoutExt()
        {
            this.OpenFancyZonesPanel();

            this.AttachFancyZonesEditor();
            var element = this.Find<Element>("Grid custom layout");
            element.Click();
            this.CloseFancyZonesEditor();
            this.ExitScopeExe();

            // Add virtual desktop
            SendKeys(Key.Ctrl, Key.Win, Key.D);
            this.RestartScopeExe();
            this.OpenFancyZonesPanel();
            this.AttachFancyZonesEditor();
            element = this.Find<Element>("Grid-9");
            element.Click();
            this.CloseFancyZonesEditor();
            this.ExitScopeExe();

            SendKeys(Key.Ctrl, Key.Win, Key.Left);
            this.RestartScopeExe();
            this.OpenFancyZonesPanel();
            this.AttachFancyZonesEditor();
            element = this.Find<Element>("Grid custom layout");
            Assert.IsTrue(element.Selected, $"{element.Selected} Canvas custom layout is not visible");
            this.CloseFancyZonesEditor();
            this.ExitScopeExe();

            // close the virtual desktop
            SendKeys(Key.Ctrl, Key.Win, Key.Right);
            Task.Delay(500).Wait(); // Optional: Wait for a moment to ensure window switch
            SendKeys(Key.Ctrl, Key.Win, Key.F4);
            Task.Delay(500).Wait(); // Optional: Wait for a moment to ensure window switch
        }

        [TestMethod]
        public void TestDeleteCustomLayoutBehavior()
        {
            this.OpenFancyZonesPanel();

            this.AttachFancyZonesEditor();
            this.Find<Element>("Grid custom layout").Click();
            this.Find<Element>("Grid custom layout").Find<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            Session.Find<Button>(By.AccessibilityId(AccessibilityId.DeleteLayoutButton)).Click();
            Session.SendKeySequence(Key.Tab, Key.Enter);

            // verify the empty layout is selected
            Assert.IsTrue(Session.Find<Element>(TestConstants.TemplateLayoutNames[LayoutType.Blank])!.Selected);
        }

        [TestMethod]
        public void TestCreateGridLayoutChangeMonitorSetting()
        {
            this.OpenFancyZonesPanel();
            this.AttachFancyZonesEditor();

            string name = "Custom layout 1";
            this.Session.Find<Element>(By.AccessibilityId(AccessibilityId.NewLayoutButton)).Click();
            this.Session.Find<Element>(By.AccessibilityId(AccessibilityId.PrimaryButton)).Click();
            this.Session.Find<Button>(ElementName.Save).Click();

            // verify new layout presented
            Assert.IsNotNull(Session.Find<Element>(name));
            this.CloseFancyZonesEditor();

            int nowHeight = UITestBase.MonitorInfoData.Monitors[UITestBase.MonitorInfoData.Monitors.Count - 1].PelsHeight;
            int nowWidth = UITestBase.MonitorInfoData.Monitors[UITestBase.MonitorInfoData.Monitors.Count - 1].PelsWidth;
            int height = UITestBase.MonitorInfoData.Monitors[0].PelsHeight;
            int width = UITestBase.MonitorInfoData.Monitors[0].PelsWidth;
            UITestBase.NativeMethods.ChangeDispalyResolution(width, height);
            this.AttachPowertoySetting();
            this.AttachFancyZonesEditor();
            Session.Find<Element>(By.AccessibilityId("Monitors")).Find<Element>("Monitor 1").Find(width + " x " + height);
            this.CloseFancyZonesEditor();
            UITestBase.NativeMethods.ChangeDispalyResolution(nowWidth, nowHeight);
        }

        private void OpenFancyZonesPanel(bool launchAsAdmin = false)
        {
            var windowingElement = this.Find<NavigationViewItem>("Windowing & Layouts");

            // Goto FancyZones Editor setting page
            if (this.FindAll<NavigationViewItem>("FancyZones").Count == 0)
            {
                // Expand Advanced list-group if needed
                windowingElement.Click();
            }

            windowingElement.Find<Element>("FancyZones").Click();
            this.Find<ToggleSwitch>("Enable FancyZones").Toggle(true);
            this.Session.SetMainWindowSize(WindowSize.Large_Vertical);
        }

        private void ControlQuickLayoutSwitch(bool flag)
        {
            int tries = 12;
            Pull(tries, "down"); // Pull the setting page up to make sure the setting is visible
            this.Find<ToggleSwitch>("Enable quick layout switch").Toggle(flag);

            tries = 12;
            Pull(tries, "up");
        }

        private void AttachPowertoySetting()
        {
            Task.Delay(200).Wait();
            this.Session.Attach(PowerToysModule.PowerToysSettings);
        }

        private void AttachFancyZonesEditor()
        {
            this.Find<Button>("Launch layout editor").Click();

            Task.Delay(4000).Wait();
            this.Session.Attach(PowerToysModule.FancyZone);
        }

        private void CloseFancyZonesEditor()
        {
            this.Session.Find<Element>("Close").Click();
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
