// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace ImageResizer.Cli.Options
{
    internal static class DimensionOptionValidator
    {
        internal static string Validate(string valueText)
        {
            if (string.IsNullOrWhiteSpace(valueText))
            {
                return null;
            }

            if (!double.TryParse(valueText, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) &&
                !double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                // Leave type-conversion errors to System.CommandLine.
                return null;
            }

            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
            {
                return Properties.Resources.CLI_ErrorInvalidDimension;
            }

            if (value > int.MaxValue)
            {
                return Properties.Resources.Error_DimensionOutOfRange;
            }

            return null;
        }
    }
}
