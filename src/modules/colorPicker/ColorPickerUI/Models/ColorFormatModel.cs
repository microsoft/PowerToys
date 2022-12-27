// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows.Media;
using ColorPicker.Helpers;
using ManagedCommon;

namespace ColorPicker.Models
{
    public class ColorFormatModel
    {
        public string FormatName { get; set; }

        public Func<Color, string> Convert { get; set; }

        public string FormatString { get; set; }

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
