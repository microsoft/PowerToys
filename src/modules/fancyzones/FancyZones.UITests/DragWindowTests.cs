// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZones.UITests.Utils;
using Microsoft.FancyZonesEditor.UITests.Utils;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualBasic.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using static FancyZonesEditorCommon.Data.CustomLayouts;

namespace UITests_FancyZones
{
    [TestClass]
    public class DragWindowTests : UITestBase
    {
        private static readonly IOTestHelper AppZoneHistory = new FancyZonesEditorFiles().AppZoneHistoryIOHelper;
        private static string nonPrimaryMouseButton = "Right";

        private static string highlightColor = "#008CFF"; // set highlight color
        private static string inactivateColor = "#AACDFF"; // set  inactivate zone color

        // set screen margin
        private static int screenMarginTop;
        private static int screenMarginLeft;
        private static int screenMarginRight;
        private static int screenMarginBottom;

        // set 1/4 margin
        private static int quarterX;
        private static int quarterY;

        private static string powertoysWindowName = "PowerToys Settings"; // set powertoys settings window name

        public DragWindowTests()
            : base(PowerToysModule.PowerToysSettings, WindowSize.Medium)
        {
        }

        [TestInitialize]
        public void TestInitialize()
        {
            Session.KillAllProcessesByName("PowerToys");
            ClearOpenWindows();

            AppZoneHistory.DeleteFile();
            FancyZonesEditorHelper.Files.Restore();
            SetupCustomLayouts();

            RestartScopeExe("Hosts");
            Thread.Sleep(2000);

            // Get the current mouse button setting
            nonPrimaryMouseButton = SystemInformation.MouseButtonsSwapped ? "Left" : "Right";

            // get PowerToys window Name
            powertoysWindowName = ZoneSwitchHelper.GetActiveWindowTitle();

            // Ensure FancyZones settings page is visible and enable FancyZones
            LaunchFancyZones();
        }

        /// <summary>
        /// Test toggling zones using a non-primary mouse click during window dragging.
        /// <list type="bullet">
        /// <item>
        /// <description>Verifies that clicking a non-primary mouse button deactivates zones while dragging a window.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod("FancyZones.Settings.TestToggleZonesWithNonPrimaryMouseClick")]
        [TestCategory("FancyZones_Dragging #3")]
        public void TestToggleZonesWithNonPrimaryMouseClick()
        {
            string testCaseName = nameof(TestToggleZonesWithNonPrimaryMouseClick);

            var windowRect = Session.GetMainWindowRect();
            int startX = windowRect.Left + 70;
            int startY = windowRect.Top + 25;
            int endX = startX + 300;
            int endY = startY + 300;

            var (initialColor, withMouseColor) = RunDragInteractions(
                preAction: () =>
                {
                    Session.MoveMouseTo(startX, startY);
                    Session.PerformMouseAction(MouseActionType.LeftDown);
                    Session.MoveMouseTo(endX, endY);
                },
                postAction: () =>
                {
                    // press non-primary mouse button to toggle zones
                    Session.PerformMouseAction(
                nonPrimaryMouseButton == "Right" ? MouseActionType.RightClick : MouseActionType.LeftClick);
                },
                releaseAction: () =>
                {
                    Session.PerformMouseAction(MouseActionType.LeftUp);
                },
                testCaseName: testCaseName);

            // check the zone color is deactivated
            Assert.AreNotEqual(highlightColor, withMouseColor, $"[{testCaseName}] Zone deactivation failed.");

            // check the zone color is activated
            Assert.AreEqual(highlightColor, initialColor, $"[{testCaseName}] Zone activation failed.");
        }

