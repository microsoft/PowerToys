using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{

    public class ImageresizerFallbackEncoder
    {
        [JsonPropertyName("value")]
        public string Value { get; set; }

        public ImageresizerFallbackEncoder()
        {
            this.Value = string.Empty;
        }
    }
}
