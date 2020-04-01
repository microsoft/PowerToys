using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    /// <summary>
    /// This class models the settings for the PowerPreview class. 
    /// Eaxmple JSON:
    /// {
    ///     "name": "File Explorer Preview",
    ///     "properties": {
    ///         "IDS_PREVPANE_SVG_BOOL_TOGGLE_CONTROLL": { "value": true },
    ///         "PREVPANE_MD_BOOL_TOGGLE_CONTROLL_ID": { "value": true }
    ///     },
    ///     "version": "1.0"
    /// }

    /// </summary>
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

        public override string ToString()
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
}
