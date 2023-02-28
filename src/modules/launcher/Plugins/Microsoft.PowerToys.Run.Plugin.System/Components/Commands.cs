// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.PowerToys.Run.Plugin.System.Properties;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Common.Win32;

namespace Microsoft.PowerToys.Run.Plugin.System.Components
{
    /// <summary>
    /// This class holds all available results
    /// </summary>
    internal static class Commands
    {
        internal const int EWXLOGOFF = 0x00000000;
        internal const int EWXSHUTDOWN = 0x00000001;
        internal const int EWXREBOOT = 0x00000002;
        internal const int EWXFORCE = 0x00000004;
        internal const int EWXPOWEROFF = 0x00000008;
        internal const int EWXFORCEIFHUNG = 0x00000010;

        // Cache for network interface information to save query time
        private const int UpdateCacheIntervalSeconds = 5;
        private static List<NetworkConnectionProperties> networkPropertiesCache = new List<NetworkConnectionProperties>();
        private static DateTime timeOfLastNetworkQuery;

        /// <summary>
        /// Returns a list with all system command results
        /// </summary>
        /// <param name="isUefi">Value indicating if the system is booted in uefi mode</param>
        /// <param name="splitRecycleBinResults">Value indicating if we should show two results for Recycle Bin.</param>
        /// <param name="confirmCommands">A value indicating if the user should confirm the system commands</param>
        /// <param name="emptyRBSuccessMessage">Show a success message after empty Recycle Bin.</param>
        /// <param name="iconTheme">The current theme to use for the icons</param>
        /// <param name="culture">The culture to use for the result's title and sub title</param>
        /// <returns>A list of all results</returns>
        internal static List<Result> GetSystemCommands(bool isUefi, bool splitRecycleBinResults, bool confirmCommands, bool emptyRBSuccessMessage, string iconTheme, CultureInfo culture)
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
            });

            // Show Recycle Bin results based on setting.
            if (splitRecycleBinResults)
            {
                results.AddRange(new[]
                {
                    new Result
                    {
                        Title = Resources.ResourceManager.GetString("Microsoft_plugin_sys_RecycleBinOpen", culture),
                        SubTitle = Resources.ResourceManager.GetString("Microsoft_plugin_sys_RecycleBin_description", culture),
                        IcoPath = $"Images\\recyclebin.{iconTheme}.png",
                        Action = c =>
                        {
                            return Helper.OpenInShell("explorer.exe", "shell:RecycleBinFolder");
                        },
                    },
                    new Result
                    {
                        Title = Resources.ResourceManager.GetString("Microsoft_plugin_sys_RecycleBinEmptyResult", culture),
                        SubTitle = Resources.ResourceManager.GetString("Microsoft_plugin_sys_RecycleBinEmpty_description", culture),
                        IcoPath = $"Images\\recyclebin.{iconTheme}.png",
                        Action = c =>
                        {
                            ResultHelper.EmptyRecycleBinAsync(emptyRBSuccessMessage);
                            return true;
                        },
                    },
                });
            }
            else
            {
                results.Add(new Result
                {
                    Title = Resources.ResourceManager.GetString("Microsoft_plugin_sys_RecycleBin", culture),
                    SubTitle = Resources.ResourceManager.GetString("Microsoft_plugin_sys_RecycleBin_description", culture),
                    IcoPath = $"Images\\recyclebin.{iconTheme}.png",
                    ContextData = new SystemPluginContext { Type = ResultContextType.RecycleBinCommand, SearchTag = Resources.ResourceManager.GetString("Microsoft_plugin_sys_RecycleBin_searchTag", culture) },
                    Action = c =>
                    {
                        return Helper.OpenInShell("explorer.exe", "shell:RecycleBinFolder");
                    },
                });
            }

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
                        return ResultHelper.ExecuteCommand(confirmCommands, Resources.Microsoft_plugin_sys_uefi_confirmation, () => Helper.OpenInShell("shutdown", "/r /fw /t 0", null, Helper.ShellRunAsType.Administrator));
                    },
                });
            }

            return results;
        }

        /// <summary>
        /// Returns a list of all ip and mac results
        /// </summary>
        /// <param name="iconTheme">The theme to use for the icons</param>
        /// <param name="culture">The culture to use for the result's title and sub title</param>
        /// <returns>The list of available results</returns>
        internal static List<Result> GetNetworkConnectionResults(string iconTheme, CultureInfo culture)
        {
            var results = new List<Result>();

            // We update the cache only if the last query is older than 'updateCacheIntervalSeconds' seconds
            DateTime timeOfLastNetworkQueryBefore = timeOfLastNetworkQuery;
            timeOfLastNetworkQuery = DateTime.Now;             // Set time of last query to this query
            if ((timeOfLastNetworkQuery - timeOfLastNetworkQueryBefore).TotalSeconds >= UpdateCacheIntervalSeconds)
            {
                networkPropertiesCache = NetworkConnectionProperties.GetList();
            }

            foreach (NetworkConnectionProperties intInfo in networkPropertiesCache)
            {
                if (!string.IsNullOrEmpty(intInfo.IPv4))
                {
                    results.Add(new Result()
                    {
                        Title = intInfo.IPv4,
                        SubTitle = string.Format(CultureInfo.InvariantCulture, Resources.ResourceManager.GetString("Microsoft_plugin_sys_ip4_description", culture), intInfo.ConnectionName) + " - " + Resources.ResourceManager.GetString("Microsoft_plugin_sys_SubTitle_CopyHint", culture),
                        IcoPath = $"Images\\networkAdapter.{iconTheme}.png",
                        ToolTipData = new ToolTipData(Resources.Microsoft_plugin_sys_ConnectionDetails, intInfo.GetConnectionDetails()),
                        ContextData = new SystemPluginContext { Type = ResultContextType.NetworkAdapterInfo, Data = intInfo.GetConnectionDetails() },
                        Action = _ => ResultHelper.CopyToClipBoard(intInfo.IPv4),
                    });
                }

                if (!string.IsNullOrEmpty(intInfo.IPv6Primary))
                {
                    results.Add(new Result()
                    {
                        Title = intInfo.IPv6Primary,
                        SubTitle = string.Format(CultureInfo.InvariantCulture, Resources.ResourceManager.GetString("Microsoft_plugin_sys_ip6_description", culture), intInfo.ConnectionName) + " - " + Resources.ResourceManager.GetString("Microsoft_plugin_sys_SubTitle_CopyHint", culture),
                        IcoPath = $"Images\\networkAdapter.{iconTheme}.png",
                        ToolTipData = new ToolTipData(Resources.Microsoft_plugin_sys_ConnectionDetails, intInfo.GetConnectionDetails()),
                        ContextData = new SystemPluginContext { Type = ResultContextType.NetworkAdapterInfo, Data = intInfo.GetConnectionDetails() },
                        Action = _ => ResultHelper.CopyToClipBoard(intInfo.IPv6Primary),
                    });
                }

                if (!string.IsNullOrEmpty(intInfo.PhysicalAddress))
                {
                    results.Add(new Result()
                    {
                        Title = intInfo.PhysicalAddress,
                        SubTitle = string.Format(CultureInfo.InvariantCulture, Resources.ResourceManager.GetString("Microsoft_plugin_sys_mac_description", culture), intInfo.Adapter, intInfo.ConnectionName) + " - " + Resources.ResourceManager.GetString("Microsoft_plugin_sys_SubTitle_CopyHint", culture),
                        IcoPath = $"Images\\networkAdapter.{iconTheme}.png",
                        ToolTipData = new ToolTipData(Resources.Microsoft_plugin_sys_AdapterDetails, intInfo.GetAdapterDetails()),
                        ContextData = new SystemPluginContext { Type = ResultContextType.NetworkAdapterInfo, Data = intInfo.GetAdapterDetails() },
                        Action = _ => ResultHelper.CopyToClipBoard(intInfo.PhysicalAddress),
                    });
                }
            }

            return results;
        }
    }
}
