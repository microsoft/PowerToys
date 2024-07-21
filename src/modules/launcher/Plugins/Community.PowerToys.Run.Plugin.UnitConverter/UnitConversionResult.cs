// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using UnitsNet;

namespace Community.PowerToys.Run.Plugin.UnitConverter
{
    public class UnitConversionResult
    {
        public static string Format { get; set; } = "g14";

        public double ConvertedValue { get; }

        public string UnitNameFrom { get; }

        public string UnitNameTo { get; }

        public QuantityInfo QuantityInfo { get; }

        public UnitConversionResult(double convertedValue, string unitNameFrom, string unitNameTo, QuantityInfo quantityInfo)
        {
            ConvertedValue = convertedValue;
            UnitNameFrom = unitNameFrom;
            UnitNameTo = unitNameTo;
            QuantityInfo = quantityInfo;
        }

        public string ToString(System.IFormatProvider provider = null)
        {
            if (provider == null)
            {
                provider = System.Globalization.CultureInfo.CurrentCulture;
            }

            return ConvertedValue.ToString(Format, provider) + " " + UnitNameTo;
        }
    }
}
