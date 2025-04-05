// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

using Microsoft.PowerToys.Run.Plugin.TimeDate.Properties;
using Microsoft.PowerToys.Settings.UI.Library;

[assembly: InternalsVisibleTo("Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests")]

namespace Microsoft.PowerToys.Run.Plugin.TimeDate.Components
{
    /// <summary>
    /// Additional settings for the WindowWalker plugin.
    /// </summary>
    /// <remarks>Some code parts reused from TimeZone plugin.</remarks>
    internal sealed class TimeDateSettings
    {
        /// <summary>
        /// Are the class properties initialized with default values
        /// </summary>
        private readonly bool _initialized;

        /// <summary>
        /// An instance of the class <see cref="TimeDateSettings"></see>
        /// </summary>
        private static TimeDateSettings instance;

        /// <summary>
        /// Gets the value of the "First Week Rule" setting
        /// </summary>
        internal int CalendarFirstWeekRule { get; private set; }

        /// <summary>
        /// Gets the value of the "First Day Of Week" setting
        /// </summary>
        internal int FirstDayOfWeek { get; private set; }

        /// <summary>
        /// Gets a value indicating whether to show only the time and date for system time in global results or not
        /// </summary>
        internal bool OnlyDateTimeNowGlobal { get; private set; }

        /// <summary>
        /// Gets a value indicating whether to show the time with seconds or not
        /// </summary>
        internal bool TimeWithSeconds { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the date with the weekday or not
        /// </summary>
        internal bool DateWithWeekday { get; private set; }

        /// <summary>
        /// Gets a value indicating whether to hide the number input error message on global results
        /// </summary>
        internal bool HideNumberMessageOnGlobalQuery { get; private set; }

        /// <summary>
        /// Gets a value containing the custom format definitions
        /// </summary>
        internal List<string> CustomFormats { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeDateSettings"/> class.
        /// Private constructor to make sure there is never more than one instance of this class
        /// </summary>
        private TimeDateSettings()
        {
            // Init class properties with default values
            UpdateSettings(null);
            _initialized = true;
        }

        /// <summary>
        /// Gets an instance property of this class that makes sure that the first instance gets created
        /// and that all the requests end up at that one instance.
        /// The benefit of this is that we don't need additional variables/parameters
        /// to communicate the settings between plugin's classes/methods.
        /// We can simply access this one instance, whenever we need the actual settings.
        /// </summary>
        internal static TimeDateSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new TimeDateSettings();
                }

                return instance;
            }
        }

