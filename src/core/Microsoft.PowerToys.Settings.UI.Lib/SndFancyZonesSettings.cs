using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class SndFancyZonesSettings
    {
        public FancyZonesSettings FancyZones { get; set; }

        public SndFancyZonesSettings()
        {
        }

        public SndFancyZonesSettings(FancyZonesSettings settings)
        {
            this.FancyZones = settings;
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
