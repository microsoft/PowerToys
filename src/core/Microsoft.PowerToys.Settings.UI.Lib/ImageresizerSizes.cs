// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class ImageResizerSizes
    {
        [JsonPropertyName("value")]
        public ObservableCollection<ImageSize> Value { get; set; }

        public ImageResizerSizes()
        {
            Value = new ObservableCollection<ImageSize>();
        }

        public ImageResizerSizes(ObservableCollection<ImageSize> value)
        {
            Value = value;
        }

        public string ToJsonString()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
            };
            return JsonSerializer.Serialize(this, options);
        }
    }
}