        /// <summary>
        /// Test both use Shift and non primary mouse off settings.
        /// <list type="bullet">
        /// <item>
        /// <description>Verifies that pressing the Shift key deactivates zones during a window drag-and-hold action.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod("FancyZones.Settings.TestShowZonesWhenShiftAndMouseOff")]
        [TestCategory("FancyZones_Dragging #4")]
        public void TestShowZonesWhenShiftAndMouseOff()
        {
            string testCaseName = nameof(TestShowZonesWhenShiftAndMouseOff);

            var windowRect = Session.GetMainWindowRect();
            int startX = windowRect.Left + 70;
            int startY = windowRect.Top + 25;
            int endX = startX + 300;
            int endY = startY + 300;

            var (initialColor, withShiftColor) = RunDragInteractions(
               preAction: () =>
               {
                   Session.MoveMouseTo(startX, startY);
                   Session.PerformMouseAction(MouseActionType.LeftDown);
                   Session.MoveMouseTo(endX, endY);
               },
               postAction: () =>
               {
                   // press Shift Key to deactivate zones
                   Session.PressKey(Key.Shift);
                   Task.Delay(1000).Wait();
               },
               releaseAction: () =>
               {
                   Session.PerformMouseAction(MouseActionType.LeftUp);
                   Session.ReleaseKey(Key.Shift);
               },
               testCaseName: testCaseName);

            Assert.AreEqual(highlightColor, initialColor, $"[{testCaseName}] Zone activation failed.");
            Assert.AreNotEqual(highlightColor, withShiftColor, $"[{testCaseName}] Zone deactivation failed.");
        }

        /// <summary>
        /// Test zone visibility when both Shift key and mouse settings are involved.
        /// <list type="bullet">
        /// <item>
        /// <description>Verifies that zones are activated when Shift is pressed during drag, and deactivated by a non-primary mouse click.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod("FancyZones.Settings.TestShowZonesWhenShiftAndMouseOn")]
        [TestCategory("FancyZones_Dragging #5")]
        public void TestShowZonesWhenShiftAndMouseOn()
        {
            string testCaseName = nameof(TestShowZonesWhenShiftAndMouseOn);

            var windowRect = Session.GetMainWindowRect();
            int startX = windowRect.Left + 70;
            int startY = windowRect.Top + 25;
            int endX = startX + 300;
            int endY = startY + 300;
            var (initialColor, withShiftColor) = RunDragInteractions(
             preAction: () =>
             {
                 Session.MoveMouseTo(startX, startY);
                 Session.PerformMouseAction(MouseActionType.LeftDown);
                 Session.MoveMouseTo(endX, endY);
             },
             postAction: () =>
             {
                 Session.PressKey(Key.Shift);
             },
             releaseAction: () =>
             {
             },
             testCaseName: testCaseName);

            Assert.AreEqual(highlightColor, withShiftColor, $"[{testCaseName}] show zone failed.");

            Session.PerformMouseAction(
             nonPrimaryMouseButton == "Right" ? MouseActionType.RightClick : MouseActionType.LeftClick);

            string zoneColorWithMouse = GetOutWindowPixelColor(30);
            Assert.AreEqual(initialColor, zoneColorWithMouse, $"[{nameof(TestShowZonesWhenShiftAndMouseOff)}] Zone deactivate failed.");

            Session.ReleaseKey(Key.Shift);
            Session.PerformMouseAction(MouseActionType.LeftUp);
        }

        /// <summary>
        /// Test that a window becomes transparent during dragging when the transparent window setting is enabled.
        /// <list type="bullet">
        /// <item>
        /// <description>Verifies that the window appears transparent while being dragged.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod("FancyZones.Settings.TestMakeDraggedWindowTransparentOn")]
        [TestCategory("FancyZones_Dragging #8")]
        public void TestMakeDraggedWindowTransparentOn()
        {
            var pixel = GetPixelWhenMakeDraggedWindow();
            Assert.AreNotEqual(pixel.PixelInWindow, pixel.TransPixel, $"[{nameof(TestMakeDraggedWindowTransparentOn)}]  Window transparency failed.");
        }

        /// <summary>
        /// Test that a window remains opaque during dragging when the transparent window setting is disabled.
        /// <list type="bullet">
        /// <item>
        /// <description>Verifies that the window is not transparent while being dragged.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod("FancyZones.Settings.TestMakeDraggedWindowTransparentOff")]
        [TestCategory("FancyZones_Dragging #8")]
        public void TestMakeDraggedWindowTransparentOff()
        {
            var pixel = GetPixelWhenMakeDraggedWindow();
            Assert.AreEqual(pixel.PixelInWindow, pixel.TransPixel, $"[{nameof(TestMakeDraggedWindowTransparentOff)}]  Window without transparency failed.");
        }

