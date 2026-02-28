// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageResizer.Helpers;

namespace ImageResizer.Models
{
    public partial class AiSize : ResizeSize
    {
        private static CompositeFormat _scaleFormat;

        private static CompositeFormat ScaleFormat
        {
            get
            {
                if (_scaleFormat == null)
                {
                    _scaleFormat = CompositeFormat.Parse(ResourceLoaderInstance.ResourceLoader.GetString("Input_AiScaleFormat"));
                }

                return _scaleFormat;
            }
        }

        [ObservableProperty]
        [JsonPropertyName("scale")]
        private int _scale = 2;

        /// <summary>
        /// Gets the formatted scale display string (e.g., "2x").
        /// </summary>
        [JsonIgnore]
        public string ScaleDisplay => string.Format(CultureInfo.CurrentCulture, ScaleFormat, Scale);

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
