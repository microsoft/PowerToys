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

// <summary>
//     Back-end thread for the socket.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
using MouseWithoutBorders.Class;

[module: SuppressMessage("Microsoft.Reliability", "CA2002:DoNotLockOnObjectsWithWeakIdentity", Scope = "member", Target = "MouseWithoutBorders.Common.#PreProcess(MouseWithoutBorders.DATA)", Justification = "Dotnet port with style preservation")]

namespace MouseWithoutBorders
{
    internal partial class Common
    {
        private static readonly uint QUEUE_SIZE = 50;
        private static readonly int[] RecentProcessedPackageIDs = new int[QUEUE_SIZE];
        private static int recentProcessedPackageIndex;
        private static long processedPackageCount;
        private static long skippedPackageCount;

        internal static long JustGotAKey { get; set; }

        private static bool PreProcess(DATA package)
        {
            if (package.Type == PackageType.Invalid)
            {
                if ((Common.InvalidPackageCount % 100) == 0)
                {
                    ShowToolTip("Invalid packages received!", 1000, ToolTipIcon.Warning, false);
                }

                Common.InvalidPackageCount++;
                Common.Log("Invalid packages received!");
                return false;
            }
            else if (package.Type == 0)
            {
                Common.Log("Got an unknown package!");
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
                    PackageReceived.Keyboard++;
                    if (package.Des == MachineID || package.Des == ID.ALL)
                    {
                        JustGotAKey = GetTick();

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
                    PackageReceived.Mouse++;

                    if (package.Des == MachineID || package.Des == ID.ALL)
                    {
                        if (desMachineID != MachineID)
                        {
                            NewDesMachineID = DesMachineID = MachineID;
                        }

                        // NOTE(@yuyoyuppe): disabled to drop elevation requirement
                        bool nonElevated = Common.RunWithNoAdminRight && false;
                        if (nonElevated && Setting.Values.OneWayControlMode && package.Md.dwFlags != Common.WM_MOUSEMOVE)
                        {
                            if (!IsDropping)
                            {
                                if (package.Md.dwFlags is WM_LBUTTONDOWN or WM_RBUTTONDOWN)
                                {
                                    Common.ShowOneWayModeMessage();
                                }
                            }
                            else if (package.Md.dwFlags is WM_LBUTTONUP or WM_RBUTTONUP)
                            {
                                IsDropping = false;
                            }

                            return;
                        }

                        if (Math.Abs(package.Md.X) >= MOVE_MOUSE_RELATIVE && Math.Abs(package.Md.Y) >= MOVE_MOUSE_RELATIVE)
                        {
                            if (package.Md.dwFlags == Common.WM_MOUSEMOVE)
                            {
                                InputSimulation.MoveMouseRelative(
                                    package.Md.X < 0 ? package.Md.X + MOVE_MOUSE_RELATIVE : package.Md.X - MOVE_MOUSE_RELATIVE,
                                    package.Md.Y < 0 ? package.Md.Y + MOVE_MOUSE_RELATIVE : package.Md.Y - MOVE_MOUSE_RELATIVE);
                                _ = NativeMethods.GetCursorPos(ref lastXY);

                                Point p = MoveToMyNeighbourIfNeeded(lastXY.X, lastXY.Y, MachineID);

                                if (!p.IsEmpty)
                                {
                                    HasSwitchedMachineSinceLastCopy = true;

                                    Common.LogDebug(string.Format(
                                        CultureInfo.CurrentCulture,
                                        "***** Controlled Machine: newDesMachineIdEx set = [{0}]. Mouse is now at ({1},{2})",
                                        newDesMachineIdEx,
                                        lastXY.X,
                                        lastXY.Y));

                                    SendNextMachine(package.Src, newDesMachineIdEx, p);
                                }
                            }
                            else
                            {
                                _ = NativeMethods.GetCursorPos(ref lastXY);
                                package.Md.X = lastXY.X * 65535 / screenWidth;
                                package.Md.Y = lastXY.Y * 65535 / screenHeight;
                                _ = InputSimulation.SendMouse(package.Md);
                            }
                        }
                        else
                        {
                            _ = InputSimulation.SendMouse(package.Md);
                            _ = NativeMethods.GetCursorPos(ref lastXY);
                        }

                        LastX = lastXY.X;
                        LastY = lastXY.Y;
                        CustomCursor.ShowFakeMouseCursor(LastX, LastY);
                    }

                    DragDropStep01(package.Md.dwFlags);
                    DragDropStep09(package.Md.dwFlags);
                    break;

                case PackageType.NextMachine:
                    LogDebug("PackageType.NextMachine received!");

                    if (IsSwitchingByMouseEnabled())
                    {
                        PrepareToSwitchToMachine((ID)package.Md.WheelDelta, new Point(package.Md.X, package.Md.Y));
                    }

                    break;

                case PackageType.ExplorerDragDrop:
                    PackageReceived.ExplorerDragDrop++;
                    DragDropStep03(package);
                    break;

                case PackageType.Heartbeat:
                case PackageType.Heartbeat_ex:
                    PackageReceived.Heartbeat++;

                    Common.GeneratedKey = Common.GeneratedKey || package.Type == PackageType.Heartbeat_ex;

                    if (Common.GeneratedKey)
                    {
                        Setting.Values.MyKey = Common.MyKey;
                        SendPackage(ID.ALL, PackageType.Heartbeat_ex_l2);
                    }

                    string desMachine = Common.AddToMachinePool(package);

                    if (Setting.Values.FirstRun && !string.IsNullOrEmpty(desMachine))
                    {
                        Common.UpdateSetupMachineMatrix(desMachine);
                        Common.UpdateClientSockets("UpdateSetupMachineMatrix");
                    }

                    break;

                case PackageType.Heartbeat_ex_l2:
                    Common.GeneratedKey = true;
                    Setting.Values.MyKey = Common.MyKey;
                    SendPackage(ID.ALL, PackageType.Heartbeat_ex_l3);

                    break;

                case PackageType.Heartbeat_ex_l3:
                    Common.GeneratedKey = true;
                    Setting.Values.MyKey = Common.MyKey;

                    break;

                case PackageType.Awake:
                    PackageReceived.Heartbeat++;
                    _ = Common.AddToMachinePool(package);
                    Common.HumanBeingDetected();
                    break;

                case PackageType.Hello:
                    PackageReceived.Hello++;
                    SendHeartBeat();
                    string newMachine = Common.AddToMachinePool(package);
                    if (Setting.Values.MachineMatrixString == null)
                    {
                        string tip = newMachine + " saying Hello!";
                        tip += "\r\n Right Click to setup your machine Matrix";
                        ShowToolTip(tip);
                    }

                    break;

                case PackageType.Hi:
                    PackageReceived.Hello++;
                    break;

                case PackageType.ByeBye:
                    PackageReceived.ByeBye++;
                    ProcessByeByeMessage(package);
                    break;

                case PackageType.Clipboard:
                    PackageReceived.Clipboard++;
                    if (!RunOnLogonDesktop && !RunOnScrSaverDesktop)
                    {
                        clipboardCopiedTime = GetTick();
                        GetNameOfMachineWithClipboardData(package);
                        SignalBigClipboardData();
                    }

                    break;

                case PackageType.MachineSwitched:
                    if (GetTick() - clipboardCopiedTime < BIG_CLIPBOARD_DATA_TIMEOUT && (package.Des == MachineID))
                    {
                        clipboardCopiedTime = 0;
                        Common.GetRemoteClipboard("PackageType.MachineSwitched");
                    }

                    break;

                case PackageType.ClipboardCapture:
                    PackageReceived.Clipboard++;
                    if (!RunOnLogonDesktop && !RunOnScrSaverDesktop)
                    {
                        if (package.Des == MachineID || package.Des == ID.ALL)
                        {
                            GetNameOfMachineWithClipboardData(package);
                            GetRemoteClipboard("mspaint," + LastMachineWithClipboardData);
                        }
                    }

                    break;

                case PackageType.CaptureScreenCommand:
                    PackageReceived.Clipboard++;
                    if (package.Des == MachineID || package.Des == ID.ALL)
                    {
                        Common.SendImage(package.Src, Common.CaptureScreen());
                    }

                    break;

                case PackageType.ClipboardAsk:
                    PackageReceived.ClipboardAsk++;

                    if (package.Des == MachineID)
                    {
                        _ = Task.Run(() =>
                        {
                            try
                            {
                                System.Threading.Thread thread = Thread.CurrentThread;
                                thread.Name = $"{nameof(PackageType.ClipboardAsk)}.{thread.ManagedThreadId}";
                                Thread.UpdateThreads(thread);

                                string remoteMachine = package.MachineName;
                                System.Net.Sockets.TcpClient client = ConnectToRemoteClipboardSocket(remoteMachine);
                                bool clientPushData = true;

                                if (ShakeHand(ref remoteMachine, client.Client, out Stream enStream, out Stream deStream, ref clientPushData, ref package.PostAction))
                                {
                                    SocketStuff.SendClipboardData(client.Client, enStream);
                                }
                            }
                            catch (Exception e)
                            {
                                Log(e);
                            }
                        });
                    }

                    break;

                case PackageType.ClipboardDragDrop:
                    PackageReceived.ClipboardDragDrop++;
                    DragDropStep08(package);
                    break;

                case PackageType.ClipboardDragDropOperation:
                    PackageReceived.ClipboardDragDrop++;
                    DragDropStep08_2(package);
                    break;

                case PackageType.ClipboardDragDropEnd:
                    PackageReceived.ClipboardDragDropEnd++;
                    DragDropStep12();
                    break;

                case PackageType.ClipboardText:
                case PackageType.ClipboardImage:
                    clipboardCopiedTime = 0;
                    if (package.Type == PackageType.ClipboardImage)
                    {
                        PackageReceived.ClipboardImage++;
                    }
                    else
                    {
                        PackageReceived.ClipboardText++;
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
                    HasSwitchedMachineSinceLastCopy = true;
                    HideMouseCursor(true);
                    MainFormDotEx(false);
                    ReleaseAllKeys();
                    break;

                default:
                    if ((package.Type & PackageType.Matrix) == PackageType.Matrix)
                    {
                        PackageReceived.Matrix++;
                        UpdateMachineMatrix(package);
                        break;
                    }
                    else
                    {
                        // We should never get to this point!
                        Common.Log("Invalid package received!");
                        return;
                    }
            }
        }

        private static void GetNameOfMachineWithClipboardData(DATA package)
        {
            LastIDWithClipboardData = package.Src;
            List<MachineInf> matchingMachines = Common.MachinePool.TryFindMachineByID(LastIDWithClipboardData);
            if (matchingMachines.Count >= 1)
            {
                LastMachineWithClipboardData = matchingMachines[0].Name.Trim();
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
            LogDebug("SignalBigClipboardData");
            SetToggleIcon(new int[TOGGLE_ICONS_SIZE] { ICON_BIG_CLIPBOARD, -1, ICON_BIG_CLIPBOARD, -1 });
        }
    }
}