        /// <summary>
        /// Test Use Shift key to activate zones while dragging a window in FancyZones Zone Behaviour Settings
        /// <list type="bullet">
        /// <item>
        /// <description>Verifies that holding Shift while dragging shows all zones as expected.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod("FancyZones.Settings.TestShowZonesOnShiftDuringDrag")]
        [TestCategory("FancyZones_Dragging #1")]
        public void TestShowZonesOnShiftDuringDrag()
        {
            string testCaseName = nameof(TestShowZonesOnShiftDuringDrag);

            var windowRect = Session.GetMainWindowRect();
            int startX = windowRect.Left + 70;
            int startY = windowRect.Top + 25;
            int endX = startX + 300;
            int endY = startY + 300;

            var (initialColor, withShiftColor) = RunDragInteractions(
              preAction: () =>
              {
                  Session.MoveMouseTo(startX, startY);
                  Session.PerformMouseAction(MouseActionType.LeftDown);
                  Session.MoveMouseTo(endX, endY);
              },
              postAction: () =>
              {
                  Session.PressKey(Key.Shift);
                  Task.Delay(500).Wait();
              },
              releaseAction: () =>
              {
                  Session.ReleaseKey(Key.Shift);
                  Task.Delay(1000).Wait(); // Optional: Wait for a moment to ensure window switch
              },
              testCaseName: testCaseName);

            string zoneColorWithoutShift = GetOutWindowPixelColor(30);

            Assert.AreNotEqual(initialColor, withShiftColor, $"[{testCaseName}] Zone color did not change; zone activation failed.");
            Assert.AreEqual(highlightColor, withShiftColor, $"[{testCaseName}] Zone color did not match the highlight color; activation failed.");

            Session.PerformMouseAction(MouseActionType.LeftUp);
        }

        /// <summary>
        /// Test dragging a window during Shift key press in FancyZones Zone Behaviour Settings
        /// <list type="bullet">
        /// <item>
        /// <description>Verifies that dragging activates zones as expected.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod("FancyZones.Settings.TestShowZonesOnDragDuringShift")]
        [TestCategory("FancyZones_Dragging #2")]
        public void TestShowZonesOnDragDuringShift()
        {
            string testCaseName = nameof(TestShowZonesOnDragDuringShift);

            var windowRect = Session.GetMainWindowRect();
            int startX = windowRect.Left + 70;
            int startY = windowRect.Top + 25;
            int endX = startX + 300;
            int endY = startY + 300;

            var (initialColor, withDragColor) = RunDragInteractions(
                preAction: () =>
                {
                    Session.PressKey(Key.Shift);
                    Task.Delay(100).Wait();
                },
                postAction: () =>
                {
                    Session.MoveMouseTo(startX, startY);
                    Session.PerformMouseAction(MouseActionType.LeftDown);
                    Session.MoveMouseTo(endX, endY);
                    Task.Delay(1000).Wait();
                },
                releaseAction: () =>
                {
                    Session.PerformMouseAction(MouseActionType.LeftUp);
                    Session.ReleaseKey(Key.Shift);
                    Task.Delay(100).Wait();
                },
                testCaseName: testCaseName);

            Assert.AreNotEqual(initialColor, withDragColor, $"[{testCaseName}] Zone color did not change; zone activation failed.");
            Assert.AreEqual(highlightColor, withDragColor, $"[{testCaseName}] Zone color did not match the highlight color; activation failed.");

            // double check by app-zone-history.json
            string appZoneHistoryJson = AppZoneHistory.GetData();
            string? zoneNumber = ZoneSwitchHelper.GetZoneIndexSetByAppName(powertoysWindowName, appZoneHistoryJson);
            Assert.IsNull(zoneNumber, $"[{testCaseName}] AppZoneHistory layout was unexpectedly set.");
        }

        // Helper method to ensure the desktop has no open windows by clicking the "Show Desktop" button
        private void ClearOpenWindows()
        {
            string desktopButtonName;

            // Check for both possible button names (Win10/Win11)
            if (this.FindAll<Microsoft.PowerToys.UITest.Button>("Show Desktop", 5000, true).Count == 0)
            {
                // win10
                desktopButtonName = "Show desktop";
            }
            else
            {
                // win11
                desktopButtonName = "Show Desktop";
            }

            this.Find<Microsoft.PowerToys.UITest.Button>(By.Name(desktopButtonName), 5000, true).Click(false, 500, 1000);
        }

