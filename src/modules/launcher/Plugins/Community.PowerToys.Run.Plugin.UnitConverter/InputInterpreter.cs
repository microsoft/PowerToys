using System.Globalization;
using System.Text.RegularExpressions;
using UnitsNet;

namespace Community.PowerToys.Run.Plugin.UnitConverter
{
    public static class InputInterpreter
    {
        public static string[] RegexSplitter(ref string[] split) {
            return Regex.Split(split[0], @"(?<=\d)(?![,.])(?=\D)|(?<=\D)(?<![,.])(?=\d)");
        }

        /// <summary>
        /// Replaces a split input array with shorthand feet/inch notation (1', 1'2" etc) to 'x foot in cm'. 
        /// </summary>
        /// <param name="split"></param>
        /// <param name="culture"></param>
        public static void ShorthandFeetInchHandler(ref string[] split, CultureInfo culture) {
            // catches 1' || 1" || 1'2 || 1'2" in cm
            // by converting it to "x foot in cm"
            if (split.Length == 3) {
                string[] shortsplit = RegexSplitter(ref split);

                switch (shortsplit.Length) {
                    case 2:
                        // ex: 1' & 1"
                        if (shortsplit[1] == "\'") {
                            string[] newInput = new string[] { shortsplit[0], "foot", split[1], split[2] };
                            split = newInput;
                        }
                        else if (shortsplit[1] == "\"") {
                            string[] newInput = new string[] { shortsplit[0], "inch", split[1], split[2] };
                            split = newInput;
                        }
                        break;

                    case 3:
                    case 4:
                        // ex: 1'2 and 1'2"
                        if (shortsplit[1] == "\'") {
                            bool isFeet = double.TryParse(shortsplit[0], NumberStyles.AllowDecimalPoint, culture, out double feet);
                            bool isInches = double.TryParse(shortsplit[2], NumberStyles.AllowDecimalPoint, culture, out double inches);

                            if (!isFeet || !isInches) {
                                // atleast one could not be parsed correctly
                                break;
                            }

                            double totalInFeet = Length.FromFeetInches(feet, inches).Feet;
                            string convertedTotalInFeet = totalInFeet.ToString();

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
        /// Adds degree prefixes to degree units for shorthand notation. E.g. '10 c in fahrenheit' to '10 °c in degreeFahrenheit'. 
        /// </summary>
        /// <param name="split"></param>
        public static void DegreePrefixer(ref string[] split) {
            switch (split[1]) {
                case "celsius":
                    split[1] = "degreeCelsius";
                    break;

                case "fahrenheit":
                    split[1] = "degreeFahrenheit";
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

            switch (split[3]) {
                case "celsius":
                    split[3] = "degreeCelsius";
                    break;

                case "fahrenheit":
                    split[3] = "degreeFahrenheit";
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
    }
}
