// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

using ManagedCommon;
using Microsoft.CmdPal.Ext.WindowsSettings.Classes;
using Microsoft.CmdPal.Ext.WindowsSettings.Properties;

namespace Microsoft.CmdPal.Ext.WindowsSettings.Helpers;

/// <summary>
/// Helper class to enumerate the Control Panel tasks that Windows exposes via
/// the shell "All Tasks" folder. These are the same entries Control Panel's
/// own search offers (e.g. "Set up USB game controllers"), most of which are
/// not part of the static <c>WindowsSettings.json</c> list. Names come from
/// the shell already localized in the user's display language.
/// </summary>
internal static class ControlPanelTasksHelper
{
    /// <summary>
    /// Marker prefix identifying commands that are shell parsing names of
    /// Control Panel task items (instead of executable command lines).
    /// </summary>
    internal const string ShellItemCommandPrefix = "::{";

    /// <summary>
    /// Enumerates all Control Panel tasks exposed by the shell.
    /// Returns an empty list when enumeration is not possible (e.g. server
    /// SKUs without the Control Panel namespace, or shell errors).
    /// </summary>
    internal static List<WindowsSetting> GetAllControlPanelTasks()
    {
        var result = new List<WindowsSetting>();

        var settingType = Resources.ResourceManager.GetString("AppControlPanel", CultureInfo.CurrentUICulture)
            ?? "Control Panel";

        try
        {
            var folder = ShellInterop.CreateItemFromParsingName(ShellInterop.AllTasksFolderParsingName);
            if (folder is null)
            {
                Logger.LogWarning("Could not open the shell All Tasks folder; Control Panel tasks will not be available in search.");
                return result;
            }

            var enumerator = ShellInterop.GetItemEnumerator(folder);
            if (enumerator is null)
            {
                Logger.LogWarning("Could not enumerate the shell All Tasks folder; Control Panel tasks will not be available in search.");
                return result;
            }

            while (enumerator.Next(1, out var itemPtr, out var fetched) == 0 && fetched == 1)
            {
                try
                {
                    var setting = CreateSettingFromShellItem(itemPtr, settingType);
                    if (setting is not null)
                    {
                        result.Add(setting);
                    }
                }
                finally
                {
                    if (itemPtr != IntPtr.Zero)
                    {
                        Marshal.Release(itemPtr);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            Logger.LogError($"Failed to enumerate Control Panel tasks: {exception.Message}");
        }

        return result;
    }

    /// <summary>
    /// Merges dynamically enumerated Control Panel tasks into the given
    /// settings list, skipping entries whose name is already known (either as
    /// a name or an alternative name of an existing setting). Returns the
    /// number of entries added.
    /// </summary>
    internal static int MergeIntoSettings(Classes.WindowsSettings windowsSettings, IReadOnlyCollection<WindowsSetting> controlPanelTasks)
    {
        if (windowsSettings?.Settings is null || controlPanelTasks is null || controlPanelTasks.Count == 0)
        {
            return 0;
        }

        var existingSettings = windowsSettings.Settings.ToList();

        var knownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var setting in existingSettings)
        {
            knownNames.Add(setting.Name);

            if (setting.AltNames is not null)
            {
                foreach (var altName in setting.AltNames)
                {
                    knownNames.Add(altName);
                }
            }
        }

        var added = 0;
        foreach (var task in controlPanelTasks)
        {
            if (string.IsNullOrWhiteSpace(task.Name) || string.IsNullOrWhiteSpace(task.Command))
            {
                continue;
            }

            if (!knownNames.Add(task.Name))
            {
                continue;
            }

            existingSettings.Add(task);
            added++;
        }

        if (added > 0)
        {
            windowsSettings.Settings = existingSettings;
        }

        return added;
    }

    private static WindowsSetting CreateSettingFromShellItem(IntPtr itemPtr, string settingType)
    {
        var item = ShellInterop.CreateShellItemFromPointer(itemPtr);
        if (item is null)
        {
            return null;
        }

        if (item.GetDisplayName(ShellInterop.SIGDNNormalDisplay, out var displayName) != 0
            || string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        if (item.GetDisplayName(ShellInterop.SIGDNDesktopAbsoluteParsing, out var parsingName) != 0
            || string.IsNullOrWhiteSpace(parsingName)
            || !parsingName.StartsWith(ShellItemCommandPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        // Task items cannot be re-parsed from their parsing name later, so
        // their id list has to be captured now — it is the only way to launch
        // them when the user selects the result.
        if (ShellInterop.SHGetIDListFromObject(itemPtr, out var pidl) != 0 || pidl == IntPtr.Zero)
        {
            return null;
        }

        byte[] idList;
        try
        {
            idList = ShellInterop.CopyIdList(pidl);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pidl);
        }

        return new WindowsSetting()
        {
            Name = displayName,
            Command = parsingName,
            Type = settingType,
            TaskIdList = idList,

            // Control Panel tasks have no areas, so their settings path is
            // just the type. Filled here rather than by re-running
            // WindowsSettingsPathHelper over the whole list, which would
            // mutate entries the search page may be reading concurrently.
            JoinedAreaPath = string.Empty,
            JoinedFullSettingsPath = settingType,
        };
    }
}
