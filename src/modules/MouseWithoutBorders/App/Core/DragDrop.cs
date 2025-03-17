// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;

using Microsoft.PowerToys.Telemetry;
using MouseWithoutBorders.Class;

// <summary>
//     Drag/Drop implementation.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
namespace MouseWithoutBorders.Core;

/* Common.DragDrop.cs
 * Drag&Drop is one complicated implementation of the tool with some tricks.
 *
 * SEQUENCE OF EVENTS:
 * DragDropStep01: MachineX: Remember mouse down state since it could be a start of a dragging
 * DragDropStep02: MachineY: Send an message to the MachineX to ask it to check if it is
 *                           doing drag/drop
 * DragDropStep03: MachineX: Got explorerDragDrop, send WM_CHECK_EXPLORER_DRAG_DROP to its mainForm
 * DragDropStep04: MachineX: Show Mouse Without Borders Helper form at mouse cursor to get DragEnter event.
 * DragDropStepXX: MachineX: Mouse Without Borders Helper: Called by DragEnter, check if dragging a single file,
 *                           remember the file (set as its window caption)
 * DragDropStep05: MachineX: Get the file name from Mouse Without Borders Helper, hide Mouse Without Borders Helper window
 * DragDropStep06: MachineX: Broadcast a message saying that it has some drag file.
 * DragDropStep08: MachineY: Got ClipboardDragDrop, isDropping set, get the MachineX name from the package.
 * DragDropStep09: MachineY: Since isDropping is true, show up the drop form (looks like an icon).
 * DragDropStep10: MachineY: MouseUp, set isDropping to false, hide the drop "icon" and get data.
 * DragDropStep11: MachineX: Mouse move back without drop event, cancelling drag/dop
 *                           SendClipboardBeatDragDropEnd
 * DragDropStep12: MachineY: Hide the drop "icon" when received ClipboardDragDropEnd.
 *
 * FROM VERSION 1.6.3: Drag/Drop is temporary removed, Drop action cannot be done from a lower integrity app to a higher one.
 * We have to run a helper process...
 * http://forums.microsoft.com/MSDN/ShowPost.aspx?PageIndex=1&SiteID=1&PageID=1&PostID=736086
 *
 * 2008.10.28: Trying to restore the Drag/Drop feature by adding the drag/drop helper process. Coming in version
 * 1.6.5
 * */

internal static class DragDrop
{
    private static bool isDragging;

    internal static bool IsDragging
    {
        get => DragDrop.isDragging;
        set => DragDrop.isDragging = value;
    }

    internal static void DragDropStep01(int wParam)
    {
        if (!Setting.Values.TransferFile)
        {
            return;
        }

        if (wParam == Common.WM_LBUTTONDOWN)
        {
            MouseDown = true;
            DragMachine = MachineStuff.desMachineID;
            MachineStuff.dropMachineID = ID.NONE;
            Logger.LogDebug("DragDropStep01: MouseDown");
        }
        else if (wParam == Common.WM_LBUTTONUP)
        {
            MouseDown = false;
            Logger.LogDebug("DragDropStep01: MouseUp");
        }

        if (wParam == Common.WM_RBUTTONUP && IsDropping)
        {
            IsDropping = false;
            Common.LastIDWithClipboardData = ID.NONE;
        }
    }

    internal static void DragDropStep02()
    {
        if (MachineStuff.desMachineID == Common.MachineID)
        {
            Logger.LogDebug("DragDropStep02: SendCheckExplorerDragDrop sent to myself");
            Common.DoSomethingInUIThread(() =>
            {
                _ = NativeMethods.PostMessage(Common.MainForm.Handle, NativeMethods.WM_CHECK_EXPLORER_DRAG_DROP, (IntPtr)0, (IntPtr)0);
            });
        }
        else
        {
            SendCheckExplorerDragDrop();
            Logger.LogDebug("DragDropStep02: SendCheckExplorerDragDrop sent");
        }
    }

    internal static void DragDropStep03(DATA package)
    {
        if (Common.RunOnLogonDesktop || Common.RunOnScrSaverDesktop)
        {
            return;
        }

        if (package.Des == Common.MachineID || package.Des == ID.ALL)
        {
            Logger.LogDebug("DragDropStep03: ExplorerDragDrop Received.");
            MachineStuff.dropMachineID = package.Src; // Drop machine is the machine that sent ExplorerDragDrop
            if (MouseDown || IsDropping)
            {
                Logger.LogDebug("DragDropStep03: Mouse is down, check if dragging...sending WM_CHECK_EXPLORER_DRAG_DROP to myself...");
                Common.DoSomethingInUIThread(() =>
                {
                    _ = NativeMethods.PostMessage(Common.MainForm.Handle, NativeMethods.WM_CHECK_EXPLORER_DRAG_DROP, (IntPtr)0, (IntPtr)0);
                });
            }
        }
    }

