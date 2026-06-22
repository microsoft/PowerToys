// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace WorkspacesEditor.Helpers
{
    public class WindowStateData
    {
        [JsonPropertyName("top")]
        public double Top { get; set; }

        [JsonPropertyName("left")]
        public double Left { get; set; }

        [JsonPropertyName("width")]
        public double Width { get; set; }

        [JsonPropertyName("height")]
        public double Height { get; set; }

        [JsonPropertyName("maximized")]
        public bool Maximized { get; set; }

        public bool IsValid()
        {
            return Width > 0 && Height > 0;
        }
    }
}
