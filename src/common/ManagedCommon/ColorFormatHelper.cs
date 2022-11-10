// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ManagedCommon
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Drawing;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public static class ColorFormatHelper
    {
        private static readonly Dictionary<char, char> DefaultFormatTypes = new Dictionary<char, char>()
        {
            { 'R', 'b' },   // red              byte
            { 'G', 'b' },   // green            byte
            { 'B', 'b' },   // blue             byte
            { 'A', 'b' },   // alpha            byte
            { 'C', 'p' },   // cyan             percent
            { 'M', 'p' },   // magenta          percent
            { 'E', 'p' },   // yellow           percent
            { 'K', 'p' },   // black key        percent
            { 'H', 'i' },   // hue              int
            { 'S', 'p' },   // saturation       percent
            { 'T', 'p' },   // brightnes        percent
            { 'I', 'p' },   // intensity        percent
            { 'L', 'p' },   // lightness        percent
            { 'U', 'p' },   // value            percent
            { 'W', 'p' },   // whiteness        percent
            { 'N', 'p' },   // blacknes         percent
            { 'O', 'p' },   // chromaticityA    percent
            { 'F', 'p' },   // chromaticityB    percent
            { 'X', 'i' },   // X value          int
            { 'Y', 'i' },   // Y value          int
            { 'Z', 'i' },   // Z value          int
            { 'D', 'i' },   // Decimal value    int
        };

        private static readonly Dictionary<char, string> FormatTypeToStringFormatters = new Dictionary<char, string>()
        {
            { 'b', "b" },       // 0..255 byte
            { 'p', "%" },       // percent value
            { 'f', "f" },       // float with leading zero, 2 digits
            { 'h', "x2" },
            { 'i', "i" },
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
                if (formatterPosition >= formatString.Length - 1)
                {
                    // the formatter % was the last character, we are done
                    break;
                }

                char paramFormat;
                char paramType = formatString[formatterPosition + 1];
                int paramCount = 2;
                if (DefaultFormatTypes.ContainsKey(paramType))
                {
                    // check the next char, which could be a formatter
                    if (formatterPosition >= formatString.Length - 2)
                    {
                        // not enough characters, end of string, no formatter, use the default one
                        paramFormat = DefaultFormatTypes[paramType];
                        paramCount = 1;
                    }
                    else
                    {
                        paramFormat = formatString[formatterPosition + 2];

                        // check if it a valid formatter
                        if (!FormatTypeToStringFormatters.ContainsKey(paramFormat))
                        {
                            paramFormat = DefaultFormatTypes[paramType];
                            paramCount = 1;
                        }
                    }

                    formatString = string.Concat(formatString.AsSpan(0, formatterPosition), GetStringRepresentation(color.Value, paramFormat, paramType), formatString.AsSpan(formatterPosition + paramCount + 1));
                }

                // search for the next occurence of the formatter char
                formatterPosition = formatString.IndexOf('%', formatterPosition + 1);
            }

            return formatString;
        }

        private static string GetStringRepresentation(Color color, char paramFormat, char paramType)
        {
            if (!DefaultFormatTypes.ContainsKey(paramType) || !FormatTypeToStringFormatters.ContainsKey(paramFormat))
            {
                return string.Empty;
            }

            switch (paramType)
            {
                case 'R': return color.R.ToString(CultureInfo.InvariantCulture);
                case 'G': return color.G.ToString(CultureInfo.InvariantCulture);
                case 'B': return color.B.ToString(CultureInfo.InvariantCulture);
                case 'A': return color.A.ToString(CultureInfo.InvariantCulture);
                default: return string.Empty;
            }
        }
    }
}
