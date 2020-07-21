using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{

    public class ImageResizerCustomSizeProperty
    {
        [JsonPropertyName("value")]
        public ImageSize Value { get; set; }

        public ImageResizerCustomSizeProperty()
        {
            this.Value = new ImageSize();
        }

        public ImageResizerCustomSizeProperty(ImageSize value)
        {
            Value = value;
        }
    }
}
