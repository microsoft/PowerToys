// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Threading;

// <summary>
//     Initialization and clean up.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
using Microsoft.Win32;
using MouseWithoutBorders.Class;
using MouseWithoutBorders.Core;
using MouseWithoutBorders.Form;
using Windows.UI.Input.Preview.Injection;

using Thread = MouseWithoutBorders.Core.Thread;

namespace MouseWithoutBorders
{
    internal partial class Common
    {
        private static bool initDone;
        internal static int REOPEN_WHEN_WSAECONNRESET = -10054;
        internal static int REOPEN_WHEN_HOTKEY = -10055;
        internal static int PleaseReopenSocket;
        internal static bool ReopenSocketDueToReadError;

        internal static DateTime LastResumeSuspendTime { get; set; } = DateTime.UtcNow;

        internal static bool InitDone
        {
            get => Common.initDone;
            set => Common.initDone = value;
        }

        internal static void UpdateMachineTimeAndID()
        {
            Common.MachineName = Common.MachineName.Trim();
            _ = MachineStuff.MachinePool.TryUpdateMachineID(Common.MachineName, Common.MachineID, true);
        }

        private static void InitializeMachinePoolFromSettings()
        {
            try
            {
                MachineInf[] info = MachinePoolHelpers.LoadMachineInfoFromMachinePoolStringSetting(Setting.Values.MachinePoolString);
                for (int i = 0; i < info.Length; i++)
                {
                    info[i].Name = info[i].Name.Trim();
                }

                MachineStuff.MachinePool.Initialize(info);
                MachineStuff.MachinePool.ResetIPAddressesForDeadMachines(true);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                MachineStuff.MachinePool.Clear();
            }
        }

