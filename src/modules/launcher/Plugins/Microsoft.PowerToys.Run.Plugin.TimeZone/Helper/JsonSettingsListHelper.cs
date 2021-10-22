// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Classes;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.TimeZone.Helper
{
    /// <summary>
    /// Helper class to easier work with the JSON file that contains all Windows settings
    /// </summary>
    internal static class JsonSettingsListHelper
    {
        /// <summary>
        /// The name of the file that contains all settings for the query
        /// </summary>
        private const string _settingsFile = "timeZones.json";

        /// <summary>
        /// Read all possible Windows settings.
        /// </summary>
        /// <returns>A list with all possible windows settings.</returns>
        internal static TimeZoneList ReadAllPossibleTimezones()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var type = Array.Find(assembly.GetTypes(), x => x.Name == nameof(Main));

            TimeZoneList? settings = null;

            try
            {
                var resourceName = $"{type?.Namespace}.{_settingsFile}";
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream is null)
                {
                    throw new Exception("stream is null");
                }

                var options = new JsonSerializerOptions();
                options.Converters.Add(new JsonStringEnumConverter());

                using var reader = new StreamReader(stream);
                var text = reader.ReadToEnd();

                settings = JsonSerializer.Deserialize<TimeZoneList>(text, options);
            }
            catch (JsonException exception)
            {
                Log.Exception("Error loading settings JSON file", exception, typeof(JsonSettingsListHelper));
            }
            catch (Exception exception)
            {
                Log.Exception("Error loading settings JSON file", exception, typeof(JsonSettingsListHelper));
                throw;
            }

            return settings ?? new TimeZoneList();
        }
    }
}
