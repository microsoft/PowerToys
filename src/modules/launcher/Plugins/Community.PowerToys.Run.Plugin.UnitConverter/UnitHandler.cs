// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using UnitsNet;

namespace Community.PowerToys.Run.Plugin.UnitConverter
{
    public static class UnitHandler
    {
        private static readonly int _roundingFractionalDigits = 4;

        private static readonly string[] _ounceRepresentation =
        {
            "ounce",
            "oz",
            "o.z.",
            "o.z",
        };

        private static readonly QuantityInfo[] _included = new QuantityInfo[]
        {
            Acceleration.Info,
            Angle.Info,
            Area.Info,
            Duration.Info,
            Energy.Info,
            Information.Info,
            Length.Info,
            Mass.Info,
            Power.Info,
            Pressure.Info,
            Speed.Info,
            Temperature.Info,
            Volume.Info,
        };

        /// <summary>
        /// Given string representation of unit, converts it to the enum.
        /// </summary>
        /// <returns>Corresponding enum or null.</returns>
        private static Enum GetUnitEnum(string unit, QuantityInfo unitInfo)
        {
            UnitInfo first = Array.Find(unitInfo.UnitInfos, info =>
                string.Equals(unit, info.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(unit, info.PluralName, StringComparison.OrdinalIgnoreCase));

            if (first != null)
            {
                return first.Value;
            }

            if (UnitsNetSetup.Default.UnitParser.TryParse(unit, unitInfo.UnitType, out Enum enum_unit))
            {
                return enum_unit;
            }

            var cultureInfoEnglish = new System.Globalization.CultureInfo("en-US");
            if (UnitsNetSetup.Default.UnitParser.TryParse(unit, unitInfo.UnitType, cultureInfoEnglish, out Enum enum_unit_en))
            {
                return enum_unit_en;
            }

            return null;
        }

        /// <summary>
        /// Rounds the value to the predefined number of significant digits.
        /// </summary>
        /// <param name="value">Value to be rounded</param>
        public static double Round(double value)
        {
            if (value == 0.0D)
            {
                return 0;
            }

            var power = Math.Floor(Math.Log10(Math.Abs(value)));
            var exponent = Math.Pow(10, power);
            var rounded = Math.Round(value / exponent, _roundingFractionalDigits) * exponent;
            return rounded;
        }

        /// <summary>
        /// Given parsed ConvertModel, computes result. (E.g "1 foot in cm").
        /// </summary>
        /// <returns>The converted value as a double.</returns>
        public static double ConvertInput(ConvertModel convertModel, QuantityInfo quantityInfo)
        {
            var fromUnit = GetUnitEnum(convertModel.FromUnit, quantityInfo);
            var toUnit = GetUnitEnum(convertModel.ToUnit, quantityInfo);

            if (fromUnit != null && toUnit != null)
            {
                return UnitsNet.UnitConverter.Convert(convertModel.Value, fromUnit, toUnit);
            }

            return double.NaN;
        }

        /// <summary>
        /// Given ConvertModel and a QuantityInfo adds a result to the possible results.
        /// </summary>
        public static void ConvertAndAddToResult(ConvertModel convertModel, QuantityInfo quantityInfo, List<UnitConversionResult> results)
        {
            double convertedValue = UnitHandler.ConvertInput(convertModel, quantityInfo);

            if (!double.IsNaN(convertedValue))
            {
                UnitConversionResult result = new UnitConversionResult(Round(convertedValue), convertModel.FromUnit, convertModel.ToUnit, quantityInfo);
                results.Add(result);
            }
        }

        /// <summary>
        /// Given ConvertModel returns collection of possible results.
        /// </summary>
        /// <returns>The converted value as a double.</returns>
        public static IEnumerable<UnitConversionResult> Convert(ConvertModel convertModel)
        {
            var results = new List<UnitConversionResult>();
            foreach (var quantityInfo in _included)
            {
                if (quantityInfo == Volume.Info && (_ounceRepresentation.Contains(convertModel.FromUnit) || _ounceRepresentation.Contains(convertModel.ToUnit)))
                {
                    if (_ounceRepresentation.Contains(convertModel.FromUnit))
                    {
                        string temp = convertModel.FromUnit;

                        convertModel.FromUnit = "usounce";
                        ConvertAndAddToResult(convertModel, quantityInfo, results);

                        convertModel.FromUnit = "imperialounce";
                        ConvertAndAddToResult(convertModel, quantityInfo, results);

                        convertModel.FromUnit = temp;
                    }
                    else
                    {
                        string temp = convertModel.ToUnit;

                        convertModel.ToUnit = "usounce";
                        ConvertAndAddToResult(convertModel, quantityInfo, results);

                        convertModel.ToUnit = "imperialounce";
                        ConvertAndAddToResult(convertModel, quantityInfo, results);

                        convertModel.ToUnit = temp;
                    }
                }
                else
                {
                    ConvertAndAddToResult(convertModel, quantityInfo, results);
                }
            }

            return results;
        }
    }
}
