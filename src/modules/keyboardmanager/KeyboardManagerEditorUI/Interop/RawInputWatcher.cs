// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using ManagedCommon;

namespace KeyboardManagerEditorUI.Interop
{
    /// <summary>
    /// Watches raw keyboard input on a dedicated hidden-window thread so the auto-switch dialog can
    /// identify which physical keyboard the user is typing on (the same identity the engine sees).
    /// Fires <see cref="DetectedKeyboard"/> for each key-down; ignores injected input (hDevice == 0).
    /// The callback runs on the watcher thread — marshal to the UI thread before touching UI.
    /// </summary>
    internal sealed class RawInputWatcher : IDisposable
    {
        private const uint RidInput = 0x10000003;
        private const uint RimTypeKeyboard = 1;
        private const uint RidevInputSink = 0x00000100;
        private const uint WmInput = 0x00FF;
        private const uint WmQuit = 0x0012;
        private const int WmKeyDown = 0x0100;
        private const int WmSysKeyDown = 0x0104;

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputDevice
        {
            public ushort UsUsagePage;
            public ushort UsUsage;
            public uint DwFlags;
            public IntPtr HwndTarget;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WndClass
        {
            public uint Style;
            public IntPtr LpfnWndProc;
            public int CbClsExtra;
            public int CbWndExtra;
            public IntPtr HInstance;
            public IntPtr HIcon;
            public IntPtr HCursor;
            public IntPtr HbrBackground;
            public IntPtr LpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string LpszClassName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage
        {
            public IntPtr Hwnd;
            public uint Message;
            public IntPtr WParam;
            public IntPtr LParam;
            public uint Time;
            public int PtX;
            public int PtY;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ushort RegisterClassW(ref WndClass lpWndClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowExW(uint exStyle, string className, string windowName, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DefWindowProcW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterClassW(string className, IntPtr instance);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetMessageW(out NativeMessage msg, IntPtr hwnd, uint filterMin, uint filterMax);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TranslateMessage(ref NativeMessage msg);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DispatchMessageW(ref NativeMessage msg);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostThreadMessageW(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterRawInputDevices([In] RawInputDevice[] devices, uint count, uint size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint command, IntPtr data, ref uint size, uint headerSize);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandleW(string? name);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private readonly Action<DetectedKeyboard> _onKeyboard;
        private readonly WndProcDelegate _wndProc;
        private Thread? _thread;
        private uint _threadId;

        public RawInputWatcher(Action<DetectedKeyboard> onKeyboard)
        {
            _onKeyboard = onKeyboard;
            _wndProc = WndProc; // hold a reference so the delegate isn't collected
        }

        public void Start()
        {
            if (_thread != null)
            {
                return;
            }

            _thread = new Thread(ThreadMain) { IsBackground = true, Name = "KbmRawInputWatcher" };
            _thread.Start();
        }

        public void Stop()
        {
            Thread? thread = _thread;
            if (thread == null)
            {
                return;
            }

            _thread = null;
            if (_threadId != 0)
            {
                PostThreadMessageW(_threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
            }

            thread.Join();
            _threadId = 0;
        }

        public void Dispose() => Stop();

        private void ThreadMain()
        {
            _threadId = GetCurrentThreadId();
            const string className = "KbmEditorRawInputWatcher";

            var wc = new WndClass
            {
                LpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                HInstance = GetModuleHandleW(null),
                LpszClassName = className,
            };
            RegisterClassW(ref wc);

            IntPtr hwnd = CreateWindowExW(0, className, string.Empty, 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, wc.HInstance, IntPtr.Zero);
            if (hwnd == IntPtr.Zero)
            {
                Logger.LogError("RawInputWatcher: CreateWindow failed");
                return;
            }

            var devices = new[]
            {
                new RawInputDevice { UsUsagePage = 0x01, UsUsage = 0x06, DwFlags = RidevInputSink, HwndTarget = hwnd },
            };
            if (!RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<RawInputDevice>()))
            {
                Logger.LogError("RawInputWatcher: RegisterRawInputDevices failed");
                DestroyWindow(hwnd);
                UnregisterClassW(className, wc.HInstance);
                return;
            }

            while (GetMessageW(out NativeMessage msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessageW(ref msg);
            }

            DestroyWindow(hwnd);
            UnregisterClassW(className, wc.HInstance);
        }

        private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WmInput)
            {
                HandleRawInput(lParam);
            }

            return DefWindowProcW(hwnd, msg, wParam, lParam);
        }

        private void HandleRawInput(IntPtr hRawInput)
        {
            uint headerSize = (uint)((2 * sizeof(uint)) + (2 * IntPtr.Size)); // RAWINPUTHEADER
            uint size = 0;
            if (GetRawInputData(hRawInput, RidInput, IntPtr.Zero, ref size, headerSize) != 0 || size == 0)
            {
                return;
            }

            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(hRawInput, RidInput, buffer, ref size, headerSize) != size)
                {
                    return;
                }

                if ((uint)Marshal.ReadInt32(buffer, 0) != RimTypeKeyboard)
                {
                    return;
                }

                IntPtr hDevice = Marshal.ReadIntPtr(buffer, 2 * sizeof(uint));
                if (hDevice == IntPtr.Zero)
                {
                    return; // injected input (e.g. a remap's own output)
                }

                // RAWKEYBOARD.Message sits 8 bytes into the keyboard payload (after MakeCode/Flags/
                // Reserved/VKey), which starts right after the header.
                int message = Marshal.ReadInt32(buffer, (int)headerSize + 8);
                if (message != WmKeyDown && message != WmSysKeyDown)
                {
                    return;
                }

                DetectedKeyboard? keyboard = RawInputDeviceEnumerator.DescribeDevice(hDevice);
                if (keyboard != null)
                {
                    _onKeyboard(keyboard);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