        /// <summary>
        /// Return a list with all additional plugin options.
        /// </summary>
        /// <returns>A list with all additional plugin options.</returns>
        internal static List<PluginAdditionalOption> GetAdditionalOptions()
        {
            var optionList = new List<PluginAdditionalOption>
            {
                new PluginAdditionalOption()
                {
                    Key = nameof(OnlyDateTimeNowGlobal),
                    DisplayLabel = Resources.Microsoft_plugin_timedate_SettingOnlyDateTimeNowGlobal,
                    DisplayDescription = Resources.Microsoft_plugin_timedate_SettingOnlyDateTimeNowGlobal_Description,
                    Value = true,
                },
                new PluginAdditionalOption()
                {
                    Key = nameof(TimeWithSeconds),
                    DisplayLabel = Resources.Microsoft_plugin_timedate_SettingTimeWithSeconds,
                    DisplayDescription = Resources.Microsoft_plugin_timedate_SettingTimeWithSeconds_Description,
                    Value = false,
                },
                new PluginAdditionalOption()
                {
                    Key = nameof(DateWithWeekday),
                    DisplayLabel = Resources.Microsoft_plugin_timedate_SettingDateWithWeekday,
                    DisplayDescription = Resources.Microsoft_plugin_timedate_SettingDateWithWeekday_Description,
                    Value = false,
                },
                new PluginAdditionalOption()
                {
                    Key = nameof(HideNumberMessageOnGlobalQuery),
                    DisplayLabel = Resources.Microsoft_plugin_timedate_SettingHideNumberMessageOnGlobalQuery,
                    Value = false,
                },
                new PluginAdditionalOption()
                {
                    Key = nameof(CalendarFirstWeekRule),
                    DisplayLabel = Resources.Microsoft_plugin_timedate_SettingFirstWeekRule,
                    DisplayDescription = Resources.Microsoft_plugin_timedate_SettingFirstWeekRule_Description,
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
                    ComboBoxItems = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>(Resources.Microsoft_plugin_timedate_Setting_UseSystemSetting, "-1"),
                        new KeyValuePair<string, string>(Resources.Microsoft_plugin_timedate_SettingFirstWeekRule_FirstDay, "0"),
                        new KeyValuePair<string, string>(Resources.Microsoft_plugin_timedate_SettingFirstWeekRule_FirstFullWeek, "1"),
                        new KeyValuePair<string, string>(Resources.Microsoft_plugin_timedate_SettingFirstWeekRule_FirstFourDayWeek, "2"),
                    },
                    ComboBoxValue = -1,
                },
                new PluginAdditionalOption()
                {
                    Key = nameof(FirstDayOfWeek),
                    DisplayLabel = Resources.Microsoft_plugin_timedate_SettingFirstDayOfWeek,
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
                    ComboBoxItems = GetSortedListForWeekDaySetting(),
                    ComboBoxValue = -1,
                },
                new PluginAdditionalOption()
                {
                    Key = nameof(CustomFormats),
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.MultilineTextbox,
                    DisplayLabel = Resources.Microsoft_plugin_timedate_Setting_CustomFormats,
                    DisplayDescription = string.Format(CultureInfo.CurrentCulture, Resources.Microsoft_plugin_timedate_Setting_CustomFormatsDescription.ToString(), "DOW", "DIM", "WOM", "WOY", "EAB", "WFT", "UXT", "UMS", "OAD", "EXC", "EXF", "UTC:"),
                    PlaceholderText = "MyFormat=dd-MMM-yyyy\rMySecondFormat=dddd (Da\\y nu\\mber: DOW)\rMyUtcFormat=UTC:hh:mm:ss",
                    TextValue = string.Empty,
                },
            };

