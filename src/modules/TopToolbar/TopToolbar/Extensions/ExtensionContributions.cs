// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TopToolbar.Extensions
{
    public sealed class ExtensionContributions
    {
        [JsonPropertyName("providers")]
        public List<string> Providers { get; set; } = new List<string>();

        [JsonPropertyName("actions")]
        public List<StaticActionContribution> Actions { get; set; } = new List<StaticActionContribution>();
    }
}
