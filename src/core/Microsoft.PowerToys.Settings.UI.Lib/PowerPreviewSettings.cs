using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class PowerPreviewSettings
    {
        public string name { get; set; }
        public Properties properties { get; set; }
        public string version { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    public class Property
    {
        public bool value { get; set; }
        
        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    public class Properties
    {
        public Property IDS_PREVPANE_SVG_BOOL_TOGGLE_CONTROLL { get; set; }
        public Property PREVPANE_MD_BOOL_TOGGLE_CONTROLL_ID { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
