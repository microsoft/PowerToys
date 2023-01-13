// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System.Text.Json.Serialization;
using ImageResizer.Properties;

namespace ImageResizer.Models
{
    public class CustomSize : ResizeSize
    {
        [JsonIgnore]
        public override string Name
        {
            get => Resources.Input_Custom;
            set { /* no-op */ }
        }

        [JsonConstructor]
        public CustomSize(ResizeFit fit, double width, double height, ResizeUnit unit)
        {
            Fit = fit;
            Width = width;
            Height = height;
            Unit = unit;
        }

        public CustomSize()
        {
        }
    }
}
