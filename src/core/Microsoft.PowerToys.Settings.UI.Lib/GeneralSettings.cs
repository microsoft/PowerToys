using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class GeneralSettings
    {
        public bool packaged { get; set; }
        public bool startup { get; set; }
        public bool is_elevated { get; set; }
        public bool run_elevated { get; set; }
        public bool is_admin { get; set; }
        public string theme { get; set; }
        public string system_theme { get; set; }
        public string powertoys_version { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    public class OutGoingGeneralSettings
    {
        public GeneralSettings general { get; set; }

        public OutGoingGeneralSettings()
        {
            this.general = null;
        }

        public OutGoingGeneralSettings(GeneralSettings generalSettings)
        {
            this.general = generalSettings;
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
