// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ImageResizerSizes
    {
        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        // Suppressing this warning because removing the setter breaks
        // deserialization with System.Text.Json. This affects the UI display.
        // See: https://github.com/dotnet/runtime/issues/30258
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
            var options = _serializerOptions;
            return JsonSerializer.Serialize(this, options);
        }
    }
}
