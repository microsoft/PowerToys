// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Windows;

using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.Calculator
{
    public static class ResultHelper
    {
        public static Result CreateResult(CalculateResult result, string iconPath, CultureInfo inputCulture, CultureInfo outputCulture)
        {
            return CreateResult(result.RoundedResult, iconPath, inputCulture, outputCulture);
        }

        public static Result CreateResult(decimal? roundedResult, string iconPath, CultureInfo inputCulture, CultureInfo outputCulture)
        {
            // Return null when the expression is not a valid calculator query.
            if (roundedResult == null)
            {
                return null;
            }

            return new Result
            {
                // Using CurrentCulture since this is user facing
                Title = roundedResult?.ToString(outputCulture),
                IcoPath = iconPath,
                Score = 300,
                SubTitle = Properties.Resources.wox_plugin_calculator_copy_number_to_clipboard,
                Action = c => Action(roundedResult, outputCulture),
                QueryTextDisplay = roundedResult?.ToString(inputCulture),
            };
        }

        public static bool Action(decimal? roundedResult, CultureInfo culture)
        {
            if (roundedResult == null)
            {
                return false;
            }

            if (!ClipboardHelper.CopyToClipboard(roundedResult?.ToString(culture)))
            {
                Log.Warn("Copy failed", typeof(ResultHelper));
                MessageBox.Show(Properties.Resources.wox_plugin_calculator_copy_failed, Properties.Resources.wox_plugin_calculator_copy_failed);
                return false;
            }

            return true;
        }
    }
}
