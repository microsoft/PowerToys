// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

using MouseWithoutBorders.Class;

[module: SuppressMessage("Microsoft.Reliability", "CA2002:DoNotLockOnObjectsWithWeakIdentity", Scope = "member", Target = "MouseWithoutBorders.Common.#PreProcess(MouseWithoutBorders.DATA)", Justification = "Dotnet port with style preservation")]

// <summary>
//     Back-end thread for the socket.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
namespace MouseWithoutBorders.Core;

internal static class Receiver
{
    private static readonly uint QUEUE_SIZE = 50;
    private static readonly int[] RecentProcessedPackageIDs = new int[QUEUE_SIZE];
    private static int recentProcessedPackageIndex;
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
    internal static long processedPackageCount;
    internal static long skippedPackageCount;
#pragma warning restore SA1307

    private static long JustGotAKey { get; set; }

    private static bool PreProcess(DATA package)
    {
        if (package.Type == PackageType.Invalid)
        {
            if ((Common.InvalidPackageCount % 100) == 0)
            {
                Common.ShowToolTip("Invalid packages received!", 1000, ToolTipIcon.Warning, false);
            }

            Common.InvalidPackageCount++;
            Logger.Log("Invalid packages received!");
            return false;
        }
        else if (package.Type == 0)
        {
            Logger.Log("Got an unknown package!");
            return false;
        }
        else if (package.Type is not PackageType.ClipboardText and not PackageType.ClipboardImage

            // BEGIN: These package types are sent by TcpSend which is single direction.
            and not PackageType.Handshake and not PackageType.HandshakeAck)
        {
            // END
            lock (RecentProcessedPackageIDs)
            {
                for (int i = 0; i < QUEUE_SIZE; i++)
                {
                    if (RecentProcessedPackageIDs[i] == package.Id)
                    {
                        skippedPackageCount++;
                        return false;
                    }
                }

                processedPackageCount++;
                recentProcessedPackageIndex = (int)((recentProcessedPackageIndex + 1) % QUEUE_SIZE);
                RecentProcessedPackageIDs[recentProcessedPackageIndex] = package.Id;
            }
        }

        return true;
    }

    private static System.Drawing.Point lastXY;

