// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{

    public class ImageresizerSizes
    {
        [JsonPropertyName("value")]
        public ObservableCollection<ImageSize> Value { get; set; }

        public ImageresizerSizes()
        {
            this.Value = new ObservableCollection<ImageSize>();
        }

        public ImageresizerSizes(ObservableCollection<ImageSize> value)
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
