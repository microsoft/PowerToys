// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json.Serialization;
using Settings.UI.Library.Enumerations;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class HostsProperties
    {
        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool ShowStartupWarning { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool LaunchAdministrator { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool LoopbackDuplicates { get; set; }

        public HostsAdditionalLinesPosition AdditionalLinesPosition { get; set; }

        public HostsEncoding Encoding { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool NoLeadingSpaces { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool BackupHosts { get; set; }

        public string BackupPath { get; set; }

        public HostsDeleteBackupMode DeleteBackupsMode { get; set; }

        public int DeleteBackupsDays { get; set; }

        public int DeleteBackupsCount { get; set; }

        public HostsProperties()
        {
            ShowStartupWarning = true;
            LaunchAdministrator = true;
            LoopbackDuplicates = false;
            AdditionalLinesPosition = HostsAdditionalLinesPosition.Top;
            Encoding = HostsEncoding.Utf8;
            NoLeadingSpaces = false;
            BackupHosts = true;
            BackupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\drivers\etc");
            DeleteBackupsMode = HostsDeleteBackupMode.Age;
            DeleteBackupsDays = 15;
            DeleteBackupsCount = 5;
        }
    }
}