        // Setup custom layout with 1 subzones
        private void SetupCustomLayouts()
        {
            var customLayouts = new CustomLayouts();
            var customLayoutListWrapper = CustomLayoutsList;

            if (TestContext.TestName == "TestMakeDraggedWindowTransparentOff")
            {
                customLayoutListWrapper = CustomLayoutsListWithTwo;
            }

            FancyZonesEditorHelper.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));
        }

        // launch FancyZones settings page
        private void LaunchFancyZones()
        {
            this.Find<NavigationViewItem>(By.AccessibilityId("WindowingAndLayoutsNavItem")).Click();

            this.Find<NavigationViewItem>(By.AccessibilityId("FancyZonesNavItem")).Click();
            this.Find<ToggleSwitch>(By.AccessibilityId("EnableFancyZonesToggleSwitch")).Toggle(true);

            this.Session.SetMainWindowSize(WindowSize.Large);
            Find<Element>(By.AccessibilityId("HeaderPresenter")).Click();
            this.Scroll(6, "Down"); // Pull the settings page up to make sure the settings are visible
            ZoneBehaviourSettings(TestContext.TestName);

            // Go back and forth to make sure settings applied
            this.Find<NavigationViewItem>("Workspaces").Click();
            Task.Delay(200).Wait();
            this.Find<NavigationViewItem>("FancyZones").Click();

            this.Find<Microsoft.PowerToys.UITest.Button>(By.AccessibilityId("LaunchLayoutEditorButton")).Click(false, 500, 10000);
            this.Session.Attach(PowerToysModule.FancyZone);

            // pipeline machine may have an unstable delays, causing the custom layout to be unavailable as we set. then A retry is required.
            // Console.WriteLine($"after launch, Custom layout data: {customLayoutData}");
            try
            {
                this.Find<Microsoft.PowerToys.UITest.Button>("Maximize").Click();

                // Set the FancyZones layout to a custom layout
                this.Find<Element>(By.Name("Custom Column")).Click();
            }
            catch (Exception)
            {
                // Console.WriteLine($"[Exception] Failed to attach to FancyZones window. Retrying...{ex.Message}");
                this.Find<Microsoft.PowerToys.UITest.Button>("Close").Click();
                this.Session.Attach(PowerToysModule.PowerToysSettings);
                SetupCustomLayouts();
                this.Find<Microsoft.PowerToys.UITest.Button>(By.AccessibilityId("LaunchLayoutEditorButton")).Click(false, 5000, 5000);
                this.Session.Attach(PowerToysModule.FancyZone);
                this.Find<Microsoft.PowerToys.UITest.Button>("Maximize").Click();

                // customLayoutData = FancyZonesEditorHelper.Files.CustomLayoutsIOHelper.GetData();
                // Console.WriteLine($"after retry, Custom layout data: {customLayoutData}");

                // Set the FancyZones layout to a custom layout
                this.Find<Element>(By.Name("Custom Column")).Click();
            }

            // Get screen margins for positioning checks
            GetScreenMargins();

            // Close layout editor window
            SendKeys(Key.Alt, Key.F4);

            // make window small to detect zone easily
            Session.Attach(powertoysWindowName, WindowSize.Small);
        }

        // Get the screen margins to calculate the dragged window position
        private void GetScreenMargins()
        {
            var rect = Session.GetMainWindowRect();
            screenMarginTop = rect.Top;
            screenMarginLeft = rect.Left;
            screenMarginRight = rect.Right;
            screenMarginBottom = rect.Bottom;
            (quarterX, quarterY) = ZoneSwitchHelper.GetScreenMargins(rect, 4);
        }

        // Get the mouse color of the pixel when make dragged window
        private (string PixelInWindow, string TransPixel) GetPixelWhenMakeDraggedWindow()
        {
            var windowRect = Session.GetMainWindowRect();
            int startX = windowRect.Left + 70;
            int startY = windowRect.Top + 25;
            int endX = startX + 100;
            int endY = startY + 100;

            Session.MoveMouseTo(startX, startY);

            // Session.PerformMouseAction(MouseActionType.LeftDoubleClick);
            Session.PressKey(Key.Shift);
            Session.PerformMouseAction(MouseActionType.LeftDown);
            Session.MoveMouseTo(endX, endY);

            Tuple<int, int> pos = GetMousePosition();
            string pixelInWindow = this.GetPixelColorString(pos.Item1, pos.Item2);
            Session.ReleaseKey(Key.Shift);
            Task.Delay(1000).Wait();
            string transPixel = this.GetPixelColorString(pos.Item1, pos.Item2);

            Session.PerformMouseAction(MouseActionType.LeftUp);
            return (pixelInWindow, transPixel);
        }

        /// <summary>
        /// Gets the color of a pixel located just outside the application's window.
        /// </summary>
        /// <param name="spacing">
        /// The minimum spacing (in pixels) required between the window edge and screen margin
        /// to determine a safe pixel sampling area outside the window.
        /// </param>
        /// <returns>
        /// A string representing the color of the pixel at the computed location outside the window,
        /// </returns>
        private string GetOutWindowPixelColor(int spacing)
        {
            var rect = Session.GetMainWindowRect();
            int checkX, checkY;

            if ((rect.Top - screenMarginTop) >= spacing)
            {
                checkX = rect.Left;
                checkY = screenMarginTop + (spacing / 2);
            }
            else if ((screenMarginBottom - rect.Bottom) >= spacing)
            {
                checkX = rect.Left;
                checkY = rect.Bottom + (spacing / 2);
            }
            else if ((rect.Left - screenMarginLeft) >= spacing)
            {
                checkX = rect.Left - (spacing / 2);
                checkY = rect.Top;
            }
            else if ((screenMarginRight - rect.Right) >= spacing)
            {
                checkX = rect.Right + (spacing / 2);
                checkY = rect.Top;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(spacing), "No sufficient margin to sample outside the window.");
            }

            Task.Delay(1000).Wait(); // Optional: Wait for a moment to ensure the mouse is in position
            string zoneColor = this.GetPixelColorString(checkX, checkY);
            return zoneColor;
        }

        /// <summary>
        /// Runs drag interactions during a FancyZones test and returns the initial and final zone highlight colors.
        /// </summary>
        /// <param name="preAction">An optional action to execute before the drag starts (e.g., setup or key press).</param>
        /// <param name="postAction">An optional action to execute after the drag is initiated but before it's released.</param>
        /// <param name="releaseAction">An optional action to execute when releasing the dragged window (e.g., mouse up).</param>
        /// <param name="testCaseName">The name of the test case for logging or diagnostics.</param>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///   <item><description><c>InitialZoneColor</c>: The zone highlight color before interaction completes.</description></item>
        ///   <item><description><c>FinalZoneColor</c>: The zone highlight color after interaction completes.</description></item>
        /// </list>
        /// </returns>
        private (string InitialZoneColor, string FinalZoneColor) RunDragInteractions(
        Action? preAction,
        Action? postAction,
        Action? releaseAction,
        string testCaseName)
        {
            // Invoke the pre-action
            preAction?.Invoke();

            // Capture initial window state and zone color
            var initialWindowRect = Session.GetMainWindowRect();
            string initialZoneColor = GetOutWindowPixelColor(30);

            // Invoke the post-action
            postAction?.Invoke();

            // Capture final zone color after the interaction
            string finalZoneColor = GetOutWindowPixelColor(30);

            releaseAction?.Invoke();

            // Return initial and final zone colors
            return (initialZoneColor, finalZoneColor);
        }

        // set the custom layout
        private static readonly CustomLayouts.CustomLayoutListWrapper CustomLayoutsList = new CustomLayouts.CustomLayoutListWrapper
        {
            CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper>
            {
                new CustomLayouts.CustomLayoutWrapper
                {
                    Uuid = "{63F09977-D327-4DAC-98F4-0C886CAE9517}",
                    Type = CustomLayout.Grid.TypeToString(),
                    Name = "Custom Column",
                    Info = new CustomLayouts().ToJsonElement(new CustomLayouts.GridInfoWrapper
                    {
                        Rows = 1,
                        Columns = 1,
                        RowsPercentage = new List<int> { 10000 },
                        ColumnsPercentage = new List<int> { 10000 },
                        CellChildMap = new int[][] { [0] },
                        SensitivityRadius = 20,
                        ShowSpacing = true,
                        Spacing = 10, // set spacing to 0 make sure the zone is full of the screen
                    }),
                },
            },
        };

        // set the custom layout with 1 subzones
        private static readonly CustomLayouts.CustomLayoutListWrapper CustomLayoutsListWithTwo = new CustomLayouts.CustomLayoutListWrapper
        {
            CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper>
            {
                new CustomLayouts.CustomLayoutWrapper
                {
                    Uuid = "{63F09977-D327-4DAC-98F4-0C886CAE9517}",
                    Type = CustomLayout.Grid.TypeToString(),
                    Name = "Custom Column",
                    Info = new CustomLayouts().ToJsonElement(new CustomLayouts.GridInfoWrapper
                    {
                        Rows = 1,
                        Columns = 2,
                        RowsPercentage = new List<int> { 10000 },
                        ColumnsPercentage = new List<int> { 5000, 5000 },
                        CellChildMap = new int[][] { [0, 1] },
                        SensitivityRadius = 20,
                        ShowSpacing = true,
                        Spacing = 10,
                    }),
                },
            },
        };

        private string GetZoneColor(string color)
        {
            // Click on the "Highlight color" group
            Find<Group>(color).Click();

            // Optional: Ensure the hex textbox is found (to wait until the UI loads)
            var hexBox = Find<Element>(By.AccessibilityId("HexTextBox"));
            Task.Delay(500).Wait(); // Optional: Wait for the UI to update

            // Get and return the RGB hex value text
            var hexColorElement = Find<Element>("RGB hex");

            // return mouse to color set position
            Find<Group>(color).Click();

            return hexColorElement.Text;
        }

        // set the zone behaviour settings
        private void ZoneBehaviourSettings(string? testName)
        {
            // test settings
            Microsoft.PowerToys.UITest.CheckBox useShiftCheckBox = this.Find<Microsoft.PowerToys.UITest.CheckBox>("Hold Shift key to activate zones while dragging a window");
            Microsoft.PowerToys.UITest.CheckBox useNonPrimaryMouseCheckBox = this.Find<Microsoft.PowerToys.UITest.CheckBox>("Use a non-primary mouse button to toggle zone activation");
            Microsoft.PowerToys.UITest.CheckBox makeDraggedWindowTransparent = this.Find<Microsoft.PowerToys.UITest.CheckBox>("Make the dragged window transparent");

            Find<Microsoft.PowerToys.UITest.CheckBox>("Show zone number").SetCheck(false, 100);
            Find<Slider>("Opacity (%)").QuickSetValue(100); // make highlight color visible with opacity 100

            // Get the highlight and inactivate color from appearance settings
            Find<Microsoft.PowerToys.UITest.ComboBox>("Zone appearance").Click();
            Find<Element>("Custom colors").Click();

            // get the highlight (activated) and inactivate zone color
            highlightColor = GetZoneColor("Highlight color");
            inactivateColor = GetZoneColor("Inactive color");

            this.Scroll(2, "Down");
            makeDraggedWindowTransparent.SetCheck(false, 500); // set make dragged window transparent to false or will influence the color comparison
            this.Scroll(6, "Up");

            switch (testName)
            {
                case "TestShowZonesOnShiftDuringDrag":
                    useShiftCheckBox.SetCheck(true, 500);
                    useNonPrimaryMouseCheckBox.SetCheck(false, 500);
                    break;
                case "TestShowZonesOnDragDuringShift":
                    useShiftCheckBox.SetCheck(true, 500);
                    useNonPrimaryMouseCheckBox.SetCheck(false, 500);
                    break;
                case "TestToggleZonesWithNonPrimaryMouseClick":
                    useShiftCheckBox.SetCheck(false, 500);
                    useNonPrimaryMouseCheckBox.SetCheck(true, 500);
                    break;
                case "TestShowZonesWhenShiftAndMouseOff":
                    useShiftCheckBox.SetCheck(false, 500);
                    useNonPrimaryMouseCheckBox.SetCheck(false, 500);
                    break;
                case "TestShowZonesWhenShiftAndMouseOn":
                    useShiftCheckBox.SetCheck(true, 500);
                    useNonPrimaryMouseCheckBox.SetCheck(true, 500);
                    break;
                case "TestMakeDraggedWindowTransparentOff":
                    useShiftCheckBox.SetCheck(true, 500);
                    useNonPrimaryMouseCheckBox.SetCheck(false, 500);
                    break; // Added break to prevent fall-through
                case "TestMakeDraggedWindowTransparentOn":
                    useNonPrimaryMouseCheckBox.SetCheck(false, 500);
                    useShiftCheckBox.SetCheck(true, 500);
                    this.Scroll(5, "Down"); // Pull the settings page up to make sure the settings are visible
                    makeDraggedWindowTransparent.SetCheck(true, 500);
                    this.Scroll(5, "Up");
                    break; // Added break to prevent fall-through
                default:
                    throw new ArgumentException("Unsupported Test Case.", testName);
            }
        }
    }
}
