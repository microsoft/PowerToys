// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;

// <summary>
//     Some other helper methods.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
using Microsoft.Win32;
using MouseWithoutBorders.Class;
using MouseWithoutBorders.Core;
using static System.Windows.Forms.Control;

namespace MouseWithoutBorders
{
    internal partial class Common
    {
        internal const string HELPER_FORM_TEXT = "Mouse without Borders Helper";
        internal const string HelperProcessName = "PowerToys.MouseWithoutBordersHelper";
        private static bool signalHelperToExit;
        private static bool signalWatchDogToExit;
        internal static long WndProcCounter;

        private static void WatchDogThread()
        {
            long oldCounter = WndProcCounter;

            do
            {
                for (int i = 0; i < 10; i++)
                {
                    Thread.Sleep(1000);

                    if (signalWatchDogToExit)
                    {
                        break;
                    }
                }

                while (BlockingUI)
                {
                    Thread.Sleep(1000);
                }

                if (WndProcCounter == oldCounter)
                {
                    Process p = Process.GetCurrentProcess();
                    string procInfo = $"{p.PrivateMemorySize64 / 1024 / 1024}MB, {p.TotalProcessorTime}, {Environment.ProcessorCount}.";
                    string threadStacks = $"{procInfo} {Thread.DumpThreadsStack()}";
                    Logger.TelemetryLogTrace(threadStacks, SeverityLevel.Error);
                    break;
                }

                oldCounter = WndProcCounter;
            }
            while (true);
        }

