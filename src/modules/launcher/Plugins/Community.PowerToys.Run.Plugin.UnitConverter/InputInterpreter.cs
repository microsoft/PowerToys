// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnitsNet;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.UnitConverter
{
    public static class InputInterpreter
    {
        private static string pattern = @"(?<=\d)(?![,.])(?=\D)|(?<=\D)(?<![,.])(?=\d)";

        public static string[] RegexSplitter(string[] split)
        {
            return Regex.Split(split[0], pattern);
        }

        /// <summary>
        /// Separates input like: "1ft in cm" to "1 ft in cm"
        /// </summary>
        public static void InputSpaceInserter(ref string[] split)
        {
            if (split.Length != 3)
            {
                return;
            }

            string[] parseInputWithoutSpace = Regex.Split(split[0], pattern);

            if (parseInputWithoutSpace.Length > 1)
            {
                string[] firstEntryRemoved = split.Skip(1).ToArray();
                string[] newSplit = new string[] { parseInputWithoutSpace[0], parseInputWithoutSpace[1] };

                split = newSplit.Concat(firstEntryRemoved).ToArray();
            }
        }

        /// <summary>
        /// Replaces a split input array with shorthand feet/inch notation (1', 1'2" etc) to 'x foot in cm'.
        /// </summary>
        public static void ShorthandFeetInchHandler(ref string[] split, CultureInfo culture)
        {
            if (!split[0].Contains('\'') && !split[0].Contains('\"'))
            {
                return;
            }

            // catches 1' || 1" || 1'2 || 1'2" in cm
            // by converting it to "x foot in cm"
            if (split.Length == 3)
            {
                string[] shortsplit = RegexSplitter(split);

                switch (shortsplit.Length)
                {
                    case 2:
                        // ex: 1' & 1"
                        if (shortsplit[1] == "\'")
                        {
                            string[] newInput = new string[] { shortsplit[0], "foot", split[1], split[2] };
                            split = newInput;
                        }
                        else if (shortsplit[1] == "\"")
                        {
                            string[] newInput = new string[] { shortsplit[0], "inch", split[1], split[2] };
                            split = newInput;
                        }

                        break;

                    case 3:
                    case 4:
                        // ex: 1'2 and 1'2"
                        if (shortsplit[1] == "\'")
                        {
                            bool isFeet = double.TryParse(shortsplit[0], NumberStyles.AllowDecimalPoint, culture, out double feet);
                            bool isInches = double.TryParse(shortsplit[2], NumberStyles.AllowDecimalPoint, culture, out double inches);

                            if (!isFeet || !isInches)
                            {
                                // atleast one could not be parsed correctly
                                break;
                            }

                            string convertedTotalInFeet = Length.FromFeetInches(feet, inches).Feet.ToString(culture);

                            string[] newInput = new string[] { convertedTotalInFeet, "foot", split[1], split[2] };
                            split = newInput;
                        }

                        break;

                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Adds degree prefixes to degree units for shorthand notation. E.g. '10 c in fahrenheit' becomes '10 °c in DegreeFahrenheit'.
        /// </summary>
        public static void DegreePrefixer(ref string[] split)
        {
            switch (split[1].ToLower(CultureInfo.CurrentCulture))
            {
                case "celsius":
                    split[1] = "DegreeCelsius";
                    break;

                case "fahrenheit":
                    split[1] = "DegreeFahrenheit";
                    break;

                case "c":
                    split[1] = "°c";
                    break;

                case "f":
                    split[1] = "°f";
                    break;

                default:
                    break;
            }

            switch (split[3].ToLower(CultureInfo.CurrentCulture))
            {
                case "celsius":
                    split[3] = "DegreeCelsius";
                    break;

                case "fahrenheit":
                    split[3] = "DegreeFahrenheit";
                    break;

                case "c":
                    split[3] = "°c";
                    break;

                case "f":
                    split[3] = "°f";
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// The plural form "feet" is not recognized by UniteNets. Replace it with "ft".
        /// </summary>
        public static void FeetToFt(ref string[] split)
        {
            if (split[1].ToLowerInvariant() == "feet")
            {
                split[1] = "ft";
            }

            if (split[3].ToLowerInvariant() == "feet")
            {
                split[3] = "ft";
            }
        }

        /// <summary>
        /// Converts spelling "metre" to "meter"
        /// </summary>
        public static void MetreToMeter(ref string[] split)
        {
            if (split[1].ToLowerInvariant() == "metre")
            {
                split[1] = "meter";
            }

            if (split[3].ToLowerInvariant() == "metre")
            {
                split[3] = "meter";
            }
        }

        /// <summary>
        /// Choose "UsGallon" or "ImperialGallon" according to current culture when the input contains "gal" or "gallon".
        /// </summary>
        public static void GallonHandler(ref string[] split, CultureInfo culture)
        {
            HashSet<string> britishCultureNames = new HashSet<string>() { "en-AI", "en-VG", "en-GB", "en-KY", "en-MS", "en-AG", "en-DM", "en-GD", "en-KN", "en-LC", "en-VC", "en-IE", "en-GY", "en-AE" };
            if (split[1].ToLowerInvariant() == "gal" || split[1].ToLowerInvariant() == "gallon")
            {
                if (britishCultureNames.Contains(culture.Name))
                {
                    split[1] = "ImperialGallon";
                }
                else
                {
                    split[1] = "UsGallon";
                }
            }

            if (split[3].ToLowerInvariant() == "gal" || split[3].ToLowerInvariant() == "gallon")
            {
                if (britishCultureNames.Contains(culture.Name))
                {
                    split[3] = "ImperialGallon";
                }
                else
                {
                    split[3] = "UsGallon";
                }
            }
        }

        public static ConvertModel Parse(Query query)
        {
            string[] split = query.Search.Split(' ');

            InputInterpreter.ShorthandFeetInchHandler(ref split, CultureInfo.CurrentCulture);
            InputInterpreter.InputSpaceInserter(ref split);

            if (split.Length != 4)
            {
                // deny any other queries than:
                // 10 ft in cm
                // 10 ft to cm
                return null;
            }

            InputInterpreter.DegreePrefixer(ref split);
            InputInterpreter.MetreToMeter(ref split);
            InputInterpreter.FeetToFt(ref split);
            InputInterpreter.GallonHandler(ref split, CultureInfo.CurrentCulture);
            if (!double.TryParse(split[0], out double value))
            {
                return null;
            }

            return new ConvertModel()
            {
                Value = value,
                FromUnit = split[1],
                ToUnit = split[3],
            };
        }
    }
}
