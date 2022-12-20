// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

namespace ManagedCommon
{
    public static class ColorFormatHelper
    {
        /// <summary>
        /// Convert a given <see cref="Color"/> to a CMYK color (cyan, magenta, yellow, black key)
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>The cyan[0..1], magenta[0..1], yellow[0..1] and black key[0..1] of the converted color</returns>
        public static (double Cyan, double Magenta, double Yellow, double BlackKey) ConvertToCMYKColor(Color color)
        {
            // special case for black (avoid division by zero)
            if (color.R == 0 && color.G == 0 && color.B == 0)
            {
                return (0d, 0d, 0d, 1d);
            }

            var red = color.R / 255d;
            var green = color.G / 255d;
            var blue = color.B / 255d;

            var blackKey = 1d - Math.Max(Math.Max(red, green), blue);

            // special case for black (avoid division by zero)
            if (1d - blackKey == 0d)
            {
                return (0d, 0d, 0d, 1d);
            }

            var cyan = (1d - red - blackKey) / (1d - blackKey);
            var magenta = (1d - green - blackKey) / (1d - blackKey);
            var yellow = (1d - blue - blackKey) / (1d - blackKey);

            return (cyan, magenta, yellow, blackKey);
        }

        /// <summary>
        /// Convert a given <see cref="Color"/> to a HSB color (hue, saturation, brightness)
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>The hue [0°..360°], saturation [0..1] and brightness [0..1] of the converted color</returns>
        public static (double Hue, double Saturation, double Brightness) ConvertToHSBColor(Color color)
        {
            // HSB and HSV represents the same color space
            return ConvertToHSVColor(color);
        }

        /// <summary>
        /// Convert a given <see cref="Color"/> to a HSV color (hue, saturation, value)
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>The hue [0°..360°], saturation [0..1] and value [0..1] of the converted color</returns>
        public static (double Hue, double Saturation, double Value) ConvertToHSVColor(Color color)
        {
            var min = Math.Min(Math.Min(color.R, color.G), color.B) / 255d;
            var max = Math.Max(Math.Max(color.R, color.G), color.B) / 255d;

            return (color.GetHue(), max == 0d ? 0d : (max - min) / max, max);
        }

        /// <summary>
        /// Convert a given <see cref="Color"/> to a HSI color (hue, saturation, intensity)
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>The hue [0°..360°], saturation [0..1] and intensity [0..1] of the converted color</returns>
        public static (double Hue, double Saturation, double Intensity) ConvertToHSIColor(Color color)
        {
            // special case for black
            if (color.R == 0 && color.G == 0 && color.B == 0)
            {
                return (0d, 0d, 0d);
            }

            var red = color.R / 255d;
            var green = color.G / 255d;
            var blue = color.B / 255d;

            var intensity = (red + green + blue) / 3d;

            var min = Math.Min(Math.Min(color.R, color.G), color.B) / 255d;

            return (color.GetHue(), 1d - (min / intensity), intensity);
        }

        /// <summary>
        /// Convert a given <see cref="Color"/> to a HSL color (hue, saturation, lightness)
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>The hue [0°..360°], saturation [0..1] and lightness [0..1] values of the converted color</returns>
        public static (double Hue, double Saturation, double Lightness) ConvertToHSLColor(Color color)
        {
            var min = Math.Min(Math.Min(color.R, color.G), color.B) / 255d;
            var max = Math.Max(Math.Max(color.R, color.G), color.B) / 255d;

            var lightness = (max + min) / 2d;

            if (lightness == 0d || min == max)
            {
                return (color.GetHue(), 0d, lightness);
            }
            else if (lightness > 0d && lightness <= 0.5d)
            {
                return (color.GetHue(), (max - min) / (max + min), lightness);
            }

            return (color.GetHue(), (max - min) / (2d - (max + min)), lightness);
        }

        /// <summary>
        /// Convert a given <see cref="Color"/> to a HWB color (hue, whiteness, blackness)
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>The hue [0°..360°], whiteness [0..1] and blackness [0..1] of the converted color</returns>
        public static (double Hue, double Whiteness, double Blackness) ConvertToHWBColor(Color color)
        {
            var min = Math.Min(Math.Min(color.R, color.G), color.B) / 255d;
            var max = Math.Max(Math.Max(color.R, color.G), color.B) / 255d;

            return (color.GetHue(), min, 1 - max);
        }

