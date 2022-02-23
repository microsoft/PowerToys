// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Classes;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Properties;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.TimeZone.Helper
{
    /// <summary>
    /// Helper class to easier work with translations.
    /// </summary>
    internal static class TranslationHelper
    {
        /// <summary>
        /// Translate all names and countries of the <see cref="TimeZoneList"/> class.
        /// </summary>
        /// <param name="timeZoneList">A class that contain all possible time zones.</param>
        internal static void TranslateAllSettings(in TimeZoneList timeZoneList)
        {
            if (timeZoneList?.TimeZones is null)
            {
                return;
            }

            foreach (var timeZone in timeZoneList.TimeZones)
            {
                // Translate Name
                if (!string.IsNullOrWhiteSpace(timeZone.Name))
                {
                    var name = Resources.ResourceManager.GetString(timeZone.Name, CultureInfo.InvariantCulture);
                    if (string.IsNullOrEmpty(name))
                    {
                        Log.Warn($"Resource string for [{timeZone.Name}] not found", typeof(TranslationHelper));
                    }

                    timeZone.Name = name ?? timeZone.Name ?? string.Empty;
                }

                // Translate MilitaryName
                if (!string.IsNullOrWhiteSpace(timeZone.MilitaryName))
                {
                    var militaryName = Resources.ResourceManager.GetString(timeZone.MilitaryName, CultureInfo.InvariantCulture);
                    if (string.IsNullOrEmpty(militaryName))
                    {
                        Log.Warn($"Resource string for [{timeZone.MilitaryName}] not found", typeof(TranslationHelper));
                    }

                    timeZone.MilitaryName = militaryName ?? timeZone.MilitaryName ?? string.Empty;
                }

                // Translate TimeNamesDaylight
                if (!(timeZone.TimeNamesDaylight is null))
                {
                    var timeNamesDaylight = new List<string>();

                    foreach (var nameDaylight in timeZone.TimeNamesDaylight)
                    {
                        var nameDaylightT = Resources.ResourceManager.GetString(nameDaylight, CultureInfo.InvariantCulture);
                        if (string.IsNullOrEmpty(nameDaylightT))
                        {
                            Log.Warn($"Resource string for [{nameDaylight}] not found", typeof(TranslationHelper));
                        }

                        timeNamesDaylight.Add(nameDaylightT ?? nameDaylight ?? string.Empty);
                    }

                    timeZone.TimeNamesDaylight = timeNamesDaylight;
                }

                // Translate TimeNamesStandard
                if (!(timeZone.TimeNamesStandard is null))
                {
                    var timeNamesStandard = new List<string>();

                    foreach (var nameStandard in timeZone.TimeNamesStandard)
                    {
                        var nameStandardT = Resources.ResourceManager.GetString(nameStandard, CultureInfo.InvariantCulture);
                        if (string.IsNullOrEmpty(nameStandardT))
                        {
                            Log.Warn($"Resource string for [{nameStandard}] not found", typeof(TranslationHelper));
                        }

                        timeNamesStandard.Add(nameStandardT ?? nameStandard ?? string.Empty);
                    }

                    timeZone.TimeNamesStandard = timeNamesStandard;
                }

                // Translate CountriesDaylight
                if (!(timeZone.CountriesDaylight is null))
                {
                    var countriesDaylight = new List<string>();

                    foreach (var countryDaylight in timeZone.CountriesDaylight)
                    {
                        var countryDaylightT = Resources.ResourceManager.GetString(countryDaylight, CultureInfo.InvariantCulture);
                        if (string.IsNullOrEmpty(countryDaylightT))
                        {
                            Log.Warn($"Resource string for [{countryDaylight}] not found", typeof(TranslationHelper));
                        }

                        countriesDaylight.Add(countryDaylightT ?? countryDaylight ?? string.Empty);
                    }

                    timeZone.CountriesDaylight = countriesDaylight;
                }

                // Translate CountriesStandard
                if (!(timeZone.CountriesStandard is null))
                {
                    var countriesStandard = new List<string>();

                    foreach (var countryStandard in timeZone.CountriesStandard)
                    {
                        var countryStandardT = Resources.ResourceManager.GetString(countryStandard, CultureInfo.InvariantCulture);
                        if (string.IsNullOrEmpty(countryStandardT))
                        {
                            Log.Warn($"Resource string for [{countryStandard}] not found", typeof(TranslationHelper));
                        }

                        countriesStandard.Add(countryStandardT ?? countryStandard ?? string.Empty);
                    }

                    timeZone.CountriesStandard = countriesStandard;
                }
            }
        }
    }
}
