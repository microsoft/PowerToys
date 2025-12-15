// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UITests;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FancyZonesEditorCommon.Data.CustomLayouts;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorHelper;

namespace UITests_FancyZones
{
    [TestClass]
    public class LayoutApplyHotKeyTests : UITestBase
    {
        public LayoutApplyHotKeyTests()
            : base(PowerToysModule.PowerToysSettings, WindowSize.Large)
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

            RestartScopeExe("Hosts");
        }

        [TestMethod("FancyZones.Settings.TestApplyHotKey")]
        [TestCategory("FancyZones #1")]
        public void TestApplyHotKey()
        {
            this.OpenFancyZonesPanel();
            this.ControlQuickLayoutSwitch(true);

            // Set Hotkey
            this.AttachFancyZonesEditor();
            Session.Find<Element>(By.AccessibilityId(AccessibilityId.GridCustomLayoutCard)).Find<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            const string key = "0";
            var hotkeyComboBox = Session.Find<Element>(By.AccessibilityId(AccessibilityId.HotkeyComboBox));
            Assert.IsNotNull(hotkeyComboBox);
            hotkeyComboBox.Click();
            var popup = Session.Find<Element>(By.ClassName(ClassName.Popup));
            Assert.IsNotNull(popup);
            popup.Find<Element>($"{key}").Click(); // assign a free hotkey

            Task.Delay(3000).Wait();
            this.CloseFancyZonesEditor();
            this.AttachPowertoySetting();
            SendKeys(Key.Win, Key.Ctrl, Key.Alt, Key.Num0);
            Task.Delay(3000).Wait();
            this.AttachFancyZonesEditor();
            var element = this.Find<Element>(By.AccessibilityId(AccessibilityId.GridCustomLayoutCard));
            Assert.IsTrue(element.Selected, $"{element.Selected} Grid custom layout is not visible");
            this.CloseFancyZonesEditor();
            this.AttachPowertoySetting();

            SendKeys(Key.Win, Key.Ctrl, Key.Alt, Key.Num1);
            Task.Delay(3000).Wait();
            this.AttachFancyZonesEditor();
            element = this.Find<Element>(By.AccessibilityId(AccessibilityId.Grid9LayoutCard));
            Assert.IsTrue(element.Selected, $"{element.Selected} Grid-9 is not visible");
            this.CloseFancyZonesEditor();
            this.AttachPowertoySetting();

            SendKeys(Key.Win, Key.Ctrl, Key.Alt, Key.Num2);
            Task.Delay(3000).Wait();
            this.AttachFancyZonesEditor();
            element = this.Find<Element>(By.AccessibilityId(AccessibilityId.CanvasCustomLayoutCard));
            Assert.IsTrue(element.Selected, $"{element.Selected} Canvas custom layout is not visible");
            this.CloseFancyZonesEditor();
            this.AttachPowertoySetting();
        }

        /*
        [TestMethod]
        [TestCategory("FancyZones #2")]
        public void TestDragShiftHotKey()
        {
            this.OpenFancyZonesPanel();
            this.ControlQuickLayoutSwitch(true);

            int screenWidth = 1920;  // default 1920

            int targetX = screenWidth / 2 / 3;
            int targetY = screenWidth / 2 / 2;

            LaunchHostFromSetting();
            this.Session.Attach(PowerToysModule.Hosts, WindowSize.Large_Vertical);
            var hostsView = Find<Pane>(By.Name("Non Client Input Sink Window"));
            hostsView.DoubleClick(); // maximize the window

            hostsView.HoldShiftToDrag(Key.Shift, targetX, targetY);
            SendKeys(Key.Num0);
            hostsView.ReleaseAction();
            hostsView.ReleaseKey(Key.Shift);
            SendKeys(Key.Alt, Key.F4);

            // Attach FancyZones Editor
            this.AttachPowertoySetting();
            this.Find<Pane>(By.ClassName("InputNonClientPointerSource")).Click();
            this.OpenFancyZonesPanel(isMax: false);
            this.AttachFancyZonesEditor();
            var elements = this.FindAll<Element>("Grid custom layout");
            if (elements.Count == 0)
            {
                this.Session.Attach(PowerToysModule.Hosts, WindowSize.Large_Vertical);
                hostsView = Find<Pane>(By.Name("Non Client Input Sink Window"));
                hostsView.DoubleClick(); // maximize the window

                hostsView.HoldShiftToDrag(Key.Shift, targetX, targetY);
                SendKeys(Key.Num0);
                hostsView.ReleaseAction();
                hostsView.ReleaseKey(Key.Shift);
                SendKeys(Key.Alt, Key.F4);
                this.AttachPowertoySetting();
                this.Find<Pane>(By.ClassName("InputNonClientPointerSource")).Click();
                this.OpenFancyZonesPanel(isMax: false);
                this.AttachFancyZonesEditor();
                elements = this.FindAll<Element>("Grid custom layout");
            }

            Assert.IsTrue(elements[0].Selected, "Grid custom layout is not visible");
            this.CloseFancyZonesEditor();

            Clean();
        }
        */