    private static int dragDropStep05ExCalledByIpc;

    internal static void DragDropStep04()
    {
        if (!IsDropping)
        {
            IntPtr h = (IntPtr)NativeMethods.FindWindow(null, Helper.HELPER_FORM_TEXT);
            if (h.ToInt32() > 0)
            {
                _ = Interlocked.Exchange(ref dragDropStep05ExCalledByIpc, 0);

                Common.MainForm.Hide();
                Common.MainFormVisible = false;

                Point p = default;

                // NativeMethods.SetWindowText(h, "");
                _ = NativeMethods.SetWindowPos(h, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, NativeMethods.SWP_SHOWWINDOW);

                for (int i = -10; i < 10; i++)
                {
                    if (dragDropStep05ExCalledByIpc > 0)
                    {
                        Logger.LogDebug("DragDropStep04: DragDropStep05ExCalledByIpc.");
                        break;
                    }

                    _ = NativeMethods.GetCursorPos(ref p);
                    Logger.LogDebug("DragDropStep04: Moving Mouse Without Borders Helper to (" + p.X.ToString(CultureInfo.CurrentCulture) + ", " + p.Y.ToString(CultureInfo.CurrentCulture) + ")");
                    _ = NativeMethods.SetWindowPos(h, NativeMethods.HWND_TOPMOST, p.X - 100 + i, p.Y - 100 + i, 200, 200, 0);
                    _ = NativeMethods.SendMessage(h, 0x000F, IntPtr.Zero, IntPtr.Zero); // WM_PAINT
                    Thread.Sleep(20);
                    Application.DoEvents();

                    // if (GetText(h).Length > 1) break;
                }
            }
            else
            {
                Logger.LogDebug("DragDropStep04: Mouse without Borders Helper not found!");
            }
        }
        else
        {
            Logger.LogDebug("DragDropStep04: IsDropping == true, skip checking");
        }

        Logger.LogDebug("DragDropStep04: Got WM_CHECK_EXPLORER_DRAG_DROP, done with processing jump to DragDropStep05...");
    }

    internal static void DragDropStep05Ex(string dragFileName)
    {
        Logger.LogDebug("DragDropStep05 called.");

        _ = Interlocked.Exchange(ref dragDropStep05ExCalledByIpc, 1);

        if (Common.RunOnLogonDesktop || Common.RunOnScrSaverDesktop)
        {
            return;
        }

        if (!IsDropping)
        {
            _ = Launch.ImpersonateLoggedOnUserAndDoSomething(() =>
            {
                if (!string.IsNullOrEmpty(dragFileName) && (File.Exists(dragFileName) || Directory.Exists(dragFileName)))
                {
                    Common.LastDragDropFile = dragFileName;
                    /*
                     * possibleDropMachineID is used as desID sent in DragDropStep06();
                     * */
                    if (MachineStuff.dropMachineID == ID.NONE)
                    {
                        MachineStuff.dropMachineID = MachineStuff.newDesMachineID;
                    }

                    DragDropStep06();
                    Logger.LogDebug("DragDropStep05: File dragging: " + dragFileName);
                    _ = NativeMethods.PostMessage(Common.MainForm.Handle, NativeMethods.WM_HIDE_DD_HELPER, (IntPtr)1, (IntPtr)0);
                }
                else
                {
                    Logger.LogDebug("DragDropStep05: File not found: [" + dragFileName + "]");
                    _ = NativeMethods.PostMessage(Common.MainForm.Handle, NativeMethods.WM_HIDE_DD_HELPER, (IntPtr)0, (IntPtr)0);
                }

                Logger.LogDebug("DragDropStep05: WM_HIDE_DDHelper sent");
            });
        }
        else
        {
            Logger.LogDebug("DragDropStep05: IsDropping == true, change drop machine...");
            IsDropping = false;
            Common.MainFormVisible = true; // WM_HIDE_DRAG_DROP
            SendDropBegin(); // To dropMachineID set in DragDropStep03
        }

        MouseDown = false;
    }

    private static void DragDropStep06()
    {
        IsDragging = true;
        Logger.LogDebug("DragDropStep06: SendClipboardBeatDragDrop");
        SendClipboardBeatDragDrop();
        SendDropBegin();
    }

    internal static void DragDropStep08(DATA package)
    {
        Receiver.GetNameOfMachineWithClipboardData(package);
        Logger.LogDebug("DragDropStep08: ClipboardDragDrop Received. machine with drag file was set");
    }

    internal static void DragDropStep08_2(DATA package)
    {
        if (package.Des == Common.MachineID && !Common.RunOnLogonDesktop && !Common.RunOnScrSaverDesktop)
        {
            IsDropping = true;
            MachineStuff.dropMachineID = Common.MachineID;
            Logger.LogDebug("DragDropStep08_2: ClipboardDragDropOperation Received. IsDropping set");
        }
    }

