// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// <summary>
//     Keyboard/Mouse hook callbacks, pre-process before calling to routines in Common.Event.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using Microsoft.PowerToys.Settings.UI.Library;
using MouseWithoutBorders.Core;

[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.InputHook.#MouseHookProc(System.Int32,System.Int32,System.IntPtr)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Scope = "member", Target = "MouseWithoutBorders.InputHook.#ProcessKeyEx(System.Int32,System.Int32)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions", Scope = "member", Target = "MouseWithoutBorders.InputHook.#Start()", Justification = "Dotnet port with style preservation")]

namespace MouseWithoutBorders.Class
{
    internal class InputHook
    {
        internal delegate void MouseEvHandler(MOUSEDATA e, int dx, int dy);

        internal delegate void KeybdEvHandler(KEYBDDATA e);

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseHookStruct
        {
            internal NativeMethods.POINT Pt;
            internal int Hwnd;
            internal int WHitTestCode;
            internal int DwExtraInfo;
        }

        // http://msdn.microsoft.com/en-us/library/ms644970(VS.85).aspx
        [StructLayout(LayoutKind.Sequential)]
        private struct MouseLLHookStruct
        {
            internal NativeMethods.POINT Pt;
            internal int MouseData;
            internal int Flags;
            internal int Time;
            internal int DwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardHookStruct
        {
            internal int VkCode;
            internal int ScanCode;
            internal int Flags;
            internal int Time;
            internal int DwExtraInfo;
        }

        internal event MouseEvHandler MouseEvent;

        internal event KeybdEvHandler KeyboardEvent;

        private int hMouseHook;
        private int hKeyboardHook;
        private static NativeMethods.HookProc mouseHookProcedure;
        private static NativeMethods.HookProc keyboardHookProcedure;

        private static MouseLLHookStruct mouseHookStruct;
        private static KeyboardHookStruct keyboardHookStruct;
        private static MOUSEDATA hookCallbackMouseData;
        private static KEYBDDATA hookCallbackKeybdData;

        private static bool winDown;
        private static bool altDown;
        private static bool shiftDown;

        internal static bool RealData { get; set; } = true;

        internal static int SkipMouseUpCount { get; set; }

        internal static bool SkipMouseUpDown { get; set; }

        internal static bool CtrlDown { get; private set; }

        internal static bool EasyMouseKeyDown { get; set; }

        internal InputHook()
        {
            Start();
        }

        ~InputHook()
        {
            Stop();
        }

        internal void Start()
        {
            int le;
            bool er = false;

            // Install Mouse Hook
            mouseHookProcedure = new NativeMethods.HookProc(MouseHookProc);
            hMouseHook = NativeMethods.SetWindowsHookEx(
                Common.WH_MOUSE_LL,
                mouseHookProcedure,
                Marshal.GetHINSTANCE(
                    Assembly.GetExecutingAssembly().GetModules()[0]),
                0);

            if (hMouseHook == 0)
            {
                le = Marshal.GetLastWin32Error();
                Logger.Log("Error installing Mouse hook: " + le.ToString(CultureInfo.CurrentCulture));
                er = true;
                Stop();
            }

            // Install Keyboard Hook
            keyboardHookProcedure = new NativeMethods.HookProc(KeyboardHookProc);
            hKeyboardHook = NativeMethods.SetWindowsHookEx(
                Common.WH_KEYBOARD_LL,
                keyboardHookProcedure,
                Marshal.GetHINSTANCE(
                Assembly.GetExecutingAssembly().GetModules()[0]),
                0);
            if (hKeyboardHook == 0)
            {
                le = Marshal.GetLastWin32Error();
                Logger.Log("Error installing keyboard hook: " + le.ToString(CultureInfo.CurrentCulture));
                er = true;
                Stop();
            }

            if (er)
            {
                if (!Common.RunOnLogonDesktop && !Common.RunOnScrSaverDesktop)
                {
                    _ = MessageBox.Show(
                        "Error installing keyboard/Mouse hook!",
                        Application.ProductName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            else
            {
                Common.InitLastInputEventCount();
            }
        }

        internal void Stop()
        {
            if (hMouseHook != 0)
            {
                int retMouse = NativeMethods.UnhookWindowsHookEx(hMouseHook);
                hMouseHook = 0;
                if (retMouse == 0)
                {
                    int errorCode = Marshal.GetLastWin32Error();

                    // throw new Win32Exception(errorCode);
                    Logger.Log("Exception uninstalling Mouse hook, error code: " + errorCode.ToString(CultureInfo.CurrentCulture));
                }
            }

            if (hKeyboardHook != 0)
            {
                int retKeyboard = NativeMethods.UnhookWindowsHookEx(hKeyboardHook);
                hKeyboardHook = 0;
                if (retKeyboard == 0)
                {
                    int errorCode = Marshal.GetLastWin32Error();

                    // throw new Win32Exception(errorCode);
                    Logger.Log("Exception uninstalling keyboard hook, error code: " + errorCode.ToString(CultureInfo.CurrentCulture));
                }
            }
        }

        // Better performance, compared to Marshal.PtrToStructure.
        private static MouseLLHookStruct LParamToMouseLLHookStruct(IntPtr lParam)
        {
            unsafe
            {
                return *(MouseLLHookStruct*)lParam;
            }
        }

        private static KeyboardHookStruct LParamToKeyboardHookStruct(IntPtr lParam)
        {
            unsafe
            {
                return *(KeyboardHookStruct*)lParam;
            }
        }

        private int MouseHookProc(int nCode, int wParam, IntPtr lParam)
        {
            int rv = 1, dx = 0, dy = 0;
            bool local = false;
            Common.InputEventCount++;

            try
            {
                if (!RealData)
                {
                    RealData = true;

                    // Common.Log("MouseHookProc: Not real data!");
                    // return rv;
                    rv = NativeMethods.CallNextHookEx(hMouseHook, nCode, wParam, lParam);
                }
                else
                {
                    Common.RealInputEventCount++;

                    if (MachineStuff.NewDesMachineID == Common.MachineID || MachineStuff.NewDesMachineID == ID.ALL)
                    {
                        local = true;
                        if (Common.MainFormVisible && !DragDrop.IsDropping)
                        {
                            Common.MainFormDot();
                        }
                    }

                    if (nCode >= 0 && MouseEvent != null)
                    {
                        if (wParam == Common.WM_LBUTTONUP && SkipMouseUpCount > 0)
                        {
                            Logger.LogDebug($"{nameof(SkipMouseUpCount)}: {SkipMouseUpCount}.");
                            SkipMouseUpCount--;
                            rv = NativeMethods.CallNextHookEx(hMouseHook, nCode, wParam, lParam);
                            return rv;
                        }

                        if ((wParam == Common.WM_LBUTTONUP || wParam == Common.WM_LBUTTONDOWN) && SkipMouseUpDown)
                        {
                            rv = NativeMethods.CallNextHookEx(hMouseHook, nCode, wParam, lParam);
                            return rv;
                        }

                        mouseHookStruct = LParamToMouseLLHookStruct(lParam);
                        hookCallbackMouseData.dwFlags = wParam;

                        // Use WheelDelta to store XBUTTON1/XBUTTON2 data.
                        hookCallbackMouseData.WheelDelta = (short)((mouseHookStruct.MouseData >> 16) & 0xffff);

                        if (local)
                        {
                            hookCallbackMouseData.X = mouseHookStruct.Pt.x;
                            hookCallbackMouseData.Y = mouseHookStruct.Pt.y;

                            if (Setting.Values.DrawMouse && Common.MouseCursorForm != null)
                            {
                                CustomCursor.ShowFakeMouseCursor(int.MinValue, int.MinValue);
                            }
                        }
                        else
                        {
                            if (MachineStuff.SwitchLocation.Count > 0 && MachineStuff.NewDesMachineID != Common.MachineID && MachineStuff.NewDesMachineID != ID.ALL)
                            {
                                MachineStuff.SwitchLocation.Count--;

                                if (MachineStuff.SwitchLocation.X > Common.XY_BY_PIXEL - 100000 || MachineStuff.SwitchLocation.Y > Common.XY_BY_PIXEL - 100000)
                                {
                                    hookCallbackMouseData.X = MachineStuff.SwitchLocation.X - Common.XY_BY_PIXEL;
                                    hookCallbackMouseData.Y = MachineStuff.SwitchLocation.Y - Common.XY_BY_PIXEL;
                                }
                                else
                                {
                                    hookCallbackMouseData.X = (MachineStuff.SwitchLocation.X * Common.ScreenWidth / 65535) + MachineStuff.PrimaryScreenBounds.Left;
                                    hookCallbackMouseData.Y = (MachineStuff.SwitchLocation.Y * Common.ScreenHeight / 65535) + MachineStuff.PrimaryScreenBounds.Top;
                                }

                                Common.HideMouseCursor(false);
                            }
                            else
                            {
                                dx = mouseHookStruct.Pt.x - Common.LastPos.X;
                                dy = mouseHookStruct.Pt.y - Common.LastPos.Y;

                                hookCallbackMouseData.X += dx;
                                hookCallbackMouseData.Y += dy;

                                if (hookCallbackMouseData.X < MachineStuff.PrimaryScreenBounds.Left)
                                {
                                    hookCallbackMouseData.X = MachineStuff.PrimaryScreenBounds.Left - 1;
                                }
                                else if (hookCallbackMouseData.X > MachineStuff.PrimaryScreenBounds.Right)
                                {
                                    hookCallbackMouseData.X = MachineStuff.PrimaryScreenBounds.Right + 1;
                                }

                                if (hookCallbackMouseData.Y < MachineStuff.PrimaryScreenBounds.Top)
                                {
                                    hookCallbackMouseData.Y = MachineStuff.PrimaryScreenBounds.Top - 1;
                                }
                                else if (hookCallbackMouseData.Y > MachineStuff.PrimaryScreenBounds.Bottom)
                                {
                                    hookCallbackMouseData.Y = MachineStuff.PrimaryScreenBounds.Bottom + 1;
                                }

                                dx += dx < 0 ? -Common.MOVE_MOUSE_RELATIVE : Common.MOVE_MOUSE_RELATIVE;
                                dy += dy < 0 ? -Common.MOVE_MOUSE_RELATIVE : Common.MOVE_MOUSE_RELATIVE;
                            }
                        }

                        MouseEvent(hookCallbackMouseData, dx, dy);

                        DragDrop.DragDropStep01(wParam);
                        DragDrop.DragDropStep09(wParam);
                    }

                    if (local)
                    {
                        rv = NativeMethods.CallNextHookEx(hMouseHook, nCode, wParam, lParam);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
                rv = NativeMethods.CallNextHookEx(hMouseHook, nCode, wParam, lParam);
            }

            return rv;
        }

        private int KeyboardHookProc(int nCode, int wParam, IntPtr lParam)
        {
            Common.InputEventCount++;
            if (!RealData)
            {
                return NativeMethods.CallNextHookEx(hKeyboardHook, nCode, wParam, lParam);
            }

            Common.RealInputEventCount++;

            keyboardHookStruct = LParamToKeyboardHookStruct(lParam);
            hookCallbackKeybdData.dwFlags = keyboardHookStruct.Flags;
            hookCallbackKeybdData.wVk = (short)keyboardHookStruct.VkCode;

            if (nCode >= 0 && KeyboardEvent != null)
            {
                if (!ProcessKeyEx(keyboardHookStruct.VkCode, keyboardHookStruct.Flags, hookCallbackKeybdData))
                {
                    return 1;
                }

                KeyboardEvent(hookCallbackKeybdData);
            }

            if (Common.DesMachineID == ID.NONE || Common.DesMachineID == ID.ALL || Common.DesMachineID == Common.MachineID)
            {
                return NativeMethods.CallNextHookEx(hKeyboardHook, nCode, wParam, lParam);
            }
            else
            {
                return 1;
            }
        }

        private bool ProcessKeyEx(int vkCode, int flags, KEYBDDATA hookCallbackKeybdData)
        {
            if ((flags & (int)Common.LLKHF.UP) == (int)Common.LLKHF.UP)
            {
                EasyMouseKeyDown = false;

                switch ((VK)vkCode)
                {
                    case VK.LWIN:
                    case VK.RWIN:
                        winDown = false;
                        break;

                    case VK.LCONTROL:
                    case VK.RCONTROL:
                        CtrlDown = false;
                        break;

                    case VK.LMENU:
                    case VK.RMENU:
                        altDown = false;
                        break;

                    case VK.LSHIFT:
                        shiftDown = false;
                        break;

                    default:
                        break;
                }
            }
            else
            {
                UpdateEasyMouseKeyDown((VK)vkCode);

                switch ((VK)vkCode)
                {
                    case VK.LWIN:
                    case VK.RWIN:
                        winDown = true;
                        break;

                    case VK.LCONTROL:
                    case VK.RCONTROL:
                        CtrlDown = true;
                        break;

                    case VK.LMENU:
                    case VK.RMENU:
                        altDown = true;
                        break;

                    case VK.LSHIFT:
                        shiftDown = true;
                        break;

                    case VK.DELETE:
                        if (CtrlDown && altDown)
                        {
                            CtrlDown = altDown = false;
                            KeyboardEvent(hookCallbackKeybdData);

                            if (Common.DesMachineID != ID.ALL)
                            {
                                MachineStuff.SwitchToMachine(Common.MachineName.Trim());
                            }

                            /*
#if CUSTOMIZE_LOGON_SCREEN
                           Common.DoSomethingInUIThread(delegate()
                           {
                               Common.MainForm.LoadNewLogonBackground();
                           });
#endif
* */
                        }

                        break;

                    case VK.ESCAPE:
                        if (Common.IsTopMostMessageNotNull())
                        {
                            Common.HideTopMostMessage();
                        }

                        break;

                    default:
                        Logger.LogDebug("X");
                        return ProcessHotKeys(vkCode, hookCallbackKeybdData);
                }
            }

            return true;
        }

        private void UpdateEasyMouseKeyDown(VK vkCode)
        {
            EasyMouseOption easyMouseOption = (EasyMouseOption)Setting.Values.EasyMouse;

            EasyMouseKeyDown = (easyMouseOption == EasyMouseOption.Ctrl && (vkCode == VK.LCONTROL || vkCode == VK.RCONTROL))
                || (easyMouseOption == EasyMouseOption.Shift && (vkCode == VK.LSHIFT || vkCode == VK.RSHIFT));
        }

        private static long lastHotKeyLockMachine;

        private void ResetModifiersState(HotkeySettings matchingHotkey)
        {
            CtrlDown = CtrlDown && matchingHotkey.Ctrl;
            altDown = altDown && matchingHotkey.Alt;
            shiftDown = shiftDown && matchingHotkey.Shift;
            winDown = winDown && matchingHotkey.Win;
        }

        private List<short> GetVkCodesList(HotkeySettings hotkey)
        {
            var list = new List<short>();
            if (hotkey.Alt)
            {
                list.Add((short)VK.MENU);
            }

            if (hotkey.Shift)
            {
                list.Add((short)VK.SHIFT);
            }

            if (hotkey.Win)
            {
                list.Add((short)VK.LWIN);
            }

            if (hotkey.Ctrl)
            {
                list.Add((short)VK.CONTROL);
            }

            if (hotkey.Code != 0)
            {
                list.Add((short)hotkey.Code);
            }

            return list;
        }

        private bool ProcessHotKeys(int vkCode, KEYBDDATA hookCallbackKeybdData)
        {
            if (Common.HotkeyMatched(vkCode, winDown, CtrlDown, altDown, shiftDown, Setting.Values.HotKeySwitch2AllPC))
            {
                ResetLastSwitchKeys();
                MachineStuff.SwitchToMultipleMode(Common.DesMachineID != ID.ALL, true);
            }

            if (Common.HotkeyMatched(vkCode, winDown, CtrlDown, altDown, shiftDown, Setting.Values.HotKeyToggleEasyMouse))
            {
                if (!Common.RunOnLogonDesktop && !Common.RunOnScrSaverDesktop)
                {
                    EasyMouseOption easyMouseOption = (EasyMouseOption)Setting.Values.EasyMouse;

                    if (easyMouseOption is EasyMouseOption.Disable or EasyMouseOption.Enable)
                    {
                        Setting.Values.EasyMouse = (int)(easyMouseOption == EasyMouseOption.Disable ? EasyMouseOption.Enable : EasyMouseOption.Disable);

                        Common.ShowToolTip($"Easy Mouse has been toggled to [{(EasyMouseOption)Setting.Values.EasyMouse}] by a hotkey. You can change the hotkey in the Settings form.", 5000);
                        return false;
                    }
                }
            }
            else if (Common.HotkeyMatched(vkCode, winDown, CtrlDown, altDown, shiftDown, Setting.Values.HotKeyLockMachine))
            {
                if (!Common.RunOnLogonDesktop
                    && !Common.RunOnScrSaverDesktop)
                {
                    if (Common.GetTick() - lastHotKeyLockMachine < 500)
                    {
                        MachineStuff.SwitchToMultipleMode(true, true);

                        var codes = GetVkCodesList(Setting.Values.HotKeyLockMachine);

                        foreach (var code in codes)
                        {
                            hookCallbackKeybdData.wVk = code;
                            KeyboardEvent(hookCallbackKeybdData);
                        }

                        hookCallbackKeybdData.dwFlags |= (int)Common.LLKHF.UP;

                        foreach (var code in codes)
                        {
                            hookCallbackKeybdData.wVk = code;
                            KeyboardEvent(hookCallbackKeybdData);
                        }

                        MachineStuff.SwitchToMultipleMode(false, true);

                        _ = NativeMethods.LockWorkStation();
                    }
                    else
                    {
                        KeyboardEvent(hookCallbackKeybdData);
                    }

                    lastHotKeyLockMachine = Common.GetTick();

                    return false;
                }
            }
            else if (Common.HotkeyMatched(vkCode, winDown, CtrlDown, altDown, shiftDown, Setting.Values.HotKeyReconnect))
            {
                Common.ShowToolTip("Reconnecting...", 2000);
                Common.LastReconnectByHotKeyTime = Common.GetTick();
                Common.PleaseReopenSocket = Common.REOPEN_WHEN_HOTKEY;
                return false;
            }

            if (CtrlDown && altDown)
            {
                if (shiftDown && vkCode == Setting.Values.HotKeyExitMM &&
                    (Common.DesMachineID == Common.MachineID || Common.DesMachineID == ID.ALL))
                {
                    Common.DoSomethingInUIThread(() =>
                    {
                        Common.MainForm.NotifyIcon.Visible = false;

                        for (int i = 1; i < 10; i++)
                        {
                            Application.DoEvents();
                            Thread.Sleep(20);
                        }

                        Common.MainForm.Quit(false, false);
                    });
                }
                else if (shiftDown || winDown)
                {
                    // The following else cases should work if control and alt modifiers are pressed. The hotkeys should still be captured.
                    // But if any of the other 2 modifiers (shift or win) are pressed, they hotkeys should not be activated.
                    // Issue #26597
                    return true;
                }
                else if (vkCode == Setting.Values.HotKeySwitchMachine ||
                     vkCode == Setting.Values.HotKeySwitchMachine + 1 ||
                     vkCode == Setting.Values.HotKeySwitchMachine + 2 ||
                     vkCode == Setting.Values.HotKeySwitchMachine + 3)
                {
                    if (Switch2(vkCode - Setting.Values.HotKeySwitchMachine))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool Switch2(int index)
        {
            if (MachineStuff.MachineMatrix != null && MachineStuff.MachineMatrix.Length > index)
            {
                string mcName = MachineStuff.MachineMatrix[index].Trim();
                if (!string.IsNullOrEmpty(mcName))
                {
                    // Common.DoSomethingInUIThread(delegate()
                    {
                        Common.ReleaseAllKeys();
                    }

                    // );
                    MachineStuff.SwitchToMachine(mcName);

                    if (!Common.RunOnLogonDesktop && !Common.RunOnScrSaverDesktop)
                    {
                        Common.ShowToolTip(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                "Control has been switched to {0} by the hotkey Ctrl+Alt+{1}{2}",
                                mcName,
                                Setting.Values.HotKeySwitchMachine == (int)VK.F1 ? "F" : string.Empty,
                                index + 1),
                            3000);
                    }

                    return true;
                }
            }

            return false;
        }

        internal void ResetLastSwitchKeys()
        {
            CtrlDown = winDown = altDown = false;
        }
    }
}
