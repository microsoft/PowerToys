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
    /// Gets a resource string in the requested culture. Used so that the command names can
    /// be shown/matched in English instead of the system language if the user disabled the
    /// "localize system commands" setting. (Confirmation dialogs always follow the UI
    /// language, matching the behavior of the PowerToys Run System plugin.)
    /// </summary>
    private static string GetString(string resourceName, CultureInfo culture)
        => Resources.ResourceManager.GetString(resourceName, culture) ?? string.Empty;

    /// <summary>
    /// Returns a list with all system command results
    /// </summary>
    /// <param name="isUefi">Value indicating if the system is booted in uefi mode</param>
    /// <param name="hideEmptyRecycleBin">Value indicating if we should hide the Empty Recycle Bin command.</param>
    /// <param name="confirmCommands">A value indicating if the user should confirm the system commands</param>
    /// <param name="emptyRBSuccessMessage">Show a success message after empty Recycle Bin.</param>
    /// <param name="culture">The culture to use for the result's title and subtitle</param>
    /// <returns>A list of all results</returns>
    public static List<IListItem> GetSystemCommands(bool isUefi, bool hideEmptyRecycleBin, bool confirmCommands, bool emptyRBSuccessMessage, CultureInfo culture)
    {
        var results = new List<IListItem>();
        results.AddRange(new[]
        {
            new ListItem(
                new ExecuteCommandConfirmation(Resources.Microsoft_plugin_command_name_shutdown, confirmCommands, Resources.Microsoft_plugin_sys_shutdown_computer_confirmation, () => OpenInShellHelper.OpenInShell("shutdown", "/s /hybrid /t 0", runWithHiddenWindow: true))
                {
                    Id = "com.microsoft.cmdpal.builtin.system.shutdown",
                })
            {
                Title = GetString(nameof(Resources.Microsoft_plugin_sys_shutdown_computer), culture),
                Icon = Icons.ShutdownIcon,
            },
            new ListItem(
                new ExecuteCommandConfirmation(Resources.Microsoft_plugin_command_name_restart, confirmCommands, Resources.Microsoft_plugin_sys_restart_computer_confirmation, () => OpenInShellHelper.OpenInShell("shutdown", "/g /t 0", runWithHiddenWindow: true))
                {
                    Id = "com.microsoft.cmdpal.builtin.system.restart",
                })
            {
                Title = GetString(nameof(Resources.Microsoft_plugin_sys_restart_computer), culture),
                Icon = Icons.RestartIcon,
            },
            new ListItem(
                new ExecuteCommandConfirmation(Resources.Microsoft_plugin_command_name_signout, confirmCommands, Resources.Microsoft_plugin_sys_sign_out_confirmation, () => NativeMethods.ExitWindowsEx(EWXLOGOFF, 0))
                {
                    Id = "com.microsoft.cmdpal.builtin.system.signout",
                })
            {
                Title = GetString(nameof(Resources.Microsoft_plugin_sys_sign_out), culture),
                Icon = Icons.LogoffIcon,
            },
            new ListItem(
                new ExecuteCommandConfirmation(Resources.Microsoft_plugin_command_name_lock, confirmCommands, Resources.Microsoft_plugin_sys_lock_confirmation, () => NativeMethods.LockWorkStation())
                {
                    Id = "com.microsoft.cmdpal.builtin.system.lock",
                })
            {
                Title = GetString(nameof(Resources.Microsoft_plugin_sys_lock), culture),
                Icon = Icons.LockIcon,
            },
            new ListItem(
                new ExecuteCommandConfirmation(Resources.Microsoft_plugin_command_name_sleep, confirmCommands, Resources.Microsoft_plugin_sys_sleep_confirmation, () => NativeMethods.SetSuspendState(false, true, true))
                {
                    Id = "com.microsoft.cmdpal.builtin.system.sleep",
                })
            {
                Title = GetString(nameof(Resources.Microsoft_plugin_sys_sleep), culture),
                Icon = Icons.SleepIcon,
            },
            new ListItem(
                new ExecuteCommandConfirmation(Resources.Microsoft_plugin_command_name_hibernate, confirmCommands, Resources.Microsoft_plugin_sys_hibernate_confirmation, () => NativeMethods.SetSuspendState(true, true, true))
                {
                    Id = "com.microsoft.cmdpal.builtin.system.hibernate",
                })
            {
                Title = GetString(nameof(Resources.Microsoft_plugin_sys_hibernate), culture),
                Icon = Icons.HibernateIcon,
            },
        });

        // Show Recycle Bin results based on setting.
        if (!hideEmptyRecycleBin)
        {
            results.AddRange(new[]
            {
                new ListItem(new OpenInShellCommand(Resources.Microsoft_plugin_command_name_open, "explorer.exe", "shell:RecycleBinFolder")
                {
                    Id = "com.microsoft.cmdpal.builtin.system.recycle_bin",
                })
                {
                    Title = GetString(nameof(Resources.Microsoft_plugin_sys_RecycleBinOpen), culture),
                    Icon = Icons.RecycleBinIcon,
                },
                new ListItem(new EmptyRecycleBinConfirmation(emptyRBSuccessMessage)
                {
                    Id = "com.microsoft.cmdpal.builtin.system.empty_recycle_bin",
                })
                {
                    Title = GetString(nameof(Resources.Microsoft_plugin_sys_RecycleBinEmptyResult), culture),
                    Icon = Icons.RecycleBinIcon,
                },
            });
        }
        else
        {
            results.Add(
                new ListItem(new OpenInShellCommand(Resources.Microsoft_plugin_command_name_open, "explorer.exe", "shell:RecycleBinFolder")
                {
                    Id = "com.microsoft.cmdpal.builtin.system.recycle_bin",
                })
                {
                    Title = GetString(nameof(Resources.Microsoft_plugin_sys_RecycleBin), culture),
                    Icon = Icons.RecycleBinIcon,
                });
        }

        results.Add(new ListItem(
            new ExecuteCommandConfirmation(
                    Resources.Microsoft_plugin_sys_RestartShell_name!,
                    confirmCommands,
                    Resources.Microsoft_plugin_sys_RestartShell_confirmation!,
                    static () => OpenInShellHelper.OpenInShell("cmd", "/C tskill explorer && start explorer", runWithHiddenWindow: true))
            {
                Id = "com.microsoft.cmdpal.builtin.system.restart_shell",
            })
        {
            Title = GetString(nameof(Resources.Microsoft_plugin_sys_RestartShell), culture),
            Subtitle = GetString(nameof(Resources.Microsoft_plugin_sys_RestartShell_description), culture),
            Icon = Icons.RestartShellIcon,
        });

        // UEFI command/result. It is only available on systems booted in UEFI mode.
        if (isUefi)
        {
            results.Add(new ListItem(new ExecuteCommandConfirmation(Resources.Microsoft_plugin_command_name_reboot, confirmCommands, Resources.Microsoft_plugin_sys_uefi_confirmation, () => OpenInShellHelper.OpenInShell("shutdown", "/r /fw /t 0", null, OpenInShellHelper.ShellRunAsType.Administrator)))
            {
                Title = GetString(nameof(Resources.Microsoft_plugin_sys_uefi), culture),
                Subtitle = GetString(nameof(Resources.Microsoft_plugin_sys_uefi_description), culture),
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
    public static List<IListItem> GetNetworkConnectionResults(ISettingsInterface manager, CultureInfo culture)
    {
        var results = new List<IListItem>();

        // We update the cache only if the last query is older than 'updateCacheIntervalSeconds' seconds
        var timeOfLastNetworkQueryBefore = timeOfLastNetworkQuery;
        timeOfLastNetworkQuery = DateTime.Now;             // Set time of last query to this query
        if ((timeOfLastNetworkQuery - timeOfLastNetworkQueryBefore).TotalSeconds >= UpdateCacheIntervalSeconds)
        {
            networkPropertiesCache = NetworkConnectionProperties.GetList();
        }

        var sysIpv4DescriptionCompositeFormate = CompositeFormat.Parse(GetString(nameof(Resources.Microsoft_plugin_sys_ip4_description), culture));
        var sysIpv6DescriptionCompositeFormate = CompositeFormat.Parse(GetString(nameof(Resources.Microsoft_plugin_sys_ip6_description), culture));
        var sysMacDescriptionCompositeFormate = CompositeFormat.Parse(GetString(nameof(Resources.Microsoft_plugin_sys_mac_description), culture));
        var hideDisconnectedNetworkInfo = manager.HideDisconnectedNetworkInfo();

        foreach (var intInfo in networkPropertiesCache)
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
                    Subtitle = string.Format(CultureInfo.InvariantCulture, sysIpv6DescriptionCompositeFormate, intInfo.ConnectionName),
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
                    Details = new Details() { Title = Resources.Microsoft_plugin_ext_adapter_details, Body = intInfo.GetAdapterDetails() },
                });
            }
        }

        return results;
    }

    public static List<IListItem> GetAllCommands(ISettingsInterface manager)
    {
        var list = new List<IListItem>();
        var listLock = new object();

        var culture = manager.LocalizeSystemCommands() ? CultureInfo.CurrentUICulture : new CultureInfo("en-US");

        // Network (ip and mac) results are slow with many network cards and returned delayed.
        // On global queries the first word/part has to be 'ip', 'mac' or 'address' for network results
        var networkConnectionResults = Commands.GetNetworkConnectionResults(manager, culture);

        var isBootedInUefiMode = manager.GetSystemFirmwareType() == FirmwareType.Uefi;

        var hideEmptyRB = manager.HideEmptyRecycleBin();
        var confirmSystemCommands = manager.ShowDialogToConfirmCommand();
        var showSuccessOnEmptyRB = manager.ShowSuccessMessageAfterEmptyingRecycleBin();

        // normal system commands are fast and can be returned immediately
        var systemCommands = Commands.GetSystemCommands(isBootedInUefiMode, hideEmptyRB, confirmSystemCommands, showSuccessOnEmptyRB, culture);
        list.AddRange(systemCommands);
        list.AddRange(networkConnectionResults);

        return list;
    }
}
