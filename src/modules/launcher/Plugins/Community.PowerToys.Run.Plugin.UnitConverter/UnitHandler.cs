﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using UnitsNet;

namespace Community.PowerToys.Run.Plugin.UnitConverter
{
    public static class UnitHandler
    {
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
        /// Given ConvertModel returns collection of possible results.
        /// </summary>
        /// <returns>The converted value as a double.</returns>
        public static IEnumerable<UnitConversionResult> Convert(ConvertModel convertModel)
        {
            var results = new List<UnitConversionResult>();
            foreach (var quantityInfo in _included)
            {
                double convertedValue = UnitHandler.ConvertInput(convertModel, quantityInfo);

                if (!double.IsNaN(convertedValue))
                {
                    UnitConversionResult result = new UnitConversionResult(convertedValue, convertModel.ToUnit, quantityInfo);
                    results.Add(result);
                }
            }

            return results;
        }
    }
}
