// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System.Text.Json.Serialization;

namespace ImageResizer.Properties
{
    public class SettingsWrapper
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "ImageResizer";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "1";

        [JsonPropertyName("properties")]
        public Settings Properties { get; set; }
    }
}
