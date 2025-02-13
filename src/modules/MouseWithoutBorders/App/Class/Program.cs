// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// <summary>
//     Application entry and pre-process/initialization.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Authentication.ExtendedProtection;
using System.Security.Principal;
using System.ServiceModel.Channels;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Telemetry;
using Newtonsoft.Json;
using StreamJsonRpc;

using Logger = MouseWithoutBorders.Core.Logger;
using Thread = MouseWithoutBorders.Core.Thread;

[module: SuppressMessage("Microsoft.MSInternal", "CA904:DeclareTypesInMicrosoftOrSystemNamespace", Scope = "namespace", Target = "MouseWithoutBorders", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1014:MarkAssembliesWithClsCompliant", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Globalization", "CA1304:SpecifyCultureInfo", Scope = "member", Target = "MouseWithoutBorders.Program.#Main()", MessageId = "System.String.ToLower", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions", Scope = "member", Target = "MouseWithoutBorders.Program.#Main()", Justification = "Dotnet port with style preservation")]

namespace MouseWithoutBorders.Class
{
    internal static class Program
    {
        private static readonly string ServiceName = "PowerToys.MWB.Service";

        private static readonly string ServiceModeArg = "UseService";

        public static bool ShowServiceModeErrorTooltip;

        [STAThread]
        private static void Main()
        {
            ManagedCommon.Logger.InitializeLogger("\\MouseWithoutBorders\\Logs");
            Logger.Log(Application.ProductName + " Started!");

            ETWTrace etwTrace = new ETWTrace();

            if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredMouseWithoutBordersEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                Logger.Log("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
                return;
            }

            Thread.CurrentThread.Name = Application.ProductName + " main thread";
            Common.BinaryName = Path.GetFileNameWithoutExtension(Application.ExecutablePath);

            WindowsIdentity currentUser = WindowsIdentity.GetCurrent();
            SecurityIdentifier currentUserSID = currentUser.User;
            SecurityIdentifier localSystemSID = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

            bool runningAsSystem = currentUserSID.Equals(localSystemSID);
            Common.RunWithNoAdminRight = !runningAsSystem;
            try
            {
                string[] args = Environment.GetCommandLineArgs();

                string firstArg = string.Empty;
                if (args.Length > 1 && args[1] != null)
                {
                    firstArg = args[1].Trim();
                }

                User = WindowsIdentity.GetCurrent().Name;
                Logger.LogDebug("*** Started as " + User);

                Logger.Log(Environment.CommandLine);

                bool serviceMode = firstArg == ServiceModeArg;

                if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredMwbAllowServiceModeValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
                {
                    if (runningAsSystem)
                    {
                        Logger.Log("Can't run as a service. It's not allowed according to GPO policy. Please contact your systems administrator.");
                        return;
                    }

                    serviceMode = false;
                }

                // If we're started from the .dll module or from the service process, we should
                // assume the service mode.
                if (serviceMode && !runningAsSystem)
                {
                    try
                    {
                        var sc = new ServiceController(ServiceName);
                        sc.Start();
                        return;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("Couldn't start the service. Will try to continue as not a service.");
                        Logger.Log(ex);
                        ShowServiceModeErrorTooltip = true;
                        serviceMode = false;
                        Setting.Values.UseService = false;
                    }
                }

                if (serviceMode || runningAsSystem)
                {
                    if (args.Length > 2)
                    {
                        Helper.UserLocalAppDataPath = args[2].Trim();
                    }
                }

                ShutdownWithPowerToys.WaitForPowerToysRunner(etwTrace);

                if (firstArg != string.Empty)
                {
                    if (Common.CheckSecondInstance(Common.RunWithNoAdminRight))
                    {
                        Logger.Log("*** Second instance, exiting...");
                        return;
                    }

                    string myDesktop = Common.GetMyDesktop();

                    if (firstArg.Equals("winlogon", StringComparison.OrdinalIgnoreCase))
                    {
                        // Executed by service, running on logon desktop
                        Logger.Log("*** Running on " + firstArg + " desktop");
                        Common.RunOnLogonDesktop = true;
                    }
                    else if (args[1].Trim().Equals("default", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Log("*** Running on " + firstArg + " desktop");
                    }
                    else if (args[1].Equals(myDesktop, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Log("*** Running on " + myDesktop);
                        if (myDesktop.Equals("Screen-saver", StringComparison.OrdinalIgnoreCase))
                        {
                            Common.RunOnScrSaverDesktop = true;
                            Setting.Values.LastX = Common.JUST_GOT_BACK_FROM_SCREEN_SAVER;
                        }
                    }
                }
                else
                {
                    if (Common.CheckSecondInstance(true))
                    {
                        Logger.Log("*** Second instance, exiting...");
                        return;
                    }
                }

                PowerToysTelemetry.Log.WriteEvent(new MouseWithoutBorders.Telemetry.MouseWithoutBordersStartedEvent());

                try
                {
                    Common.CurrentProcess = Process.GetCurrentProcess();
                    Common.CurrentProcess.PriorityClass = ProcessPriorityClass.RealTime;
                }
                catch (Exception e)
                {
                    Logger.Log(e);
                }

                Logger.Log(Environment.OSVersion.ToString());

                // Environment.OSVersion is unreliable from 6.2 and up, so just forcefully call the APIs and log the exception unsupported by Windows:
                int setProcessDpiAwarenessResult = -1;

                try
                {
                    setProcessDpiAwarenessResult = NativeMethods.SetProcessDpiAwareness(2);
                    Logger.Log(string.Format(CultureInfo.InvariantCulture, "SetProcessDpiAwareness: {0}.", setProcessDpiAwarenessResult));
                }
                catch (DllNotFoundException)
                {
                    Logger.Log("SetProcessDpiAwareness is unsupported in Windows 7 and lower.");
                }
                catch (EntryPointNotFoundException)
                {
                    Logger.Log("SetProcessDpiAwareness is unsupported in Windows 7 and lower.");
                }
                catch (Exception e)
                {
                    Logger.Log(e);
                }

                try
                {
                    if (setProcessDpiAwarenessResult != 0)
                    {
                        Logger.Log(string.Format(CultureInfo.InvariantCulture, "SetProcessDPIAware: {0}.", NativeMethods.SetProcessDPIAware()));
                    }
                }
                catch (Exception e)
                {
                    Logger.Log(e);
                }

                System.Threading.Thread mainUIThread = Thread.CurrentThread;
                Common.UIThreadID = mainUIThread.ManagedThreadId;
                Thread.UpdateThreads(mainUIThread);

                StartInputCallbackThread();
                if (!Common.RunOnLogonDesktop)
                {
                    StartSettingSyncThread();
                }

                Application.EnableVisualStyles();
                _ = Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.SetCompatibleTextRenderingDefault(false);

                Common.Init();
                Common.WndProcCounter++;

                var formScreen = new FrmScreen();

                Application.Run(formScreen);

                etwTrace?.Dispose();
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        private interface ISettingsSyncHelper
        {
            [JsonObject(MemberSerialization.OptIn)]
            public struct MachineSocketState
            {
                // Disable false-positive warning due to IPC
#pragma warning disable CS0649
                [JsonProperty]
                public string Name;

                [JsonProperty]
                public SocketStatus Status;
#pragma warning restore CS0649
            }

            void Shutdown();

            void Reconnect();

            void GenerateNewKey();

            void ConnectToMachine(string machineName, string securityKey);

            Task<MachineSocketState[]> RequestMachineSocketStateAsync();
        }

        private sealed class SettingsSyncHelper : ISettingsSyncHelper
        {
            public Task<ISettingsSyncHelper.MachineSocketState[]> RequestMachineSocketStateAsync()
            {
                var machineStates = new Dictionary<string, SocketStatus>();
                if (Common.Sk == null || Common.Sk.TcpSockets == null)
                {
                    return Task.FromResult(Array.Empty<ISettingsSyncHelper.MachineSocketState>());
                }

                foreach (var client in Common.Sk.TcpSockets
                    .Where(t => t != null && t.IsClient && !string.IsNullOrEmpty(t.MachineName)))
                {
                    var exists = machineStates.TryGetValue(client.MachineName, out var existingStatus);
                    if (!exists || existingStatus == SocketStatus.NA)
                    {
                        machineStates[client.MachineName] = client.Status;
                    }
                }

                return Task.FromResult(machineStates.Select((state) => new ISettingsSyncHelper.MachineSocketState { Name = state.Key, Status = state.Value }).ToArray());
            }

            public void ConnectToMachine(string pcName, string securityKey)
            {
                Setting.Values.PauseInstantSaving = true;

                Common.ClearComputerMatrix();
                Setting.Values.MyKey = securityKey;
                Common.MyKey = securityKey;
                Common.MagicNumber = Common.Get24BitHash(Common.MyKey);
                Common.MachineMatrix = new string[Common.MAX_MACHINE] { pcName.Trim().ToUpper(CultureInfo.CurrentCulture), Common.MachineName.Trim(), string.Empty, string.Empty };

                string[] machines = Common.MachineMatrix;
                Common.MachinePool.Initialize(machines);
                Common.UpdateMachinePoolStringSetting();

                SocketStuff.InvalidKeyFound = false;
                Common.ReopenSocketDueToReadError = true;
                Common.ReopenSockets(true);
                Common.SendMachineMatrix();

                Setting.Values.PauseInstantSaving = false;
                Setting.Values.SaveSettings();
            }

            public void GenerateNewKey()
            {
                Setting.Values.PauseInstantSaving = true;

                Setting.Values.EasyMouse = (int)EasyMouseOption.Enable;
                Common.ClearComputerMatrix();
                Setting.Values.MyKey = Common.MyKey = Common.CreateRandomKey();
                Common.GeneratedKey = true;

                Setting.Values.PauseInstantSaving = false;
                Setting.Values.SaveSettings();

                Reconnect();
            }

            public void Reconnect()
            {
                SocketStuff.InvalidKeyFound = false;
                Common.ReopenSocketDueToReadError = true;
                Common.ReopenSockets(true);

                for (int i = 0; i < 10; i++)
                {
                    if (Common.AtLeastOneSocketConnected())
                    {
                        Common.MMSleep(0.5);
                        break;
                    }

                    Common.MMSleep(0.2);
                }

                Common.SendMachineMatrix();
            }

            public void Shutdown()
            {
                Process[] ps = Process.GetProcessesByName("PowerToys.MouseWithoutBorders");
                Process me = Process.GetCurrentProcess();

                foreach (Process p in ps)
                {
                    if (p.Id != me.Id)
                    {
                        p.Kill();
                    }
                }

                Common.MainForm.Quit(true, false);
            }
        }

        internal static void StartSettingSyncThread()
        {
            var serverTaskCancellationSource = new CancellationTokenSource();
            CancellationToken cancellationToken = serverTaskCancellationSource.Token;

            IpcChannel<SettingsSyncHelper>.StartIpcServer("MouseWithoutBorders/SettingsSync", cancellationToken);
        }

        internal static void StartInputCallbackThread()
        {
            Thread inputCallback = new(new ThreadStart(InputCallbackThread), "InputCallback Thread");
            inputCallback.SetApartmentState(ApartmentState.STA);
            inputCallback.Priority = ThreadPriority.Highest;
            inputCallback.Start();
        }

        private static void InputCallbackThread()
        {
            // SuppressFlow fixes an issue on service mode, where the helper process can't get enough permissions to be started again.
            // More details can be found on: https://github.com/microsoft/PowerToys/pull/36892
            using var asyncFlowControl = ExecutionContext.SuppressFlow();

            Common.InputCallbackThreadID = Thread.CurrentThread.ManagedThreadId;
            while (!Common.InitDone)
            {
                Thread.Sleep(100);
            }

            Application.Run(new FrmInputCallback());
        }

        internal static void StartService()
        {
            if (Common.RunWithNoAdminRight)
            {
                return;
            }

            try
            {
                // Kill all but me
                Process me = Process.GetCurrentProcess();
                Process[] ps = Process.GetProcessesByName(Common.BinaryName);
                foreach (Process pp in ps)
                {
                    if (pp.Id != me.Id)
                    {
                        Logger.Log(string.Format(CultureInfo.InvariantCulture, "Killing process {0}.", pp.Id));
                        pp.KillProcess();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }

            Common.StartMouseWithoutBordersService();
        }

        internal static string User { get; set; }
    }
}
