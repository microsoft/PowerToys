// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class SndFancyZonesSettings
    {
        public FancyZonesSettings FancyZones { get; set; }

        public SndFancyZonesSettings()
        {
        }

        public SndFancyZonesSettings(FancyZonesSettings settings)
        {
            FancyZones = settings;
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