    internal static void DragDropStep09(int wParam)
    {
        if (wParam == Common.WM_MOUSEMOVE && IsDropping)
        {
            // Show/Move form
            Common.DoSomethingInUIThread(() =>
            {
                _ = NativeMethods.PostMessage(Common.MainForm.Handle, NativeMethods.WM_SHOW_DRAG_DROP, (IntPtr)0, (IntPtr)0);
            });
        }
        else if (wParam == Common.WM_LBUTTONUP && (IsDropping || IsDragging))
        {
            if (IsDropping)
            {
                // Hide form, get data
                DragDropStep10();
            }
            else
            {
                IsDragging = false;
                Common.LastIDWithClipboardData = ID.NONE;
            }
        }
    }

    private static void DragDropStep10()
    {
        Logger.LogDebug("DragDropStep10: Hide the form and get data...");
        IsDropping = false;
        IsDragging = false;
        Common.LastIDWithClipboardData = ID.NONE;

        Common.DoSomethingInUIThread(() =>
        {
            _ = NativeMethods.PostMessage(Common.MainForm.Handle, NativeMethods.WM_HIDE_DRAG_DROP, (IntPtr)0, (IntPtr)0);
        });

        PowerToysTelemetry.Log.WriteEvent(new MouseWithoutBorders.Telemetry.MouseWithoutBordersDragAndDropEvent());
        Common.GetRemoteClipboard("desktop");
    }

    internal static void DragDropStep11()
    {
        Logger.LogDebug("DragDropStep11: Mouse drag coming back, canceling drag/drop");
        SendClipboardBeatDragDropEnd();
        IsDropping = false;
        IsDragging = false;
        DragMachine = (ID)1;
        Common.LastIDWithClipboardData = ID.NONE;
        Common.LastDragDropFile = null;
        MouseDown = false;
    }

    internal static void DragDropStep12()
    {
        Logger.LogDebug("DragDropStep12: ClipboardDragDropEnd received");
        IsDropping = false;
        Common.LastIDWithClipboardData = ID.NONE;

        Common.DoSomethingInUIThread(() =>
        {
            _ = NativeMethods.PostMessage(Common.MainForm.Handle, NativeMethods.WM_HIDE_DRAG_DROP, (IntPtr)0, (IntPtr)0);
        });
    }

    private static void SendCheckExplorerDragDrop()
    {
        DATA package = new();
        package.Type = PackageType.ExplorerDragDrop;

        /*
         * package.src = newDesMachineID:
         * sent from the master machine but the src must be the
         * new des machine since the previous des machine will get this and set
         * to possibleDropMachineID in DragDropStep3()
         * */
        package.Src = MachineStuff.newDesMachineID;

        package.Des = MachineStuff.desMachineID;
        package.MachineName = Common.MachineName;

        Common.SkSend(package, null, false);
    }

    internal static void ChangeDropMachine()
    {
        // desMachineID = current drop machine
        // newDesMachineID = new drop machine

        // 1. Cancelling dropping in current drop machine
        if (MachineStuff.dropMachineID == Common.MachineID)
        {
            // Drag/Drop coming through me
            IsDropping = false;
        }
        else
        {
            // Drag/Drop coming back
            SendClipboardBeatDragDropEnd();
        }

        // 2. SendClipboardBeatDragDrop to new drop machine
        // new drop machine is not me
        if (MachineStuff.newDesMachineID != Common.MachineID)
        {
            MachineStuff.dropMachineID = MachineStuff.newDesMachineID;
            SendDropBegin();
        }

        // New drop machine is me
        else
        {
            IsDropping = true;
        }
    }

    private static void SendClipboardBeatDragDrop()
    {
        Common.SendPackage(ID.ALL, PackageType.ClipboardDragDrop);
    }

    private static void SendDropBegin()
    {
        Logger.LogDebug("SendDropBegin...");
        Common.SendPackage(MachineStuff.dropMachineID, PackageType.ClipboardDragDropOperation);
    }

    private static void SendClipboardBeatDragDropEnd()
    {
        if (MachineStuff.desMachineID != Common.MachineID)
        {
            Common.SendPackage(MachineStuff.desMachineID, PackageType.ClipboardDragDropEnd);
        }
    }

    private static bool isDropping;
    private static ID dragMachine;

    internal static ID DragMachine
    {
        get => DragDrop.dragMachine;
        set => DragDrop.dragMachine = value;
    }

    internal static bool IsDropping
    {
        get => DragDrop.isDropping;
        set => DragDrop.isDropping = value;
    }

    internal static bool MouseDown { get; set; }
}
