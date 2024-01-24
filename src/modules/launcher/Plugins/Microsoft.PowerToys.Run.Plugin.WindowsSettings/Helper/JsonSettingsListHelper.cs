// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.WindowsSettings.Helper
{
    /// <summary>
    /// Helper class to easier work with the JSON file that contains all Windows settings
    /// </summary>
    internal static class JsonSettingsListHelper
    {
        /// <summary>
        /// The name of the file that contains all settings for the query
        /// </summary>
        private const string _settingsFile = "WindowsSettings.json";

        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
        };

        /// <summary>
        /// Read all possible Windows settings.
        /// </summary>
        /// <returns>A list with all possible windows settings.</returns>
        internal static WindowsSettings ReadAllPossibleSettings()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var type = assembly.GetTypes().FirstOrDefault(x => x.Name == nameof(Main));

            WindowsSettings? settings = null;

            try
            {
                var resourceName = $"{type?.Namespace}.{_settingsFile}";
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream is null)
                {
                    throw new ArgumentNullException(nameof(stream), "stream is null");
                }

                var options = _serializerOptions;
                options.Converters.Add(new JsonStringEnumConverter());

                using var reader = new StreamReader(stream);
                var text = reader.ReadToEnd();

                settings = JsonSerializer.Deserialize<WindowsSettings>(text, options);
            }
            catch (Exception exception)
            {
                Log.Exception("Error loading settings JSON file", exception, typeof(JsonSettingsListHelper));
            }

            return settings ?? new WindowsSettings();
        }
    }
}
