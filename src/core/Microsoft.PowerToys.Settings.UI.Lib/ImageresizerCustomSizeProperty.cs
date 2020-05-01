using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{

    public class ImageresizerCustomSizeProperty
    {
        [JsonPropertyName("value")]
        public ImageSize Value { get; set; }

        public ImageresizerCustomSizeProperty()
        {
            this.Value = new ImageSize();
        }

        public ImageresizerCustomSizeProperty(ImageSize value)
        {
            Value = value;
        }
    }
}
