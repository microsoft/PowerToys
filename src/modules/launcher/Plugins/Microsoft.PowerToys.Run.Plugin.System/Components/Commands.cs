// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Interop;
using Microsoft.PowerToys.Run.Plugin.System.Properties;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Common.Win32;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.System.Components
{
    internal static class Commands
    {
        internal const int EWXLOGOFF = 0x00000000;
        internal const int EWXSHUTDOWN = 0x00000001;
        internal const int EWXREBOOT = 0x00000002;
        internal const int EWXFORCE = 0x00000004;
        internal const int EWXPOWEROFF = 0x00000008;
        internal const int EWXFORCEIFHUNG = 0x00000010;

        internal static List<Result> GetSystemCommands(bool isUefi, string iconTheme, CultureInfo culture, bool confirmCommands)
        {
            var results = new List<Result>();
            results.AddRange(new[]
            {
                new Result
                {
                    Title = Resources.ResourceManager.GetString("Microsoft_plugin_sys_shutdown_computer", culture),
                    SubTitle = Resources.ResourceManager.GetString("Microsoft_plugin_sys_shutdown_computer_description", culture),
                    IcoPath = $"Images\\shutdown.{iconTheme}.png",
                    Action = c =>
                    {
                        return ResultHelper.ExecuteCommand(confirmCommands, Resources.Microsoft_plugin_sys_shutdown_computer_confirmation, () => Helper.OpenInShell("shutdown", "/s /hybrid /t 0"));
                    },
                },
                new Result
                {
                    Title = Resources.ResourceManager.GetString("Microsoft_plugin_sys_restart_computer", culture),
                    SubTitle = Resources.ResourceManager.GetString("Microsoft_plugin_sys_restart_computer_description", culture),
                    IcoPath = $"Images\\restart.{iconTheme}.png",
                    Action = c =>
                    {
                        return ResultHelper.ExecuteCommand(confirmCommands, Resources.Microsoft_plugin_sys_restart_computer_confirmation, () => Helper.OpenInShell("shutdown", "/r /t 0"));
                    },
                },
                new Result
                {
                    Title = Resources.ResourceManager.GetString("Microsoft_plugin_sys_sign_out", culture),
                    SubTitle = Resources.ResourceManager.GetString("Microsoft_plugin_sys_sign_out_description", culture),
                    IcoPath = $"Images\\logoff.{iconTheme}.png",
                    Action = c =>
                    {
                        return ResultHelper.ExecuteCommand(confirmCommands, Resources.Microsoft_plugin_sys_sign_out_confirmation, () => NativeMethods.ExitWindowsEx(EWXLOGOFF, 0));
                    },
                },
                new Result
                {
                    Title = Resources.ResourceManager.GetString("Microsoft_plugin_sys_lock", culture),
                    SubTitle = Resources.ResourceManager.GetString("Microsoft_plugin_sys_lock_description", culture),
                    IcoPath = $"Images\\lock.{iconTheme}.png",
                    Action = c =>
                    {
                        return ResultHelper.ExecuteCommand(confirmCommands, Resources.Microsoft_plugin_sys_lock_confirmation, () => NativeMethods.LockWorkStation());
                    },
                },
                new Result
                {
                    Title = Resources.ResourceManager.GetString("Microsoft_plugin_sys_sleep", culture),
                    SubTitle = Resources.ResourceManager.GetString("Microsoft_plugin_sys_sleep_description", culture),
                    IcoPath = $"Images\\sleep.{iconTheme}.png",
                    Action = c =>
                    {
                        return ResultHelper.ExecuteCommand(confirmCommands, Resources.Microsoft_plugin_sys_sleep_confirmation, () => NativeMethods.SetSuspendState(false, true, true));
                    },
                },
                new Result
                {
                    Title = Resources.ResourceManager.GetString("Microsoft_plugin_sys_hibernate", culture),
                    SubTitle = Resources.ResourceManager.GetString("Microsoft_plugin_sys_hibernate_description", culture),
                    IcoPath = $"Images\\sleep.{iconTheme}.png", // Icon change needed
                    Action = c =>
                    {
                        return ResultHelper.ExecuteCommand(confirmCommands, Resources.Microsoft_plugin_sys_hibernate_confirmation, () => NativeMethods.SetSuspendState(true, true, true));
                    },
                },
                new Result
                {
                    Title = Resources.ResourceManager.GetString("Microsoft_plugin_sys_emptyrecyclebin", culture),
                    SubTitle = Resources.ResourceManager.GetString("Microsoft_plugin_sys_emptyrecyclebin_description", culture),
                    IcoPath = $"Images\\recyclebin.{iconTheme}.png",
                    Action = c =>
                    {
                        // http://www.pinvoke.net/default.aspx/shell32/SHEmptyRecycleBin.html
                        // FYI, couldn't find documentation for this but if the recycle bin is already empty, it will return -2147418113 (0x8000FFFF (E_UNEXPECTED))
                        // 0 for nothing
                        var result = NativeMethods.SHEmptyRecycleBin(new WindowInteropHelper(Application.Current.MainWindow).Handle, 0);
                        if (result != (uint)HRESULT.S_OK && result != 0x8000FFFF)
                        {
                            var name = "Plugin: " + Resources.Microsoft_plugin_sys_plugin_name;
                            var message = $"Error emptying recycle bin, error code: {result}\n" +
                                          "please refer to https://msdn.microsoft.com/en-us/library/windows/desktop/aa378137";
                            Log.Error(message, typeof(Commands));
                            _ = MessageBox.Show(message, name);
                        }

                        return true;
                    },
                },
            });

            // UEFI command/result. It is only available on systems booted in UEFI mode.
            if (isUefi)
            {
                results.Add(new Result
                {
                    Title = Resources.ResourceManager.GetString("Microsoft_plugin_sys_uefi", culture),
                    SubTitle = Resources.ResourceManager.GetString("Microsoft_plugin_sys_uefi_description", culture),
                    IcoPath = $"Images\\firmwareSettings.{iconTheme}.png",
                    Action = c =>
                    {
                        return ResultHelper.ExecuteCommand(confirmCommands, Resources.Microsoft_plugin_sys_uefi_confirmation, () => Helper.OpenInShell("shutdown", "/r /fw /t 0", null, true));
                    },
                });
            }

            return results;
        }

        internal static List<Result> GetNetworkConnectionResults(string iconTheme, CultureInfo culture)
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces();
            var results = new List<Result>();

            foreach (NetworkInterface a in adapters)
            {
                string mac = a.GetPhysicalAddress().ToString();
                var ips = NetworkInfoHelper.GetMainIpsForConnection(a);
                string connectionDetails = NetworkInfoHelper.GetConnectionDetails(a);
                string adapterDetails = NetworkInfoHelper.GetAdapterDetails(a);

                if (!string.IsNullOrEmpty(ips["IpV4"]))
                {
                    results.Add(new Result()
                    {
                        Title = ips["IpV4"],
                        SubTitle = "IPv4 address of " + a.Name + " - Press enter to copy",
                        IcoPath = $"Images\\network.{iconTheme}.png",
                        ToolTipData = new ToolTipData("Connection details", string.Format(CultureInfo.InvariantCulture, connectionDetails, ips["IpV4"], ips["IpV6"]) + "\n\nFor detailed IP information please use the ipconfig command!"),
                        ContextData = new SystemCommandResultContext { Type = SystemCommandResultType.IpResult, Data = connectionDetails },
                        Action = _ => ResultHelper.CopyToClipBoard(ips["IpV4"]),
                    });
                }

                if (!string.IsNullOrEmpty(ips["IpV6"]))
                {
                    results.Add(new Result()
                    {
                        Title = ips["IpV6"],
                        SubTitle = "IPv6 address of " + a.Name + " - Press enter to copy",
                        IcoPath = $"Images\\network.{iconTheme}.png",
                        ToolTipData = new ToolTipData("Connection details", string.Format(CultureInfo.InvariantCulture, connectionDetails, ips["IpV4"], ips["IpV6"]) + "\n\nFor detailed IP information please use the ipconfig command!"),
                        ContextData = new SystemCommandResultContext { Type = SystemCommandResultType.IpResult, Data = connectionDetails },
                        Action = _ => ResultHelper.CopyToClipBoard(ips["IpV6"]),
                    });
                }

                if (!string.IsNullOrEmpty(mac))
                {
                    results.Add(new Result()
                    {
                        Title = mac,
                        SubTitle = "MAC address of " + a.Name + " - Press enter to copy",
                        IcoPath = $"Images\\networkCard.{iconTheme}.png",
                        ToolTipData = new ToolTipData("Adapter details", adapterDetails),
                        ContextData = new SystemCommandResultContext { Type = SystemCommandResultType.MacResult, Data = adapterDetails },
                        Action = _ => ResultHelper.CopyToClipBoard(mac),
                    });
                }
            }

            return results;
        }
    }
}