    internal static void ProcessPackage(DATA package, TcpSk tcp)
    {
        if (!PreProcess(package))
        {
            return;
        }

        switch (package.Type)
        {
            case PackageType.Keyboard:
                Common.PackageReceived.Keyboard++;
                if (package.Des == Common.MachineID || package.Des == ID.ALL)
                {
                    JustGotAKey = Common.GetTick();

                    // NOTE(@yuyoyuppe): disabled to drop elevation requirement
                    bool nonElevated = Common.RunWithNoAdminRight && false;
                    if (nonElevated && Setting.Values.OneWayControlMode)
                    {
                        if ((package.Kd.dwFlags & (int)Common.LLKHF.UP) == (int)Common.LLKHF.UP)
                        {
                            Common.ShowOneWayModeMessage();
                        }

                        return;
                    }

                    InputSimulation.SendKey(package.Kd);
                }

                break;

            case PackageType.Mouse:
                Common.PackageReceived.Mouse++;

                if (package.Des == Common.MachineID || package.Des == ID.ALL)
                {
                    if (MachineStuff.desMachineID != Common.MachineID)
                    {
                        MachineStuff.NewDesMachineID = Common.DesMachineID = Common.MachineID;
                    }

                    // NOTE(@yuyoyuppe): disabled to drop elevation requirement
                    bool nonElevated = Common.RunWithNoAdminRight && false;
                    if (nonElevated && Setting.Values.OneWayControlMode && package.Md.dwFlags != Common.WM_MOUSEMOVE)
                    {
                        if (!DragDrop.IsDropping)
                        {
                            if (package.Md.dwFlags is Common.WM_LBUTTONDOWN or Common.WM_RBUTTONDOWN)
                            {
                                Common.ShowOneWayModeMessage();
                            }
                        }
                        else if (package.Md.dwFlags is Common.WM_LBUTTONUP or Common.WM_RBUTTONUP)
                        {
                            DragDrop.IsDropping = false;
                        }

                        return;
                    }

                    if (Math.Abs(package.Md.X) >= Common.MOVE_MOUSE_RELATIVE && Math.Abs(package.Md.Y) >= Common.MOVE_MOUSE_RELATIVE)
                    {
                        if (package.Md.dwFlags == Common.WM_MOUSEMOVE)
                        {
                            InputSimulation.MoveMouseRelative(
                                package.Md.X < 0 ? package.Md.X + Common.MOVE_MOUSE_RELATIVE : package.Md.X - Common.MOVE_MOUSE_RELATIVE,
                                package.Md.Y < 0 ? package.Md.Y + Common.MOVE_MOUSE_RELATIVE : package.Md.Y - Common.MOVE_MOUSE_RELATIVE);
                            _ = NativeMethods.GetCursorPos(ref lastXY);

                            Point p = MachineStuff.MoveToMyNeighbourIfNeeded(lastXY.X, lastXY.Y, Common.MachineID);

                            if (!p.IsEmpty)
                            {
                                Common.HasSwitchedMachineSinceLastCopy = true;

                                Logger.LogDebug(string.Format(
                                    CultureInfo.CurrentCulture,
                                    "***** Controlled Machine: newDesMachineIdEx set = [{0}]. Mouse is now at ({1},{2})",
                                    MachineStuff.newDesMachineIdEx,
                                    lastXY.X,
                                    lastXY.Y));

                                Common.SendNextMachine(package.Src, MachineStuff.newDesMachineIdEx, p);
                            }
                        }
                        else
                        {
                            _ = NativeMethods.GetCursorPos(ref lastXY);
                            package.Md.X = lastXY.X * 65535 / Common.screenWidth;
                            package.Md.Y = lastXY.Y * 65535 / Common.screenHeight;
                            _ = InputSimulation.SendMouse(package.Md);
                        }
                    }
                    else
                    {
                        _ = InputSimulation.SendMouse(package.Md);
                        _ = NativeMethods.GetCursorPos(ref lastXY);
                    }

                    Common.LastX = lastXY.X;
                    Common.LastY = lastXY.Y;
                    CustomCursor.ShowFakeMouseCursor(Common.LastX, Common.LastY);
                }

                DragDrop.DragDropStep01(package.Md.dwFlags);
                DragDrop.DragDropStep09(package.Md.dwFlags);
                break;

            case PackageType.NextMachine:
                Logger.LogDebug("PackageType.NextMachine received!");

                if (Common.IsSwitchingByMouseEnabled())
                {
                    Common.PrepareToSwitchToMachine((ID)package.Md.WheelDelta, new Point(package.Md.X, package.Md.Y));
                }

                break;

            case PackageType.ExplorerDragDrop:
                Common.PackageReceived.ExplorerDragDrop++;
                DragDrop.DragDropStep03(package);
                break;

            case PackageType.Heartbeat:
            case PackageType.Heartbeat_ex:
                Common.PackageReceived.Heartbeat++;

                Common.GeneratedKey = Common.GeneratedKey || package.Type == PackageType.Heartbeat_ex;

                if (Common.GeneratedKey)
                {
                    Setting.Values.MyKey = Common.MyKey;
                    Common.SendPackage(ID.ALL, PackageType.Heartbeat_ex_l2);
                }

                string desMachine = MachineStuff.AddToMachinePool(package);

                if (Setting.Values.FirstRun && !string.IsNullOrEmpty(desMachine))
                {
                    Common.UpdateSetupMachineMatrix(desMachine);
                    MachineStuff.UpdateClientSockets("UpdateSetupMachineMatrix");
                }

                break;

            case PackageType.Heartbeat_ex_l2:
                Common.GeneratedKey = true;
                Setting.Values.MyKey = Common.MyKey;
                Common.SendPackage(ID.ALL, PackageType.Heartbeat_ex_l3);

                break;

            case PackageType.Heartbeat_ex_l3:
                Common.GeneratedKey = true;
                Setting.Values.MyKey = Common.MyKey;

                break;

            case PackageType.Awake:
                Common.PackageReceived.Heartbeat++;
                _ = MachineStuff.AddToMachinePool(package);
                Common.HumanBeingDetected();
                break;

            case PackageType.Hello:
                Common.PackageReceived.Hello++;
                Common.SendHeartBeat();
                string newMachine = MachineStuff.AddToMachinePool(package);
                if (Setting.Values.MachineMatrixString == null)
                {
                    string tip = newMachine + " saying Hello!";
                    tip += "\r\n Right Click to setup your machine Matrix";
                    Common.ShowToolTip(tip);
                }

                break;

            case PackageType.Hi:
                Common.PackageReceived.Hello++;
                break;

            case PackageType.ByeBye:
                Common.PackageReceived.ByeBye++;
                Common.ProcessByeByeMessage(package);
                break;

            case PackageType.Clipboard:
                Common.PackageReceived.Clipboard++;
                if (!Common.RunOnLogonDesktop && !Common.RunOnScrSaverDesktop)
                {
                    Common.clipboardCopiedTime = Common.GetTick();
                    GetNameOfMachineWithClipboardData(package);
                    SignalBigClipboardData();
                }

                break;

            case PackageType.MachineSwitched:
                if (Common.GetTick() - Common.clipboardCopiedTime < Common.BIG_CLIPBOARD_DATA_TIMEOUT && (package.Des == Common.MachineID))
                {
                    Common.clipboardCopiedTime = 0;
                    Common.GetRemoteClipboard("PackageType.MachineSwitched");
                }

                break;

            case PackageType.ClipboardCapture:
                Common.PackageReceived.Clipboard++;
                if (!Common.RunOnLogonDesktop && !Common.RunOnScrSaverDesktop)
                {
                    if (package.Des == Common.MachineID || package.Des == ID.ALL)
                    {
                        GetNameOfMachineWithClipboardData(package);
                        Common.GetRemoteClipboard("mspaint," + Common.LastMachineWithClipboardData);
                    }
                }

                break;

            case PackageType.CaptureScreenCommand:
                Common.PackageReceived.Clipboard++;
                if (package.Des == Common.MachineID || package.Des == ID.ALL)
                {
                    Common.SendImage(package.Src, Common.CaptureScreen());
                }

                break;

            case PackageType.ClipboardAsk:
                Common.PackageReceived.ClipboardAsk++;

                if (package.Des == Common.MachineID)
                {
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            System.Threading.Thread thread = Thread.CurrentThread;
                            thread.Name = $"{nameof(PackageType.ClipboardAsk)}.{thread.ManagedThreadId}";
                            Thread.UpdateThreads(thread);

                            string remoteMachine = package.MachineName;
                            System.Net.Sockets.TcpClient client = Common.ConnectToRemoteClipboardSocket(remoteMachine);
                            bool clientPushData = true;

                            if (Common.ShakeHand(ref remoteMachine, client.Client, out Stream enStream, out Stream deStream, ref clientPushData, ref package.PostAction))
                            {
                                SocketStuff.SendClipboardData(client.Client, enStream);
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Log(e);
                        }
                    });
                }

