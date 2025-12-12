// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using WinRT;

namespace OverlayTestingWinUI
{
    public sealed partial class MainWindow : Window
    {
        private readonly DispatcherQueueTimer _hideTimer;
        private readonly StringBuilder _builder = new StringBuilder();

        private readonly DispatcherQueueTimer _typingTimer;
        private readonly StringBuilder _typedWord = new StringBuilder();

        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc _proc;

        public MainWindow()
        {
            this.InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            AppWindow.TitleBar.PreferredHeightOption =
                Microsoft.UI.Windowing.TitleBarHeightOption.Collapsed;

            var dq = DispatcherQueue.GetForCurrentThread();

            _hideTimer = dq.CreateTimer();
            _hideTimer.Interval = TimeSpan.FromMilliseconds(1000);
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                OverlayPill.Opacity = 0;
            };

            _typingTimer = dq.CreateTimer();
            _typingTimer.Interval = TimeSpan.FromMilliseconds(1500);
            _typingTimer.Tick += (s, e) =>
            {
                _typingTimer.Stop();
                _typedWord.Clear();
                LettersPanel.Children.Clear();
                OverlayPill.Opacity = 0;
            };

            Activated += MainWindow_Activated;
            Closed += MainWindow_Closed;
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (_hookId != IntPtr.Zero)
            {
                return;
            }

            _proc = HookCallback;
            _hookId = SetHook(_proc);

            IntPtr hwnd = this.As<IWindowNative>().WindowHandle;
            int exStyle = GetWindowLong(hwnd, GwlExstyle);
            SetWindowLong(hwnd, GwlExstyle, exStyle | WsExTransparent | WsExToolwindow);
        }

        private void MainWindow_Closed(object sender, WindowEventArgs e)
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private void ShowKeystroke(string text)
        {
            LettersPanel.Children.Clear();
            LettersPanel.Children.Add(CreateLetterBox(text));

            OverlayPill.Opacity = 1;

            _hideTimer.Stop();
            _hideTimer.Start();
        }

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
                VirtualKey key = (VirtualKey)vkCode;

                bool isLetter = key >= VirtualKey.A && key <= VirtualKey.Z;
                bool hasModifiers = IsAnyModifierDown();

                if (isLetter && !hasModifiers)
                {
                    char c = (char)('a' + (key - VirtualKey.A));

                    DispatcherQueue.TryEnqueue(() => AddLetterBox(c));
                }
                else
                {
                    string text = FormatChord(vkCode);
                    DispatcherQueue.TryEnqueue(() => ShowKeystroke(text));
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private static bool IsAnyModifierDown()
        {
            return
                (GetKeyState((int)VirtualKey.Control) & 0x8000) != 0 ||
                (GetKeyState((int)VirtualKey.Menu) & 0x8000) != 0 ||
                (GetKeyState((int)VirtualKey.Shift) & 0x8000) != 0 ||
                (GetKeyState((int)VirtualKey.LeftWindows) & 0x8000) != 0 ||
                (GetKeyState((int)VirtualKey.RightWindows) & 0x8000) != 0;
        }

        private string FormatChord(int vkCode)
        {
            _builder.Clear();

            if ((GetKeyState((int)VirtualKey.Control) & 0x8000) != 0)
            {
                _builder.Append("Ctrl + ");
            }

            if ((GetKeyState((int)VirtualKey.Shift) & 0x8000) != 0)
            {
                _builder.Append("Shift + ");
            }

            if ((GetKeyState((int)VirtualKey.Menu) & 0x8000) != 0)
            {
                _builder.Append("Alt + ");
            }

            VirtualKey key = (VirtualKey)vkCode;

            if (key == VirtualKey.Control ||
                key == VirtualKey.Shift ||
                key == VirtualKey.Menu)
            {
                return _builder.ToString().TrimEnd(' ', '+');
            }

            _builder.Append(key.ToString());
            return _builder.ToString();
        }

        private void AddLetterBox(char c)
        {
            OverlayPill.Opacity = 1;

            _typedWord.Append(c);
            LettersPanel.Children.Add(CreateLetterBox(c.ToString()));

            _typingTimer.Stop();
            _typingTimer.Start();
        }

        private static Border CreateLetterBox(string text)
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 46, 52, 64)),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(2, 0, 2, 0),
                Padding = new Thickness(6, 2, 6, 2),
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = new SolidColorBrush(Colors.White),
                    FontSize = 18,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                }
            };
        }

        private const int GwlExstyle = -20;
        private const int WsExTransparent = 0x00000020;
        private const int WsExToolwindow = 0x00000080;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);
    }
}
