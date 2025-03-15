// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;

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
            // Check if type value is filled. If not, then write log warning.
            if (string.IsNullOrEmpty(settings.Type))
            {
                // TODO GH #108 Logging is something we have to take care of
                // Log.Warn($"The type property is not set for setting [{settings.Name}] in json. Skipping generating of settings path.", typeof(WindowsSettingsPathHelper));
                continue;
            }

            // Check if "JoinedAreaPath" and "JoinedFullSettingsPath" are filled. Then log debug message.
            if (!string.IsNullOrEmpty(settings.JoinedAreaPath))
            {
                // Log.Debug($"The property [JoinedAreaPath] of setting [{settings.Name}] was filled from the json. This value is not used and will be overwritten.", typeof(WindowsSettingsPathHelper));
            }

            if (!string.IsNullOrEmpty(settings.JoinedFullSettingsPath))
            {
                // TODO GH #108 Logging is something we have to take care of
                // Log.Debug($"The property [JoinedFullSettingsPath] of setting [{settings.Name}] was filled from the json. This value is not used and will be overwritten.", typeof(WindowsSettingsPathHelper));
            }

            // Generating path values.
            if (!(settings.Areas is null) && settings.Areas.Any())
            {
                var areaValue = string.Join(_pathDelimiterSequence, settings.Areas);
                settings.JoinedAreaPath = areaValue;
                settings.JoinedFullSettingsPath = $"{settings.Type}{_pathDelimiterSequence}{areaValue}";
            }
            else
            {
                settings.JoinedAreaPath = string.Empty;
                settings.JoinedFullSettingsPath = settings.Type;
            }
        }
    }
}
