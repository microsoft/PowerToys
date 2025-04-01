// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// <summary>
//     Keyboard/Mouse simulation.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Library;
using MouseWithoutBorders.Core;
using Windows.UI.Input.Preview.Injection;

using static MouseWithoutBorders.Class.NativeMethods;

[module: SuppressMessage("Microsoft.Portability", "CA1901:PInvokeDeclarationsShouldBePortable", Scope = "member", Target = "MouseWithoutBorders.InputSimulation.#keybd_event(System.Byte,System.Byte,System.UInt32,System.Int32)", MessageId = "3", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", Scope = "member", Target = "MouseWithoutBorders.InputSimulation.#InputProcessKeyEx(System.Int32,System.Int32,System.Boolean&)", MessageId = "MouseWithoutBorders.NativeMethods.LockWorkStation", Justification = "Dotnet port with style preservation")]

namespace MouseWithoutBorders.Class
{
    internal class InputSimulation
    {
        public static InputInjector Injector;

        private InputSimulation()
        {
        }

        internal static InjectedInputMouseInfo MouseInputToInjectedInputMouseInfo(MOUSEINPUT mouseInput)
        {
            var injectedInput = new InjectedInputMouseInfo();

            injectedInput.DeltaX = mouseInput.dx;
            injectedInput.DeltaY = mouseInput.dy;
            injectedInput.MouseData = (uint)mouseInput.mouseData;
            injectedInput.MouseOptions = (InjectedInputMouseOptions)mouseInput.dwFlags;
            injectedInput.TimeOffsetInMilliseconds = (uint)mouseInput.time;

            return injectedInput;
        }

        private static uint SendInputEx(NativeMethods.INPUT input)
        {
            Common.PaintCount = 0;

            uint rv = 0;
            if (Common.Is64bitOS)
            {
                NativeMethods.INPUT64 input64 = default;
                input64.type = input.type;

                // Keyboard
                if (input.type == 1)
                {
                    input64.ki.wVk = input.ki.wVk;
                    input64.ki.wScan = input.ki.wScan;
                    input64.ki.dwFlags = input.ki.dwFlags;
                    input64.ki.time = input.ki.time;
                    input64.ki.dwExtraInfo = input.ki.dwExtraInfo;
                }

                // Mouse
                else
                {
                    input64.mi.dx = input.mi.dx;
                    input64.mi.dy = input.mi.dy;
                    input64.mi.dwFlags = input.mi.dwFlags;
                    input64.mi.mouseData = input.mi.mouseData;
                    input64.mi.time = input.mi.time;
                    input64.mi.dwExtraInfo = input.mi.dwExtraInfo;
                }

                if (input.type == 0 && (input.mi.dwFlags & (int)NativeMethods.MOUSEEVENTF.MOVE) != 0 && NativeMethods.InjectMouseInputAvailable)
                {
                    Injector.InjectMouseInput(new[] { MouseInputToInjectedInputMouseInfo(input64.mi) });
                }
                else
                {
                    NativeMethods.INPUT64[] inputs = { input64 };
                    rv = NativeMethods.SendInput64(1, inputs, Marshal.SizeOf(input64));
                }
            }
            else
            {
                if (input.type == 0 && (input.mi.dwFlags & (int)NativeMethods.MOUSEEVENTF.MOVE) != 0 && NativeMethods.InjectMouseInputAvailable)
                {
                    Injector.InjectMouseInput(new[] { MouseInputToInjectedInputMouseInfo(input.mi) });
                }
                else
                {
                    NativeMethods.INPUT[] inputs = { input };
                    rv = NativeMethods.SendInput(1, inputs, Marshal.SizeOf(input));
                }
            }

            return rv;
        }

        internal static void SendKey(KEYBDDATA kd)
        {
            string log = string.Empty;
            NativeMethods.KEYEVENTF dwFlags = NativeMethods.KEYEVENTF.KEYDOWN;
            uint scanCode = 0;

            // http://msdn.microsoft.com/en-us/library/ms644967(VS.85).aspx
            if ((kd.dwFlags & (int)Common.LLKHF.UP) == (int)Common.LLKHF.UP)
            {
                dwFlags = NativeMethods.KEYEVENTF.KEYUP;
            }

            if ((kd.dwFlags & (int)Common.LLKHF.EXTENDED) == (int)Common.LLKHF.EXTENDED)
            {
                dwFlags |= NativeMethods.KEYEVENTF.EXTENDEDKEY;
            }

            scanCode = NativeMethods.MapVirtualKey((uint)kd.wVk, 0);

            InputProcessKeyEx(kd.wVk, kd.dwFlags, out bool eatKey);

            if (!eatKey)
            {
                InputHook.RealData = false;
#if !USING_keybd_event
                // #if USING_SendInput
                NativeMethods.INPUT structInput;
                structInput = default;
                structInput.type = 1;
                structInput.ki.wScan = (short)scanCode;
                structInput.ki.time = 0;
                structInput.ki.wVk = (short)kd.wVk;
                structInput.ki.dwFlags = (int)dwFlags;
                structInput.ki.dwExtraInfo = NativeMethods.GetMessageExtraInfo();

                Common.DoSomethingInTheInputSimulationThread(() =>
                {
                    // key press simulation
                    SendInputEx(structInput);
                });

#else
                keybd_event((byte)kd.wVk, (byte)scanCode, (UInt32)dwFlags, 0);
#endif
                InputHook.RealData = true;
            }

            log += "*"; // ((Keys)kd.wVk).ToString(CultureInfo.InvariantCulture);
            Logger.LogDebug(log);
        }

        // Md.X, Md.Y is from 0 to 65535
        internal static uint SendMouse(MOUSEDATA md)
        {
            uint rv = 0;
            NativeMethods.INPUT mouse_input = default;

            long w65535 = (MachineStuff.DesktopBounds.Right - MachineStuff.DesktopBounds.Left) * 65535 / Common.ScreenWidth;
            long h65535 = (MachineStuff.DesktopBounds.Bottom - MachineStuff.DesktopBounds.Top) * 65535 / Common.ScreenHeight;
            long l65535 = MachineStuff.DesktopBounds.Left * 65535 / Common.ScreenWidth;
            long t65535 = MachineStuff.DesktopBounds.Top * 65535 / Common.ScreenHeight;
            mouse_input.type = 0;
            long dx = (md.X * w65535 / 65535) + l65535;
            long dy = (md.Y * h65535 / 65535) + t65535;
            mouse_input.mi.dx = (int)dx;
            mouse_input.mi.dy = (int)dy;
            mouse_input.mi.mouseData = md.WheelDelta;

            if (md.dwFlags != Common.WM_MOUSEMOVE)
            {
                Logger.LogDebug($"InputSimulation.SendMouse: x = {md.X}, y = {md.Y}, WheelDelta = {md.WheelDelta}, dwFlags = {md.dwFlags}.");
            }

            switch (md.dwFlags)
            {
                case Common.WM_MOUSEMOVE:
                    mouse_input.mi.dwFlags |= (int)(NativeMethods.MOUSEEVENTF.MOVE | NativeMethods.MOUSEEVENTF.ABSOLUTE);
                    break;
                case Common.WM_LBUTTONDOWN:
                    mouse_input.mi.dwFlags |= (int)NativeMethods.MOUSEEVENTF.LEFTDOWN;
                    break;
                case Common.WM_LBUTTONUP:
                    mouse_input.mi.dwFlags |= (int)NativeMethods.MOUSEEVENTF.LEFTUP;
                    break;
                case Common.WM_RBUTTONDOWN:
                    mouse_input.mi.dwFlags |= (int)NativeMethods.MOUSEEVENTF.RIGHTDOWN;
                    break;
                case Common.WM_RBUTTONUP:
                    mouse_input.mi.dwFlags |= (int)NativeMethods.MOUSEEVENTF.RIGHTUP;
                    break;
                case Common.WM_MBUTTONDOWN:
                    mouse_input.mi.dwFlags |= (int)NativeMethods.MOUSEEVENTF.MIDDLEDOWN;
                    break;
                case Common.WM_MBUTTONUP:
                    mouse_input.mi.dwFlags |= (int)NativeMethods.MOUSEEVENTF.MIDDLEUP;
                    break;
                case Common.WM_MOUSEWHEEL:
                    mouse_input.mi.dwFlags |= (int)NativeMethods.MOUSEEVENTF.WHEEL;
                    break;
                case Common.WM_XBUTTONUP:
                    mouse_input.mi.dwFlags |= (int)NativeMethods.MOUSEEVENTF.XUP;
                    break;
                case Common.WM_XBUTTONDOWN:
                    mouse_input.mi.dwFlags |= (int)NativeMethods.MOUSEEVENTF.XDOWN;
                    break;

                default:
                    break;
            }

            Common.DoSomethingInTheInputSimulationThread(() =>
            {
                InputHook.RealData = false;
                rv = SendInputEx(mouse_input);
            });

            if (Common.MainFormVisible && !DragDrop.IsDropping)
            {
                Helper.MainFormDot();
            }

            return rv;
        }

        internal static void MoveMouseEx(int x, int y)
        {
            NativeMethods.INPUT mouse_input = default;

            long w65535 = (MachineStuff.DesktopBounds.Right - MachineStuff.DesktopBounds.Left) * 65535 / Common.ScreenWidth;
            long h65535 = (MachineStuff.DesktopBounds.Bottom - MachineStuff.DesktopBounds.Top) * 65535 / Common.ScreenHeight;
            long l65535 = MachineStuff.DesktopBounds.Left * 65535 / Common.ScreenWidth;
            long t65535 = MachineStuff.DesktopBounds.Top * 65535 / Common.ScreenHeight;
            mouse_input.type = 0;
            long dx = (x * w65535 / 65535) + l65535;
            long dy = (y * h65535 / 65535) + t65535;
            mouse_input.mi.dx = (int)dx;
            mouse_input.mi.dy = (int)dy;

            Logger.LogDebug($"InputSimulation.MoveMouseEx: x = {x}, y = {y}.");

            mouse_input.mi.dwFlags |= (int)(NativeMethods.MOUSEEVENTF.MOVE | NativeMethods.MOUSEEVENTF.ABSOLUTE);

            Common.DoSomethingInTheInputSimulationThread(() =>
            {
                InputHook.RealData = false;
                SendInputEx(mouse_input);
            });
        }

        // x, y is in pixel
        internal static void MoveMouse(int x, int y)
        {
            // Common.Log("Mouse move: " + x.ToString(CultureInfo.CurrentCulture) + "," + y.ToString(CultureInfo.CurrentCulture));
            NativeMethods.INPUT mouse_input = default;
            mouse_input.type = 0;
            mouse_input.mi.dx = x * 65535 / Common.ScreenWidth;
            mouse_input.mi.dy = y * 65535 / Common.ScreenHeight;
            mouse_input.mi.mouseData = 0;
            mouse_input.mi.dwFlags = (int)(NativeMethods.MOUSEEVENTF.MOVE | NativeMethods.MOUSEEVENTF.ABSOLUTE);

            Logger.LogDebug($"InputSimulation.MoveMouse: x = {x}, y = {y}.");

            Common.DoSomethingInTheInputSimulationThread(() =>
            {
                InputHook.RealData = false;
                SendInputEx(mouse_input);

                // NativeMethods.SetCursorPos(x, y);
            });
        }

        // dx, dy is in pixel, relative
        internal static void MoveMouseRelative(int dx, int dy)
        {
            NativeMethods.INPUT mouse_input = default;
            mouse_input.type = 0;
            mouse_input.mi.dx = dx; // *65535 / Common.ScreenWidth;
            mouse_input.mi.dy = dy; // *65535 / Common.ScreenHeight;
            mouse_input.mi.mouseData = 0;
            mouse_input.mi.dwFlags = (int)NativeMethods.MOUSEEVENTF.MOVE;

            Logger.LogDebug($"InputSimulation.MoveMouseRelative: x = {dx}, y = {dy}.");

            Common.DoSomethingInTheInputSimulationThread(() =>
            {
                InputHook.RealData = false;
                SendInputEx(mouse_input);

                // NativeMethods.SetCursorPos(x, y);
            });
        }

        internal static void MouseUp()
        {
            Common.DoSomethingInTheInputSimulationThread(() =>
            {
                NativeMethods.INPUT input = default;
                input.type = 0;
                input.mi.dx = 0;
                input.mi.dy = 0;
                input.mi.mouseData = 0;
                input.mi.dwFlags = (int)NativeMethods.MOUSEEVENTF.LEFTUP;

                InputHook.SkipMouseUpCount++;
                _ = SendInputEx(input);
                Logger.LogDebug("MouseUp() called");
            });
        }

        internal static void MouseClickDotForm(int x, int y)
        {
            _ = Task.Factory.StartNew(
                () =>
            {
                NativeMethods.INPUT input = default;
                input.type = 0;
                input.mi.dx = 0;
                input.mi.dy = 0;
                input.mi.mouseData = 0;

                InputHook.SkipMouseUpDown = true;

                try
                {
                    MoveMouse(x, y);

                    InputHook.RealData = false;
                    input.mi.dwFlags = (int)NativeMethods.MOUSEEVENTF.LEFTDOWN;
                    _ = SendInputEx(input);

                    InputHook.RealData = false;
                    input.mi.dwFlags = (int)NativeMethods.MOUSEEVENTF.LEFTUP;
                    _ = SendInputEx(input);

                    Logger.LogDebug("MouseClick() called");
                    Thread.Sleep(200);
                }
                finally
                {
                    InputHook.SkipMouseUpDown = false;
                }
            },
                System.Threading.CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.Default);
        }

        private static bool winDown;
        private static bool ctrlDown;
        private static bool altDown;
        private static bool shiftDown;
        internal static readonly string[] Args = new string[] { "CAD" };

        private static void ResetModifiersState(HotkeySettings matchingHotkey)
        {
            ctrlDown = ctrlDown && matchingHotkey.Ctrl;
            altDown = altDown && matchingHotkey.Alt;
            shiftDown = shiftDown && matchingHotkey.Shift;
            winDown = winDown && matchingHotkey.Win;
        }

        private static void InputProcessKeyEx(int vkCode, int flags, out bool eatKey)
        {
            eatKey = false;

            if ((flags & (int)Common.LLKHF.UP) == (int)Common.LLKHF.UP)
            {
                switch ((VK)vkCode)
                {
                    case VK.LWIN:
                    case VK.RWIN:
                        winDown = false;
                        break;

                    case VK.LCONTROL:
                    case VK.RCONTROL:
                        ctrlDown = false;
                        break;

                    case VK.LMENU:
                    case VK.RMENU:
                        altDown = false;
                        break;

                    case VK.LSHIFT:
                    case VK.RSHIFT:
                        shiftDown = false;
                        break;

                    default:
                        break;
                }
            }
            else
            {
                if (Common.HotkeyMatched(vkCode, winDown, ctrlDown, altDown, shiftDown, Setting.Values.HotKeyLockMachine))
                {
                    if (!Common.RunOnLogonDesktop
                        && !Common.RunOnScrSaverDesktop)
                    {
                        ResetModifiersState(Setting.Values.HotKeyLockMachine);
                        eatKey = true;
                        Common.ReleaseAllKeys();
                        _ = NativeMethods.LockWorkStation();
                    }
                }

                switch ((VK)vkCode)
                {
                    case VK.LWIN:
                    case VK.RWIN:
                        winDown = true;
                        break;

                    case VK.LCONTROL:
                    case VK.RCONTROL:
                        ctrlDown = true;
                        break;

                    case VK.LMENU:
                    case VK.RMENU:
                        altDown = true;
                        break;

                    case VK.LSHIFT:
                    case VK.RSHIFT:
                        shiftDown = true;
                        break;

                    case VK.DELETE:
                        if (ctrlDown && altDown)
                        {
                            ctrlDown = altDown = false;
                            eatKey = true;
                            Common.ReleaseAllKeys();
                        }

                        break;

                    case (VK)'L':
                        if (winDown)
                        {
                            winDown = false;
                            eatKey = true;
                            Common.ReleaseAllKeys();
                            uint rv = NativeMethods.LockWorkStation();
                            Logger.LogDebug("LockWorkStation returned " + rv.ToString(CultureInfo.CurrentCulture));
                        }

                        break;

                    case VK.END:
                        if (ctrlDown && altDown)
                        {
                            ctrlDown = altDown = false;
                            new ServiceController("MouseWithoutBordersSvc").Start(Args);
                        }

                        break;

                    default:
                        break;
                }
            }
        }

        internal static void ResetSystemKeyFlags()
        {
            ctrlDown = winDown = altDown = false;
        }
    }
}
