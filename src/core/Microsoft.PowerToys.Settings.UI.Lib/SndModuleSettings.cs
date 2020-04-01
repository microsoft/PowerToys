using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class SndModuleSettings<S>
    {
        public S powertoys { get; set; }

        public SndModuleSettings(S settings)
        {
            this.powertoys = settings;
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