        /// <summary>
        /// Convert a given <see cref="Color"/> to a CIE LAB color (LAB)
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>The lightness [0..100] and two chromaticities [-128..127]</returns>
        public static (double Lightness, double ChromaticityA, double ChromaticityB) ConvertToCIELABColor(Color color)
        {
            var xyz = ConvertToCIEXYZColor(color);
            var lab = GetCIELABColorFromCIEXYZ(xyz.X, xyz.Y, xyz.Z);

            return lab;
        }

        /// <summary>
        /// Convert a given <see cref="Color"/> to a CIE XYZ color (XYZ)
        /// The constants of the formula matches this Wikipedia page, but at a higher precision:
        /// https://en.wikipedia.org/wiki/SRGB#The_reverse_transformation_(sRGB_to_CIE_XYZ)
        /// This page provides a method to calculate the constants:
        /// http://www.brucelindbloom.com/index.html?Eqn_RGB_XYZ_Matrix.html
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>The X [0..1], Y [0..1] and Z [0..1]</returns>
        public static (double X, double Y, double Z) ConvertToCIEXYZColor(Color color)
        {
            double r = color.R / 255d;
            double g = color.G / 255d;
            double b = color.B / 255d;

            // inverse companding, gamma correction must be undone
            double rLinear = (r > 0.04045) ? Math.Pow((r + 0.055) / 1.055, 2.4) : (r / 12.92);
            double gLinear = (g > 0.04045) ? Math.Pow((g + 0.055) / 1.055, 2.4) : (g / 12.92);
            double bLinear = (b > 0.04045) ? Math.Pow((b + 0.055) / 1.055, 2.4) : (b / 12.92);

            return (
                (rLinear * 0.41239079926595948) + (gLinear * 0.35758433938387796) + (bLinear * 0.18048078840183429),
                (rLinear * 0.21263900587151036) + (gLinear * 0.71516867876775593) + (bLinear * 0.07219231536073372),
                (rLinear * 0.01933081871559185) + (gLinear * 0.11919477979462599) + (bLinear * 0.95053215224966058)
            );
        }

        /// <summary>
        /// Convert a CIE XYZ color <see cref="double"/> to a CIE LAB color (LAB) adapted to sRGB D65 white point
        /// The constants of the formula used come from this wikipedia page:
        /// https://en.wikipedia.org/wiki/CIELAB_color_space#Converting_between_CIELAB_and_CIEXYZ_coordinates
        /// </summary>
        /// <param name="x">The <see cref="x"/> represents a mix of the three CIE RGB curves</param>
        /// <param name="y">The <see cref="y"/> represents the luminance</param>
        /// <param name="z">The <see cref="z"/> is quasi-equal to blue (of CIE RGB)</param>
        /// <returns>The lightness [0..100] and two chromaticities [-128..127]</returns>
        private static (double Lightness, double ChromaticityA, double ChromaticityB)
            GetCIELABColorFromCIEXYZ(double x, double y, double z)
        {
            // sRGB reference white (x=0.3127, y=0.3290, Y=1.0), actually CIE Standard Illuminant D65 truncated to 4 decimal places,
            // then converted to XYZ using the formula:
            //   X = x * (Y / y)
            //   Y = Y
            //   Z = (1 - x - y) * (Y / y)
            double x_n = 0.9504559270516717;
            double y_n = 1.0;
            double z_n = 1.0890577507598784;

            // Scale XYZ values relative to reference white
            x /= x_n;
            y /= y_n;
            z /= z_n;

            // XYZ to CIELab transformation
            double delta = 6d / 29;
            double m = (1d / 3) * Math.Pow(delta, -2);
            double t = Math.Pow(delta, 3);

            double fx = (x > t) ? Math.Pow(x, 1.0 / 3.0) : (x * m) + (16.0 / 116.0);
            double fy = (y > t) ? Math.Pow(y, 1.0 / 3.0) : (y * m) + (16.0 / 116.0);
            double fz = (z > t) ? Math.Pow(z, 1.0 / 3.0) : (z * m) + (16.0 / 116.0);

            double l = (116 * fy) - 16;
            double a = 500 * (fx - fy);
            double b = 200 * (fy - fz);

            return (l, a, b);
        }

