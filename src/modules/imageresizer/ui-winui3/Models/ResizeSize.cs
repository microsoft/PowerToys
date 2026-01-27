#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageResizer.Helpers;
using ManagedCommon;

namespace ImageResizer.Models
{
    public partial class ResizeSize : ObservableObject, IHasId
    {
        private static readonly Dictionary<string, string> _tokens = new Dictionary<string, string>
        {
            ["$small$"] = ResourceLoaderInstance.ResourceLoader.GetString("Small"),
            ["$medium$"] = ResourceLoaderInstance.ResourceLoader.GetString("Medium"),
            ["$large$"] = ResourceLoaderInstance.ResourceLoader.GetString("Large"),
            ["$phone$"] = ResourceLoaderInstance.ResourceLoader.GetString("Phone"),
        };

        [ObservableProperty]
        [JsonPropertyName("Id")]
        private int _id;

        private string _name;

        [ObservableProperty]
        [JsonPropertyName("fit")]
        [NotifyPropertyChangedFor(nameof(ShowHeight))]
        private ResizeFit _fit = ResizeFit.Fit;

        [ObservableProperty]
        [JsonPropertyName("width")]
        private double _width;

        [ObservableProperty]
        [JsonPropertyName("height")]
        private double _height;

        [ObservableProperty]
        [JsonPropertyName("unit")]
        [NotifyPropertyChangedFor(nameof(ShowHeight))]
        private ResizeUnit _unit = ResizeUnit.Pixel;

        public ResizeSize(int id, string name, ResizeFit fit, double width, double height, ResizeUnit unit)
        {
            Id = id;
            Name = name;
            Fit = fit;
            Width = width;
            Height = height;
            Unit = unit;
        }

        public ResizeSize()
        {
        }

        [JsonPropertyName("name")]
        public virtual string Name
        {
            get => _name;
            set => SetProperty(ref _name, ReplaceTokens(value));
        }

        public bool ShowHeight => Fit == ResizeFit.Stretch || Unit != ResizeUnit.Percent;

        public bool HasAuto
            => Width == 0 || Height == 0 || double.IsNaN(Width) || double.IsNaN(Height);

        public double GetPixelWidth(int originalWidth, double dpi)
            => ConvertToPixels(Width, Unit, originalWidth, dpi);

        public double GetPixelHeight(int originalHeight, double dpi)
            => ConvertToPixels(
                Fit != ResizeFit.Stretch && Unit == ResizeUnit.Percent
                    ? Width
                    : Height,
                Unit,
                originalHeight,
                dpi);

        private static string ReplaceTokens(string text)
            => (text != null && _tokens.TryGetValue(text, out var result))
                ? result
                : text;

        private double ConvertToPixels(double value, ResizeUnit unit, int originalValue, double dpi)
        {
            if (value == 0 || double.IsNaN(value))
            {
                if (Fit == ResizeFit.Fit)
                {
                    return double.PositiveInfinity;
                }

                Debug.Assert(Fit == ResizeFit.Fill || Fit == ResizeFit.Stretch, "Unexpected ResizeFit value: " + Fit);

                return originalValue;
            }

            switch (unit)
            {
                case ResizeUnit.Inch:
                    return value * dpi;

                case ResizeUnit.Centimeter:
                    return value * dpi / 2.54;

                case ResizeUnit.Percent:
                    return value / 100 * originalValue;

                default:
                    Debug.Assert(unit == ResizeUnit.Pixel, "Unexpected unit value: " + unit);
                    return value;
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
