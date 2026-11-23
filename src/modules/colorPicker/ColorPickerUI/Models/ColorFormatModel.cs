// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

using ColorPicker.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
using Windows.UI;

namespace ColorPicker.Models
{
    public class ColorFormatModel : ObservableObject
    {
        private string _colorText;
        private string _copyHelperText;

        public string FormatName { get; set; }

        public Func<Color, string> Convert { get; set; }

        public string FormatString { get; set; }

        public string ColorText
        {
            get => _colorText;
            private set => SetProperty(ref _colorText, value);
        }

        public string CopyHelperText
        {
            get => _copyHelperText;
            private set => SetProperty(ref _copyHelperText, value);
        }

        public void UpdateColor(Color color)
        {
            ColorText = GetColorText(color);
            CopyHelperText = string.Format(CultureInfo.InvariantCulture, "{0} {1}", FormatName, ColorText);
        }

        public string GetColorText(Color color)
        {
            if (Convert != null)
            {
                return Convert(color);
            }

            System.Drawing.Color drawingColor = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);

            // get string representation in 2 steps. First replace all color specific number values then in 2nd step replace color name with localisation
            return ColorRepresentationHelper.ReplaceName(ColorFormatHelper.GetStringRepresentation(drawingColor, FormatString), drawingColor);
        }
    }
}
