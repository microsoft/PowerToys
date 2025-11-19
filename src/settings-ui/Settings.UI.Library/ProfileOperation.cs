// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Represents a pending profile operation to be applied by PowerDisplay
    /// </summary>
    public class ProfileOperation
    {
        [JsonPropertyName("profileName")]
        public string ProfileName { get; set; }

        [JsonPropertyName("monitorSettings")]
        public List<ProfileMonitorSetting> MonitorSettings { get; set; }

        public ProfileOperation()
        {
            ProfileName = string.Empty;
            MonitorSettings = new List<ProfileMonitorSetting>();
        }

        public ProfileOperation(string profileName, List<ProfileMonitorSetting> monitorSettings)
        {
            ProfileName = profileName;
            MonitorSettings = monitorSettings ?? new List<ProfileMonitorSetting>();
        }
    }
}
