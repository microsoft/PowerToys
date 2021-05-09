// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.PowerToys.Run.Plugin.WindowsSettings.Classes;
using Microsoft.PowerToys.Run.Plugin.WindowsSettings.Properties;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.WindowsSettings.Helper
{
    /// <summary>
    /// Helper class to easier work with translations.
    /// </summary>
    internal static class TranslationHelper
    {
        /// <summary>
        /// Translate all settings of the given list with <see cref="WindowsSetting"/>.
        /// </summary>
        /// <param name="settingsList">The list that contains <see cref="WindowsSetting"/> to translate.</param>
        internal static void TranslateAllSettings(in IEnumerable<WindowsSetting>? settingsList)
        {
            if (settingsList is null)
            {
                return;
            }

            foreach (var settings in settingsList)
            {
                var area = Resources.ResourceManager.GetString($"Area{settings.Area}");
                var name = Resources.ResourceManager.GetString(settings.Name);

                if (string.IsNullOrEmpty(area))
                {
                    Log.Warn($"Resource string for [Area{settings.Area}] not found", typeof(Main));
                }

                if (string.IsNullOrEmpty(name))
                {
                    Log.Warn($"Resource string for [{settings.Name}] not found", typeof(Main));
                }

                settings.Area = area ?? settings.Area;
                settings.Name = name ?? settings.Name;

                if (!string.IsNullOrEmpty(settings.Note))
                {
                    var note = Resources.ResourceManager.GetString(settings.Note);
                    settings.Note = note ?? settings.Note;

                    if (string.IsNullOrEmpty(note))
                    {
                        Log.Warn($"Resource string for [{settings.Note}] not found", typeof(Main));
                    }
                }

                if (!(settings.AltNames is null) && settings.AltNames.Any())
                {
                    var translatedAltNames = new Collection<string>();

                    foreach (var altName in settings.AltNames)
                    {
                        var translatedAltName = Resources.ResourceManager.GetString(altName);

                        if (string.IsNullOrEmpty(translatedAltName))
                        {
                            Log.Warn($"Resource string for [{altName}] not found", typeof(Main));
                        }

                        translatedAltNames.Add(translatedAltName ?? altName);
                    }

                    settings.AltNames = translatedAltNames;
                }
            }
        }
    }
}
