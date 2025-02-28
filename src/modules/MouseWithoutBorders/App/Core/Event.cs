// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using MouseWithoutBorders.Class;

// <summary>
//     Keyboard/Mouse hook callback implementation.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
namespace MouseWithoutBorders.Core;

internal static class Event
{
    private static readonly DATA KeybdPackage = new();
    private static readonly DATA MousePackage = new();
#pragma warning disable SA1307 // Accessible fields should begin with upper-case names
    internal static ulong inputEventCount;
    internal static ulong invalidPackageCount;
#pragma warning restore SA1307
    internal static int MOVE_MOUSE_RELATIVE = 100000;
    internal static int XY_BY_PIXEL = 300000;

    static Event()
    {
    }

    internal static ulong InvalidPackageCount
    {
        get => Event.invalidPackageCount;
        set => Event.invalidPackageCount = value;
    }

    internal static ulong InputEventCount
    {
        get => Event.inputEventCount;
        set => Event.inputEventCount = value;
    }

    internal static ulong RealInputEventCount
    {
        get;
        set;
    }

    private static Point actualLastPos;
#pragma warning disable SA1307 // Accessible fields should begin with upper-case names
    internal static int myLastX;
    internal static int myLastY;
#pragma warning restore SA1307

    [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Dotnet port with style preservation")]
    internal static void MouseEvent(MOUSEDATA e, int dx, int dy)
    {
        try
        {
            Common.PaintCount = 0;
            bool switchByMouseEnabled = IsSwitchingByMouseEnabled();

            if (switchByMouseEnabled && Common.Sk != null && (Common.DesMachineID == Common.MachineID || !Setting.Values.MoveMouseRelatively) && e.dwFlags == Common.WM_MOUSEMOVE)
            {
                Point p = MachineStuff.MoveToMyNeighbourIfNeeded(e.X, e.Y, MachineStuff.desMachineID);

                if (!p.IsEmpty)
                {
                    Common.HasSwitchedMachineSinceLastCopy = true;

                    Logger.LogDebug(string.Format(
                        CultureInfo.CurrentCulture,
                        "***** Host Machine: newDesMachineIdEx set = [{0}]. Mouse is now at ({1},{2})",
                        MachineStuff.newDesMachineIdEx,
                        e.X,
                        e.Y));

                    myLastX = e.X;
                    myLastY = e.Y;

                    PrepareToSwitchToMachine(MachineStuff.newDesMachineIdEx, p);
                }
            }

            if (MachineStuff.desMachineID != Common.MachineID && MachineStuff.SwitchLocation.Count <= 0)
            {
                MousePackage.Des = MachineStuff.desMachineID;
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
                    MousePackage.Md.X = (e.X - MachineStuff.primaryScreenBounds.Left) * 65535 / Common.screenWidth;
                    MousePackage.Md.Y = (e.Y - MachineStuff.primaryScreenBounds.Top) * 65535 / Common.screenHeight;
                }

                Common.SkSend(MousePackage, null, false);

                if (MousePackage.Md.dwFlags is Common.WM_LBUTTONUP or Common.WM_RBUTTONUP)
                {
                    Thread.Sleep(10);
                }

                NativeMethods.GetCursorPos(ref actualLastPos);

                if (actualLastPos != Common.LastPos)
                {
                    Logger.LogDebug($"Mouse cursor has moved unexpectedly: Expected: {Common.LastPos}, actual: {actualLastPos}.");
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
            Logger.Log(ex);
        }
    }

    internal static bool IsSwitchingByMouseEnabled()
    {
        return (EasyMouseOption)Setting.Values.EasyMouse == EasyMouseOption.Enable || InputHook.EasyMouseKeyDown;
    }

    internal static void PrepareToSwitchToMachine(ID newDesMachineID, Point desMachineXY)
    {
        Logger.LogDebug($"PrepareToSwitchToMachine: newDesMachineID = {newDesMachineID}, desMachineXY = {desMachineXY}");

        if (((Common.GetTick() - MachineStuff.lastJump < 100) && (Common.GetTick() - MachineStuff.lastJump > 0)) || MachineStuff.desMachineID == ID.ALL)
        {
            Logger.LogDebug("PrepareToSwitchToMachine: lastJump");
            return;
        }

        MachineStuff.lastJump = Common.GetTick();

        string newDesMachineName = MachineStuff.NameFromID(newDesMachineID);

        if (!Common.IsConnectedTo(newDesMachineID))
        {// Connection lost, cancel switching
            Logger.LogDebug("No active connection found for " + newDesMachineName);

            // ShowToolTip("No active connection found for [" + newDesMachineName + "]!", 500);
        }
        else
        {
            MachineStuff.newDesMachineID = newDesMachineID;
            MachineStuff.SwitchLocation.X = desMachineXY.X;
            MachineStuff.SwitchLocation.Y = desMachineXY.Y;
            MachineStuff.SwitchLocation.ResetCount();
            _ = Common.EvSwitch.Set();

            // PostMessage(mainForm.Handle, WM_SWITCH, IntPtr.Zero, IntPtr.Zero);
            if (newDesMachineID != DragDrop.DragMachine)
            {
                if (!DragDrop.IsDragging && !DragDrop.IsDropping)
                {
                    if (DragDrop.MouseDown && !Common.RunOnLogonDesktop && !Common.RunOnScrSaverDesktop)
                    {
                        DragDrop.DragDropStep02();
                    }
                }
                else if (DragDrop.DragMachine != (ID)1)
                {
                    DragDrop.ChangeDropMachine();
                }
            }
            else
            {
                DragDrop.DragDropStep11();
            }

            // Change des machine
            if (MachineStuff.desMachineID != newDesMachineID)
            {
                Logger.LogDebug("MouseEvent: Switching to new machine:" + newDesMachineName);

                // Ask current machine to hide the Mouse cursor
                if (newDesMachineID != ID.ALL && MachineStuff.desMachineID != Common.MachineID)
                {
                    Common.SendPackage(MachineStuff.desMachineID, PackageType.HideMouse);
                }

                Common.DesMachineID = newDesMachineID;

                if (MachineStuff.desMachineID == Common.MachineID)
                {
                    if (Common.GetTick() - Common.clipboardCopiedTime < Common.BIG_CLIPBOARD_DATA_TIMEOUT)
                    {
                        Common.clipboardCopiedTime = 0;
                        Common.GetRemoteClipboard("PrepareToSwitchToMachine");
                    }
                }
                else
                {
                    // Ask the new active machine to get clipboard data (if the data is too big)
                    Common.SendPackage(MachineStuff.desMachineID, PackageType.MachineSwitched);
                }

                _ = Interlocked.Increment(ref Common.switchCount);
            }
        }
    }

    internal static void SaveSwitchCount()
    {
        if (Common.SwitchCount > 0)
        {
            _ = Task.Run(() =>
            {
                Setting.Values.SwitchCount += Common.SwitchCount;
                _ = Interlocked.Exchange(ref Common.switchCount, 0);
            });
        }
    }

    internal static void KeybdEvent(KEYBDDATA e)
    {
        try
        {
            Common.PaintCount = 0;
            if (MachineStuff.desMachineID != MachineStuff.newDesMachineID)
            {
                Logger.LogDebug("KeybdEvent: Switching to new machine...");
                Common.DesMachineID = MachineStuff.newDesMachineID;
            }

            if (MachineStuff.desMachineID != Common.MachineID)
            {
                KeybdPackage.Des = MachineStuff.desMachineID;
                KeybdPackage.Type = PackageType.Keyboard;
                KeybdPackage.Kd = e;
                KeybdPackage.DateTime = Common.GetTick();
                Common.SkSend(KeybdPackage, null, false);
                if (KeybdPackage.Kd.dwFlags is Common.WM_KEYUP or Common.WM_SYSKEYUP)
                {
                    Thread.Sleep(10);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }
}
