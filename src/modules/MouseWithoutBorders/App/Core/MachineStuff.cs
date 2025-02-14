// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

using Microsoft.PowerToys.Telemetry;
using MouseWithoutBorders.Class;

// <summary>
//     Machine setup/switching implementation.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
namespace MouseWithoutBorders.Core;

internal static class MachineStuff
{
    private static readonly Lock McMatrixLock = new();

    internal const byte MAX_MACHINE = 4;
    private const byte MAX_SOCKET = MAX_MACHINE * 2;
    internal const long HEARTBEAT_TIMEOUT = 1500000; // 30 Mins
    private const int SKIP_PIXELS = 1;
    private const int JUMP_PIXELS = 2;

#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
    internal static ID desMachineID;
#pragma warning restore SA1307
#pragma warning disable SA1306 // Field should begin with a lower-case letter
    internal static string DesMachineName = string.Empty;
#pragma warning restore SA1306
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
    internal static ID newDesMachineID;
    internal static ID newDesMachineIdEx;
    internal static ID dropMachineID;
    internal static long lastJump = Common.GetTick();
    internal static MyRectangle desktopBounds = new();
    internal static MyRectangle primaryScreenBounds = new();
#pragma warning restore SA1307
    private static MachinePool _machinePool;

    internal static MachinePool MachinePool
    {
        get
        {
            _machinePool ??= new MachinePool();
            return _machinePool;
        }
    }

    internal static MyRectangle PrimaryScreenBounds => MachineStuff.primaryScreenBounds;

#pragma warning disable SA1306 // Field should begin with a lower-case letter
    internal static MouseLocation SwitchLocation = new();
#pragma warning restore SA1306

    internal static ID NewDesMachineID
    {
        get => MachineStuff.newDesMachineID;
        set => MachineStuff.newDesMachineID = value;
    }

    internal static MyRectangle DesktopBounds => MachineStuff.desktopBounds;

#if OLD_VERSION
    static bool MoveToMyNeighbourIfNeeded(int x, int y)
    {
        if (Math.Abs(x) > 10) LastX = x;
        if (Math.Abs(y) > 10) LastY = y;
        if (GetTick() - lastJump < 500 || desMachineID == IP.ALL) return false;
        if (desMachineID == machineID)
        {
            if (x < desktopBounds.Left + skipPixels) return MoveLeft(x, y, x - desktopBounds.Left, 0);
        }
        else
        {
            if (x < primaryScreenBounds.Left + skipPixels)
            {
                if (MoveLeft(x, y, x - primaryScreenBounds.Left, 0))
                {
                    return true;
                }
                else
                {
                    if (desktopBounds.Left < primaryScreenBounds.Left)
                    {
                        RequestedX_Ex = primaryScreenBounds.Left;
                        RequestedY_Ex = y;
                        return true;
                    }
                }
            }
        }

        if (desMachineID == machineID)
        {
            if (x > desktopBounds.Right - skipPixels) return MoveRight(x, y, x - desktopBounds.Right, 0);
        }
        else
        {
            if (x > primaryScreenBounds.Right - skipPixels)
            {
                if (MoveRight(x, y, x - primaryScreenBounds.Right, 0))
                {
                    return true;
                }
                else
                {
                    if (desktopBounds.Right > primaryScreenBounds.Right)
                    {
                        RequestedX_Ex = primaryScreenBounds.Right;
                        RequestedY_Ex = y;
                        return true;
                    }
                }
            }
        }

        if (desMachineID == machineID)
        {
            if (y < desktopBounds.Top + skipPixels) return MoveUp(x, y, 0, y - desktopBounds.Top);
        }
        else
        {
            if (y < primaryScreenBounds.Top + skipPixels)
            {
                if (MoveUp(x, y, 0, y - primaryScreenBounds.Top))
                {
                    return true;
                }
                else
                {
                    if (desktopBounds.Top < primaryScreenBounds.Top)
                    {
                        RequestedX_Ex = x;
                        RequestedY_Ex = primaryScreenBounds.Top;
                        return true;
                    }
                }
            }
        }

        if (desMachineID == machineID)
        {
            if (y > desktopBounds.Bottom - skipPixels) return MoveDown(x, y, 0, y - desktopBounds.Bottom);
        }
        else
        {
            if (y > primaryScreenBounds.Bottom - skipPixels)
            {
                if (MoveDown(x, y, 0, y - primaryScreenBounds.Bottom))
                {
                    return true;
                }
                else
                {
                    if (desktopBounds.Bottom > primaryScreenBounds.Bottom)
                    {
                        RequestedX_Ex = x;
                        RequestedY_Ex = primaryScreenBounds.Bottom;
                        return true;
                    }
                }
            }
        }

        return false;
    }
#else

