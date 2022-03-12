// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Interop;
using Microsoft.PowerToys.Run.Plugin.System.Properties;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Common.Win32;

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

        internal static List<Result> GetSystemCommands(string iconTheme, bool isUefi)
        {
            CultureInfo culture = CultureInfo.CurrentUICulture;

            if (!SystemPluginSettings.Instance.LocalizeSystemCommands)
            {
                culture = new CultureInfo("en-US");
            }

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
                        return ExecuteCommand(Resources.Microsoft_plugin_sys_shutdown_computer_confirmation, () => Helper.OpenInShell("shutdown", "/s /hybrid /t 0"));
                    },
                },
                new Result
                {
                    Title = Resources.ResourceManager.GetString("Microsoft_plugin_sys_restart_computer", culture),
                    SubTitle = Resources.ResourceManager.GetString("Microsoft_plugin_sys_restart_computer_description", culture),
                    IcoPath = $"Images\\restart.{iconTheme}.png",
                    Action = c =>
                    {
                        return ExecuteCommand(Resources.Microsoft_plugin_sys_restart_computer_confirmation, () => Helper.OpenInShell("shutdown", "/r /t 0"));
                    },
                },
                new Result
                {
                    Title = Resources.ResourceManager.GetString("Microsoft_plugin_sys_sign_out", culture),
                    SubTitle = Resources.ResourceManager.GetString("Microsoft_plugin_sys_sign_out_description", culture),
                    IcoPath = $"Images\\logoff.{iconTheme}.png",
                    Action = c =>
                    {
                        return ExecuteCommand(Resources.Microsoft_plugin_sys_sign_out_confirmation, () => NativeMethods.ExitWindowsEx(EWXLOGOFF, 0));
                    },
                },
                new Result
                {
                    Title = Resources.ResourceManager.GetString("Microsoft_plugin_sys_lock", culture),
                    SubTitle = Resources.ResourceManager.GetString("Microsoft_plugin_sys_lock_description", culture),
                    IcoPath = $"Images\\lock.{iconTheme}.png",
                    Action = c =>
                    {
                        return ExecuteCommand(Resources.Microsoft_plugin_sys_lock_confirmation, () => NativeMethods.LockWorkStation());
                    },
                },
                new Result
                {
                    Title = Resources.ResourceManager.GetString("Microsoft_plugin_sys_sleep", culture),
                    SubTitle = Resources.ResourceManager.GetString("Microsoft_plugin_sys_sleep_description", culture),
                    IcoPath = $"Images\\sleep.{iconTheme}.png",
                    Action = c =>
                    {
                        return ExecuteCommand(Resources.Microsoft_plugin_sys_sleep_confirmation, () => NativeMethods.SetSuspendState(false, true, true));
                    },
                },
                new Result
                {
                    Title = Resources.ResourceManager.GetString("Microsoft_plugin_sys_hibernate", culture),
                    SubTitle = Resources.ResourceManager.GetString("Microsoft_plugin_sys_hibernate_description", culture),
                    IcoPath = $"Images\\sleep.{iconTheme}.png", // Icon change needed
                    Action = c =>
                    {
                        return ExecuteCommand(Resources.Microsoft_plugin_sys_hibernate_confirmation, () => NativeMethods.SetSuspendState(true, true, true));
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
                            _ = MessageBox.Show(message, name, MessageBoxButton.OK, MessageBoxImage.Error);
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
                        return ExecuteCommand(Resources.Microsoft_plugin_sys_uefi_confirmation, () => Helper.OpenInShell("shutdown", "/r /fw /t 0", null, true));
                    },
                });
            }

            return results;
        }

        internal static List<Result> GetNetworkAdapterAdresses(string iconTheme)
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces();
            var results = new List<Result>();

            foreach (NetworkInterface a in adapters)
            {
                string mac = a.GetPhysicalAddress().ToString();
                UnicastIPAddressInformationCollection adresses = a.GetIPProperties().UnicastAddresses;
                string ip4 = adresses.Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork).FirstOrDefault().Address.ToString();
                string ip6 = adresses.Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetworkV6).FirstOrDefault().Address.ToString();

                if (!string.IsNullOrEmpty(mac))
                {
                    results.AddRange(new[]
                    {
                        new Result()
                        {
                            Title = ip4,
                            SubTitle = "IPv4 address of " + a.Name + " - State: " + a.OperationalStatus,
                        },
                        new Result()
                        {
                            Title = ip6,
                            SubTitle = "IPv6 address of " + a.Name + " - State: " + a.OperationalStatus,
                        },
                        new Result()
                        {
                            Title = mac,
                            SubTitle = "MAC address of " + a.Name + " - State: " + a.OperationalStatus,
                        },
                    });
                }
            }

            return results;
        }

        internal static List<Result> GetNetworkCommands(string parameterList, string iconTheme)
        {
            return new List<Result>()
            {
                new Result()
                {
                    Title = "ping " + parameterList,
                    SubTitle = "Execute ping command",
                    Action = _ => Helper.OpenInShell("cmd.exe", "/k ping " + parameterList),
                },
                new Result()
                {
                    Title = "nslookup " + parameterList,
                    SubTitle = "Execute nslookup command",
                    Action = _ => Helper.OpenInShell("cmd.exe", "/k nslookup " + parameterList),
                },
            };
        }

        private static bool ExecuteCommand(string confirmationMessage, Action command)
        {
            if (SystemPluginSettings.Instance.ConfirmSystemCommands)
            {
                MessageBoxResult messageBoxResult = MessageBox.Show(
                    confirmationMessage,
                    Resources.Microsoft_plugin_sys_confirmation,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (messageBoxResult == MessageBoxResult.No)
                {
                    return false;
                }
            }

            command();
            return true;
        }
    }
}
