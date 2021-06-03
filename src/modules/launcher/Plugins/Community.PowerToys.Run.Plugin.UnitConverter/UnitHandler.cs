using System;
using System.Globalization;
using UnitsNet;

namespace Community.PowerToys.Run.Plugin.UnitConverter
{
    public static class UnitHandler
    {
        /// <summary>
        /// Given string representation of unit, converts it to the enum.
        /// </summary>
        /// <returns>Corresponding enum or null.</returns>
        private static Enum GetUnitEnum(string unit, QuantityInfo unitInfo)
        {
            UnitInfo first = Array.Find(unitInfo.UnitInfos, info => info.Name.ToLower() == unit.ToLower());
            if (first != null)
            {
                return first.Value;
            }

            if (UnitParser.Default.TryParse(unit, unitInfo.UnitType, out Enum enum_unit))
            {
                return enum_unit;
            }

            return null;
        }

        /// <summary>
        /// Given user unit input, converts it. (E.g "1 foot in cm").
        /// </summary>
        /// <returns>The converted value as a double.</returns>
        public static double ConvertInput(string[] split, QuantityType quantityType, CultureInfo currentCulture)
        {
            string firstUnitString = split[1];
            string secondUnitString = split[3];
            QuantityInfo unitInfo = Quantity.GetInfo(quantityType);

            var firstUnit = GetUnitEnum(firstUnitString, unitInfo);
            var secondUnit = GetUnitEnum(secondUnitString, unitInfo);

            if (firstUnit != null && secondUnit != null)
            {
                return UnitsNet.UnitConverter.Convert(double.Parse(split[0], currentCulture), firstUnit, secondUnit);
            }

            return double.NaN;
        }
    }
}
