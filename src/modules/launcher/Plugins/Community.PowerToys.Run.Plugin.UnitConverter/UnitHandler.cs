using System;
using System.Globalization;
using UnitsNet;

namespace Community.PowerToys.Run.Plugin.UnitConverter
{
    public static class UnitHandler
    {
        public enum Abbreviated
        {
            First,
            Second,
            Both,
            Neither,
            NotFound,
        }

        /// <summary>
        /// Given user unit input, converts it. (E.g "1 foot in cm").
        /// </summary>
        /// <returns>The converted value as a double.</returns>
        public static double ConvertInput(string[] split, QuantityType quantityType, CultureInfo currentCulture)
        {
            string input_first_unit = split[1];
            string input_second_unit = split[3];

            (Abbreviated abbreviated, QuantityInfo unitInfo) = ParseInputForAbbreviation(split, quantityType);

            switch (abbreviated)
            {
                case Abbreviated.Both:
                    return UnitsNet.UnitConverter.ConvertByAbbreviation(double.Parse(split[0], currentCulture), unitInfo.Name, input_first_unit, input_second_unit);

                case Abbreviated.Neither:
                    return UnitsNet.UnitConverter.ConvertByName(double.Parse(split[0], currentCulture), unitInfo.Name, input_first_unit, input_second_unit);

                case Abbreviated.Second:
                    UnitInfo first = Array.Find(unitInfo.UnitInfos, info => info.Name.ToLower() == input_first_unit.ToLower());
                    UnitParser.Default.TryParse(split[3], unitInfo.UnitType, out Enum second_unit);
                    return UnitsNet.UnitConverter.Convert(double.Parse(split[0], currentCulture), first.Value, second_unit);

                case Abbreviated.First:
                    UnitParser.Default.TryParse(split[1], unitInfo.UnitType, out Enum first_unit);
                    UnitInfo second = Array.Find(unitInfo.UnitInfos, info => info.Name.ToLower() == input_second_unit.ToLower());
                    return UnitsNet.UnitConverter.Convert(double.Parse(split[0], currentCulture), first_unit, second.Value);

                case Abbreviated.NotFound:
                default:
                    return double.NaN;
            }
        }

        /// <summary>
        /// Given a split array of user input, parses the input for abbreviations (e.g. "1 cm in meters").
        /// </summary>
        /// <returns>A tuple consisting of an Abbreviated enum and QuantityInfo.</returns>
        public static (Abbreviated Abbreviated, QuantityInfo UnitInfo) ParseInputForAbbreviation(string[] split, QuantityType quantityType)
        {
            string input_first_unit = split[1];
            string input_second_unit = split[3];

            QuantityInfo unit_info = Quantity.GetInfo(quantityType);
            bool first_unit_is_abbreviated = UnitParser.Default.TryParse(split[1], unit_info.UnitType, out Enum _);
            bool second_unit_is_abbreviated = UnitParser.Default.TryParse(split[3], unit_info.UnitType, out Enum _);

            // 3 types of matches:
            // a) 10 ft in cm (double abbreviation)
            // b) 10 feet in centimeter (double unabbreviated)
            // c) 10 feet in cm (single abbreviation)

            if (first_unit_is_abbreviated && second_unit_is_abbreviated)
            {
                // a
                return (Abbreviated.Both, unit_info);
            }
            else if ((!first_unit_is_abbreviated) && (!second_unit_is_abbreviated))
            {
                // b
                bool first_unabbreviated = Array.Exists(unit_info.UnitInfos, unitName => unitName.Name.ToLower() == input_first_unit.ToLower());
                bool second_unabbreviated = Array.Exists(unit_info.UnitInfos, unitName => unitName.Name.ToLower() == input_second_unit.ToLower());

                if (first_unabbreviated && second_unabbreviated)
                {
                    return (Abbreviated.Neither, unit_info);
                }
            }
            else if (first_unit_is_abbreviated != second_unit_is_abbreviated)
            {
                // c
                if (first_unit_is_abbreviated)
                {
                    bool second_unabbreviated = Array.Exists(unit_info.UnitInfos, unitName => unitName.Name.ToLower() == input_second_unit.ToLower());

                    if (second_unabbreviated)
                    {
                        return (Abbreviated.First, unit_info);
                    }
                }
                else if (second_unit_is_abbreviated)
                {
                    bool first_unabbreviated = Array.Exists(unit_info.UnitInfos, unitName => unitName.Name.ToLower() == input_first_unit.ToLower());

                    if (first_unabbreviated)
                    {
                        return (Abbreviated.Second, unit_info);
                    }
                }
            }

            return (Abbreviated.NotFound, null);
        }
    }
}
