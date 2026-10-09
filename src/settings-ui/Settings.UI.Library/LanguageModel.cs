// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class LanguageModel
    {
        public const string SettingsFilePath = "\\Microsoft\\PowerToys\\";
        public const string SettingsFile = "language.json";

        public string Tag { get; set; }

        public string ResourceID { get; set; }

        public string Language { get; set; }

        public static string LoadSetting()
        {
            FileSystem fileSystem = new FileSystem();
            var localAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var file = localAppDataDir + SettingsFilePath + SettingsFile;

            if (fileSystem.File.Exists(file))
            {
                try
                {
                    FileSystemStream inputStream = fileSystem.File.Open(file, FileMode.Open);
                    StreamReader reader = new StreamReader(inputStream);
                    string data = reader.ReadToEnd();
                    inputStream.Close();
                    reader.Dispose();

                    return JsonSerializer.Deserialize<OutGoingLanguageSettings>(data).LanguageTag;
                }
                catch (Exception)
                {
                }
            }

            return string.Empty;
        }
    }
}
