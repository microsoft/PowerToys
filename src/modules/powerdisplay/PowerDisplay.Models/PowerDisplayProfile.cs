// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PowerDisplay.Models
{
    /// <summary>
    /// Represents a PowerDisplay profile containing monitor settings
    /// </summary>
    [JsonConverter(typeof(PowerDisplayProfileJsonConverter))]
    public class PowerDisplayProfile
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("order")]
        public int Order { get; set; } = -1;

        /// <summary>
        /// Retains the former numeric id so references in other settings files can be upgraded
        /// after the profile's UUID has been durably saved.
        /// </summary>
        [JsonPropertyName("legacyId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? LegacyId { get; set; }

        [JsonPropertyName("monitorSettings")]
        public List<ProfileMonitorSetting> MonitorSettings { get; set; }

        [JsonPropertyName("createdDate")]
        public DateTime CreatedDate { get; set; }

        [JsonPropertyName("lastModified")]
        public DateTime LastModified { get; set; }

        public PowerDisplayProfile()
        {
            Name = string.Empty;
            MonitorSettings = new List<ProfileMonitorSetting>();
            CreatedDate = DateTime.UtcNow;
            LastModified = DateTime.UtcNow;
        }

        public PowerDisplayProfile(string name, List<ProfileMonitorSetting> monitorSettings)
        {
            Name = name;
            MonitorSettings = monitorSettings ?? new List<ProfileMonitorSetting>();
            CreatedDate = DateTime.UtcNow;
            LastModified = DateTime.UtcNow;
        }

        /// <summary>
        /// Validates that the profile has at least one monitor configured
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Name) && MonitorSettings != null && MonitorSettings.Count > 0;
        }

        /// <summary>
        /// Updates the last modified timestamp
        /// </summary>
        public void Touch()
        {
            LastModified = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets a human-readable label that disambiguates duplicate names using a short UUID.
        /// Not serialized; UI display only.
        /// </summary>
        [JsonIgnore]
        public string DisplayName => ProfileDisplayNameFormatter.Format(Name, Id);
    }
}
