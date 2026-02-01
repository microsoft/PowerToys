// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
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
        private float DPI => DpiHelper.GetDPIScaleForWindow(WindowNative.GetWindowHandle(this).ToInt32());

        private Rect WorkArea => DisplayHelper.GetWorkAreaForDisplayWithWindow(WindowNative.GetWindowHandle(this));

        public TaskbarWindow()
        {
            InitializeComponent();
            UpdateTasklistButtons();
            this.Activated += (_, _) => UpdateTasklistButtons();
        }

        public void UpdateTasklistButtons()
        {
            // This move ensures the window spawns on the same monitor as the main window
            AppWindow.MoveInZOrderAtBottom();
            AppWindow.Move(App.MainWindow.AppWindow.Position);

            TasklistButton[] buttons = TasklistPositions.GetButtons();
            if (buttons.Length == 0)
            {
                AppWindow.Hide();
                return;
            }

            double windowsLogoColumnWidth = WindowsLogoColumnWidth.Width.Value;
            double windowHeight = 58;
            double windowMargin = 8 * DPI;
            double windowWidth = windowsLogoColumnWidth;
            double xPosition = buttons[0].X - (windowsLogoColumnWidth * DPI);
            double yPosition = WorkArea.Bottom - (windowHeight * DPI);

            KeyHolder.Children.Clear();

            foreach (TasklistButton b in buttons)
            {
                TaskbarIndicator indicator = new()
                {
                    Label = b.Keynum >= 10 ? "0" : b.Keynum.ToString(CultureInfo.InvariantCulture),
                    Height = b.Height / DPI,
                    Width = b.Width / DPI,
                };

                windowWidth += indicator.Width;

                KeyHolder.Children.Add(indicator);

                double indicatorPos = (b.X - xPosition) / DPI;
                Canvas.SetLeft(indicator, indicatorPos - windowsLogoColumnWidth);
            }

            this.MoveAndResize(xPosition - windowMargin, yPosition, windowWidth + (2 * windowMargin), windowHeight);
            AppWindow.MoveInZOrderAtTop();
        }
    }
}
