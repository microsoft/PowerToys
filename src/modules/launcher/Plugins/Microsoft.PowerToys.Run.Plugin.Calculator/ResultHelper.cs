// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Wox.Plugin;

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
            var ret = false;

            if (roundedResult != null)
            {
                var thread = new Thread(() =>
                {
                    try
                    {
                        Clipboard.SetText(roundedResult?.ToString(culture));
                        ret = true;
                    }
                    catch (ExternalException)
                    {
                        MessageBox.Show(Properties.Resources.wox_plugin_calculator_copy_failed);
                    }
                });

                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
            }

            return ret;
        }
    }
}