            return optionList;
        }

        /// <summary>
        /// Update this settings.
        /// </summary>
        /// <param name="settings">The settings for all power launcher plugins.</param>
        internal void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            if ((settings is null || settings.AdditionalOptions is null) & _initialized)
            {
                return;
            }

            CalendarFirstWeekRule = GetEnumSettingOrDefault(settings, nameof(CalendarFirstWeekRule));
            FirstDayOfWeek = GetEnumSettingOrDefault(settings, nameof(FirstDayOfWeek));
            OnlyDateTimeNowGlobal = GetSettingOrDefault(settings, nameof(OnlyDateTimeNowGlobal));
            TimeWithSeconds = GetSettingOrDefault(settings, nameof(TimeWithSeconds));
            DateWithWeekday = GetSettingOrDefault(settings, nameof(DateWithWeekday));
            HideNumberMessageOnGlobalQuery = GetSettingOrDefault(settings, nameof(HideNumberMessageOnGlobalQuery));
            CustomFormats = GetMultilineTextSettingOrDefault(settings, nameof(CustomFormats));
        }

        /// <summary>
        /// Return one <see cref="bool"/> setting of the given settings list with the given name.
        /// </summary>
        /// <param name="settings">The object that contain all settings.</param>
        /// <param name="name">The name of the setting.</param>
        /// <returns>A settings value.</returns>
        private static bool GetSettingOrDefault(PowerLauncherPluginSettings settings, string name)
        {
            var option = settings?.AdditionalOptions?.FirstOrDefault(x => x.Key == name);

            // If a setting isn't available, we use the value defined in the method GetAdditionalOptions() as fallback.
            // We can use First() instead of FirstOrDefault() because the values must exist. Otherwise, we made a mistake when defining the settings.
            return option?.Value ?? GetAdditionalOptions().First(x => x.Key == name).Value;
        }

        /// <summary>
        /// Return the combobox value of the given settings list with the given name.
        /// </summary>
        /// <param name="settings">The object that contain all settings.</param>
        /// <param name="name">The name of the setting.</param>
        /// <returns>A settings value.</returns>
        private static int GetEnumSettingOrDefault(PowerLauncherPluginSettings settings, string name)
        {
            var option = settings?.AdditionalOptions?.FirstOrDefault(x => x.Key == name);

            // If a setting isn't available, we use the value defined in the method GetAdditionalOptions() as fallback.
            // We can use First() instead of FirstOrDefault() because the values must exist. Otherwise, we made a mistake when defining the settings.
            return option?.ComboBoxValue ?? GetAdditionalOptions().First(x => x.Key == name).ComboBoxValue;
        }

        /// <summary>
        /// Return the combobox value of the given settings list with the given name.
        /// </summary>
        /// <param name="settings">The object that contain all settings.</param>
        /// <param name="name">The name of the setting.</param>
        /// <returns>A settings value.</returns>
        private static List<string> GetMultilineTextSettingOrDefault(PowerLauncherPluginSettings settings, string name)
        {
            var option = settings?.AdditionalOptions?.FirstOrDefault(x => x.Key == name);

            // If a setting isn't available, we use the value defined in the method GetAdditionalOptions() as fallback.
            // We can use First() instead of FirstOrDefault() because the values must exist. Otherwise, we made a mistake when defining the settings.
            return option?.TextValueAsMultilineList ?? GetAdditionalOptions().First(x => x.Key == name).TextValueAsMultilineList;
        }

        /// <summary>
        /// Returns a sorted list of values for the combo box of 'first day of week' setting.
        /// The list is sorted based on the current system culture setting.
        /// </summary>
        /// <remarks>In the world we have three groups of countries: Saturday, Sunday, Monday (Everything else is chosen by the user.)</remarks>
        /// <returns>List of values for combo box.</returns>
        private static List<KeyValuePair<string, string>> GetSortedListForWeekDaySetting()
        {
            // List (Sorted for first day is Sunday)
            var list = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(Resources.Microsoft_plugin_timedate_Setting_UseSystemSetting, "-1"),
                new KeyValuePair<string, string>(Resources.Microsoft_plugin_timedate_SettingFirstDayOfWeek_Sunday, "0"),
                new KeyValuePair<string, string>(Resources.Microsoft_plugin_timedate_SettingFirstDayOfWeek_Monday, "1"),
                new KeyValuePair<string, string>(Resources.Microsoft_plugin_timedate_SettingFirstDayOfWeek_Tuesday, "2"),
                new KeyValuePair<string, string>(Resources.Microsoft_plugin_timedate_SettingFirstDayOfWeek_Wednesday, "3"),
                new KeyValuePair<string, string>(Resources.Microsoft_plugin_timedate_SettingFirstDayOfWeek_Thursday, "4"),
                new KeyValuePair<string, string>(Resources.Microsoft_plugin_timedate_SettingFirstDayOfWeek_Friday, "5"),
                new KeyValuePair<string, string>(Resources.Microsoft_plugin_timedate_SettingFirstDayOfWeek_Saturday, "6"),
            };

            // Order Rules
            string[] orderRuleSaturday = new string[] { "-1", "6", "0", "1", "2", "3", "4", "5" };
            string[] orderRuleMonday = new string[] { "-1", "1", "2", "3", "4", "5", "6", "0" };

            switch (DateTimeFormatInfo.CurrentInfo.FirstDayOfWeek)
            {
                case DayOfWeek.Saturday:
                    return list.OrderBy(x => Array.IndexOf(orderRuleSaturday, x.Value)).ToList();
                case DayOfWeek.Monday:
                    return list.OrderBy(x => Array.IndexOf(orderRuleMonday, x.Value)).ToList();
                default:
                    // DayOfWeek.Sunday
                    return list;
            }
        }
    }
}
