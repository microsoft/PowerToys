// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace OverlayTesting
{
    /// <summary>
    /// Main window that shows the keystroke overlay.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _hideTimer;
        private readonly StringBuilder _builder = new StringBuilder();
        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc _proc;

        public MainWindow()
        {
            InitializeComponent();

            _hideTimer = new DispatcherTimer();
            _hideTimer.Interval = TimeSpan.FromMilliseconds(1000);
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                OverlayPill.Opacity = 0;
            };

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Left = SystemParameters.WorkArea.Width - Width;
            Top = 0;

            _proc = HookCallback;
            _hookId = SetHook(_proc);

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GwlExstyle);
            SetWindowLong(hwnd, GwlExstyle, exStyle | WsExTransparent | WsExToolwindow);
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private void ShowKeystroke(string text)
        {
            OverlayText.Text = text;
            OverlayPill.Opacity = 1;

            _hideTimer.Stop();
            _hideTimer.Start();
        }

        // ===== keyboard hook =====
        private const int WhKeyboardLl = 13;
        private const int WmKeydown = 0x0100;
        private const int WmSyskeydown = 0x0104;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            Process curProcess = Process.GetCurrentProcess();
            ProcessModule curModule = curProcess.MainModule;
            return SetWindowsHookEx(WhKeyboardLl, proc, GetModuleHandle(curModule.ModuleName), 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 &&
                (wParam == (IntPtr)WmKeydown || wParam == (IntPtr)WmSyskeydown))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                string text = FormatChord(vkCode);

                Dispatcher.Invoke(new Action(() => ShowKeystroke(text)));
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private string FormatChord(int vkCode)
        {
            _builder.Clear();

            ModifierKeys mods = Keyboard.Modifiers;

            if ((mods & ModifierKeys.Control) == ModifierKeys.Control)
            {
                _builder.Append("Ctrl + ");
            }

            if ((mods & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                _builder.Append("Shift + ");
            }

            if ((mods & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                _builder.Append("Alt + ");
            }

            Key key = KeyInterop.KeyFromVirtualKey(vkCode);

            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftAlt || key == Key.RightAlt)
            {
                return _builder.ToString().TrimEnd(' ', '+');
            }

            _builder.Append(key.ToString());
            return _builder.ToString();
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int GwlExstyle = -20;
        private const int WsExTransparent = 0x00000020;
        private const int WsExToolwindow = 0x00000080;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