        [TestMethod("FancyZones.Settings.HotKeyWindowFlashTest")]
        [TestCategory("FancyZones #3")]
        public void HotKeyWindowFlashTest()
        {
            this.OpenFancyZonesPanel();
            this.ControlQuickLayoutSwitch(true);

            this.TryReaction();
            int tries = 24;
            Pull(tries, "down");
            var switchGroup = this.Find<Group>("Enable quick layout switch");
            switchGroup.Click();
            var checkbox1 = switchGroup.Find<Element>("Flash zones when switching layout");
            if (checkbox1.GetAttribute("Toggle.ToggleState") == "0")
            {
                checkbox1.Click();
            }

            this.Session.PressKey(Key.Win);
            this.Session.PressKey(Key.Ctrl);
            this.Session.PressKey(Key.Alt);
            this.Session.PressKey(Key.Num0);
            bool res = this.IsWindowOpen("FancyZones_ZonesOverlay");
            Assert.IsTrue(res, $" HotKeyWindowFlash Test error: FancyZones_ZonesOverlay is not open");
            this.Session.ReleaseKey(Key.Win);
            this.Session.ReleaseKey(Key.Ctrl);
            this.Session.ReleaseKey(Key.Alt);
            this.Session.ReleaseKey(Key.Num0);

            var checkbox2 = this.Find<CheckBox>("Flash zones when switching layout");
            if (checkbox2.GetAttribute("Toggle.ToggleState") == "1")
            {
                checkbox2.Click();
            }

            // this.CloseFancyZonesEditor();
            Clean();
        }

        [TestMethod("FancyZones.Settings.TestDisableApplyHotKey")]
        [TestCategory("FancyZones #4")]
        public void TestDisableApplyHotKey()
        {
            this.OpenFancyZonesPanel();
            this.ControlQuickLayoutSwitch(false);

            SendKeys(Key.Win, Key.Ctrl, Key.Alt, Key.Num0);
            this.AttachFancyZonesEditor();
            var element = this.Find<Element>(By.AccessibilityId(AccessibilityId.GridCustomLayoutCard));
            Assert.IsFalse(element.Selected, $"{element.Selected} Grid custom layout is not visible");
            this.CloseFancyZonesEditor();
            this.AttachPowertoySetting();

            SendKeys(Key.Win, Key.Ctrl, Key.Alt, Key.Num1);
            this.AttachFancyZonesEditor();
            element = this.Find<Element>(By.AccessibilityId(AccessibilityId.Grid9LayoutCard));
            Assert.IsFalse(element.Selected, $"{element.Selected} Grid-9 is not visible");
            this.CloseFancyZonesEditor();
            this.AttachPowertoySetting();

            SendKeys(Key.Win, Key.Ctrl, Key.Alt, Key.Num2);
            this.AttachFancyZonesEditor();
            element = this.Find<Element>(By.AccessibilityId(AccessibilityId.CanvasCustomLayoutCard));
            Assert.IsFalse(element.Selected, $"{element.Selected} Canvas custom layout is not visible");
            this.CloseFancyZonesEditor();
            this.AttachPowertoySetting();

            Clean();
        }

