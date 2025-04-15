// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

using Microsoft.CmdPal.Ext.WindowsSettings.Properties;

namespace Microsoft.CmdPal.Ext.WindowsSettings.Helpers;

/// <summary>
/// Helper class to easier work with translations.
/// </summary>
internal static class TranslationHelper
{
    /// <summary>
    /// Translate all settings of the settings list in the given <see cref="WindowsSettings"/> class.
    /// </summary>
    /// <param name="windowsSettings">A class that contain all possible windows settings.</param>
    internal static void TranslateAllSettings(in Classes.WindowsSettings windowsSettings)
    {
        if (windowsSettings?.Settings is null)
        {
            return;
        }

        foreach (var settings in windowsSettings.Settings)
        {
            // Translate Name
            if (!string.IsNullOrWhiteSpace(settings.Name))
            {
                var name = Resources.ResourceManager.GetString(settings.Name, CultureInfo.CurrentUICulture);
                if (string.IsNullOrEmpty(name))
                {
                    // Log.Warn($"Resource string for [{settings.Name}] not found", typeof(TranslationHelper));
                }

                settings.Name = name ?? settings.Name ?? string.Empty;
            }

            // Translate Type (App)
            if (!string.IsNullOrWhiteSpace(settings.Type))
            {
                var type = Resources.ResourceManager.GetString(settings.Type, CultureInfo.CurrentUICulture);
                if (string.IsNullOrEmpty(type))
                {
                    // Log.Warn($"Resource string for [{settings.Type}] not found", typeof(TranslationHelper));
                }

                settings.Type = type ?? settings.Type ?? string.Empty;
            }

            // Translate Areas
            if (!(settings.Areas is null) && settings.Areas.Any())
            {
                var translatedAreas = new List<string>();

                foreach (var area in settings.Areas)
                {
                    if (string.IsNullOrWhiteSpace(area))
                    {
                        continue;
                    }

                    var translatedArea = Resources.ResourceManager.GetString(area, CultureInfo.CurrentUICulture);
                    if (string.IsNullOrEmpty(translatedArea))
                    {
                        // Log.Warn($"Resource string for [{area}] not found", typeof(TranslationHelper));
                    }

                    translatedAreas.Add(translatedArea ?? area);
                }

                settings.Areas = translatedAreas;
            }

            // Translate Alternative names
            if (!(settings.AltNames is null) && settings.AltNames.Any())
            {
                var translatedAltNames = new Collection<string>();

                foreach (var altName in settings.AltNames)
                {
                    if (string.IsNullOrWhiteSpace(altName))
                    {
                        continue;
                    }

                    var translatedAltName = Resources.ResourceManager.GetString(altName, CultureInfo.CurrentUICulture);
                    if (string.IsNullOrEmpty(translatedAltName))
                    {
                        // Log.Warn($"Resource string for [{altName}] not found", typeof(TranslationHelper));
                    }

                    translatedAltNames.Add(translatedAltName ?? altName);
                }

                settings.AltNames = translatedAltNames;
            }

            // Translate Note
            if (!string.IsNullOrWhiteSpace(settings.Note))
            {
                var note = Resources.ResourceManager.GetString(settings.Note, CultureInfo.CurrentUICulture);
                if (string.IsNullOrEmpty(note))
                {
                    // Log.Warn($"Resource string for [{settings.Note}] not found", typeof(TranslationHelper));
                }

                settings.Note = note ?? settings.Note ?? string.Empty;
            }
        }
    }
}
