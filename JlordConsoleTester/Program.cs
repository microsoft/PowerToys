// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

// using Newtonsoft.Json.Linq;
namespace BackupAndRestoreConsoleTester
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            var backupRetoreSettings = JsonNode.Parse(File.ReadAllText(@"C:\Users\jeff\AppData\Local\Microsoft\PowerToys\backup-restore_settings.json"));

            var temperatureNode = (bool?)backupRetoreSettings!["RestartAfterRestore"];

            using var exportVersion = GetExportVersion(backupRetoreSettings, "settings.json", "C:\\Users\\jeff\\AppData\\Local\\Microsoft\\PowerToys\\settings.json");

            var json = JsonSerializer.Serialize(exportVersion);

            using var backupData = JsonDocument.Parse(JsonSerializer.Serialize(new { a = 1, b = 2, d = 4 }));

            var s = JsonSerializer.Serialize(new { a = 1, b = 22, c = 3, x = 99 });
            using var currentSettingData = JsonDocument.Parse(JsonSerializer.Serialize(new { a = 1, b = 22, c = 3, x = 99 }));

            using var merged = MergeJObjects(backupData, currentSettingData);
        }

        private static JsonDocument MergeJObjects(JsonDocument newContent, JsonDocument origDoc)
        {
            var outputBuffer = new ArrayBufferWriter<byte>();

            using (var jsonWriter = new Utf8JsonWriter(outputBuffer, new JsonWriterOptions { Indented = true }))
            {
                JsonElement root1 = origDoc.RootElement;
                JsonElement root2 = newContent.RootElement;

                jsonWriter.WriteStartObject();

                // Write all the properties of the first document that don't conflict with the second
                foreach (JsonProperty property in root1.EnumerateObject().OrderBy(p => p.Name))
                {
                    if (!root2.TryGetProperty(property.Name, out _))
                    {
                        property.WriteTo(jsonWriter);
                    }
                }

                // Write all the properties of the second document (including those that are duplicates which were skipped earlier)
                // The property values of the second document completely override the values of the first
                foreach (JsonProperty property in root2.EnumerateObject().OrderBy(p => p.Name))
                {
                    property.WriteTo(jsonWriter);
                }

                jsonWriter.WriteEndObject();
            }

            return JsonDocument.Parse(Encoding.UTF8.GetString(outputBuffer.WrittenSpan));
        }

        public static JsonDocument GetExportVersion(JsonNode backupRetoreSettings, string settingFileKey, string settingsFileName)
        {
            var ignoredSettings = GetIgnoredSettings(backupRetoreSettings, settingFileKey);
            var settingsFile = JsonDocument.Parse(File.ReadAllText(settingsFileName));

            if (ignoredSettings.Length == 0)
            {
                return settingsFile;
            }

            var outputBuffer = new ArrayBufferWriter<byte>();

            using (var jsonWriter = new Utf8JsonWriter(outputBuffer, new JsonWriterOptions { Indented = true }))
            {
                jsonWriter.WriteStartObject();
                foreach (var property in settingsFile.RootElement.EnumerateObject())
                {
                    if (!ignoredSettings.Contains(property.Name))
                    {
                        property.WriteTo(jsonWriter);
                    }
                }

                jsonWriter.WriteEndObject();
            }

            return JsonDocument.Parse(Encoding.UTF8.GetString(outputBuffer.WrittenSpan));
        }

        private static string[] GetIgnoredSettings(JsonNode backupRetoreSettings, string settingFileKey)
        {
            if (backupRetoreSettings == null)
            {
                throw new ArgumentNullException(nameof(backupRetoreSettings));
            }

            if (backupRetoreSettings["IgnoredSettings"] != null)
            {
                if (backupRetoreSettings["IgnoredSettings"][settingFileKey] != null)
                {
                    var settingsArray = (JsonArray)backupRetoreSettings["IgnoredSettings"][settingFileKey];

                    Console.WriteLine("settingsArray " + settingsArray.GetType().FullName);

                    var settingsList = new List<string>();

                    foreach (var setting in settingsArray)
                    {
                        settingsList.Add(setting.ToString());
                    }

                    return settingsList.ToArray();
                }
                else
                {
                    return Array.Empty<string>();
                }
            }

            return Array.Empty<string>();
        }

        /*
        public static JObject GetExportVersion_OLD(JObject backupRetoreSettings, string settingFileKey, string settingsFileName)
        {
            var ignoredSettings = GetIgnoredSettings_OLD(backupRetoreSettings, settingFileKey);
            var settingsFile = JObject.Parse(File.ReadAllText(settingsFileName));

            if (ignoredSettings.Length == 0)
            {
                return settingsFile;
            }

            foreach (var property in settingsFile.Properties().ToList())
            {
                if (ignoredSettings.Contains(property.Name))
                {
                    settingsFile.Remove(property.Name);
                }
            }

            return settingsFile;
        }

        private static string[] GetIgnoredSettings_OLD(JObject backupRetoreSettings, string settingFileKey)
        {
            if (backupRetoreSettings == null)
            {
                throw new ArgumentNullException(nameof(backupRetoreSettings));
            }

            var ignoredSettings = backupRetoreSettings["IgnoredSettings"];

            if (ignoredSettings != null && ignoredSettings[settingFileKey] != null)
            {
                ignoredSettings = ignoredSettings[settingFileKey];
                if (ignoredSettings != null)
                {
                    return ((JArray)ignoredSettings).ToObject<string[]>();
                }
                else
                {
                    return Array.Empty<string>();
                }
            }
            else
            {
                return Array.Empty<string>();
            }
        }
        */
    }
}
