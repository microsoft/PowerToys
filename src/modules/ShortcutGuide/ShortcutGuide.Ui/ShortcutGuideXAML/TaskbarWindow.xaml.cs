// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using ManagedCommon;
using Microsoft.UI.Xaml.Controls;
using ShortcutGuide.Controls;
using ShortcutGuide.Helpers;
using Windows.Foundation;
using WinRT.Interop;
using WinUIEx;
using static ShortcutGuide.NativeMethods;

namespace ShortcutGuide.ShortcutGuideXAML
{
    public sealed partial class TaskbarWindow : WindowEx
    {
        private float DPI => DpiHelper.GetDPIScaleForWindow(WindowNative.GetWindowHandle(this));

        private Rect WorkArea => DisplayHelper.GetWorkAreaForDisplayWithWindow(WindowNative.GetWindowHandle(this));

        public TaskbarWindow()
        {
            this.InitializeComponent();
            this.UpdateTasklistButtons();
            this.Activated += (_, _) => this.UpdateTasklistButtons();
        }

        public void UpdateTasklistButtons()
        {
            // Wrap the entire body: this method runs from the ctor and from `Activated`,
            // both of which can fire while MainWindow is closing or AppWindow is in a
            // transient null state. An exception here used to crash the overlay because
            // there was no caller-side try/catch (issue #48441).
            try
            {
                // This move ensures the window spawns on the same monitor as the main window.
                // App.MainWindow / its AppWindow can briefly be null during the reentrant
                // Hide → Activate → BringToFront chain triggered from SelectionChanged.
                var mainAppWindow = App.MainWindow?.AppWindow;
                if (mainAppWindow is null)
                {
                    return;
                }

                AppWindow.MoveInZOrderAtBottom();
                AppWindow.Move(mainAppWindow.Position);
                TasklistButton[] buttons = [];
                try
                {
                    buttons = TasklistPositions.GetButtons();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to enumerate taskbar buttons via TasklistPositions.GetButtons.", ex);
                }

                if (buttons.Length == 0)
                {
                    AppWindow.Hide();
                    return;
                }

                float dpi = this.DPI;
                double windowsLogoColumnWidth = this.WindowsLogoColumnWidth.Width.Value;
                double windowHeight = 58;
                double windowMargin = 8 * dpi;
                double windowWidth = windowsLogoColumnWidth;
                double xPosition = buttons[0].X - (windowsLogoColumnWidth * dpi);
                double yPosition = this.WorkArea.Bottom - (windowHeight * dpi);

                this.KeyHolder.Children.Clear();

                foreach (TasklistButton b in buttons)
                {
                    TaskbarIndicator indicator = new()
                    {
                        Label = b.Keynum >= 10 ? "0" : b.Keynum.ToString(CultureInfo.InvariantCulture),
                        Height = b.Height / dpi,
                        Width = b.Width / dpi,
                    };

                    windowWidth += indicator.Width;

                    this.KeyHolder.Children.Add(indicator);

                    double indicatorPos = (b.X - xPosition) / dpi;
                    Canvas.SetLeft(indicator, indicatorPos - windowsLogoColumnWidth);
                }

                this.MoveAndResize(xPosition - windowMargin, yPosition, windowWidth + (2 * windowMargin), windowHeight);
                AppWindow.MoveInZOrderAtTop();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to update Shortcut Guide taskbar indicator window.", ex);
            }
        }
    }
}
