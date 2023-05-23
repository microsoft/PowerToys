// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

// <summary>
//     Keyboard/Mouse hook callback implementation.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
using MouseWithoutBorders.Class;
using MouseWithoutBorders.Form;

namespace MouseWithoutBorders
{
    internal partial class Common
    {
        private static readonly DATA KeybdPackage = new();
        private static readonly DATA MousePackage = new();
        private static ulong inputEventCount;
        private static ulong invalidPackageCount;
        internal static int MOVE_MOUSE_RELATIVE = 100000;
        internal static int XY_BY_PIXEL = 300000;

        static Common()
        {
        }

        internal static ulong InvalidPackageCount
        {
            get => Common.invalidPackageCount;
            set => Common.invalidPackageCount = value;
        }

        internal static ulong InputEventCount
        {
            get => Common.inputEventCount;
            set => Common.inputEventCount = value;
        }

        internal static ulong RealInputEventCount
        {
            get;
            set;
        }

        private static Point actualLastPos;
        private static int myLastX;
        private static int myLastY;

        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Dotnet port with style preservation")]
        internal static void MouseEvent(MOUSEDATA e, int dx, int dy)
        {
            try
            {
                PaintCount = 0;
                bool switchByMouseEnabled = IsSwitchingByMouseEnabled();

                if (switchByMouseEnabled && Sk != null && (DesMachineID == MachineID || !Setting.Values.MoveMouseRelatively) && e.dwFlags == WM_MOUSEMOVE)
                {
                    Point p = MoveToMyNeighbourIfNeeded(e.X, e.Y, desMachineID);

                    if (!p.IsEmpty)
                    {
                        HasSwitchedMachineSinceLastCopy = true;

                        Common.LogDebug(string.Format(
                            CultureInfo.CurrentCulture,
                            "***** Host Machine: newDesMachineIdEx set = [{0}]. Mouse is now at ({1},{2})",
                            newDesMachineIdEx,
                            e.X,
                            e.Y));

                        myLastX = e.X;
                        myLastY = e.Y;

                        PrepareToSwitchToMachine(newDesMachineIdEx, p);
                    }
                }

                if (desMachineID != MachineID && SwitchLocation.Count <= 0)
                {
                    MousePackage.Des = desMachineID;
                    MousePackage.Type = PackageType.Mouse;
                    MousePackage.Md.dwFlags = e.dwFlags;
                    MousePackage.Md.WheelDelta = e.WheelDelta;

                    // Relative move
                    if (Setting.Values.MoveMouseRelatively && Math.Abs(dx) >= MOVE_MOUSE_RELATIVE && Math.Abs(dy) >= MOVE_MOUSE_RELATIVE)
                    {
                        MousePackage.Md.X = dx;
                        MousePackage.Md.Y = dy;
                    }
                    else
                    {
                        MousePackage.Md.X = (e.X - primaryScreenBounds.Left) * 65535 / screenWidth;
                        MousePackage.Md.Y = (e.Y - primaryScreenBounds.Top) * 65535 / screenHeight;
                    }

                    SkSend(MousePackage, null, false);

                    if (MousePackage.Md.dwFlags is WM_LBUTTONUP or WM_RBUTTONUP)
                    {
                        Thread.Sleep(10);
                    }

                    NativeMethods.GetCursorPos(ref actualLastPos);

                    if (actualLastPos != Common.LastPos)
                    {
                        Common.LogDebug($"Mouse cursor has moved unexpectedly: Expected: {Common.LastPos}, actual: {actualLastPos}.");
                        Common.LastPos = actualLastPos;
                    }
                }

#if SHOW_ON_WINLOGON_EX
                if (RunOnLogonDesktop && e.dwFlags == WM_RBUTTONUP &&
                    desMachineID == machineID &&
                    e.x > 2 && e.x < 100 && e.y > 2 && e.y < 20)
                {
                    DoSomethingInUIThread(delegate()
                    {
                        MainForm.HideMenuWhenRunOnLogonDesktop();
                        MainForm.MainMenu.Hide();
                        MainForm.MainMenu.Show(e.x - 5, e.y - 3);
                    });
                }
#endif
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        private static bool IsSwitchingByMouseEnabled()
        {
            return (EasyMouseOption)Setting.Values.EasyMouse == EasyMouseOption.Enable || InputHook.EasyMouseKeyDown;
        }

        internal static void PrepareToSwitchToMachine(ID newDesMachineID, Point desMachineXY)
        {
            LogDebug($"PrepareToSwitchToMachine: newDesMachineID = {newDesMachineID}, desMachineXY = {desMachineXY}");

            if (((GetTick() - lastJump < 100) && (GetTick() - lastJump > 0)) || desMachineID == ID.ALL)
            {
                LogDebug("PrepareToSwitchToMachine: lastJump");
                return;
            }

            lastJump = GetTick();

            string newDesMachineName = NameFromID(newDesMachineID);

            if (!IsConnectedTo(newDesMachineID))
            {// Connection lost, cancel switching
                LogDebug("No active connection found for " + newDesMachineName);

                // ShowToolTip("No active connection found for [" + newDesMachineName + "]!", 500);
            }
            else
            {
                Common.newDesMachineID = newDesMachineID;
                SwitchLocation.X = desMachineXY.X;
                SwitchLocation.Y = desMachineXY.Y;
                SwitchLocation.ResetCount();
                _ = EvSwitch.Set();

                // PostMessage(mainForm.Handle, WM_SWITCH, IntPtr.Zero, IntPtr.Zero);
                if (newDesMachineID != DragMachine)
                {
                    if (!IsDragging && !IsDropping)
                    {
                        if (MouseDown && !RunOnLogonDesktop && !RunOnScrSaverDesktop)
                        {
                            DragDropStep02();
                        }
                    }
                    else if (DragMachine != (ID)1)
                    {
                        ChangeDropMachine();
                    }
                }
                else
                {
                    DragDropStep11();
                }

                // Change des machine
                if (desMachineID != newDesMachineID)
                {
                    LogDebug("MouseEvent: Switching to new machine:" + newDesMachineName);

                    // Ask current machine to hide the Mouse cursor
                    if (newDesMachineID != ID.ALL && desMachineID != MachineID)
                    {
                        SendPackage(desMachineID, PackageType.HideMouse);
                    }

                    DesMachineID = newDesMachineID;

                    if (desMachineID == MachineID)
                    {
                        if (GetTick() - clipboardCopiedTime < BIG_CLIPBOARD_DATA_TIMEOUT)
                        {
                            clipboardCopiedTime = 0;
                            Common.GetRemoteClipboard("PrepareToSwitchToMachine");
                        }
                    }
                    else
                    {
                        // Ask the new active machine to get clipboard data (if the data is too big)
                        SendPackage(desMachineID, PackageType.MachineSwitched);
                    }

                    _ = Interlocked.Increment(ref switchCount);
                }
            }
        }

        internal static void SaveSwitchCount()
        {
            if (SwitchCount > 0)
            {
                _ = Task.Run(() =>
                {
                    Setting.Values.SwitchCount += SwitchCount;
                    _ = Interlocked.Exchange(ref switchCount, 0);
                });
            }
        }

        internal static void KeybdEvent(KEYBDDATA e)
        {
            try
            {
                PaintCount = 0;
                if (desMachineID != newDesMachineID)
                {
                    LogDebug("KeybdEvent: Switching to new machine...");
                    DesMachineID = newDesMachineID;
                }

                if (desMachineID != MachineID)
                {
                    KeybdPackage.Des = desMachineID;
                    KeybdPackage.Type = PackageType.Keyboard;
                    KeybdPackage.Kd = e;
                    KeybdPackage.DateTime = GetTick();
                    SkSend(KeybdPackage, null, false);
                    if (KeybdPackage.Kd.dwFlags is WM_KEYUP or WM_SYSKEYUP)
                    {
                        Thread.Sleep(10);
                    }
                }
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }
    }
}