        /// <summary>
        /// Convert a given <see cref="Color"/> to a natural color (hue, whiteness, blackness)
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to convert</param>
        /// <returns>The hue, whiteness [0..1] and blackness [0..1] of the converted color</returns>
        public static (string Hue, double Whiteness, double Blackness) ConvertToNaturalColor(Color color)
        {
            var min = Math.Min(Math.Min(color.R, color.G), color.B) / 255d;
            var max = Math.Max(Math.Max(color.R, color.G), color.B) / 255d;

            return (GetNaturalColorFromHue(color.GetHue()), min, 1 - max);
        }

        /// <summary>
        /// Return the natural color for the given hue value
        /// </summary>
        /// <param name="hue">The hue value to convert</param>
        /// <returns>A natural color</returns>
        private static string GetNaturalColorFromHue(double hue)
        {
            if (hue < 60d)
            {
                return $"R{Math.Round(hue / 0.6d, 0)}";
            }

            if (hue < 120d)
            {
                return $"Y{Math.Round((hue - 60d) / 0.6d, 0)}";
            }

            if (hue < 180d)
            {
                return $"G{Math.Round((hue - 120d) / 0.6d, 0)}";
            }

            if (hue < 240d)
            {
                return $"C{Math.Round((hue - 180d) / 0.6d, 0)}";
            }

            if (hue < 300d)
            {
                return $"B{Math.Round((hue - 240d) / 0.6d, 0)}";
            }

            return $"M{Math.Round((hue - 300d) / 0.6d, 0)}";
        }

        private static readonly Dictionary<string, char> DefaultFormatTypes = new Dictionary<string, char>()
        {
            { "Re", 'b' },   // red              byte
            { "Gr", 'b' },   // green            byte
            { "Bl", 'b' },   // blue             byte
            { "Al", 'b' },   // alpha            byte
            { "Cy", 'p' },   // cyan             percent
            { "Ma", 'p' },   // magenta          percent
            { "Ye", 'p' },   // yellow           percent
            { "Bk", 'p' },   // black key        percent
            { "Hu", 'i' },   // hue              int
            { "Hn", 'i' },   // hue natural      string
            { "Si", 'p' },   // saturation (HSI) percent
            { "Sl", 'p' },   // saturation (HSL) percent
            { "Sb", 'p' },   // saturation (HSB) percent
            { "Br", 'p' },   // brightness       percent
            { "In", 'p' },   // intensity        percent
            { "Ll", 'p' },   // lightness (HSL)  percent
            { "Lc", 'p' },   // lightness(CIELAB)percent
            { "Va", 'p' },   // value            percent
            { "Wh", 'p' },   // whiteness        percent
            { "Bn", 'p' },   // blackness        percent
            { "Ca", 'p' },   // chromaticityA    percent
            { "Cb", 'p' },   // chromaticityB    percent
            { "Xv", 'i' },   // X value          int
            { "Yv", 'i' },   // Y value          int
            { "Zv", 'i' },   // Z value          int
            { "Dr", 'i' },   // Decimal value (RGB)   int
            { "Dv", 'i' },   // Decimal value (BGR)   int

            // Removed Parameter Na, as the color name gets replaced separately, in localised way
            // { "Na", 's' },   // Color name       string
        };

        public static string GetColorNameParameter() => "%Na";

        private static readonly Dictionary<char, string> FormatTypeToStringFormatters = new Dictionary<char, string>()
        {
            { 'b', "b" },       // 0..255 byte
            { 'h', "x1" },      // hex lowercase one digit
            { 'H', "X1" },      // hex uppercase one digit
            { 'x', "x2" },      // hex lowercase two digits
            { 'X', "X2" },      // hex uppercase two digits
            { 'f', "0.##" },    // float with leading zero, 2 digits
            { 'F', ".##" },     // float without leading zero, 2 digits
            { 'p', "%" },       // percent value
            { 'i', "i" },       // int value
            { 's', "s" },       // string value
        };

