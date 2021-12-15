// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ImageResizerFallbackEncoder
    {
        [JsonPropertyName("value")]
        public string Value { get; set; }

        public ImageResizerFallbackEncoder()
        {
            Value = string.Empty;
        }
    }
}
