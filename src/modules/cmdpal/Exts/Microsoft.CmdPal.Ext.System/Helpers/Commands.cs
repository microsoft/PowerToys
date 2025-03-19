// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Text;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.System.Helpers;

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
    /// <returns>A list of all results</returns>
    public static List<IListItem> GetSystemCommands(bool isUefi, bool splitRecycleBinResults, bool confirmCommands, bool emptyRBSuccessMessage)
    {
        var results = new List<IListItem>();
        results.AddRange(new[]
        {
            new ListItem(new ExecuteCommandConfirmation(Resources.Microsoft_plugin_command_name_shutdown, confirmCommands, Resources.Microsoft_plugin_sys_shutdown_computer_confirmation, () => OpenInShellHelper.OpenInShell("shutdown", "/s /hybrid /t 0")))
            {
                Title = Resources.Microsoft_plugin_sys_shutdown_computer,
                Subtitle = Resources.Microsoft_plugin_sys_shutdown_computer_description,
                Icon = Icons.ShutdownIcon,
            },
            new ListItem(new ExecuteCommandConfirmation(Resources.Microsoft_plugin_command_name_restart, confirmCommands, Resources.Microsoft_plugin_sys_restart_computer_confirmation, () => OpenInShellHelper.OpenInShell("shutdown", "/g /t 0")))
            {
                Title = Resources.Microsoft_plugin_sys_restart_computer,
                Subtitle = Resources.Microsoft_plugin_sys_restart_computer_description,
                Icon = Icons.RestartIcon,
            },
            new ListItem(new ExecuteCommandConfirmation(Resources.Microsoft_plugin_command_name_signout, confirmCommands, Resources.Microsoft_plugin_sys_sign_out_confirmation, () => NativeMethods.ExitWindowsEx(EWXLOGOFF, 0)))
            {
                Title = Resources.Microsoft_plugin_sys_sign_out,
                Subtitle = Resources.Microsoft_plugin_sys_sign_out_description,
                Icon = Icons.LogoffIcon,
            },
            new ListItem(new ExecuteCommandConfirmation(Resources.Microsoft_plugin_command_name_lock, confirmCommands, Resources.Microsoft_plugin_sys_lock_confirmation, () => NativeMethods.LockWorkStation()))
            {
                Title = Resources.Microsoft_plugin_sys_lock,
                Subtitle = Resources.Microsoft_plugin_sys_lock_description,
                Icon = Icons.LockIcon,
            },
            new ListItem(new ExecuteCommandConfirmation(Resources.Microsoft_plugin_command_name_sleep, confirmCommands, Resources.Microsoft_plugin_sys_sleep_confirmation, () => NativeMethods.SetSuspendState(false, true, true)))
            {
                Title = Resources.Microsoft_plugin_sys_sleep,
                Subtitle = Resources.Microsoft_plugin_sys_sleep_description,
                Icon = Icons.SleepIcon,
            },
            new ListItem(new ExecuteCommandConfirmation(Resources.Microsoft_plugin_command_name_hibernate, confirmCommands, Resources.Microsoft_plugin_sys_hibernate_confirmation, () => NativeMethods.SetSuspendState(true, true, true)))
            {
                Title = Resources.Microsoft_plugin_sys_hibernate,
                Subtitle = Resources.Microsoft_plugin_sys_hibernate_description,
                Icon = Icons.SleepIcon, // Icon change needed
            },
        });

        // Show Recycle Bin results based on setting.
        if (splitRecycleBinResults)
        {
            results.AddRange(new[]
            {
                new ListItem(new OpenInShellCommand(Resources.Microsoft_plugin_command_name_empty, "explorer.exe", "shell:RecycleBinFolder"))
                {
                    Title = Resources.Microsoft_plugin_sys_RecycleBinOpen,
                    Subtitle = Resources.Microsoft_plugin_sys_RecycleBin_description,
                    Icon = Icons.RecycleBinIcon,
                },
                new ListItem(new EmptyRecycleBinConfirmation(emptyRBSuccessMessage))
                {
                    Title = Resources.Microsoft_plugin_sys_RecycleBinEmptyResult,
                    Subtitle = Resources.Microsoft_plugin_sys_RecycleBinEmpty_description,
                    Icon = Icons.RecycleBinIcon,
                },
            });
        }
        else
        {
            results.Add(
                new ListItem(new OpenInShellCommand(Resources.Microsoft_plugin_command_name_empty, "explorer.exe", "shell:RecycleBinFolder"))
                {
                    Title = Resources.Microsoft_plugin_sys_RecycleBin,
                    Subtitle = Resources.Microsoft_plugin_sys_RecycleBin_description,
                    Icon = Icons.RecycleBinIcon,
                });
        }

        // UEFI command/result. It is only available on systems booted in UEFI mode.
        if (isUefi)
        {
            results.Add(new ListItem(new ExecuteCommandConfirmation(Resources.Microsoft_plugin_command_name_reboot, confirmCommands, Resources.Microsoft_plugin_sys_uefi_confirmation, () => OpenInShellHelper.OpenInShell("shutdown", "/r /fw /t 0", null, OpenInShellHelper.ShellRunAsType.Administrator)))
            {
                Title = Resources.Microsoft_plugin_sys_uefi,
                Subtitle = Resources.Microsoft_plugin_sys_uefi_description,
                Icon = Icons.FirmwareSettingsIcon,
            });
        }

        return results;
    }

    /// <summary>
    /// Returns a list of all ip and mac results
    /// </summary>
    /// <param name="manager">The tSettingsManager instance</param>
    /// <returns>The list of available results</returns>
    public static List<IListItem> GetNetworkConnectionResults(SettingsManager manager)
    {
        var results = new List<IListItem>();

        // We update the cache only if the last query is older than 'updateCacheIntervalSeconds' seconds
        DateTime timeOfLastNetworkQueryBefore = timeOfLastNetworkQuery;
        timeOfLastNetworkQuery = DateTime.Now;             // Set time of last query to this query
        if ((timeOfLastNetworkQuery - timeOfLastNetworkQueryBefore).TotalSeconds >= UpdateCacheIntervalSeconds)
        {
            networkPropertiesCache = NetworkConnectionProperties.GetList();
        }

        CompositeFormat sysIpv4DescriptionCompositeFormate = CompositeFormat.Parse(Resources.Microsoft_plugin_sys_ip4_description);
        CompositeFormat sysMacDescriptionCompositeFormate = CompositeFormat.Parse(Resources.Microsoft_plugin_sys_mac_description);
        var hideDisconnectedNetworkInfo = manager.HideDisconnectedNetworkInfo;

        foreach (NetworkConnectionProperties intInfo in networkPropertiesCache)
        {
            if (hideDisconnectedNetworkInfo)
            {
                if (intInfo.State != OperationalStatus.Up)
                {
                    continue;
                }
            }

            if (!string.IsNullOrEmpty(intInfo.IPv4))
            {
                results.Add(new ListItem(new CopyTextCommand(intInfo.GetConnectionDetails()))
                {
                    Title = intInfo.IPv4,
                    Subtitle = string.Format(CultureInfo.InvariantCulture, sysIpv4DescriptionCompositeFormate, intInfo.ConnectionName),
                    Icon = Icons.NetworkAdapterIcon,
                    Details = new Details() { Title = Resources.Microsoft_plugin_ext_connection_details, Body = intInfo.GetConnectionDetails() },
                });
            }

            if (!string.IsNullOrEmpty(intInfo.IPv6Primary))
            {
                results.Add(new ListItem(new CopyTextCommand(intInfo.GetConnectionDetails()))
                {
                    Title = intInfo.IPv6Primary,
                    Subtitle = string.Format(CultureInfo.InvariantCulture, sysIpv4DescriptionCompositeFormate, intInfo.ConnectionName),
                    Icon = Icons.NetworkAdapterIcon,
                    Details = new Details() { Title = Resources.Microsoft_plugin_ext_connection_details, Body = intInfo.GetConnectionDetails() },
                });
            }

            if (!string.IsNullOrEmpty(intInfo.PhysicalAddress))
            {
                results.Add(new ListItem(new CopyTextCommand(intInfo.GetAdapterDetails()))
                {
                    Title = intInfo.PhysicalAddress,
                    Subtitle = string.Format(CultureInfo.InvariantCulture, sysMacDescriptionCompositeFormate, intInfo.Adapter, intInfo.ConnectionName),
                    Icon = Icons.NetworkAdapterIcon,
                    Details = new Details() { Title = Resources.Microsoft_plugin_ext_connection_details, Body = intInfo.GetConnectionDetails() },
                });
            }
        }

        return results;
    }

    public static List<IListItem> GetAllCommands(SettingsManager manager)
    {
        var list = new List<IListItem>();
        var listLock = new object();

        // Network (ip and mac) results are slow with many network cards and returned delayed.
        // On global queries the first word/part has to be 'ip', 'mac' or 'address' for network results
        var networkConnectionResults = Commands.GetNetworkConnectionResults(manager);

        var isBootedInUefiMode = Win32Helpers.GetSystemFirmwareType() == FirmwareType.Uefi;

        var separateEmptyRB = manager.ShowSeparateResultForEmptyRecycleBin;
        var confirmSystemCommands = manager.ShowDialogToConfirmCommand;
        var showSuccessOnEmptyRB = manager.ShowSuccessMessageAfterEmptyingRecycleBin;

        // normal system commands are fast and can be returned immediately
        var systemCommands = Commands.GetSystemCommands(isBootedInUefiMode, separateEmptyRB, confirmSystemCommands, showSuccessOnEmptyRB);
        list.AddRange(systemCommands);
        list.AddRange(networkConnectionResults);

        return list;
    }
}
