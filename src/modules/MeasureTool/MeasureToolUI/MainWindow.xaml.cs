// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using WinUIEx;

namespace MeasureToolUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WindowEx
    {
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

#pragma warning disable SA1312 // Importing native function
#pragma warning disable SA1310
        private static readonly IntPtr HWND_TOPMOST = new System.IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;
#pragma warning restore SA1310
#pragma warning restore SA1312

        private PowerToys.MeasureToolCore.Core coreLogic = new PowerToys.MeasureToolCore.Core();

        public MainWindow()
        {
            InitializeComponent();

            RectInt32 rect;
            rect.Width = 216;
            rect.Height = 50;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
            var presenter = appWindow.Presenter as OverlappedPresenter;
            presenter.IsAlwaysOnTop = true;
            this.SetIsAlwaysOnTop(true);
            this.SetIsShownInSwitchers(false);
            this.SetIsResizable(false);
            this.SetWindowSize(rect.Width, rect.Height);
            this.SetIsMinimizable(false);
            this.SetIsMaximizable(false);
            IsTitleBarVisible = false;

            var cursorPosition = coreLogic.GetCursorPosition();
            var cursorPositionInt32 = new PointInt32(cursorPosition.X, cursorPosition.Y);
            DisplayArea displayArea = DisplayArea.GetFromPoint(cursorPositionInt32, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);

            appWindow.Move(new PointInt32(displayArea.WorkArea.X + (displayArea.WorkArea.Width / 2) - (rect.Width / 2), displayArea.WorkArea.Y + 12));
        }

        private void BoundsTool_Click(object sender, RoutedEventArgs e)
        {
            coreLogic.StartBoundsTool();
        }

        private void MeasureTool_Click(object sender, RoutedEventArgs e)
        {
            coreLogic.StartMeasureTool(true, true);
        }

        private void HorizontalMeasureTool_Click(object sender, RoutedEventArgs e)
        {
            coreLogic.StartMeasureTool(true, false);
        }

        private void VerticalMeasureTool_Click(object sender, RoutedEventArgs e)
        {
            coreLogic.StartMeasureTool(false, true);
        }

        private void ClosePanelTool_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
