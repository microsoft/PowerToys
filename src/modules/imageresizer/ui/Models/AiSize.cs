#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System.Text.Json.Serialization;

using ImageResizer.Properties;

namespace ImageResizer.Models
{
    public class AiSize : ResizeSize
    {
        private int _scale = 2;

        [JsonIgnore]
        public override string Name
        {
            get => Resources.Input_AiSuperResolution;
            set { /* no-op */ }
        }

        [JsonPropertyName("scale")]
        public int Scale
        {
            get => _scale;
            set => Set(ref _scale, value);
        }

        [JsonConstructor]
        public AiSize(int scale)
        {
            Scale = scale;
        }

        public AiSize()
        {
        }
    }
}
