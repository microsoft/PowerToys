using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{

    public class ImageResizerKeepDateModified
    {
        [JsonPropertyName("value")]
        public bool Value { get; set; }

        public ImageResizerKeepDateModified()
        {
            this.Value = false;
        }
    }
}
