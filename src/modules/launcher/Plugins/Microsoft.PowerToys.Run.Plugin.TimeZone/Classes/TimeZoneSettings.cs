// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Properties;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.Run.Plugin.TimeZone.Classes
{
    /// <summary>
    /// Additional settings for the time zone plugin.
    /// </summary>
    internal sealed class TimeZoneSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether the time zone name of a time zone is shown in the results.
        /// </summary>
        internal bool ShowTimeZoneNames { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the time name of a time zone is shown in the results.
        /// </summary>
        internal bool ShowTimeNames { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the military name of a time zone is shown in the results.
        /// </summary>
        internal bool ShowMilitaryTimeZoneNames { get; set; }

        /// <summary>
        /// Return a list with all settings. Additional
        /// </summary>
        /// <returns>A list with all settings.</returns>
        internal static List<PluginAdditionalOption> GetAdditionalOptions()
        {
            var optionList = new List<PluginAdditionalOption>
            {
                new PluginAdditionalOption
                {
                    Key = "ShowTimeZoneNames",
                    DisplayLabel = Resources.ShowTimeZoneNames,
                    Value = true,
                },
                new PluginAdditionalOption
                {
                    Key = "ShowTimeNames",
                    DisplayLabel = Resources.ShowTimeNames,
                    Value = true,
                },
                new PluginAdditionalOption
                {
                    Key = "ShowMilitaryTimeZoneNames",
                    DisplayLabel = Resources.ShowMilitaryTimeZoneNames,
                    Value = false,
                },
            };

            return optionList;
        }

        /// <summary>
        /// Update this settings.
        /// </summary>
        /// <param name="settings">The settings for all power launcher plugin.</param>
        internal void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            if (settings is null || settings.AdditionalOptions is null)
            {
                return;
            }

            ShowTimeZoneNames = GetSettingOrDefault(settings, "ShowTimeZoneNames");
            ShowTimeNames = GetSettingOrDefault(settings, "ShowTimeNames");
            ShowMilitaryTimeZoneNames = GetSettingOrDefault(settings, "ShowMilitaryTimeZoneNames");
        }

        /// <summary>
        /// Return one <see cref="bool"/> setting of the given settings list with the given name.
        /// </summary>
        /// <param name="settings">The object that contain all settings.</param>
        /// <param name="name">The name of the setting.</param>
        /// <returns>A settings value.</returns>
        private static bool GetSettingOrDefault(PowerLauncherPluginSettings settings, string name)
        {
            var option = settings.AdditionalOptions.FirstOrDefault(x => x.Key == name);

            // As a fallback if a setting isn't available, we use the value defined in the method GetAdditionalOptions()
            var settingsValue = option?.Value
                ?? GetAdditionalOptions().FirstOrDefault(x => x.Key == name)?.Value
                ?? default;

            return settingsValue;
        }
    }
}