        [TestMethod("FancyZones.Settings.TestVirtualDesktopLayout")]
        [TestCategory("FancyZones #6")]
        public void TestVirtualDesktopLayout()
        {
            this.OpenFancyZonesPanel();

            this.AttachFancyZonesEditor();
            var element = this.Find<Element>(By.AccessibilityId(AccessibilityId.GridCustomLayoutCard));
            element.Click();
            this.CloseFancyZonesEditor();
            this.ExitScopeExe();

            // Add virtual desktop
            SendKeys(Key.Ctrl, Key.Win, Key.D);
            this.RestartScopeExe();
            this.OpenFancyZonesPanel();
            this.AttachFancyZonesEditor();
            element = this.Find<Element>(By.AccessibilityId(AccessibilityId.GridCustomLayoutCard));
            Assert.IsTrue(element.Selected, $"{element.Selected} Grid custom layout is not visible");
            this.CloseFancyZonesEditor();

            // close the virtual desktop
            SendKeys(Key.Ctrl, Key.Win, Key.Right);
            Task.Delay(500).Wait(); // Optional: Wait for a moment to ensure window switch
            SendKeys(Key.Ctrl, Key.Win, Key.F4);
            Task.Delay(500).Wait(); // Optional: Wait for a moment to ensure window switch

            Clean();
        }

        [TestMethod("FancyZones.Settings.TestVirtualDesktopLayoutExt")]
        [TestCategory("FancyZones #7")]
        public void TestVirtualDesktopLayoutExt()
        {
            this.OpenFancyZonesPanel();

            this.AttachFancyZonesEditor();
            var element = this.Find<Element>(By.AccessibilityId(AccessibilityId.GridCustomLayoutCard));
            element.Click();
            this.CloseFancyZonesEditor();
            this.ExitScopeExe();

            // Add virtual desktop
            SendKeys(Key.Ctrl, Key.Win, Key.D);
            this.RestartScopeExe();
            this.OpenFancyZonesPanel();
            this.AttachFancyZonesEditor();
            element = this.Find<Element>(By.AccessibilityId(AccessibilityId.Grid9LayoutCard));
            element.Click();
            this.CloseFancyZonesEditor();
            this.ExitScopeExe();

            SendKeys(Key.Ctrl, Key.Win, Key.Left);
            this.RestartScopeExe();
            this.OpenFancyZonesPanel();
            this.AttachFancyZonesEditor();
            element = this.Find<Element>(By.AccessibilityId(AccessibilityId.GridCustomLayoutCard));
            Assert.IsTrue(element.Selected, $"{element.Selected} Grid custom layout is not visible");
            this.CloseFancyZonesEditor();
            this.ExitScopeExe();

            // close the virtual desktop
            SendKeys(Key.Ctrl, Key.Win, Key.Right);
            Task.Delay(500).Wait(); // Optional: Wait for a moment to ensure window switch
            SendKeys(Key.Ctrl, Key.Win, Key.F4);
            Task.Delay(500).Wait(); // Optional: Wait for a moment to ensure window switch

            Clean();
        }

        [TestMethod("FancyZones.Settings.TestDeleteCustomLayoutBehavior")]
        [TestCategory("FancyZones #8")]
        public void TestDeleteCustomLayoutBehavior()
        {
            this.OpenFancyZonesPanel();

            this.AttachFancyZonesEditor();
            this.Find<Element>(By.AccessibilityId(AccessibilityId.GridCustomLayoutCard)).Click();
            this.Find<Element>(By.AccessibilityId(AccessibilityId.GridCustomLayoutCard)).Find<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            Session.Find<Button>(By.AccessibilityId(AccessibilityId.DeleteLayoutButton)).Click();
            Session.SendKeySequence(Key.Tab, Key.Enter);

            // verify the empty layout is selected
            Assert.IsTrue(Session.Find<Element>(TestConstants.TemplateLayoutNames[LayoutType.Blank])!.Selected);

            Clean();
        }

