// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    // Contains information for a release. Used to deserialize release JSON info from GitHub.
    public sealed class PowerToysReleaseInfo
    {
        [JsonPropertyName("published_at")]
        public DateTimeOffset PublishedDate { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("tag_name")]
        public string TagName { get; set; }

        [JsonPropertyName("body")]
        public string ReleaseNotes { get; set; }
    }
}
