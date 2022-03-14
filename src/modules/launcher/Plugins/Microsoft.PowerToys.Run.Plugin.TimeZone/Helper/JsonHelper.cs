// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Classes;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.TimeZone.Helper
{
    /// <summary>
    /// Helper class to easier work with the JSON files.
    /// </summary>
    internal static class JsonHelper
    {
        /// <summary>
        /// The name of the file that contains all time zones.
        /// </summary>
        private const string _settingsFile = "timeZones.json";

        /// <summary>
        /// Read all possible time zones.
        /// </summary>
        /// <returns>A object that contain a list with time zones.</returns>
        internal static TimeZoneList ReadAllPossibleTimeZones()
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

                using var reader = new StreamReader(stream);
                var text = reader.ReadToEnd();

                settings = JsonSerializer.Deserialize<TimeZoneList>(text);
            }
            catch (JsonException exception)
            {
                Log.Exception("Error loading settings JSON file", exception, typeof(JsonHelper));
            }
            catch (Exception exception)
            {
                Log.Exception("Error loading settings JSON file", exception, typeof(JsonHelper));
                throw;
            }

            return settings ?? new TimeZoneList();
        }
    }
}
