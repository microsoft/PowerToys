// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.PowerToys.Run.Plugin.WindowsSettings.Properties;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.WindowsSettings.Helper
{
    /// <summary>
    /// Helper class to help with the settings path (app and area) and area path.
    /// </summary>
    internal static class WindowsSettingsPathHelper
    {
        /// <summary>
        /// Symbol to use between the parts of the path.
        /// </summary>
        internal static readonly string PathOrderSymbol = "\u25B9";

        /// <summary>
        /// Return a string with the area path of a <see cref="WindowsSetting"/>. The result includes the delimiter sign.
        /// </summary>
        /// <param name="areaList"> The list the ordered areas to combine.
        internal static string ReturnAreaPath(in IList<string>? areaList)
        {
            if (areaList is null)
            {
                return string.Empty;
            }

            return string.Join($" {WindowsSettingsPathHelper.PathOrderSymbol} ", areaList);
        }

        /// <summary>
        /// Return a string with the complete settings path (app and area) of a <see cref="WindowsSetting"/>. If areas are not defined then only the app is returned. The result includes the delimiter sign.
        /// </summary>
        /// <param name="settingsApp"> The type/app of the setting.
        /// <param name="areaList"> The list the ordered areas to combine.
        internal static string ReturnFullSettingsPath(in string settingsApp, in IList<string>? areaList)
        {
            if (areaList != null)
            {
                string areaString = string.Join($" {WindowsSettingsPathHelper.PathOrderSymbol} ", areaList);
                return $"{settingsApp} {WindowsSettingsPathHelper.PathOrderSymbol} {areaString}";
            }
            else
            {
                // If no areas are defined reutrn only the name of the app {WindowsSetting.type}.
                return settingsApp;
            }
        }
    }
}
