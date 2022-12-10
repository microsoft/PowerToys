// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows.Media;
using ColorPicker.Models;

namespace ColorPicker.Helpers
{
    internal enum GroupExportedColorsBy
    {
        Color,
        Format,
    }

    internal static class SerializationHelper
    {
        public static Dictionary<string, Dictionary<string, string>> ConvertToDesiredColorFormats(
            IList colorsToExport,
            IEnumerable<ColorFormatModel> colorRepresentations,
            GroupExportedColorsBy method)
        {
            var colors = new Dictionary<string, Dictionary<string, string>>();
            var colorFormatModels = colorRepresentations.ToList();
            var i = 1;
            switch (method)
            {
                case GroupExportedColorsBy.Color:
                    {
                        foreach (Color color in (IList)colorsToExport)
                        {
                            var tmp = new Dictionary<string, string>();
                            foreach (var colorFormatModel in colorFormatModels)
                            {
                                var colorInSpecificFormat = colorFormatModel.GetColorText(color);
                                if (colorFormatModel.FormatName == "HEX")
                                {
                                    colorInSpecificFormat = "#" + colorInSpecificFormat;
                                }

                                tmp.Add(
                                    colorFormatModel.FormatName,
                                    colorInSpecificFormat.Replace(
                                                                  colorFormatModel.FormatName.ToLower(CultureInfo.InvariantCulture),
                                                                  string.Empty,
                                                                  StringComparison.InvariantCultureIgnoreCase));
                            }

                            colors.Add($"color{i++}", tmp);
                        }
                    }

                    break;
                case GroupExportedColorsBy.Format:
                    {
                        foreach (var colorFormatModel in colorFormatModels)
                        {
                            var tmp = new Dictionary<string, string>();
                            i = 1;
                            foreach (Color color in (IList)colorsToExport)
                            {
                                var colorInSpecificFormat = colorFormatModel.GetColorText(color);
                                if (colorFormatModel.FormatName == "HEX")
                                {
                                    colorInSpecificFormat = "#" + colorInSpecificFormat;
                                }

                                tmp.Add(
                                    $"color{i++}",
                                    colorInSpecificFormat.Replace(
                                                                  colorFormatModel.FormatName.ToLower(CultureInfo.InvariantCulture),
                                                                  string.Empty,
                                                                  StringComparison.InvariantCultureIgnoreCase));
                            }

                            colors.Add(colorFormatModel.FormatName, tmp);
                        }
                    }

                    break;
            }

            return colors;
        }

        public static string ToTxt(this Dictionary<string, Dictionary<string, string>> source, char separator)
        {
            var res = string.Empty;
            foreach (var (key, val) in source)
            {
                res += $"{key}{separator}";
                res = val.Aggregate(res, (current, pair) =>
                                         {
                                             var (p, v) = pair;

                                             // if grouped by format, add a space between color* and its value, to avoid illegibility:
                                             // decimal;color1 12345678;color2 23456789;color11 13579246
                                             if (key == "Decimal")
                                             {
                                                 v = " " + v;
                                             }

                                             return current + $"{p}{v}{separator}";
                                         });
                res = res.TrimEnd(separator) + System.Environment.NewLine;
            }

            return res;
        }

        public static string ToJson(this Dictionary<string, Dictionary<string, string>> source, bool indented = true)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = indented,
            };

            return JsonSerializer.Serialize(source, options);
        }
    }
}
