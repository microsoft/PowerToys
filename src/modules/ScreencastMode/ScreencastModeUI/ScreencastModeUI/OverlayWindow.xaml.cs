// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace ScreencastModeUI
{
    public sealed partial class OverlayWindow : Window
    {
        private ObservableCollection<KeystrokeDisplay> _keystrokes;
        private DispatcherTimer _cleanupTimer;
        public OverlayWindow()
        {
            InitializeComponent();
            _keystrokes = new ObservableCollection<KeystrokeDisplay>();
            KeystrokeList.ItemsSource = _keystrokes;
            MakeWindowTransparent();
            _cleanupTimer = new DispatcherTimer();
            _cleanupTimer.Interval = TimeSpan.FromMilliseconds(100);
            _cleanupTimer.Tick += CleanupExpiredKeystrokes;
            _cleanupTimer.Start();
        }
        private void MakeWindowTransparent()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var screenWidth = GetSystemMetrics(SM_CXSCREEN);
            var screenHeight = GetSystemMetrics(SM_CYSCREEN);
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, screenWidth, screenHeight, SWP_SHOWWINDOW);
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE,
                exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW);
        }
        public void AddKeystroke(string text)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _keystrokes.Add(new KeystrokeDisplay
                {
                    Text = text,
                    Timestamp = DateTime.Now
                });
                while (_keystrokes.Count > 5)
                {
                    _keystrokes.RemoveAt(0);
                }
            });
        }
        private void CleanupExpiredKeystrokes(object sender, object e)
        {
            var now = DateTime.Now;
            for (int i = _keystrokes.Count - 1; i >= 0; i--)
            {
                var keystroke = _keystrokes[i];
                var elapsed = (now - keystroke.Timestamp).TotalMilliseconds;
                if (elapsed >= keystroke.DisplayTimeMs)
                {
                    _keystrokes.RemoveAt(i);
                }
            }
        }
        #region Win32 API
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        #endregion
    }
}