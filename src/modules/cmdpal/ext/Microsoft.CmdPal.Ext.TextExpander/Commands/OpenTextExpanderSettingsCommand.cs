// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Security;
using Microsoft.CmdPal.Ext.TextExpander.Insertion;
using Microsoft.CmdPal.Ext.TextExpander.Properties;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Win32;

namespace Microsoft.CmdPal.Ext.TextExpander.Commands;

/// <summary>
/// Opens Text Expander's page in PowerToys Settings. PowerToys is found through the <c>powertoys:</c> protocol it
/// registers, since Command Palette is installed apart from it; without that, PowerToys Settings opens at its start.
/// Run as administrator, Command Palette trusts only a machine-wide PowerToys: anything running as the user can register
/// its own program for the protocol under HKCU, which the protocol itself resolves through too, and it would start elevated.
/// </summary>
internal sealed partial class OpenTextExpanderSettingsCommand : InvokableCommand
{
    private const string ProtocolCommandKey = @"Software\Classes\powertoys\shell\open\command";

    internal OpenTextExpanderSettingsCommand()
    {
        Name = Resources.open_settings_name;
        Icon = Icons.Settings;
    }

    public override ICommandResult Invoke()
    {
        var elevated = Win32Desktop.RunsElevated;
        var powerToys = FindPowerToys(HivesFor(elevated));
        var opened = (powerToys is not null && ShellHelpers.OpenInShell(powerToys, $"--open-settings={TextExpanderPaths.ModuleKey}"))
            || (!elevated && ShellHelpers.OpenInShell("powertoys://open_settings/"));

        return opened
            ? CommandResult.Dismiss()
            : CommandResult.ShowToast(new ToastArgs { Message = Resources.open_settings_failed_toast, Result = CommandResult.KeepOpen() });
    }

    /// <summary>
    /// Gets where to look for PowerToys: a per-user installation registers the protocol for the user, a machine-wide one
    /// for the machine. Elevated, only the machine's registration, which only an administrator can change.
    /// </summary>
    internal static RegistryHive[] HivesFor(bool elevated)
        => elevated ? [RegistryHive.LocalMachine] : [RegistryHive.CurrentUser, RegistryHive.LocalMachine];

    /// <summary>Gets PowerToys.exe, by its full path, from the command its protocol runs, or null.</summary>
    internal static string? PowerToysFromProtocolCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        // A bare name would be looked for along the search path.
        ShellHelpers.ParseExecutableAndArgs(command, out var executable, out _);
        return Path.IsPathFullyQualified(executable) && string.Equals(Path.GetFileName(executable), "PowerToys.exe", StringComparison.OrdinalIgnoreCase) ? executable : null;
    }

    private static string? FindPowerToys(RegistryHive[] hives)
    {
        foreach (var hive in hives)
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = root.OpenSubKey(ProtocolCommandKey);
                if (PowerToysFromProtocolCommand(key?.GetValue(string.Empty) as string) is { } executable && File.Exists(executable))
                {
                    return executable;
                }
            }
            catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
            {
                // Try the other hive.
            }
        }

        return null;
    }
}
