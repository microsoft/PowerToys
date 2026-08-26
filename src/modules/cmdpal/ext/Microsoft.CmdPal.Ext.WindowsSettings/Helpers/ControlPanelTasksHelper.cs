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
            Logger.LogError($"Failed to enumerate Control Panel tasks", exception);
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

        // The applet the task belongs to (e.g. "Devices and Printers") becomes
        // the area, and the keywords Control Panel's own search matches (e.g.
        // "joystick") become alternative names — so the entries take part in
        // area, path and alternative name search like any static entry. Both
        // come from the shell already localized.
        IList<string> areas = null;
        IEnumerable<string> altNames = null;
        if (item is IShellItem2 item2)
        {
            if (item2.GetString(in ShellInterop.PKeyApplicationName, out var appName) == 0
                && !string.IsNullOrWhiteSpace(appName))
            {
                areas = new List<string>(1) { appName };
            }

            if (item2.GetString(in ShellInterop.PKeyKeywords, out var keywords) == 0
                && !string.IsNullOrWhiteSpace(keywords))
            {
                altNames = ParseKeywords(keywords);
            }
        }

        var setting = new WindowsSetting()
        {
            Name = displayName,
            Command = parsingName,
            Type = settingType,
            Areas = areas,
            AltNames = altNames,
            TaskIdList = idList,
        };

        // Generate the settings path (subtitle and ">" path search) the same
        // way it is generated for the static entries. Done here per setting
        // rather than by re-running the helper over the whole merged list,
        // which would mutate entries the search page may be reading
        // concurrently.
        WindowsSettingsPathHelper.GeneratePathValues(setting);

        return setting;
    }

    /// <summary>
    /// Splits the semicolon separated keyword string of a Control Panel task
    /// into distinct, trimmed alternative names. Returns null when no usable
    /// keyword remains.
    /// </summary>
    internal static IEnumerable<string> ParseKeywords(string keywords)
    {
        if (string.IsNullOrWhiteSpace(keywords))
        {
            return null;
        }

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var keyword in keywords.Split(';'))
        {
            var trimmed = keyword.Trim();
            if (trimmed.Length == 0 || !seen.Add(trimmed))
            {
                continue;
            }

            result.Add(trimmed);
        }

        return result.Count > 0 ? result : null;
    }
}