        public static string GetStringRepresentation(Color? color, string formatString)
        {
            if (color == null)
            {
                color = Color.Moccasin; // example color
            }

            // convert all %?? expressions to strings
            int formatterPosition = formatString.IndexOf('%', 0);
            while (formatterPosition != -1)
            {
                if (formatterPosition >= formatString.Length - 2)
                {
                    // the formatter % was the last character, we are done
                    break;
                }

                char paramFormat;
                string paramType = formatString.Substring(formatterPosition + 1, 2);
                int paramCount = 3;
                if (DefaultFormatTypes.ContainsKey(paramType))
                {
                    // check the next char, which could be a formatter
                    if (formatterPosition >= formatString.Length - 3)
                    {
                        // not enough characters, end of string, no formatter, use the default one
                        paramFormat = DefaultFormatTypes[paramType];
                        paramCount = 2;
                    }
                    else
                    {
                        paramFormat = formatString[formatterPosition + 3];

                        // check if it a valid formatter
                        if (!FormatTypeToStringFormatters.ContainsKey(paramFormat))
                        {
                            paramFormat = DefaultFormatTypes[paramType];
                            paramCount = 2;
                        }
                    }

                    formatString = string.Concat(formatString.AsSpan(0, formatterPosition), GetStringRepresentation(color.Value, paramFormat, paramType), formatString.AsSpan(formatterPosition + paramCount + 1));
                }

                // search for the next occurrence of the formatter char
                formatterPosition = formatString.IndexOf('%', formatterPosition + 1);
            }

            return formatString;
        }

        private static string GetStringRepresentation(Color color, char paramFormat, string paramType)
        {
            if (!DefaultFormatTypes.ContainsKey(paramType) || !FormatTypeToStringFormatters.ContainsKey(paramFormat))
            {
                return string.Empty;
            }

            switch (paramType)
            {
                case "Re": return ColorByteFormatted(color.R, paramFormat);
                case "Gr": return ColorByteFormatted(color.G, paramFormat);
                case "Bl": return ColorByteFormatted(color.B, paramFormat);
                case "Al": return ColorByteFormatted(color.A, paramFormat);
                case "Cy":
                    var (cyan, _, _, _) = ConvertToCMYKColor(color);
                    cyan = Math.Round(cyan * 100);
                    return cyan.ToString(CultureInfo.InvariantCulture);
                case "Ma":
                    var (_, magenta, _, _) = ConvertToCMYKColor(color);
                    magenta = Math.Round(magenta * 100);
                    return magenta.ToString(CultureInfo.InvariantCulture);
                case "Ye":
                    var (_, _, yellow, _) = ConvertToCMYKColor(color);
                    yellow = Math.Round(yellow * 100);
                    return yellow.ToString(CultureInfo.InvariantCulture);
                case "Bk":
                    var (_, _, _, blackKey) = ConvertToCMYKColor(color);
                    blackKey = Math.Round(blackKey * 100);
                    return blackKey.ToString(CultureInfo.InvariantCulture);
                case "Hu":
                    var (hue, _, _) = ConvertToHSBColor(color);
                    hue = Math.Round(hue);
                    return hue.ToString(CultureInfo.InvariantCulture);
                case "Hn":
                    var (hueNatural, _, _) = ConvertToNaturalColor(color);
                    return hueNatural;
                case "Sb":
                    var (_, saturationB, _) = ConvertToHSBColor(color);
                    saturationB = Math.Round(saturationB * 100);
                    return saturationB.ToString(CultureInfo.InvariantCulture);
                case "Si":
                    var (_, saturationI, _) = ConvertToHSIColor(color);
                    saturationI = Math.Round(saturationI * 100);
                    return saturationI.ToString(CultureInfo.InvariantCulture);
                case "Sl":
                    var (_, saturationL, _) = ConvertToHSLColor(color);
                    saturationL = Math.Round(saturationL * 100);
                    return saturationL.ToString(CultureInfo.InvariantCulture);
                case "Va": // value and brightness are the same values
                case "Br":
                    var (_, _, brightness) = ConvertToHSBColor(color);
                    brightness = Math.Round(brightness * 100);
                    return brightness.ToString(CultureInfo.InvariantCulture);
                case "In":
                    var (_, _, intensity) = ConvertToHSIColor(color);
                    intensity = Math.Round(intensity * 100);
                    return intensity.ToString(CultureInfo.InvariantCulture);
                case "Ll":
                    var (_, _, lightnessL) = ConvertToHSLColor(color);
                    lightnessL = Math.Round(lightnessL * 100);
                    return lightnessL.ToString(CultureInfo.InvariantCulture);
                case "Lc":
                    var (lightnessC, _, _) = ConvertToCIELABColor(color);
                    lightnessC = Math.Round(lightnessC, 2);
                    return lightnessC.ToString(CultureInfo.InvariantCulture);
                case "Wh":
                    var (_, whiteness, _) = ConvertToHWBColor(color);
                    whiteness = Math.Round(whiteness * 100);
                    return whiteness.ToString(CultureInfo.InvariantCulture);
                case "Bn":
                    var (_, _, blackness) = ConvertToHWBColor(color);
                    blackness = Math.Round(blackness * 100);
                    return blackness.ToString(CultureInfo.InvariantCulture);
                case "Ca":
                    var (_, chromaticityA, _) = ConvertToCIELABColor(color);
                    chromaticityA = Math.Round(chromaticityA, 2);
                    return chromaticityA.ToString(CultureInfo.InvariantCulture);
                case "Cb":
                    var (_, _, chromaticityB) = ConvertToCIELABColor(color);
                    chromaticityB = Math.Round(chromaticityB, 2);
                    return chromaticityB.ToString(CultureInfo.InvariantCulture);
                case "Xv":
                    var (x, _, _) = ConvertToCIEXYZColor(color);
                    x = Math.Round(x * 100, 4);
                    return x.ToString(CultureInfo.InvariantCulture);
                case "Yv":
                    var (_, y, _) = ConvertToCIEXYZColor(color);
                    y = Math.Round(y * 100, 4);
                    return y.ToString(CultureInfo.InvariantCulture);
                case "Zv":
                    var (_, _, z) = ConvertToCIEXYZColor(color);
                    z = Math.Round(z * 100, 4);
                    return z.ToString(CultureInfo.InvariantCulture);
                case "Dr":
                    return ((color.R * 65536) + (color.G * 256) + color.B).ToString(CultureInfo.InvariantCulture);
                case "Dv":
                    return (color.R + (color.G * 256) + (color.B * 65536)).ToString(CultureInfo.InvariantCulture);

                // Removed Parameter Na, as the color name gets replaced separately, in localised way
                // case "Na":
                //     return ColorNameHelper.GetColorName(color);
                default: return string.Empty;
            }
        }

