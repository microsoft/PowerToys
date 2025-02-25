// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManagedCommon.Serialization;

namespace ManagedCommon
{
    public static class LanguageHelper
    {
        public const string SettingsFilePath = "\\Microsoft\\PowerToys\\";
        public const string SettingsFile = "language.json";

        internal sealed class OutGoingLanguageSettings
        {
            [JsonPropertyName("language")]
            public string LanguageTag { get; set; }
        }

        public static string LoadLanguage()
        {
            var localAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var file = localAppDataDir + SettingsFilePath + SettingsFile;

            if (File.Exists(file))
            {
                try
                {
                    var inputStream = File.Open(file, FileMode.Open);
                    StreamReader reader = new StreamReader(inputStream);
                    string data = reader.ReadToEnd();
                    inputStream.Close();
                    reader.Dispose();

                    return JsonSerializer.Deserialize<OutGoingLanguageSettings>(data, SourceGenerationContext.Default.OutGoingLanguageSettings).LanguageTag;
                }
                catch (Exception)
                {
                }
            }

            return string.Empty;
        }
    }
}
