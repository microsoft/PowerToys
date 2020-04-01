using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class PowerRenameSettings : BasePTModuleSettings
    {
        public PowerRenameProperties properties { get; set; }

        public PowerRenameSettings()
        {
            this.properties = new PowerRenameProperties();
            this.version = "1";
            this.name = "_unset_";
        }

        public PowerRenameSettings(string ptName)
        {
            this.properties = new PowerRenameProperties();
            this.version = "1";
            this.name = ptName;
        }

        public override string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    public class PowerRenameProperties
    {
        public PowerRenameProperties()
        {
            this.bool_persist_input = new BoolProperty();
            this.bool_mru_enabled = new BoolProperty();
            this.int_max_mru_size = new IntProperty();
            this.bool_show_icon_on_menu = new BoolProperty();
            this.bool_show_extended_menu = new BoolProperty();
        }

        public BoolProperty bool_persist_input { get; set; }
        public BoolProperty bool_mru_enabled { get; set; }
        public IntProperty int_max_mru_size { get; set; }
        public BoolProperty bool_show_icon_on_menu { get; set; }
        public BoolProperty bool_show_extended_menu { get; set; }
    }
}

