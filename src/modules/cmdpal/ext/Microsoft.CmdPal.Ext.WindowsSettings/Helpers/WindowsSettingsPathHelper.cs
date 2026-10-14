// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using ManagedCommon;

namespace Microsoft.CmdPal.Ext.WindowsSettings.Helpers;

/// <summary>
/// Helper class to help with the path of a <see cref="WindowsSetting"/>. The settings path shows where to find a setting within Windows' user interface.
/// </summary>
internal static class WindowsSettingsPathHelper
{
    /// <summary>
    /// The symbol which is used as delimiter between the parts of the path.
    /// </summary>
    private const string _pathDelimiterSequence = "\u0020\u0020\u02C3\u0020\u0020"; // = "<space><space><arrow><space><space>"

    /// <summary>
    /// Generates the values for <see cref="WindowsSetting.JoinedAreaPath"/> and <see cref="WindowsSetting.JoinedFullSettingsPath"/> on all settings of the list in the given <see cref="WindowsSettings"/> class.
    /// </summary>
    /// <param name="windowsSettings">A class that contain all possible windows settings.</param>
    internal static void GenerateSettingsPathValues(in Classes.WindowsSettings windowsSettings)
    {
        if (windowsSettings?.Settings is null)
        {
            return;
        }

        foreach (var settings in windowsSettings.Settings)
        {
            GeneratePathValues(settings);
        }
    }

    /// <summary>
    /// Generates the values for <see cref="WindowsSetting.JoinedAreaPath"/> and <see cref="WindowsSetting.JoinedFullSettingsPath"/> on a single setting.
    /// </summary>
    /// <param name="setting">The setting to generate the path values for.</param>
    internal static void GeneratePathValues(Classes.WindowsSetting setting)
    {
        // Check if type value is filled. If not, then write log warning.
        if (string.IsNullOrEmpty(setting.Type))
        {
            // TODO GH #108 Logging is something we have to take care of
            // Log.Warn($"The type property is not set for setting [{setting.Name}]. Skipping generating of settings path.", typeof(WindowsSettingsPathHelper));
            Logger.LogWarning($"The type property is not set for setting [{setting.Name}]. Skipping generating of settings path.");
            return;
        }

        // Check if "JoinedAreaPath" and "JoinedFullSettingsPath" are filled. Then log debug message.
        if (!string.IsNullOrEmpty(setting.JoinedAreaPath))
        {
            // Log.Debug($"The property [JoinedAreaPath] of setting [{setting.Name}] was filled from the json. This value is not used and will be overwritten.", typeof(WindowsSettingsPathHelper));
            Logger.LogDebug($"The property [JoinedAreaPath] of setting [{setting.Name}] was filled from the json. This value is not used and will be overwritten.");
        }

        if (!string.IsNullOrEmpty(setting.JoinedFullSettingsPath))
        {
            // TODO GH #108 Logging is something we have to take care of
            // Log.Debug($"The property [JoinedFullSettingsPath] of setting [{setting.Name}] was filled from the json. This value is not used and will be overwritten.", typeof(WindowsSettingsPathHelper));
            Logger.LogDebug($"The property [JoinedFullSettingsPath] of setting [{setting.Name}] was filled from the json. This value is not used and will be overwritten.");
        }

        // Generating path values.
        if (!(setting.Areas is null) && setting.Areas.Any())
        {
            var areaValue = string.Join(_pathDelimiterSequence, setting.Areas);
            setting.JoinedAreaPath = areaValue;
            setting.JoinedFullSettingsPath = $"{setting.Type}{_pathDelimiterSequence}{areaValue}";
        }
        else
        {
            setting.JoinedAreaPath = string.Empty;
            setting.JoinedFullSettingsPath = setting.Type;
        }
    }
}
