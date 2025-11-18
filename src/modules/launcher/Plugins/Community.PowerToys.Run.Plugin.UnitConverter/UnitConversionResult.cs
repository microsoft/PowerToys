// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using UnitsNet;

namespace Community.PowerToys.Run.Plugin.UnitConverter
{
    public class UnitConversionResult
    {
        public static string TitleFormat { get; set; } = "G14";

        public static string CopyFormat { get; set; } = "R";

        public double ConvertedValue { get; }

        public string UnitName { get; }

        public QuantityInfo QuantityInfo { get; }

        public UnitConversionResult(double convertedValue, string unitName, QuantityInfo quantityInfo)
        {
            ConvertedValue = convertedValue;
            UnitName = unitName;
            QuantityInfo = quantityInfo;
        }

        public string ToString(IFormatProvider provider = null)
        {
            if (provider == null)
            {
                provider = System.Globalization.CultureInfo.CurrentCulture;
            }

            // Check if the formatted number matches the original value. If they differ, some
            // decimal places where cut off, and therefore we add an ellipsis.
            string formatted = ConvertedValue.ToString(TitleFormat, provider);

            if (double.TryParse(formatted, provider, out double parsedNumber) &&
                Math.Abs(ConvertedValue - parsedNumber) > double.Epsilon &&
                !formatted.Contains('E', StringComparison.OrdinalIgnoreCase))
            {
                return formatted + "… " + UnitName;
            }

            return formatted + " " + UnitName;
        }
    }
}