                break;

            case PackageType.ClipboardDragDrop:
                Common.PackageReceived.ClipboardDragDrop++;
                DragDrop.DragDropStep08(package);
                break;

            case PackageType.ClipboardDragDropOperation:
                Common.PackageReceived.ClipboardDragDrop++;
                DragDrop.DragDropStep08_2(package);
                break;

            case PackageType.ClipboardDragDropEnd:
                Common.PackageReceived.ClipboardDragDropEnd++;
                DragDrop.DragDropStep12();
                break;

            case PackageType.ClipboardText:
            case PackageType.ClipboardImage:
                Common.clipboardCopiedTime = 0;
                if (package.Type == PackageType.ClipboardImage)
                {
                    Common.PackageReceived.ClipboardImage++;
                }
                else
                {
                    Common.PackageReceived.ClipboardText++;
                }

                if (tcp != null)
                {
                    Common.ReceiveClipboardDataUsingTCP(
                        package,
                        package.Type == PackageType.ClipboardImage,
                        tcp);
                }

                break;

            case PackageType.HideMouse:
                Common.HasSwitchedMachineSinceLastCopy = true;
                Common.HideMouseCursor(true);
                Common.MainFormDotEx(false);
                Common.ReleaseAllKeys();
                break;

            default:
                if ((package.Type & PackageType.Matrix) == PackageType.Matrix)
                {
                    Common.PackageReceived.Matrix++;
                    MachineStuff.UpdateMachineMatrix(package);
                    break;
                }
                else
                {
                    // We should never get to this point!
                    Logger.Log("Invalid package received!");
                    return;
                }
        }
    }

    internal static void GetNameOfMachineWithClipboardData(DATA package)
    {
        Common.LastIDWithClipboardData = package.Src;
        List<MachineInf> matchingMachines = MachineStuff.MachinePool.TryFindMachineByID(Common.LastIDWithClipboardData);
        if (matchingMachines.Count >= 1)
        {
            Common.LastMachineWithClipboardData = matchingMachines[0].Name.Trim();
        }

        /*
        lastMachineWithClipboardData =
            Common.GetString(BitConverter.GetBytes(package.machineNameHead));
        lastMachineWithClipboardData +=
            Common.GetString(BitConverter.GetBytes(package.machineNameTail));
        lastMachineWithClipboardData = lastMachineWithClipboardData.Trim();
         * */
    }

    private static void SignalBigClipboardData()
    {
        Logger.LogDebug("SignalBigClipboardData");
        Common.SetToggleIcon(new int[Common.TOGGLE_ICONS_SIZE] { Common.ICON_BIG_CLIPBOARD, -1, Common.ICON_BIG_CLIPBOARD, -1 });
    }
}
