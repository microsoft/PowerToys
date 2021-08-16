// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using UnitsNet;

namespace Community.PowerToys.Run.Plugin.UnitConverter
{
    public class UnitConversionResult
    {
        public double ConvertedValue { get; }

        public string UnitName { get; }

        public QuantityType QuantityType { get; }

        public UnitConversionResult(double convertedValue, string unitName, QuantityType quantityType)
        {
            ConvertedValue = convertedValue;
            UnitName = unitName;
            QuantityType = quantityType;
        }
    }
}
