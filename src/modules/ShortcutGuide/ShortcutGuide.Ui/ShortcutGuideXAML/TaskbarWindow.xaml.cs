// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using ShortcutGuide.Helpers;
using Windows.Foundation;
using WinUIEx;
using static ShortcutGuide.NativeMethods;

namespace ShortcutGuide.ShortcutGuideXAML
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TaskbarWindow : WindowEx
    {
        private float DPI => DpiHelper.GetDPIScaleForWindow(MainWindow.WindowHwnd.ToInt32());

        private Rect WorkArea => DisplayHelper.GetWorkAreaForDisplayWithWindow(MainWindow.WindowHwnd);

        public TaskbarWindow()
        {
            InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
        }

        private void Grid_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            TasklistButton[] buttons = TasklistPositions.GetButtons();
            double prefixWidth = PrefixColumn.Width.Value;
            double windowHeight = 96;
            double windowMargin = 8;
            double windowWidth = prefixWidth;
            double xPosition = (buttons[0].X - WorkArea.Left - prefixWidth) / DPI;
            double yPosition = WorkArea.Bottom - windowHeight;

            foreach (TasklistButton b in buttons)
            {
                TaskbarIndicator indicator = new()
                {
                    Label = b.Keynum.ToString(CultureInfo.InvariantCulture),
                    Height = b.Height / DPI,
                    Width = b.Width / DPI,
                };

                windowWidth = windowWidth + (b.Width / DPI);

                KeyHolder.Children.Add(indicator);

                Canvas.SetLeft(indicator, (b.X - xPosition - prefixWidth) / DPI);
            }

            this.MoveAndResize(xPosition - windowMargin, yPosition / DPI, windowWidth + windowMargin, windowHeight / DPI);
        }

        /*
         public TaskbarWindow()
        {
            InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            var hwnd = this.GetWindowHandle();
            HwndExtensions.ToggleWindowStyle(hwnd, false, WindowStyle.TiledWindow);
        }

        private void Grid_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            TasklistButton[] buttons = TasklistPositions.GetButtons();
            double heightPos = 48 * DPI;
            double widthPos = 0;
            double xPos = 0;
            double yPos = WorkArea.Bottom - heightPos;

            for (int i = 0; i < buttons.Length; i++)
            {
                if (i < buttons.Length)
                {
                    TasklistButton b = buttons[i];
                    TaskbarIndicator indicator = new TaskbarIndicator()
                    {
                        Label = b.Keynum.ToString(CultureInfo.InvariantCulture),
                        Height = b.Height / DPI,
                        Width = b.Width / DPI,
                    };

                    KeyHolder.Children.Add(indicator);

                    if (i == 0)
                    {
                        xPos = (b.X - WorkArea.Left) / DPI;
                    }

                    widthPos = WorkArea.Width;

                    Canvas.SetLeft(indicator, (b.X - WorkArea.Left) / DPI);
                    continue;
                }
            }

            this.MoveAndResize(0, yPos / DPI, WorkArea.Width / DPI, heightPos / DPI);
        }
        */
    }
}