        private static void HelperThread()
        {
            // SuppressFlow fixes an issue on service mode, where the helper process can't get enough permissions to be started again.
            // More details can be found on: https://github.com/microsoft/PowerToys/pull/36892
            using var asyncFlowControl = System.Threading.ExecutionContext.SuppressFlow();

            try
            {
                while (true)
                {
                    _ = EvSwitch.WaitOne(); // Switching to another machine?

                    if (signalHelperToExit)
                    {
                        break;
                    }

                    if (MachineStuff.NewDesMachineID != Common.MachineID && MachineStuff.NewDesMachineID != ID.ALL)
                    {
                        HideMouseCursor(false);
                        Common.MainFormDotEx(true);
                    }
                    else
                    {
                        if (MachineStuff.SwitchLocation.Count > 0)
                        {
                            MachineStuff.SwitchLocation.Count--;

                            // When we want to move mouse by pixels, we add 300k to x and y (search for XY_BY_PIXEL for other related code).
                            Logger.LogDebug($"+++++ Moving mouse to {MachineStuff.SwitchLocation.X}, {MachineStuff.SwitchLocation.Y}");

                            // MaxXY = 65535 so 100k is safe.
                            if (MachineStuff.SwitchLocation.X > XY_BY_PIXEL - 100000 || MachineStuff.SwitchLocation.Y > XY_BY_PIXEL - 100000)
                            {
                                InputSimulation.MoveMouse(MachineStuff.SwitchLocation.X - XY_BY_PIXEL, MachineStuff.SwitchLocation.Y - XY_BY_PIXEL);
                            }
                            else
                            {
                                InputSimulation.MoveMouseEx(MachineStuff.SwitchLocation.X, MachineStuff.SwitchLocation.Y);
                            }

                            Common.MainFormDot();
                        }
                    }

                    if (MachineStuff.NewDesMachineID == Common.MachineID)
                    {
                        ReleaseAllKeys();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }

            signalHelperToExit = false;
            Logger.LogDebug("^^^Helper Thread exiting...^^^");
        }

        internal static void MainFormDotEx(bool bCheckTS)
        {
            Logger.LogDebug("***** MainFormDotEx:");

            if (!Common.RunOnLogonDesktop && !Common.RunOnScrSaverDesktop)
            {
                int left = MachineStuff.PrimaryScreenBounds.Left + ((MachineStuff.PrimaryScreenBounds.Right - MachineStuff.PrimaryScreenBounds.Left) / 2) - 1;
                int top = Setting.Values.HideMouse ? 3 : MachineStuff.PrimaryScreenBounds.Top + ((MachineStuff.PrimaryScreenBounds.Bottom - MachineStuff.PrimaryScreenBounds.Top) / 2);

                Common.MainFormVisible = true;

                if (Setting.Values.HideMouse && Setting.Values.StealFocusWhenSwitchingMachine && Common.SendMessageToHelper(0x407, new IntPtr(left), new IntPtr(top), true) == 0)
                {
                    try
                    {
                        /* When user just switches to the Logon desktop, user is actually on the "Windows Default Lock Screen" (LockApp).
                         * If a click is sent to this during switch, it actually triggers a desktop switch on the local machine causing a reconnection affecting the machine switch.
                         * We can detect and skip in this case.
                         * */
                        IntPtr foreGroundWindow = NativeMethods.GetForegroundWindow();
                        string foreGroundWindowText = GetText(foreGroundWindow);

                        bool mouseClick = true;

                        if (foreGroundWindowText.Equals("Windows Default Lock Screen", StringComparison.OrdinalIgnoreCase))
                        {
                            mouseClick = false;
                        }

                        // Window title may be localized, check process name:
                        if (mouseClick)
                        {
                            _ = NativeMethods.GetWindowThreadProcessId(foreGroundWindow, out uint pid);

                            if (pid > 0)
                            {
                                string foreGroundWindowProcess = Process.GetProcessById((int)pid)?.ProcessName;

                                if (foreGroundWindowProcess.Equals("LockApp", StringComparison.OrdinalIgnoreCase))
                                {
                                    mouseClick = false;
                                }
                            }
                        }

                        if (mouseClick)
                        {
                            InputSimulation.MouseClickDotForm(left + 1, top + 1);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Log(e);
                    }
                }
            }

            CustomCursor.ShowFakeMouseCursor(int.MinValue, int.MinValue);
        }

        internal static void MainForm3Pixels()
        {
            Logger.LogDebug("***** MainFormDotLarge:");

            DoSomethingInUIThread(
                () =>
            {
                MainForm.Left = MachineStuff.PrimaryScreenBounds.Left + ((MachineStuff.PrimaryScreenBounds.Right - MachineStuff.PrimaryScreenBounds.Left) / 2) - 2;
                MainForm.Top = Setting.Values.HideMouse ? 3 : MachineStuff.PrimaryScreenBounds.Top + ((MachineStuff.PrimaryScreenBounds.Bottom - MachineStuff.PrimaryScreenBounds.Top) / 2) - 1;
                MainForm.Width = 3;
                MainForm.Height = 3;
                MainForm.Opacity = 0.11D;
                MainForm.TopMost = true;

                if (Setting.Values.HideMouse)
                {
                    MainForm.BackColor = Color.Black;
                    MainForm.Show();
                    Common.MainFormVisible = true;
                }
                else
                {
                    MainForm.BackColor = Color.White;
                    MainForm.Hide();
                    Common.MainFormVisible = false;
                }
            },
                true);

            CustomCursor.ShowFakeMouseCursor(int.MinValue, int.MinValue);
        }

        internal static void MainFormDot()
        {
            DoSomethingInUIThread(
                () =>
                {
                    _ = Common.SendMessageToHelper(0x408, IntPtr.Zero, IntPtr.Zero, false);

                    MainForm.Left = MachineStuff.PrimaryScreenBounds.Left + ((MachineStuff.PrimaryScreenBounds.Right - MachineStuff.PrimaryScreenBounds.Left) / 2) - 1;
                    MainForm.Top = Setting.Values.HideMouse ? 3 : MachineStuff.PrimaryScreenBounds.Top + ((MachineStuff.PrimaryScreenBounds.Bottom - MachineStuff.PrimaryScreenBounds.Top) / 2);
                    MainForm.Width = 1;
                    MainForm.Height = 1;
                    MainForm.Opacity = 0.15;
                    MainForm.Hide();
                    Common.MainFormVisible = false;
                },
                true);

            CustomCursor.ShowFakeMouseCursor(int.MinValue, int.MinValue);
        }

        internal static void ToggleIcon()
        {
            try
            {
                if (toggleIconsIndex < TOGGLE_ICONS_SIZE)
                {
                    Common.DoSomethingInUIThread(() => Common.MainForm.ChangeIcon(toggleIcons[toggleIconsIndex++]));
                }
                else
                {
                    toggleIconsIndex = 0;
                    toggleIcons = null;
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        internal static void RunDDHelper(bool cleanUp = false)
        {
            if (Common.RunOnLogonDesktop || Common.RunOnScrSaverDesktop)
            {
                return;
            }

            if (cleanUp)
            {
                try
                {
                    Process[] ps = Process.GetProcessesByName(HelperProcessName);
                    foreach (Process p in ps)
                    {
                        p.KillProcess();
                    }
                }
                catch (Exception e)
                {
                    Logger.Log(e);
                    _ = Common.SendMessageToHelper(SharedConst.QUIT_CMD, IntPtr.Zero, IntPtr.Zero);
                }

                return;
            }

            if (!Common.IsMyDesktopActive())
            {
                return;
            }

            if (!Common.IpcChannelCreated)
            {
                Logger.TelemetryLogTrace($"{nameof(Common.IpcChannelCreated)} = {Common.IpcChannelCreated}. {Logger.GetStackTrace(new StackTrace())}", SeverityLevel.Warning);
                return;
            }

            if (!MainForm.IsDisposed)
            {
                MainForm.NotifyIcon.Visible = false;
                MainForm.NotifyIcon.Visible = Setting.Values.ShowOriginalUI;
            }

            IntPtr h = (IntPtr)NativeMethods.FindWindow(null, Common.HELPER_FORM_TEXT);

            if (h.ToInt32() <= 0)
            {
                _ = Common.CreateProcessInInputDesktopSession(
                    $"\"{Path.GetDirectoryName(Application.ExecutablePath)}\\{HelperProcessName}.exe\"",
                    string.Empty,
                    Common.GetInputDesktop(),
                    0);

                HasSwitchedMachineSinceLastCopy = true;

                // Common.CreateLowIntegrityProcess("\"" + Path.GetDirectoryName(Application.ExecutablePath) + "\\MouseWithoutBordersHelper.exe\"", string.Empty, 0, false, 0);
                var processes = Process.GetProcessesByName(HelperProcessName);
                if (processes?.Length == 0)
                {
                    Logger.Log("Unable to start helper process.");
                    Common.ShowToolTip("Error starting Mouse Without Borders Helper, clipboard sharing will not work!", 5000, ToolTipIcon.Error);
                }
                else
                {
                    Logger.Log("Helper process started.");
                }
            }
            else
            {
                var processes = Process.GetProcessesByName(HelperProcessName);
                if (processes?.Length > 0)
                {
                    Logger.Log("Helper process found running.");
                }
                else
                {
                    Logger.Log("Invalid helper process found running.");
                    Common.ShowToolTip("Error finding Mouse Without Borders Helper, clipboard sharing will not work!", 5000, ToolTipIcon.Error);
                }
            }
        }

        internal static int SendMessageToHelper(int msg, IntPtr wparam, IntPtr lparam, bool wait = true, bool log = true)
        {
            int h = NativeMethods.FindWindow(null, Common.HELPER_FORM_TEXT);
            int rv = -1;

            if (h > 0)
            {
                rv = wait
                    ? (int)NativeMethods.SendMessage((IntPtr)h, msg, wparam, lparam)
                    : NativeMethods.PostMessage((IntPtr)h, msg, wparam, lparam) ? 1 : 0;
            }

            if (log)
            {
                Logger.LogDebug($"SendMessageToHelper: HelperWindow={h}, Return={rv}, msg={msg}, w={wparam.ToInt32()}, l={lparam.ToInt32()}, Post={!wait}");
            }

            return rv;
        }

        internal static bool IsWindows8AndUp()
        {
            return (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor >= 2)
                || Environment.OSVersion.Version.Major > 6;
        }

        internal static string GetMiniLog(IEnumerable<ControlCollection> optionControls)
        {
            string log = string.Empty;

            log += "=============================================================================================================================\r\n";
            log += $"{Application.ProductName} version {Application.ProductVersion}\r\n";

            log += $"{Setting.Values.Username}/{GetDebugInfo(MyKey)}\r\n";
            log += $"{MachineName}/{MachineID}/{DesMachineID}\r\n";
            log += $"Id: {Setting.Values.DeviceId}\r\n";
            log += $"Matrix: {string.Join(",", MachineStuff.MachineMatrix)}\r\n";
            log += $"McPool: {Setting.Values.MachinePoolString}\r\n";

            log += "\r\nOPTIONS:\r\n";

            foreach (ControlCollection controlCollection in optionControls)
            {
                foreach (object c in controlCollection)
                {
                    if (c is CheckBox checkBox)
                    {
                        log += $"({(checkBox.Checked ? 1 : 0)}) {checkBox.Text}\r\n";
                        continue;
                    }

                    if (c is RadioButton radioButton)
                    {
                        log += $"({(radioButton.Checked ? 1 : 0)}) {radioButton.Name}.[{radioButton.Text}]\r\n";
                        continue;
                    }

                    if (c is ComboBox comboBox)
                    {
                        log += $"{comboBox.Name} = {comboBox.Text}\r\n";
                        continue;
                    }
                }
            }

            log += "\r\n";

            SocketStuff sk = Sk;

            if (sk?.TcpSockets != null)
            {
                foreach (TcpSk tcp in sk.TcpSockets)
                {
                    log += $"{Common.MachineName}{(tcp.IsClient ? "=>" : "<=")}{tcp.MachineName}({tcp.MachineId}):{tcp.Status}\r\n";
                }
            }

            log += string.Format(CultureInfo.CurrentCulture, "Helper:{0}\r\n", SendMessageToHelper(0x400, IntPtr.Zero, IntPtr.Zero));

            log += Setting.Values.LastPersonalizeLogonScr + "\r\n";
            log += "Name2IP =\r\n" + Setting.Values.Name2IP + "\r\n";

            log += "Last 10 trace messages:\r\n";

            log += string.Join(Environment.NewLine, Logger.LogCounter.Select(item => $"({item.Value}): {item.Key}").Take(10));

            log += "\r\n=============================================================================================================================";

            return log;
        }

        internal static bool GetUserName()
        {
            if (string.IsNullOrEmpty(Setting.Values.Username) && !Common.RunOnLogonDesktop)
            {
                if (Program.User.Contains("system", StringComparison.CurrentCultureIgnoreCase))
                {
                    _ = Common.ImpersonateLoggedOnUserAndDoSomething(() =>
                    {
                        // See: https://stackoverflow.com/questions/19487541/how-to-get-windows-user-name-from-sessionid
                        static string GetUsernameBySessionId(int sessionId)
                        {
                            string username = "SYSTEM";
                            if (NativeMethods.WTSQuerySessionInformation(IntPtr.Zero, sessionId, NativeMethods.WTSInfoClass.WTSUserName, out nint buffer, out int strLen) && strLen > 1)
                            {
                                username = Marshal.PtrToStringAnsi(buffer);
                                NativeMethods.WTSFreeMemory(buffer);

                                if (NativeMethods.WTSQuerySessionInformation(IntPtr.Zero, sessionId, NativeMethods.WTSInfoClass.WTSDomainName, out buffer, out strLen) && strLen > 1)
                                {
                                    username = @$"{Marshal.PtrToStringAnsi(buffer)}\{username}";
                                    NativeMethods.WTSFreeMemory(buffer);
                                }
                            }

                            return username;
                        }

                        // The most direct way to fetch the username is WindowsIdentity.GetCurrent(true).Name
                        // but GetUserName can run within an ExecutionContext.SuppressFlow block, which creates issues
                        // with WindowsIdentity.GetCurrent.
                        // See: https://stackoverflow.com/questions/76998988/exception-when-using-executioncontext-suppressflow-in-net-7
                        // So we use WTSQuerySessionInformation as a workaround.
                        Setting.Values.Username = GetUsernameBySessionId(Process.GetCurrentProcess().SessionId);
                    });
                }
                else
                {
                    Setting.Values.Username = Program.User;
                }

                Logger.LogDebug("[Username] = " + Setting.Values.Username);
            }

            return !string.IsNullOrEmpty(Setting.Values.Username);
        }

        internal static void ShowOneWayModeMessage()
        {
            ToggleShowTopMostMessage(
    @"
Due to Security Controls, a remote device cannot control a SAW device.
Please use the keyboard and Mouse from the SAW device.
(Press Esc to hide this message)
",
    string.Empty,
    10);
        }

        internal static void ApplyCADSetting()
        {
            try
            {
                if (Setting.Values.DisableCAD)
                {
                    RegistryKey k = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
                    if (k != null)
                    {
                        k.SetValue("DisableCAD", 1, RegistryValueKind.DWord);
                        k.Close();
                    }
                }
                else
                {
                    RegistryKey k = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
                    if (k != null)
                    {
                        k.SetValue("DisableCAD", 0, RegistryValueKind.DWord);
                        k.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }
    }
}