        private static string ColorByteFormatted(byte colorByteValue, char paramFormat)
        {
            switch (paramFormat)
            {
                case 'b': return colorByteValue.ToString(CultureInfo.InvariantCulture);
                case 'h':
                case 'H':
                    return (colorByteValue / 16).ToString(FormatTypeToStringFormatters[paramFormat], CultureInfo.InvariantCulture);
                case 'x':
                case 'X':
                    return colorByteValue.ToString(FormatTypeToStringFormatters[paramFormat], CultureInfo.InvariantCulture);
                case 'f':
                case 'F':
                    return (colorByteValue / 255d).ToString(FormatTypeToStringFormatters[paramFormat], CultureInfo.InvariantCulture);
                default: return colorByteValue.ToString(CultureInfo.InvariantCulture);
            }
        }

        public static string GetDefaultFormat(string formatName)
        {
            switch (formatName)
            {
                case "HEX": return "%Rex%Grx%Blx";
                case "RGB": return "rgb(%Re, %Gr, %Bl)";
                case "HSL": return "hsl(%Hu, %Sl%, %Ll%)";
                case "HSV": return "hsv(%Hu, %Sb%, %Va%)";
                case "CMYK": return "cmyk(%Cy%, %Ma%, %Ye%, %Bk%)";
                case "HSB": return "hsb(%Hu, %Sb%, %Br%)";
                case "HSI": return "hsi(%Hu, %Si%, %In%)";
                case "HWB": return "hwb(%Hu, %Wh%, %Bn%)";
                case "NCol": return "%Hn, %Wh%, %Bn%";
                case "CIELAB": return "CIELab(%Lc, %Ca, %Cb)";
                case "CIEXYZ": return "XYZ(%Xv, %Yv, %Zv)";
                case "VEC4": return "(%Reff, %Grff, %Blff, 1f)";
                case "Decimal": return "%Dv";
                case "HEX Int": return "0xFF%ReX%GrX%BlX";
                default: return string.Empty;
            }
        }
    }
}
