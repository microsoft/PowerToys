// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Community.PowerToys.Run.Plugin.UnitConverter
{
    public class ConvertModel
    {
        public double Value { get; set; }

        public string FromUnit { get; set; }

        public string ToUnit { get; set; }

        public ConvertModel()
        {
        }

        public ConvertModel(double value, string fromUnit, string toUnit)
        {
            Value = value;
            FromUnit = fromUnit;
            ToUnit = toUnit;
        }
    }
}
