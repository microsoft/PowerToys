// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class UpdatingSettings : ISettingsConfig
    {
        public enum UpdatingState
        {
            UpToDate = 0,
            CannotDownload,
            ReadyToDownload,
            ReadyToInstall,
        }

        // Gets or sets a value of the updating state
        [JsonPropertyName("state")]
        public UpdatingState State { get; set; }

        // Gets or sets a value of the release page url
        [JsonPropertyName("releasePageUrl")]
        public System.Uri ReleasePageUrl { get; set; }

        // Gets or sets a value of the github last checked date
        [JsonPropertyName("githubUpdateLastCheckedDate")]
        public string LastCheckedDate { get; set; }

        // Gets or sets a value of the updating state
        [JsonPropertyName("downloadedInstallerFilename")]
        public string DownloadedInstallerFilename { get; set; }

        public UpdatingSettings()
        {
            State = UpdatingState.UpToDate;
        }

        public string GetModuleName()
        {
            return "update_state.json";
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

        public bool UpgradeSettingsConfiguration() => false;
    }
}
