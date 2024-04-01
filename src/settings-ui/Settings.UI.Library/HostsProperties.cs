// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Settings.UI.Library.Attributes;
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

        public HostsProperties()
        {
            ShowStartupWarning = true;
            LaunchAdministrator = true;
            LoopbackDuplicates = false;
            AdditionalLinesPosition = HostsAdditionalLinesPosition.Top;
            Encoding = HostsEncoding.Utf8;
        }
    }
}
