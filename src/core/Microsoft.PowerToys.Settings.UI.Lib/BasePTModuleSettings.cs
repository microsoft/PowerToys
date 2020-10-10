// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public abstract class BasePTModuleSettings
    {
        // Gets or sets name of the powertoy module.
        [JsonPropertyName("name")]
        public string Name { get; set; }

        // Gets or sets the powertoys version.
        [JsonPropertyName("version")]
        public string Version { get; set; }
    }
}