    private static Point ConvertToUniversalValue(Point p, MyRectangle r)
    {
        if (!p.IsEmpty)
        {
            p.X = (p.X - r.Left) * 65535 / (r.Right - r.Left);
            p.Y = (p.Y - r.Top) * 65535 / (r.Bottom - r.Top);
        }

        return p;
    }

    /* Let's say we have 3 machines A, B, and C. A is the controller machine.
     * (x, y) is the current Mouse position in pixel.
     * If Setting.Values.MoveMouseRelatively then (x, y) can be from any machine having the value bounded by desktopBounds (can be negative)
     * Else (x, y) is from the controller machine which is bounded by ONLY primaryScreenBounds (>=0);
     *
     * The return point is from 0 to 65535 which is then mapped to the desktop of the new controlled machine by the SendInput method.
     *  Let's say user is switching from machine B to machine C:
     *      If Setting.Values.MoveMouseRelatively the this method is called by B and the return point is calculated by B and sent back to A, A will use it to move Mouse to the right position when switching to C.
     *      Else this method is called by A and the return point is calculated by A.
     * */

    internal static Point MoveToMyNeighbourIfNeeded(int x, int y, ID desMachineID)
    {
        newDesMachineIdEx = desMachineID;

        if (Math.Abs(x) > 10)
        {
            Common.LastX = x;
        }

        if (Math.Abs(y) > 10)
        {
            Common.LastY = y;
        }

        if ((Common.GetTick() - lastJump < 100) || desMachineID == ID.ALL)
        {
            return Point.Empty;
        }

        if (Setting.Values.BlockMouseAtCorners)
        {
            lock (Common.SensitivePoints)
            {
                foreach (Point p in Common.SensitivePoints)
                {
                    if (Math.Abs(p.X - x) < 100 && Math.Abs(p.Y - y) < 100)
                    {
                        return Point.Empty;
                    }
                }
            }
        }

        /* If Mouse is moving in the controller machine and this method is called by the controller machine.
         * Or if Mouse is moving in the controlled machine and this method is called by the controlled machine and Setting.Values.MoveMouseRelative.
         * */
        if (desMachineID == Common.MachineID)
        {
            if (x < desktopBounds.Left + SKIP_PIXELS)
            {
                return MoveLeft(x, y);
            }
            else if (x >= desktopBounds.Right - SKIP_PIXELS)
            {
                return MoveRight(x, y);
            }
            else if (y < desktopBounds.Top + SKIP_PIXELS)
            {
                return MoveUp(x, y);
            }
            else if (y >= desktopBounds.Bottom - SKIP_PIXELS)
            {
                return MoveDown(x, y);
            }
        }

        /* If Mouse is moving in the controlled machine and this method is called by the controller machine and !Setting.Values.MoveMouseRelative.
         * Mouse location is scaled from the primary screen bound of the controller machine regardless of how many monitors the controlled machine may have.
         * */
        else
        {
            if (x < primaryScreenBounds.Left + SKIP_PIXELS)
            {
                return MoveLeft(x, y);
            }
            else if (x >= primaryScreenBounds.Right - SKIP_PIXELS)
            {
                return MoveRight(x, y);
            }
            else if (y < primaryScreenBounds.Top + SKIP_PIXELS)
            {
                return MoveUp(x, y);
            }
            else if (y >= primaryScreenBounds.Bottom - SKIP_PIXELS)
            {
                return MoveDown(x, y);
            }
        }

        return Point.Empty;
    }

#endif

