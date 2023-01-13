// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class OutGoingGeneralSettings
    {
        [JsonPropertyName("general")]
        public GeneralSettings GeneralSettings { get; set; }

        public OutGoingGeneralSettings()
        {
        }

        public OutGoingGeneralSettings(GeneralSettings generalSettings)
        {
            GeneralSettings = generalSettings;
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
