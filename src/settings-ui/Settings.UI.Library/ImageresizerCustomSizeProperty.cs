// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ImageResizerCustomSizeProperty
    {
        [JsonPropertyName("value")]
        public ImageSize Value { get; set; }

        public ImageResizerCustomSizeProperty()
        {
            Value = new ImageSize();
        }

        public ImageResizerCustomSizeProperty(ImageSize value)
        {
            Value = value;
        }
    }
}
