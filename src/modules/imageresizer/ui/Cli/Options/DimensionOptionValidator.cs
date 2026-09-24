// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

            if (!double.TryParse(valueText, out var value))
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
