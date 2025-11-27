// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Dispatching;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ScreencastModeUI
{
    public sealed partial class MainWindow : Window
    {
        private readonly DispatcherQueueTimer _hideTimer;
        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc? _proc;
        private readonly StringBuilder _builder = new();

        public MainWindow()
        {
            this.InitializeComponent();

            // Timer to hide overlay after 1000 ms (later: bind to Settings.DisplayTime)
            _hideTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _hideTimer.Interval = TimeSpan.FromMilliseconds(1000);
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                OverlayPill.Opacity = 0;
            };

            this.Closed += MainWindow_Closed;

            // Install keyboard hook after the window is created
            _proc = HookCallback;
            _hookId = SetHook(_proc);
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Show a formatted keystroke string in the overlay.
        /// </summary>
        public void ShowKeystroke(string text)
        {
            OverlayText.Text = text;
            OverlayPill.Opacity = 1;

            _hideTimer.Stop();
            _hideTimer.Start();
        }

        #region Keyboard hook (Win32)

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using Process curProcess = Process.GetCurrentProcess();
            using ProcessModule curModule = curProcess.MainModule!;
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                GetModuleHandle(curModule.ModuleName), 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 &&
                (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                string text = FormatChord(vkCode);

                // Use DispatcherQueue to update UI thread
                DispatcherQueue.TryEnqueue(() => ShowKeystroke(text));
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private string FormatChord(int vkCode)
        {
            _builder.Clear();

            // Use GetKeyState to compute modifiers (Win32, since we don't have WPF Keyboard)
            if (IsKeyDown(VK_CONTROL))
                _builder.Append("Ctrl + ");
            if (IsKeyDown(VK_SHIFT))
                _builder.Append("Shift + ");
            if (IsKeyDown(VK_MENU))
                _builder.Append("Alt + ");

            // Key name from virtual key code
            string keyName = GetKeyName(vkCode);
            _builder.Append(keyName);

            return _builder.ToString();
        }

        private static bool IsKeyDown(int vk)
            => (GetKeyState(vk) & 0x8000) != 0;

        private static string GetKeyName(int vkCode)
        {
            uint scanCode = MapVirtualKey((uint)vkCode, 0);
            StringBuilder sb = new(32);
            int result = GetKeyNameText((int)(scanCode << 16), sb, sb.Capacity);
            return result > 0 ? sb.ToString() : ((ConsoleKey)vkCode).ToString();
        }

        private const int VK_CONTROL = 0x11;
        private const int VK_SHIFT = 0x10;
        private const int VK_MENU = 0x12;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetKeyNameText(int lParam,
            [Out] StringBuilder lpString, int nSize);

        #endregion
    }
}

