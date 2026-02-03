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
            this.InitializeComponent();
            this.UpdateTasklistButtons();
            this.Activated += (_, _) => this.UpdateTasklistButtons();
        }

        public void UpdateTasklistButtons()
        {
            // This move ensures the window spawns on the same monitor as the main window
            AppWindow.MoveInZOrderAtBottom();
            AppWindow.Move(App.MainWindow.AppWindow.Position);
            TasklistButton[] buttons = [];
            try
            {
                buttons = TasklistPositions.GetButtons();
            }
            catch
            {
            }

            if (buttons.Length == 0)
            {
                AppWindow.Hide();
                return;
            }

            double windowsLogoColumnWidth = this.WindowsLogoColumnWidth.Width.Value;
            double windowHeight = 58;
            double windowMargin = 8 * this.DPI;
            double windowWidth = windowsLogoColumnWidth;
            double xPosition = buttons[0].X - (windowsLogoColumnWidth * this.DPI);
            double yPosition = this.WorkArea.Bottom - (windowHeight * this.DPI);

            this.KeyHolder.Children.Clear();

            foreach (TasklistButton b in buttons)
            {
                TaskbarIndicator indicator = new()
                {
                    Label = b.Keynum >= 10 ? "0" : b.Keynum.ToString(CultureInfo.InvariantCulture),
                    Height = b.Height / this.DPI,
                    Width = b.Width / this.DPI,
                };

                windowWidth += indicator.Width;

                this.KeyHolder.Children.Add(indicator);

                double indicatorPos = (b.X - xPosition) / this.DPI;
                Canvas.SetLeft(indicator, indicatorPos - windowsLogoColumnWidth);
            }

            this.MoveAndResize(xPosition - windowMargin, yPosition, windowWidth + (2 * windowMargin), windowHeight);
            AppWindow.MoveInZOrderAtTop();
        }
    }
}
