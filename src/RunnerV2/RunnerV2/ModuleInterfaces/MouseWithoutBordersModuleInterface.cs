// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using RunnerV2.Models;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class MouseWithoutBordersModuleInterface : ProcessModuleAbstractClass, IPowerToysModule, IPowerToysModuleSettingsChangedSubscriber, IPowerToysModuleCustomActionsProvider
    {
        public string Name => "MouseWithoutBorders";

        public bool Enabled => SettingsUtils.Default.GetSettings<GeneralSettings>().Enabled.MouseWithoutBorders;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredMouseWithoutBordersEnabledValue();

        public override string ProcessPath => "PowerToys.MouseWithoutBorders.exe";

        public override string ProcessName => "PowerToys.MouseWithoutBorders";

        public override ProcessLaunchOptions LaunchOptions => ProcessLaunchOptions.SingletonProcess;

        public override string ProcessArguments
        {
            get
            {
                var settings = SettingsUtils.Default.GetSettings<MouseWithoutBordersSettings>();
                return settings.Properties.UseService ? " UseService" : string.Empty;
            }
        }

        public Dictionary<string, Action> CustomActions => new Dictionary<string, Action>
        {
            { "add_firewall", LaunchAddFirewallProcess },
            { "uninstall_service", () => { new Thread(UnregisterService).Start(); } },
        };

        private void RegisterService()
        {
            IntPtr schSCManager = NativeMethods.OpenSCManagerW(string.Empty, "ServicesActive", NativeMethods.SCMANAGERALLACCESS);
            if (schSCManager == IntPtr.Zero)
            {
                Logger.LogError("Couldn't open Service Control Manager");
                return;
            }

            IntPtr hService = NativeMethods.OpenServiceW(schSCManager, "PowerToys.MWB.Service", 0x0004 | 0x0001 | 0x0002);

            string servicePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "PowerToys.MouseWithoutBordersService.exe");

            IntPtr pServiceConfig = IntPtr.Zero;
            if (!NativeMethods.QueryServiceConfigW(hService, IntPtr.Zero, 0, out uint bytesNeeded))
            {
                if (Marshal.GetLastWin32Error() == 122)
                {
                    pServiceConfig = Marshal.AllocHGlobal((int)bytesNeeded);
                    if (!NativeMethods.QueryServiceConfigW(hService, pServiceConfig, bytesNeeded, out _))
                    {
                        Marshal.FreeHGlobal(pServiceConfig);
                        pServiceConfig = IntPtr.Zero;
                        NativeMethods.CloseServiceHandle(hService);
                    }
                }
            }

            bool alreadyRegistered = false;
            bool isServicePathCorrect = true;

            string EscapeDoubleQuotes(string input)
            {
                StringBuilder output = new(input.Length);
                foreach (char c in input)
                {
                    if (c == '"')
                    {
                        output.Append('\\');
                    }

                    output.Append(c);
                }

                return output.ToString();
            }

            string expectedPath = "\"" + servicePath + "\" " + EscapeDoubleQuotes(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

            if (pServiceConfig != IntPtr.Zero)
            {
                alreadyRegistered = true;
                var serviceConfig = Marshal.PtrToStructure<NativeMethods.QueryServiceConfig>(pServiceConfig);
                string currentPath = serviceConfig.lpBinaryPathName;

                if (currentPath != expectedPath)
                {
                    Logger.LogInfo($"MWB Service path is incorrect. Current: {currentPath} Expected: {expectedPath}");
                }

                if (serviceConfig.dwStartType == 0x0004)
                {
                    if (!NativeMethods.ChangeServiceConfigW(hService, 0xffffffff, 0x3, 0xffffffff, null!, null!, IntPtr.Zero, null!, null!, null!, null!))
                    {
                        // Check if marked for delete
                        if (Marshal.GetLastWin32Error() == 1072)
                        {
                            alreadyRegistered = false;
                        }
                    }
                }

                Marshal.FreeHGlobal(pServiceConfig);
            }

            if (alreadyRegistered)
            {
                if (!isServicePathCorrect)
                {
                    if (!NativeMethods.ChangeServiceConfigW(hService, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, expectedPath, null!, IntPtr.Zero, null!, null!, null!, null!))
                    {
                        Logger.LogError("Couldn't update MWB Service path");
                    }
                    else
                    {
                        Logger.LogInfo("MWB Service path updated successfully");
                    }
                }
            }
            else
            {
                hService = NativeMethods.CreateServiceW(
                    schSCManager,
                    "PowerToys.MWB.Service",
                    "PowerToys.MWB.Service",
                    NativeMethods.SERVICEALLACCESS,
                    0x00000010,
                    0x00000003,
                    0x00000001,
                    expectedPath,
                    null!,
                    IntPtr.Zero,
                    null!,
                    null!,
                    null!);

                if (hService == IntPtr.Zero)
                {
                    Logger.LogError("Couldn't create MWB Service");
                    return;
                }

                string sid = WindowsIdentity.GetCurrent().User!.Value;

                string securityDescriptor = "D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWLOCRRC;;;IU)(A;;CCLCSWLOCRRC;;;SU)(A;;CR;;;AU)(A;;CCLCSWRPWPDTLOCRRC;;;PU)(A;;RPWPDTLO;;;"
                    + sid
                    + ")S:(AU;FA;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;WD)";

                if (!NativeMethods.ConvertStringSecurityDescriptorToSecurityDescriptorW(securityDescriptor, 1, out nint pSD, out uint szSD))
                {
                    Logger.LogError("Couldn't convert security descriptor string to security descriptor");
                    return;
                }

                if (!NativeMethods.SetServiceObjectSecurity(hService, 4, pSD))
                {
                    Logger.LogError("Couldn't set MWB Service security descriptor");
                    return;
                }
            }

            NativeMethods.CloseServiceHandle(schSCManager);
            NativeMethods.CloseServiceHandle(hService);
        }

        private void UnregisterService()
        {
            IntPtr schSCManager = NativeMethods.OpenSCManagerW(string.Empty, "ServicesActive", NativeMethods.SCMANAGERALLACCESS);
            if (schSCManager == IntPtr.Zero)
            {
                Logger.LogError("Couldn't open Service Control Manager");
                return;
            }

            IntPtr hService = NativeMethods.OpenServiceW(schSCManager, "PowerToys.MWB.Service", 0x0020 | 0x10000);

            if (hService == IntPtr.Zero)
            {
                Logger.LogError("Couldn't open MWB Service");
                return;
            }

            NativeMethods.ServiceStatus status = default;
            if (NativeMethods.ControlService(hService, 0x0001, ref status))
            {
                Thread.Sleep(1000);
                for (int i = 0; i < 5; ++i)
                {
                    while (NativeMethods.QueryServiceStatusW(hService, ref status))
                    {
                        if (status.dwCurrentState == 0x0003)
                        {
                            Thread.Sleep(1000);
                        }
                        else
                        {
                            goto outer;
                        }
                    }
                }
            }

        outer:
            bool deleteResult = NativeMethods.DeleteService(hService);
            NativeMethods.CloseServiceHandle(hService);

            if (!deleteResult)
            {
                Logger.LogError("Couldn't delete MWB Service");
            }
        }

        private void LaunchAddFirewallProcess()
        {
            string args = "/S /c \"" +
            "echo \"Deleting existing inbound firewall rules for PowerToys.MouseWithoutBorders.exe\"" +
            " & netsh advfirewall firewall delete rule dir=in name=all program=\"" +
            "\\PowerToys.MouseWithoutBorders.exe" +
            "\" & echo \"Adding an inbound firewall rule for PowerToys.MouseWithoutBorders.exe\"" +
            " & netsh advfirewall firewall add rule name=\"PowerToys.MouseWithoutBorders\" dir=in action=allow program=\"" +
            "\\PowerToys.MouseWithoutBorders.exe" +
            "\" enable=yes remoteip=any profile=any protocol=tcp & pause\"";

            new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    Arguments = args,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    Verb = "runas",
                    FileName = "cmd.exe",
                },
            }.Start();
        }

        public void Disable()
        {
            var services = Process.GetProcessesByName("PowerToys.MouseWithoutBordersService");
            services = [..services, ..Process.GetProcessesByName("PowerToys.MouseWithoutBorders"), ..Process.GetProcessesByName("PowerToys.MouseWithoutBordersHelper")];

            foreach (var service in services)
            {
                try
                {
                    service.Kill();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to kill process.", ex);
                }
            }
        }

        public void Enable()
        {
            OnSettingsChanged();
        }

        private bool _runInServiceMode;

        public void OnSettingsChanged()
        {
            var settings = SettingsUtils.Default.GetSettings<MouseWithoutBordersSettings>(Name).Properties;

            bool newRunInServiceMode = settings.UseService && GPOWrapper.GetConfiguredMwbAllowServiceModeValue() != GpoRuleConfigured.Disabled;

            if (newRunInServiceMode == _runInServiceMode)
            {
                return;
            }

            _runInServiceMode = newRunInServiceMode;
            Disable();

            new Thread(() =>
            {
                if (_runInServiceMode)
                {
                    RegisterService();
                }
                else
                {
                    var processes = Process.GetProcessesByName("PowerToys.MouseWithoutBordersService");
                    foreach (var p in processes)
                    {
                        bool stopped = false;
                        do
                        {
                            stopped = p.HasExited;
                        }
                        while (!stopped);
                    }

                    Thread.Sleep(1000);
                }

                LaunchProcess();
            }).Start();
        }
    }
}