        [TestMethod("FancyZones.Settings.TestCreateGridLayoutChangeMonitorSetting")]
        [TestCategory("FancyZones #9")]
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
            UITestBase.NativeMethods.ChangeDisplayResolution(width, height);
            this.AttachPowertoySetting();
            this.AttachFancyZonesEditor();
            var maxButton = this.Find<Button>("Maximize");
            maxButton.Click(); // maximize the window
            var resolution = this.Session.Find<Element>(By.AccessibilityId("Monitors")).Find<Element>("Monitor 1").Find<Element>(By.AccessibilityId("ResolutionText"));
            if (resolution.Text != "640 × 480")
            {
                this.CloseFancyZonesEditor();
                UITestBase.NativeMethods.ChangeDisplayResolution(nowWidth, nowHeight);
                Assert.AreEqual("640 × 480", resolution.Text);
            }

            this.CloseFancyZonesEditor();
            UITestBase.NativeMethods.ChangeDisplayResolution(nowWidth, nowHeight);

            Clean();
        }

        private void OpenFancyZonesPanel(bool launchAsAdmin = false, bool isMax = false)
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
            if (isMax == true)
            {
                this.Find<Button>("Maximize").Click(); // maximize the window
            }

            this.Find<Custom>("Editor").Find<TextBlock>(By.AccessibilityId("HeaderPresenter")).Click();
        }

        private void ControlQuickLayoutSwitch(bool flag)
        {
            this.TryReaction();
            int tries = 24;
            Pull(tries, "down"); // Pull the setting page up to make sure the setting is visible
            this.Find<ToggleSwitch>("FancyZonesQuickLayoutSwitch").Toggle(flag);

            // Go back and forth to make sure settings applied
            this.Find<NavigationViewItem>("Workspaces").Click();
            Task.Delay(200).Wait();
            this.Find<NavigationViewItem>("FancyZones").Click();
        }

        private void TryReaction()
        {
            this.Find<Custom>("Editor").Find<TextBlock>(By.AccessibilityId("HeaderPresenter")).Click();
        }

        private void AttachPowertoySetting()
        {
            Task.Delay(200).Wait();
            this.Session.Attach(PowerToysModule.PowerToysSettings);
        }

        private void AttachFancyZonesEditor()
        {
            Task.Delay(4000).Wait();
            this.Find<Button>(By.AccessibilityId(AccessibilityId.LaunchLayoutEditorButton)).Click();

            Task.Delay(3000).Wait();
            this.Session.Attach(PowerToysModule.FancyZone);
            Task.Delay(3000).Wait();
        }

        private void CloseFancyZonesEditor()
        {
            this.Session.Find<Button>("Close").Click();
        }

        private void Clean()
        {
            // clean app zone history file
            FancyZonesEditorHelper.Files.CustomLayoutsIOHelper.DeleteFile();
            FancyZonesEditorHelper.Files.LayoutHotkeysIOHelper.DeleteFile();
            FancyZonesEditorHelper.Files.LayoutTemplatesIOHelper.DeleteFile();
        }

        private void Pull(int tries = 5, string direction = "up")
        {
            Key keyToSend = direction == "up" ? Key.Up : Key.Down;
            for (int i = 0; i < tries; i++)
            {
                SendKeys(keyToSend);
            }
        }

        private void LaunchHostFromSetting(bool showWarning = false, bool launchAsAdmin = false)
        {
            // Goto Hosts File Editor setting page
            if (this.FindAll<NavigationViewItem>("Hosts File Editor").Count == 0)
            {
                // Expand Advanced list-group if needed
                this.Find<NavigationViewItem>("Advanced").Click();
            }

            this.Find<NavigationViewItem>("Hosts File Editor").Click();
            Task.Delay(1000).Wait();

            this.Find<ToggleSwitch>("Enable Hosts File Editor").Toggle(true);
            this.Find<ToggleSwitch>("Launch as administrator").Toggle(launchAsAdmin);
            this.Find<ToggleSwitch>("Show a warning at startup").Toggle(showWarning);

            // launch Hosts File Editor
            this.Find<Button>("Launch Hosts File Editor").Click();

            Task.Delay(5000).Wait();
        }
    }
}