        internal static void SetupMachineNameAndID()
        {
            try
            {
                GetMachineName();
                DesMachineID = MachineStuff.NewDesMachineID = MachineID;

                // MessageBox.Show(machineID.ToString(CultureInfo.CurrentCulture)); // For test
                InitializeMachinePoolFromSettings();

                Common.MachineName = Common.MachineName.Trim();
                _ = MachineStuff.MachinePool.LearnMachine(Common.MachineName);
                _ = MachineStuff.MachinePool.TryUpdateMachineID(Common.MachineName, Common.MachineID, true);

                MachineStuff.UpdateMachinePoolStringSetting();
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        internal static void Init()
        {
            _ = Common.GetUserName();
            Common.GeneratedKey = true;

            try
            {
                Common.MyKey = Setting.Values.MyKey;
                int tmp = Setting.Values.MyKeyDaysToExpire;
            }
            catch (FormatException e)
            {
                Common.KeyCorrupted = true;
                Setting.Values.MyKey = Common.MyKey = Common.CreateRandomKey();
                Logger.Log(e.Message);
            }
            catch (CryptographicException e)
            {
                Common.KeyCorrupted = true;
                Setting.Values.MyKey = Common.MyKey = Common.CreateRandomKey();
                Logger.Log(e.Message);
            }

            try
            {
                InputSimulation.Injector = InputInjector.TryCreate();
                if (InputSimulation.Injector != null)
                {
                    InputSimulation.MoveMouseRelative(0, 0);
                    NativeMethods.InjectMouseInputAvailable = true;
                }
            }
            catch (EntryPointNotFoundException)
            {
                NativeMethods.InjectMouseInputAvailable = false;
                Logger.Log($"{nameof(NativeMethods.InjectMouseInputAvailable)} = false");
            }

            bool dummy = Setting.Values.DrawMouseEx;
            Is64bitOS = IntPtr.Size == 8;
            tcpPort = Setting.Values.TcpPort;
            GetScreenConfig();
            PackageSent = new PackageMonitor(0);
            PackageReceived = new PackageMonitor(0);
            SetupMachineNameAndID();
            InitEncryption();
            CreateHelperThreads();

            SystemEvents.DisplaySettingsChanged += new EventHandler(SystemEvents_DisplaySettingsChanged);
            NetworkChange.NetworkAvailabilityChanged += new NetworkAvailabilityChangedEventHandler(NetworkChange_NetworkAvailabilityChanged);
            SystemEvents.PowerModeChanged += new PowerModeChangedEventHandler(SystemEvents_PowerModeChanged);
            PleaseReopenSocket = 9;
            /* TODO: Telemetry for the matrix? */
        }

        private static void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            Common.WndProcCounter++;

            if (e.Mode is PowerModes.Resume or PowerModes.Suspend)
            {
                Logger.TelemetryLogTrace($"{nameof(SystemEvents_PowerModeChanged)}: {e.Mode}", SeverityLevel.Information);
                LastResumeSuspendTime = DateTime.UtcNow;
                MachineStuff.SwitchToMultipleMode(false, true);
            }
        }

        private static void CreateHelperThreads()
        {
            // NOTE(@yuyoyuppe): service crashes while trying to obtain this info, disabling.
            /*
            Thread watchDogThread = new(new ThreadStart(WatchDogThread), nameof(WatchDogThread));
            watchDogThread.Priority = ThreadPriority.Highest;
            watchDogThread.Start();
            */

            helper = new Thread(new ThreadStart(HelperThread), "Helper Thread");
            helper.SetApartmentState(ApartmentState.STA);
            helper.Start();
        }

        private static void AskHelperThreadsToExit(int waitTime)
        {
            signalHelperToExit = true;
            signalWatchDogToExit = true;
            _ = EvSwitch.Set();

            int c = 0;
            if (helper != null && c < waitTime)
            {
                while (signalHelperToExit)
                {
                    Thread.Sleep(1);
                }

                helper = null;
            }
        }

        internal static void Cleanup()
        {
            try
            {
                SendByeBye();

                // UnhookClipboard();
                AskHelperThreadsToExit(500);
                MainForm.NotifyIcon.Visible = false;
                MainForm.NotifyIcon.Dispose();
                CloseAllFormsAndHooks();

                DoSomethingInUIThread(() =>
                {
                    Sk?.Close(true);
                });
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        private static long lastReleaseAllKeysCall;

        internal static void ReleaseAllKeys()
        {
            if (Math.Abs(GetTick() - lastReleaseAllKeysCall) < 2000)
            {
                return;
            }

            lastReleaseAllKeysCall = GetTick();

            KEYBDDATA kd;
            kd.dwFlags = (int)LLKHF.UP;

            VK[] keys = new VK[]
            {
                VK.LSHIFT, VK.LCONTROL, VK.LMENU, VK.LWIN, VK.RSHIFT,
                VK.RCONTROL, VK.RMENU, VK.RWIN, VK.SHIFT, VK.MENU, VK.CONTROL,
            };

            Logger.LogDebug("***** ReleaseAllKeys has been called! *****:");

            foreach (VK vk in keys)
            {
                if ((NativeMethods.GetAsyncKeyState((IntPtr)vk) & 0x8000) != 0)
                {
                    Logger.LogDebug(vk.ToString() + " is down, release it...");
                    Hook?.ResetLastSwitchKeys(); // Sticky key can turn ALL PC mode on (CtrlCtrlCtrl)
                    kd.wVk = (int)vk;
                    InputSimulation.SendKey(kd);
                    Hook?.ResetLastSwitchKeys();
                }
            }
        }

        private static void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            Logger.LogDebug("NetworkAvailabilityEventArgs.IsAvailable: " + e.IsAvailable.ToString(CultureInfo.InvariantCulture));
            Common.WndProcCounter++;
            ScheduleReopenSocketsDueToNetworkChanges(!e.IsAvailable);
        }

        private static void ScheduleReopenSocketsDueToNetworkChanges(bool closeSockets = true)
        {
            if (closeSockets)
            {
                // Slept/hibernated machine may still have the sockets' status as Connected:( (unchanged) so it would not re-connect after a timeout when waking up.
                // Closing the sockets when it is going to sleep/hibernate will trigger the reconnection faster when it wakes up.
                DoSomethingInUIThread(
                    () =>
                {
                    SocketStuff s = Sk;
                    Sk = null;
                    s?.Close(false);
                },
                    true);
            }

            if (!Common.IsMyDesktopActive())
            {
                PleaseReopenSocket = 0;
            }
            else if (PleaseReopenSocket != 10)
            {
                PleaseReopenSocket = 10;
            }
        }
    }
}
