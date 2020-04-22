// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class ImageSize
    {
        public ImageSize(int id)
        {
            Id = id;
            Name = string.Empty;
            Fit = (int)ResizeFit.Fit;
            Width = 0;
            Height = 0;
            Unit = (int)ResizeUnit.Pixel;
        }

        public ImageSize()
        {
            Id = 0;
            Name = string.Empty;
            Fit = (int)ResizeFit.Fit;
            Width = 0;
            Height = 0;
            Unit = (int)ResizeUnit.Pixel;
        }

        public ImageSize(int id, string name, ResizeFit fit, double width, double height, ResizeUnit unit)
        {
            Id = id;
            Name = name;
            Fit = (int)fit;
            Width = width;
            Height = height;
            Unit = (int)unit;
        }

        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("fit")]
        public int Fit { get; set; }

        [JsonPropertyName("width")]
        public double Width { get; set; }

        [JsonPropertyName("height")]
        public double Height { get; set; }

        [JsonPropertyName("unit")]
        public int Unit { get; set; }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
