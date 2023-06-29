// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class UpdatingSettings
    {
        public enum UpdatingState
        {
            UpToDate = 0,
            ErrorDownloading,
            ReadyToDownload,
            ReadyToInstall,
            NetworkError,
        }

        // Gets or sets a value of the updating state
        [JsonPropertyName("state")]
        public UpdatingState State { get; set; }

        // Gets or sets a value of the release page url
        [JsonPropertyName("releasePageUrl")]
        public string ReleasePageLink { get; set; }

        // Gets or sets a value of the github last checked date
        [JsonPropertyName("githubUpdateLastCheckedDate")]
        public string LastCheckedDate { get; set; }

        // Gets or sets a value of the updating state
        [JsonPropertyName("downloadedInstallerFilename")]
        public string DownloadedInstallerFilename { get; set; }

        // Non-localizable strings: Files
        public const string SettingsFilePath = "\\Microsoft\\PowerToys\\";
        public const string SettingsFile = "UpdateState.json";

        public string NewVersion
        {
            get
            {
                if (ReleasePageLink == null)
                {
                    return string.Empty;
                }

                try
                {
                    string version = ReleasePageLink.Substring(ReleasePageLink.LastIndexOf('/') + 1);
                    return version.Trim();
                }
                catch (Exception)
                {
                }

                return string.Empty;
            }
        }

        public string LastCheckedDateLocalized
        {
            get
            {
                try
                {
                    if (LastCheckedDate == null)
                    {
                        return string.Empty;
                    }

                    long seconds = long.Parse(LastCheckedDate, CultureInfo.CurrentCulture);
                    var date = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
                    return date.ToLocalTime().ToString(CultureInfo.CurrentCulture);
                }
                catch (Exception)
                {
                }

                return string.Empty;
            }
        }

        public UpdatingSettings()
        {
            State = UpdatingState.UpToDate;
        }

        public static UpdatingSettings LoadSettings()
        {
            FileSystem fileSystem = new FileSystem();
            var localAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var file = localAppDataDir + SettingsFilePath + SettingsFile;

            if (fileSystem.File.Exists(file))
            {
                try
                {
                    Stream inputStream = fileSystem.File.Open(file, FileMode.Open);
                    StreamReader reader = new StreamReader(inputStream);
                    string data = reader.ReadToEnd();
                    inputStream.Close();
                    reader.Dispose();

                    return JsonSerializer.Deserialize<UpdatingSettings>(data);
                }
                catch (Exception)
                {
                }
            }

            return null;
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
