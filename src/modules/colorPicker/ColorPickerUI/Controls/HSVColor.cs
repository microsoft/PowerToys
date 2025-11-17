// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows.Media;

namespace ColorPicker.Controls
{
    public static class HSVColor
    {
        public static Color[] GetSpectrum()
        {
            var rgbs = new Color[360];

            for (int h = 0; h < 360; h++)
            {
                rgbs[h] = RGBFromHSV(h, 1f, 1f);
            }

            return rgbs;
        }

        public static Color[] HueSpectrum(double saturation, double value)
        {
            var rgbs = new Color[7];

            for (int h = 0; h < 7; h++)
            {
                rgbs[h] = RGBFromHSV(h * 60, saturation, value);
            }

            return rgbs;
        }

        public static Color RGBFromHSV(double h, double s, double v)
        {
            if (h > 360 || h < 0 || s > 1 || s < 0 || v > 1 || v < 0)
            {
                return Color.FromRgb(0, 0, 0);
            }

            double c = v * s;
            double x = c * (1 - Math.Abs(((h / 60) % 2) - 1));
            double m = v - c;

            double r = 0, g = 0, b = 0;

            if (h < 60)
            {
                r = c;
                g = x;
            }
            else if (h < 120)
            {
                r = x;
                g = c;
            }
            else if (h < 180)
            {
                g = c;
                b = x;
            }
            else if (h < 240)
            {
                g = x;
                b = c;
            }
            else if (h < 300)
            {
                r = x;
                b = c;
            }
            else if (h <= 360)
            {
                r = c;
                b = x;
            }

            return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
        }
    }
}
