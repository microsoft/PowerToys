// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;
using ImageResizer.Helpers;
using ImageResizer.Properties;

namespace ImageResizer.Models
{
    public class ResizeSize : Observable
    {
        private static readonly IDictionary<string, string> _tokens = new Dictionary<string, string>
        {
            ["$small$"] = Resources.Small,
            ["$medium$"] = Resources.Medium,
            ["$large$"] = Resources.Large,
            ["$phone$"] = Resources.Phone,
        };

        private string _name;
        private ResizeFit _fit = ResizeFit.Fit;
        private double _width;
        private double _height;
        private bool _showHeight = true;
        private ResizeUnit _unit = ResizeUnit.Pixel;

        public ResizeSize(string name, ResizeFit fit, double width, double height, ResizeUnit unit)
        {
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
            set => Set(ref _name, ReplaceTokens(value));
        }

        [JsonPropertyName("fit")]
        public ResizeFit Fit
        {
            get => _fit;
            set
            {
                var previousFit = _fit;
                Set(ref _fit, value);
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
            set => Set(ref _width, value);
        }

        [JsonPropertyName("height")]
        public double Height
        {
            get => _height;
            set => Set(ref _height, value);
        }

        public bool ShowHeight
        {
            get => _showHeight;
            set => Set(ref _showHeight, value);
        }

        public bool HasAuto
            => Width == 0 || Height == 0;

        [JsonPropertyName("unit")]
        public ResizeUnit Unit
        {
            get => _unit;
            set
            {
                var previousUnit = _unit;
                Set(ref _unit, value);
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
            => (text != null && _tokens.TryGetValue(text, out var result))
                ? result
                : text;

        private void UpdateShowHeight()
        {
            ShowHeight = Fit == ResizeFit.Stretch || Unit != ResizeUnit.Percent;
        }

        private double ConvertToPixels(double value, ResizeUnit unit, int originalValue, double dpi)
        {
            if (value == 0)
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
    }
}
