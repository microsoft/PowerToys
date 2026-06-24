// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;

using ManagedCommon;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using WorkspacesEditor.Helpers;

namespace WorkspacesEditor
{
    public sealed partial class MainWindow : Window, IDisposable
    {
        public const int MinWindowWidth = 750;
        public const int MinWindowHeight = 680;

        private readonly CancellationTokenSource _cancellationToken = new();
        private readonly AppWindow _appWindow;

        public MainWindow()
        {
            this.InitializeComponent();

            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            SetMinSize(hwnd, MinWindowWidth, MinWindowHeight);
            RestoreWindowState(hwnd);

            // Set title from resource or fallback
            try
            {
                this.Title = ResourceLoaderInstance.ResourceLoader?.GetString("MainTitle") ?? "Workspaces";
            }
            catch
            {
                this.Title = "Workspaces";
            }

            this.Closed += OnClosed;

            // Listen for hotkey toggle event
            StartHotkeyEventLoop(hwnd);

            // Wire ViewModel navigation
            var vm = App.MainViewModel;
            vm.NavigateAction = (pageType, param) =>
            {
                ContentFrame.Navigate(pageType, (vm, param));
            };
            vm.GoBackAction = () =>
            {
                if (ContentFrame.CanGoBack)
                {
                    ContentFrame.GoBack();
                }
            };
            vm.MinimizeMainWindowAction = () =>
            {
                ShowWindow(WindowNative.GetWindowHandle(this), 6); // SW_MINIMIZE
            };
            vm.RestoreMainWindowAction = () =>
            {
                ShowWindow(WindowNative.GetWindowHandle(this), 9); // SW_RESTORE
            };
            vm.ShowLoadingAction = () =>
            {
                LoadingRing.IsActive = true;
                LoadingRing.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            };
            vm.HideLoadingAction = () =>
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            };

            // Navigate to main page
            ContentFrame.Navigate(typeof(Views.MainPage), vm);
        }

        private void RestoreWindowState(IntPtr hwnd)
        {
            var state = WindowStateHelper.Load();

            if (state != null && state.IsValid())
            {
                // Use AppWindow for positioning — it handles DPI correctly for WinUI windows
                _appWindow.Move(new Windows.Graphics.PointInt32((int)state.Left, (int)state.Top));
                _appWindow.Resize(new Windows.Graphics.SizeInt32((int)state.Width, (int)state.Height));

                if (state.Maximized)
                {
                    ShowWindow(hwnd, 3); // SW_SHOWMAXIMIZED
                }
            }
            else
            {
                // First launch: center on current display at 90% height, 75% width
                var displayArea = DisplayArea.GetFromWindowId(
                    Win32Interop.GetWindowIdFromWindow(hwnd),
                    DisplayAreaFallback.Primary);
                var workArea = displayArea.WorkArea;

                int width = (int)(workArea.Width * 0.75);
                int height = (int)(workArea.Height * 0.90);
                int x = workArea.X + (int)(workArea.Width * 0.125);
                int y = workArea.Y + (int)(workArea.Height * 0.05);

                _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, height));
            }
        }

        private void StartHotkeyEventLoop(IntPtr hwnd)
        {
            var token = _cancellationToken.Token;
            new Thread(() =>
            {
                var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, PowerToys.Interop.Constants.WorkspacesHotkeyEvent());
                while (true)
                {
                    if (WaitHandle.WaitAny(new WaitHandle[] { token.WaitHandle, eventHandle }) == 1)
                    {
                        App.DispatcherQueue.TryEnqueue(() =>
                        {
                            if (ApplicationIsInFocus())
                            {
                                Environment.Exit(0);
                            }
                            else
                            {
                                WindowHelpers.BringToForeground(hwnd);
                            }
                        });
                    }
                    else
                    {
                        return;
                    }
                }
            }) { IsBackground = true }.Start();
        }

        private void SaveWindowState()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            bool isMaximized = IsWindowMaximized(hwnd);

            // Use AppWindow for both save and restore — same coordinate space, no DPI mismatch
            var pos = _appWindow.Position;
            var size = _appWindow.Size;
            WindowStateHelper.Save(new WindowStateData
            {
                Top = pos.Y,
                Left = pos.X,
                Width = size.Width,
                Height = size.Height,
                Maximized = isMaximized,
            });
        }

        private void OnClosed(object sender, WindowEventArgs args)
        {
            SaveWindowState();
            _cancellationToken.Dispose();
            (Application.Current as IDisposable)?.Dispose();
        }

        private static bool ApplicationIsInFocus()
        {
            var activatedHandle = GetForegroundWindow();
            if (activatedHandle == IntPtr.Zero)
            {
                return false;
            }

            var procId = Environment.ProcessId;
            _ = GetWindowThreadProcessId(activatedHandle, out int activeProcId);

            return activeProcId == procId;
        }

        private static void SetMinSize(IntPtr hwnd, int minWidth, int minHeight)
        {
            var subclassId = (nuint)1;
            SubclassProc callback = (hWnd, msg, wParam, lParam, id, data) =>
            {
                if (msg == WmGetminmaxinfo)
                {
                    var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                    mmi.PtMinTrackSize.X = minWidth;
                    mmi.PtMinTrackSize.Y = minHeight;
                    Marshal.StructureToPtr(mmi, lParam, false);
                }

                return DefSubclassProc(hWnd, msg, wParam, lParam);
            };

            // prevent GC of delegate
            _subclassCallback = callback;
            SetWindowSubclass(hwnd, callback, subclassId, 0);
        }

        private static SubclassProc _subclassCallback;

        private const uint WmGetminmaxinfo = 0x0024;

        private delegate IntPtr SubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, nuint id, nuint data);

        [DllImport("comctl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, nuint uIdSubclass, nuint dwRefData);

        [DllImport("comctl32.dll")]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT PtReserved;
            public POINT PtMaxSize;
            public POINT PtMaxPosition;
            public POINT PtMinTrackSize;
            public POINT PtMaxTrackSize;
        }

        public void Dispose()
        {
            _cancellationToken?.Dispose();
            GC.SuppressFinalize(this);
        }

        // Win32 interop
        private const int SwMaximize = 3;

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowPlacement(IntPtr hWnd, out WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        private static readonly IntPtr DpiAwarenessContextUnaware = new(-1);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        private static bool IsWindowMaximized(IntPtr hwnd)
        {
            GetWindowPlacement(hwnd, out WINDOWPLACEMENT placement);
            return placement.ShowCmd == 3; // SW_SHOWMAXIMIZED
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            public uint Length;
            public uint Flags;
            public uint ShowCmd;
            public POINT PtMinPosition;
            public POINT PtMaxPosition;
            public RECT RcNormalPosition;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
