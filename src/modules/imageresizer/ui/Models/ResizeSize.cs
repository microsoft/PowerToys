#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

using CommunityToolkit.Mvvm.ComponentModel;
using ImageResizer.Properties;
using ManagedCommon;

namespace ImageResizer.Models
{
    public class ResizeSize : ObservableObject, IHasId
    {
        private static readonly Lazy<Dictionary<string, string>> _tokens = new Lazy<Dictionary<string, string>>(() => new Dictionary<string, string>
        {
            ["$small$"] = ResourceLoaderInstance.ResourceLoader.GetString("Small"),
            ["$medium$"] = ResourceLoaderInstance.ResourceLoader.GetString("Medium"),
            ["$large$"] = ResourceLoaderInstance.ResourceLoader.GetString("Large"),
            ["$phone$"] = ResourceLoaderInstance.ResourceLoader.GetString("Phone"),
        });

        private int _id;
        private string _name;
        private ResizeFit _fit = ResizeFit.Fit;
        private double _width;
        private double _height;
        private bool _showHeight = true;
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

        [JsonPropertyName("Id")]
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        [JsonPropertyName("name")]
        public virtual string Name
        {
            get => _name;
            set => SetProperty(ref _name, ReplaceTokens(value));
        }

        [JsonPropertyName("fit")]
        public ResizeFit Fit
        {
            get => _fit;
            set
            {
                var previousFit = _fit;
                SetProperty(ref _fit, value);
                if (!Equals(previousFit, value))
                {
                    UpdateShowHeight();
                }
            }
        }

        [JsonPropertyName("width")]
        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        [JsonPropertyName("height")]
        public double Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        public bool ShowHeight
        {
            get => _showHeight;
            set => SetProperty(ref _showHeight, value);
        }

        public bool HasAuto
            => Width == 0 || Height == 0 || double.IsNaN(Width) || double.IsNaN(Height);

        [JsonPropertyName("unit")]
        public ResizeUnit Unit
        {
            get => _unit;
            set
            {
                var previousUnit = _unit;
                SetProperty(ref _unit, value);
                if (!Equals(previousUnit, value))
                {
                    UpdateShowHeight();
                }
            }
        }

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
            => (text != null && _tokens.Value.TryGetValue(text, out var result))
                ? result
                : text;

        private void UpdateShowHeight()
        {
            ShowHeight = Fit == ResizeFit.Stretch || Unit != ResizeUnit.Percent;
        }

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