    [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Dotnet port with style preservation")]
    private static Point MoveRight(int x, int y)
    {
        string[] mc = LiveMachineMatrix;
        if (mc == null)
        {
            return Point.Empty;
        }

        bool oneRow = Setting.Values.MatrixOneRow;

        string currentMachine = NameFromID(desMachineID);
        if (currentMachine == null)
        {
            return Point.Empty;
        }

        ID newID;
        if (oneRow)
        {
            bool found = false;
            for (int i = 0; i < MAX_MACHINE; i++)
            {
                if (currentMachine.Trim().Equals(mc[i], StringComparison.OrdinalIgnoreCase))
                {
                    for (int j = i; j < MAX_MACHINE - 1; j++)
                    {
                        if (mc[j + 1] != null && mc[j + 1].Length > 0)
                        {
                            if ((newID = IdFromName(mc[j + 1])) > 0)
                            {
                                newDesMachineIdEx = newID;
                                found = true;
                                break;
                            }
                        }
                    }

                    if (!found && Setting.Values.MatrixCircle)
                    {
                        for (int j = 0; j < i; j++)
                        {
                            if (mc[j] != null && mc[j].Length > 0)
                            {
                                if ((newID = IdFromName(mc[j])) > 0)
                                {
                                    newDesMachineIdEx = newID;
                                    break;
                                }
                            }
                        }
                    }

                    break;
                }
            }
        }
        else
        {
            if (currentMachine.Trim().Equals(mc[0], StringComparison.OrdinalIgnoreCase) && (mc[1] != null)
                && (mc[1].Length > 0))
            {
                if ((newID = IdFromName(mc[1])) > 0)
                {
                    newDesMachineIdEx = newID;
                }
            }
            else if (currentMachine.Trim().Equals(mc[2], StringComparison.OrdinalIgnoreCase) && (mc[3] != null)
                && (mc[3].Length > 0))
            {
                if ((newID = IdFromName(mc[3])) > 0)
                {
                    newDesMachineIdEx = newID;
                }
            }
            else if (Setting.Values.MatrixCircle && currentMachine.Trim().Equals(mc[1], StringComparison.OrdinalIgnoreCase) && (mc[0] != null)
                && (mc[0].Length > 0))
            {
                if ((newID = IdFromName(mc[0])) > 0)
                {
                    newDesMachineIdEx = newID;
                }
            }
            else if (Setting.Values.MatrixCircle && currentMachine.Trim().Equals(mc[3], StringComparison.OrdinalIgnoreCase) && (mc[2] != null)
                && (mc[2].Length > 0))
            {
                if ((newID = IdFromName(mc[2])) > 0)
                {
                    newDesMachineIdEx = newID;
                }
            }
        }

        // THIS LOGIC IS THE SAME FOR Move*(int x, int y) METHODS.
        if (newDesMachineIdEx != desMachineID)
        {
            Logger.LogDebug("Move Right");

            if (!Setting.Values.MoveMouseRelatively)
            {
                if (newDesMachineIdEx == Common.MachineID)
                {
                    /* Switching back to the controller machine, we need to scale up to the desktopBounds from primaryScreenBounds (sine !Setting.Values.MoveMouseRelatively).
                     * primaryScreenBounds => 65535 => desktopBounds, so that the Mouse position is mapped to the right position when the controller machine has multiple monitors.
                     * */
                    return ConvertToUniversalValue(new Point(primaryScreenBounds.Left + JUMP_PIXELS, y), primaryScreenBounds);
                }
                else
                {
                    if (desMachineID == Common.MachineID)
                    {
                        /* Switching FROM the controller machine, since Mouse was not bounded/locked to the primary screen,
                         * Mouse position can just be mapped from desktopBounds to desktopBounds
                         * desktopBounds => 65535 => desktopBounds.
                         * */
                        return ConvertToUniversalValue(new Point(desktopBounds.Left + JUMP_PIXELS, y), desktopBounds);
                    }
                    else
                    {
                        /* Switching between two machines where non of them is the controller machine.
                         * Since the current Mouse position is "mapped" from the primary monitor of the controller machine,
                         * new Mouse position for the new controlled machine needs to be calculated from this as well.
                         * primaryScreenBounds => 65535 => desktopBounds
                         * */
                        return ConvertToUniversalValue(new Point(primaryScreenBounds.Left + JUMP_PIXELS, y), primaryScreenBounds);
                    }
                }
            }
            else
            {
                /* In the case where Mouse is moved relatively, Mouse position is simply mapped from desktopBounds to desktopBounds.
                 * desktopBounds => 65535 => desktopBounds.
                 * */
                return ConvertToUniversalValue(new Point(desktopBounds.Left + JUMP_PIXELS, y), desktopBounds);
            }
        }

        return Point.Empty;
    }

    [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Dotnet port with style preservation")]
    private static Point MoveLeft(int x, int y)
    {
        string[] mc = LiveMachineMatrix;
        if (mc == null)
        {
            return Point.Empty;
        }

        bool oneRow = Setting.Values.MatrixOneRow;

        string currentMachine = NameFromID(desMachineID);
        if (currentMachine == null)
        {
            return Point.Empty;
        }

        ID newID;
        if (oneRow)
        {
            bool found = false;
            for (int i = MAX_MACHINE - 1; i >= 0; i--)
            {
                if (currentMachine.Trim().Equals(mc[i], StringComparison.OrdinalIgnoreCase))
                {
                    for (int j = i; j > 0; j--)
                    {
                        if (mc[j - 1] != null && mc[j - 1].Length > 0)
                        {
                            if ((newID = IdFromName(mc[j - 1])) != ID.NONE)
                            {
                                newDesMachineIdEx = newID;
                                found = true;
                                break;
                            }
                        }
                    }

                    if (!found && Setting.Values.MatrixCircle)
                    {
                        for (int j = MAX_MACHINE - 1; j > i; j--)
                        {
                            if (mc[j] != null && mc[j].Length > 0)
                            {
                                if ((newID = IdFromName(mc[j])) != ID.NONE)
                                {
                                    newDesMachineIdEx = newID;
                                    break;
                                }
                            }
                        }
                    }

                    break;
                }
            }
        }
        else
        {
            if (currentMachine.Trim().Equals(mc[1], StringComparison.OrdinalIgnoreCase) && (mc[0] != null)
                && (mc[0].Length > 0))
            {
                if ((newID = IdFromName(mc[0])) != ID.NONE)
                {
                    newDesMachineIdEx = newID;
                }
            }
            else if (currentMachine.Trim().Equals(mc[3], StringComparison.OrdinalIgnoreCase) && (mc[2] != null)
                && (mc[2].Length > 0))
            {
                if ((newID = IdFromName(mc[2])) != ID.NONE)
                {
                    newDesMachineIdEx = newID;
                }
            }
            else if (Setting.Values.MatrixCircle && currentMachine.Trim().Equals(mc[0], StringComparison.OrdinalIgnoreCase) && (mc[1] != null)
                && (mc[1].Length > 0))
            {
                if ((newID = IdFromName(mc[1])) != ID.NONE)
                {
                    newDesMachineIdEx = newID;
                }
            }
            else if (Setting.Values.MatrixCircle && currentMachine.Trim().Equals(mc[2], StringComparison.OrdinalIgnoreCase) && (mc[3] != null)
                && (mc[3].Length > 0))
            {
                if ((newID = IdFromName(mc[3])) != ID.NONE)
                {
                    newDesMachineIdEx = newID;
                }
            }
        }

        if (newDesMachineIdEx != desMachineID)
        {
            Logger.LogDebug("Move Left");

            return !Setting.Values.MoveMouseRelatively
                ? newDesMachineIdEx == Common.MachineID
                    ? ConvertToUniversalValue(new Point(primaryScreenBounds.Right - JUMP_PIXELS, y), primaryScreenBounds)
                    : desMachineID == Common.MachineID
                        ? ConvertToUniversalValue(new Point(desktopBounds.Right - JUMP_PIXELS, y), desktopBounds)
                        : ConvertToUniversalValue(new Point(primaryScreenBounds.Right - JUMP_PIXELS, y), primaryScreenBounds)
                : ConvertToUniversalValue(new Point(desktopBounds.Right - JUMP_PIXELS, y), desktopBounds);
        }

        return Point.Empty;
    }

    private static Point MoveUp(int x, int y)
    {
        if (Setting.Values.MatrixOneRow)
        {
            return Point.Empty;
        }

        string[] mc = LiveMachineMatrix;
        if (mc == null)
        {
            return Point.Empty;
        }

        string currentMachine = NameFromID(desMachineID);
        if (currentMachine == null)
        {
            return Point.Empty;
        }

        ID newID;
        if (currentMachine.Trim().Equals(mc[2], StringComparison.OrdinalIgnoreCase) && (mc[0] != null)
            && (mc[0].Length > 0))
        {
            if ((newID = IdFromName(mc[0])) != ID.NONE)
            {
                newDesMachineIdEx = newID;
            }
        }
        else if (currentMachine.Trim().Equals(mc[3], StringComparison.OrdinalIgnoreCase) && (mc[1] != null)
            && (mc[1].Length > 0))
        {
            if ((newID = IdFromName(mc[1])) != ID.NONE)
            {
                newDesMachineIdEx = newID;
            }
        }
        else if (Setting.Values.MatrixCircle && currentMachine.Trim().Equals(mc[0], StringComparison.OrdinalIgnoreCase) && (mc[2] != null)
            && (mc[2].Length > 0))
        {
            if ((newID = IdFromName(mc[2])) != ID.NONE)
            {
                newDesMachineIdEx = newID;
            }
        }
        else if (Setting.Values.MatrixCircle && currentMachine.Trim().Equals(mc[1], StringComparison.OrdinalIgnoreCase) && (mc[3] != null)
            && (mc[3].Length > 0))
        {
            if ((newID = IdFromName(mc[3])) != ID.NONE)
            {
                newDesMachineIdEx = newID;
            }
        }

        if (newDesMachineIdEx != desMachineID)
        {
            Logger.LogDebug("Move Up");

            return !Setting.Values.MoveMouseRelatively
                ? newDesMachineIdEx == Common.MachineID
                    ? ConvertToUniversalValue(new Point(x, primaryScreenBounds.Bottom - JUMP_PIXELS), primaryScreenBounds)
                    : desMachineID == Common.MachineID
                        ? ConvertToUniversalValue(new Point(x, desktopBounds.Bottom - JUMP_PIXELS), desktopBounds)
                        : ConvertToUniversalValue(new Point(x, primaryScreenBounds.Bottom - JUMP_PIXELS), primaryScreenBounds)
                : ConvertToUniversalValue(new Point(x, desktopBounds.Bottom - JUMP_PIXELS), desktopBounds);
        }

        return Point.Empty;
    }

    private static Point MoveDown(int x, int y)
    {
        if (Setting.Values.MatrixOneRow)
        {
            return Point.Empty;
        }

        string[] mc = LiveMachineMatrix;
        if (mc == null)
        {
            return Point.Empty;
        }

        string currentMachine = NameFromID(desMachineID);
        if (currentMachine == null)
        {
            return Point.Empty;
        }

        ID newID;
        if (currentMachine.Trim().Equals(mc[0], StringComparison.OrdinalIgnoreCase) && (mc[2] != null)
            && (mc[2].Length > 0))
        {
            if ((newID = IdFromName(mc[2])) != ID.NONE)
            {
                newDesMachineIdEx = newID;
            }
        }
        else if (currentMachine.Trim().Equals(mc[1], StringComparison.OrdinalIgnoreCase) && (mc[3] != null)
            && (mc[3].Length > 0))
        {
            if ((newID = IdFromName(mc[3])) != ID.NONE)
            {
                newDesMachineIdEx = newID;
            }
        }

        if (Setting.Values.MatrixCircle && currentMachine.Trim().Equals(mc[2], StringComparison.OrdinalIgnoreCase) && (mc[0] != null)
            && (mc[0].Length > 0))
        {
            if ((newID = IdFromName(mc[0])) != ID.NONE)
            {
                newDesMachineIdEx = newID;
            }
        }
        else if (Setting.Values.MatrixCircle && currentMachine.Trim().Equals(mc[3], StringComparison.OrdinalIgnoreCase) && (mc[1] != null)
            && (mc[1].Length > 0))
        {
            if ((newID = IdFromName(mc[1])) != ID.NONE)
            {
                newDesMachineIdEx = newID;
            }
        }

        if (newDesMachineIdEx != desMachineID)
        {
            Logger.LogDebug("Move Down");

            return !Setting.Values.MoveMouseRelatively
                ? newDesMachineIdEx == Common.MachineID
                    ? ConvertToUniversalValue(new Point(x, primaryScreenBounds.Top + JUMP_PIXELS), primaryScreenBounds)
                    : desMachineID == Common.MachineID
                        ? ConvertToUniversalValue(new Point(x, desktopBounds.Top + JUMP_PIXELS), desktopBounds)
                        : ConvertToUniversalValue(new Point(x, primaryScreenBounds.Top + JUMP_PIXELS), primaryScreenBounds)
                : ConvertToUniversalValue(new Point(x, desktopBounds.Top + JUMP_PIXELS), desktopBounds);
        }

        return Point.Empty;
    }

    internal static bool RemoveDeadMachines(ID ip)
    {
        bool rv = false;

        // Here we are removing a dead machine by IP.
        foreach (MachineInf inf in MachineStuff.MachinePool.ListAllMachines())
        {
            if (inf.Id == ip)
            {
                if (MachinePool.SetMachineDisconnected(inf.Name))
                {
                    rv = true;
                }

                Logger.LogDebug("<><><><><>>><><><<><><><><><><><><><><>><><><><><><><><><><><" + inf.Name);
            }
        }

        return rv;
    }

    internal static void RemoveDeadMachines()
    {
        // list of live/dead machines is now automatically up-to-date
        // if it changed we need to update the UI.
        // for now assume it changed.
        // Common.MachinePool.ResetIPAddressesForDeadMachines();
        // DoSomethingInUIThread(UpdateMenu);
        MachineStuff.UpdateMachinePoolStringSetting();

        // Make sure MachinePool still holds this machine.
        if (MachineStuff.MachinePool.LearnMachine(Common.MachineName))
        {
            _ = MachineStuff.MachinePool.TryUpdateMachineID(Common.MachineName, Common.MachineID, false);
        }
    }

    internal static string AddToMachinePool(DATA package)
    {
        // Log("********** AddToMachinePool called: " + package.src.ToString(CultureInfo.InvariantCulture));

        // There should be no duplicates in machine pool.
        string name = package.MachineName;

        // a few things happening here:
        // 1) find a matching machine (by name)
        // 2) update its ID and time
        // 3) logging
        // 4) updating some variables - desMachineID/newDesMachineID
        // 5) return the matched name (trimmed) - only in the event of a match
        if (MachineStuff.MachinePool.TryFindMachineByName(name, out MachineInf machineInfo))
        {
            _ = MachineStuff.MachinePool.TryUpdateMachineID(machineInfo.Name, machineInfo.Id, true);

            _ = MachineStuff.MachinePool.TryUpdateMachineID(machineInfo.Name, package.Src, true);

            if (machineInfo.Name.Equals(DesMachineName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogDebug("AddToMachinePool: Des ID updated: " + Common.DesMachineID.ToString() + "/" + package.Src.ToString());
                newDesMachineID = desMachineID = package.Src;
            }

            return machineInfo.Name;
        }
        else
        {
            if (MachineStuff.MachinePool.LearnMachine(name))
            {
                _ = MachineStuff.MachinePool.TryUpdateMachineID(name, package.Src, true);
            }
            else
            {
                Logger.LogDebug("AddToMachinePool: could not add a new machine: " + name);
                return "The 5th machine";
            }
        }

        // if (machineCount != saved)
        {
            // DoSomethingInUIThread(UpdateMenu);
            MachineStuff.UpdateMachinePoolStringSetting();
        }

        // NOTE(yuyoyuppe): automatically active "bidirectional" control between the machines.
        string[] st = new string[MachineStuff.MAX_MACHINE];
        Array.Fill(st, string.Empty);
        var machines = MachineStuff.MachinePool.ListAllMachines();
        for (int i = 0; i < machines.Count; ++i)
        {
            if (machines[i].Id != ID.NONE && machines[i].Id != ID.ALL)
            {
                st[i] = machines[i].Name;
            }
        }

        MachineStuff.MachineMatrix = st;
        Common.ReopenSockets(true);
        MachineStuff.SendMachineMatrix();

        Logger.LogDebug("Machine added: " + name + "/" + package.Src.ToString());
        UpdateClientSockets("AddToMachinePool");
        return name;
    }

    internal static void UpdateClientSockets(string logHeader)
    {
        Logger.LogDebug("UpdateClientSockets: " + logHeader);
        Common.Sk?.UpdateTCPClients();
    }

    private static SettingsForm settings;

    internal static SettingsForm Settings
    {
        get => MachineStuff.settings;
        set => MachineStuff.settings = value;
    }

    internal static void ShowSetupForm(bool reopenSockets = false)
    {
        Logger.LogDebug("========== BEGIN THE SETUP EXPERIENCE ==========", true);
        Setting.Values.MyKey = Common.MyKey = Common.CreateRandomKey();
        Common.GeneratedKey = true;

        if (Process.GetCurrentProcess().SessionId != NativeMethods.WTSGetActiveConsoleSessionId())
        {
            Logger.Log("Not physical console session.");
            _ = MessageBox.Show(
                "Please run the program in the physical console session.\r\nThe program does not work in a remote desktop or virtual machine session.",
                Application.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Stop);
            return;
        }

        if (settings == null)
        {
            settings = new SettingsForm();
            settings.Show();
        }
        else
        {
            settings.Close();
            Common.MMSleep(0.3);
            settings = new SettingsForm();
            settings.Show();
        }

        if (reopenSockets)
        {
            Common.ReopenSockets(true);
        }
    }

    internal static void CloseSetupForm()
    {
        if (settings != null)
        {
            settings.Close();
            settings = null;
        }
    }

    internal static void ShowMachineMatrix()
    {
        if (!Setting.Values.ShowOriginalUI)
        {
            return;
        }

        if (Process.GetCurrentProcess().SessionId != NativeMethods.WTSGetActiveConsoleSessionId())
        {
            Common.ShowToolTip(Application.ProductName + " cannot be used in a remote desktop or virtual machine session.", 5000);
        }

#if NEW_SETTINGS_FORM
        Common.ShowSetupForm();
#else
        if (Setting.Values.FirstRun && !Common.AtLeastOneSocketConnected())
        {
            MachineStuff.ShowSetupForm();
        }
        else
        {
            PowerToysTelemetry.Log.WriteEvent(new MouseWithoutBorders.Telemetry.MouseWithoutBordersOldUIOpenedEvent());

            if (Common.MatrixForm == null)
            {
                Common.MatrixForm = new FrmMatrix();
                Common.MatrixForm.Show();

                if (Common.MainForm != null)
                {
                    Common.MainForm.NotifyIcon.Visible = false;
                    Common.MainForm.NotifyIcon.Visible = Setting.Values.ShowOriginalUI;
                }
            }
            else
            {
                Common.MatrixForm.WindowState = FormWindowState.Normal;
                Common.MatrixForm.Activate();
            }
        }
#endif
    }

    private static string[] mcMatrix;

    internal static string[] MachineMatrix
    {
        get
        {
            lock (McMatrixLock)
            {
                if (mcMatrix == null)
                {
                    string s = Setting.Values.MachineMatrixString;

                    if (!string.IsNullOrEmpty(s))
                    {
                        mcMatrix = s.Split(new char[] { ',' });

                        if (mcMatrix == null || mcMatrix.Length != MAX_MACHINE)
                        {
                            mcMatrix = new string[MAX_MACHINE] { string.Empty, string.Empty, string.Empty, string.Empty };
                        }
                    }
                    else
                    {
                        mcMatrix = new string[MAX_MACHINE] { string.Empty, string.Empty, string.Empty, string.Empty };
                    }
                }

                return mcMatrix;
            }
        }

        set
        {
            lock (McMatrixLock)
            {
                if (value == null)
                {
                    mcMatrix = null; // Force read from registry next time.
                    return;
                }
                else
                {
                    Setting.Values.MachineMatrixString = string.Join(",", mcMatrix = value);
                }
            }

            Common.DoSomethingInUIThread(() =>
            {
                Common.MainForm.ChangeIcon(-1);
                Common.MainForm.UpdateNotifyIcon();
            });
        }
    }

    private static string[] LiveMachineMatrix
    {
        get
        {
            bool twoRow = !Setting.Values.MatrixOneRow;
            string[] connectedMachines = twoRow ? MachineMatrix : MachineMatrix.Select(m => Common.IsConnectedTo(IdFromName(m)) ? m : string.Empty).ToArray();
            Logger.LogDebug($"Matrix: {string.Join(",", MachineMatrix)}, Connected: {string.Join(",", connectedMachines)}");

            return connectedMachines;
        }
    }

    internal static void UpdateMachinePoolStringSetting()
    {
        Setting.Values.MachinePoolString = MachineStuff.MachinePool.SerializedAsString();
    }

    internal static void SendMachineMatrix()
    {
        if (MachineMatrix == null)
        {
            return;
        }

        DATA package = new();

        for (int i = 0; i < MachineMatrix.Length; i++)
        {
            package.MachineName = MachineMatrix[i];

            package.Type = PackageType.Matrix
                | (Setting.Values.MatrixCircle ? PackageType.MatrixSwapFlag : 0)
                | (Setting.Values.MatrixOneRow ? 0 : PackageType.MatrixTwoRowFlag);

            package.Src = (ID)(i + 1);
            package.Des = ID.ALL;

            Common.SkSend(package, null, false);

            Logger.LogDebug($"matrixIncludedMachine sent: [{i + 1}]:[{MachineMatrix[i]}]");
        }
    }

    internal static void UpdateMachineMatrix(DATA package)
    {
        uint i = (uint)package.Src;
        string matrixIncludedMachine = package.MachineName;

        if (i is > 0 and <= MAX_MACHINE)
        {
            Logger.LogDebug($"matrixIncludedMachine: [{i}]:[{matrixIncludedMachine}]");

            MachineMatrix[i - 1] = matrixIncludedMachine;

            if (i == MAX_MACHINE)
            {
                Setting.Values.MatrixCircle = (package.Type & PackageType.MatrixSwapFlag) == PackageType.MatrixSwapFlag;
                Setting.Values.MatrixOneRow = !((package.Type & PackageType.MatrixTwoRowFlag) == PackageType.MatrixTwoRowFlag);
                MachineMatrix = MachineMatrix; // Save

                Common.ReopenSocketDueToReadError = true;

                UpdateClientSockets("UpdateMachineMatrix");

                Setting.Values.Changed = true;
            }
        }
        else
        {
            Logger.LogDebug("Invalid machine Matrix package!");
        }
    }

    internal static void SwitchToMachine(string name)
    {
        ID id = MachineStuff.MachinePool.ResolveID(name);

        if (id != ID.NONE)
        {
            // Ask current machine to hide the Mouse cursor
            if (desMachineID != Common.MachineID)
            {
                Common.SendPackage(desMachineID, PackageType.HideMouse);
            }

            NewDesMachineID = Common.DesMachineID = id;
            SwitchLocation.X = Common.XY_BY_PIXEL + primaryScreenBounds.Left + ((primaryScreenBounds.Right - primaryScreenBounds.Left) / 2);
            SwitchLocation.Y = Common.XY_BY_PIXEL + primaryScreenBounds.Top + ((primaryScreenBounds.Bottom - primaryScreenBounds.Top) / 2);
            SwitchLocation.ResetCount();
            Common.UpdateMultipleModeIconAndMenu();
            Common.HideMouseCursor(false);
            _ = Common.EvSwitch.Set();
        }
    }

    internal static void SwitchToMultipleMode(bool multipleMode, bool centerScreen)
    {
        if (multipleMode)
        {
            PowerToysTelemetry.Log.WriteEvent(new MouseWithoutBorders.Telemetry.MouseWithoutBordersMultipleModeEvent());
            NewDesMachineID = Common.DesMachineID = ID.ALL;
        }
        else
        {
            NewDesMachineID = Common.DesMachineID = Common.MachineID;
        }

        if (centerScreen)
        {
            Common.MoveMouseToCenter();
        }

        Common.ReleaseAllKeys();

        Common.UpdateMultipleModeIconAndMenu();
    }

    internal static bool CheckSecondInstance(bool sendMessage = false)
    {
        int h;

        if ((h = NativeMethods.FindWindow(null, Setting.Values.MyID)) > 0)
        {
            return true;
        }

        return false;
    }

#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
    internal static EventWaitHandle oneInstanceCheck;
#pragma warning restore SA1307

    internal static void AssertOneInstancePerDesktopSession()
    {
        string eventName = $"Global\\{Application.ProductName}-{FrmAbout.AssemblyVersion}-{Common.GetMyDesktop()}-{Common.CurrentProcess.SessionId}";
        oneInstanceCheck = new EventWaitHandle(false, EventResetMode.ManualReset, eventName, out bool created);

        if (!created)
        {
            Logger.TelemetryLogTrace($"Second instance found: {eventName}.", SeverityLevel.Warning, true);
            Common.CurrentProcess.KillProcess(true);
        }
    }

    internal static ID IdFromName(string name)
    {
        return MachineStuff.MachinePool.ResolveID(name);
    }

    internal static string NameFromID(ID id)
    {
        foreach (MachineInf inf in MachineStuff.MachinePool.TryFindMachineByID(id))
        {
            if (!string.IsNullOrEmpty(inf.Name))
            {
                return inf.Name;
            }
        }

        return null;
    }

    internal static bool InMachineMatrix(string name)
    {
        if (MachineMatrix == null || string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        foreach (string st in MachineMatrix)
        {
            if (!string.IsNullOrWhiteSpace(st) && st.Trim().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static void ClearComputerMatrix()
    {
        MachineStuff.MachineMatrix = new string[MachineStuff.MAX_MACHINE] { Common.MachineName.Trim(), string.Empty, string.Empty, string.Empty };
        MachineStuff.MachinePool.Initialize(new string[] { Common.MachineName });
        MachineStuff.UpdateMachinePoolStringSetting();
    }
}
