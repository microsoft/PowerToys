// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

using ImageResizer.Properties;

namespace ImageResizer.Models
{
    public class AiSize : ResizeSize
    {
        private static readonly CompositeFormat ScaleFormat = CompositeFormat.Parse(Resources.Input_AiScaleFormat);
        private int _scale = 2;

        /// <summary>
        /// Gets the formatted scale display string (e.g., "2×").
        /// </summary>
        [JsonIgnore]
        public string ScaleDisplay => string.Format(CultureInfo.CurrentCulture, ScaleFormat, _scale);

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
