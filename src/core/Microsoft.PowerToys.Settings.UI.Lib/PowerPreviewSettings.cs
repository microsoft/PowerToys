using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class PowerPreviewSettings : BasePTModuleSettings
    {
        public PowerPreviewProperties properties { get; set; }

        public PowerPreviewSettings()
        {
            this.properties = new PowerPreviewProperties();
            this.version = "1";
            this.name = "_unset_";
        }

        public PowerPreviewSettings(string ptName)
        {
            this.properties = new PowerPreviewProperties();
            this.version = "1";
            this.name = ptName;
        }

        public override string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    public class PowerPreviewProperties
    {
        public BoolProperty IDS_PREVPANE_SVG_BOOL_TOGGLE_CONTROLL { get; set; }
        public BoolProperty PREVPANE_MD_BOOL_TOGGLE_CONTROLL_ID { get; set; }

        public PowerPreviewProperties()
        {
            this.IDS_PREVPANE_SVG_BOOL_TOGGLE_CONTROLL = new BoolProperty();
            this.PREVPANE_MD_BOOL_TOGGLE_CONTROLL_ID = new BoolProperty();
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    public class SndPowerPreviewSettings
    {
        [JsonPropertyName("File Explorer Preview")]
        public PowerPreviewSettings File_Explorer_Preview { get; set; }

        public SndPowerPreviewSettings(PowerPreviewSettings settings)
        {
            this.File_Explorer_Preview = settings;
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
