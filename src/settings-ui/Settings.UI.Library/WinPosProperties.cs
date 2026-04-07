// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class WinPosProperties
    {
        public WinPosProperties()
        {
            ShouldAbsorbAlt = new BoolProperty(true);
            ExcludedApps = new StringProperty();
        }

        [JsonPropertyName("shouldAbsorbAlt")]
        public BoolProperty ShouldAbsorbAlt { get; set; }

        [JsonPropertyName("excluded_apps")]
        public StringProperty ExcludedApps { get; set; }
    }
}
