// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.WindowsSettings.Helper
{
    /// <summary>
    /// Helper class to help with the path of a <see cref="WindowsSetting"/>. The settings path shows where to find a setting within Windows' user interface.
    /// </summary>
    internal static class WindowsSettingsPathHelper
    {
        /// <summary>
        /// The symbol which is used as delimiter between the parts of the path.
        /// </summary>
        private const string _pathDelimiterSymbol = "\u25B9";

        /// <summary>
        /// Generates the values for <see cref="WindowsSetting.JoinedAreaPath"/> and <see cref="WindowsSetting.JoinedFullSettingsPath"/> on all settings of the given list with <see cref="WindowsSetting"/>.
        /// </summary>
        /// <param name="settingsList">The list that contains <see cref="WindowsSetting"/> to translate.</param>
        internal static void GenerateSettingsPathValues(in IEnumerable<WindowsSetting>? settingsList)
        {
            if (settingsList is null)
            {
                return;
            }

            foreach (var settings in settingsList)
            {
                // Check if type value is filled. If not, then write log warning.
                if (settings.Type is null)
                {
                    Log.Warn($"The type property is not set for setting [{settings.Name}] in json. Skipping generating of settings path.", typeof(Main));
                }

                // Check if "JoinedAreaPath" and "JoinedFullSettingsPath" are filled. Then log debug message.
                if (!(settings.JoinedAreaPath == string.Empty && settings.JoinedAreaPath is null))
                {
                    Log.Debug($"The property [JoinedAreaPath] of setting [{settings.Name}] was filled from the json. This value is not used and will be overwritten.", typeof(Main));
                }

                if (!(settings.JoinedFullSettingsPath == string.Empty && settings.JoinedFullSettingsPath is null))
                {
                    Log.Debug($"The property [JoinedFullSettingsPath] of setting [{settings.Name}] was filled from the json. This value is not used and will be overwritten.", typeof(Main));
                }

                // Generating path values.
                if (!(settings.Areas is null) && settings.Areas.Any())
                {
                    var areaValue = string.Join($" {WindowsSettingsPathHelper._pathDelimiterSymbol} ", settings.Areas);
                    settings.JoinedAreaPath = areaValue;
                    settings.JoinedFullSettingsPath = $"{settings.Type} {WindowsSettingsPathHelper._pathDelimiterSymbol} {areaValue}";
                }
                else
                {
                    settings.JoinedAreaPath = string.Empty;
                    settings.JoinedFullSettingsPath = settings.Type;
                }
            }
        }
    }
}
