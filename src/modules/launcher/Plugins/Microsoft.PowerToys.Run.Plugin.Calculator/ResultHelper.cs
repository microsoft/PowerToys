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
        public static Result CreateResult(CalculateResult result, string iconPath)
        {
            return CreateResult(result.RoundedResult, iconPath);
        }

        public static Result CreateResult(decimal? roundedResult, string iconPath)
        {
            // Return null when the expression is not a valid calculator query.
            if (roundedResult == null)
            {
                return null;
            }

            return new Result
            {
                // Using CurrentCulture since this is user facing
                Title = roundedResult?.ToString(CultureInfo.CurrentCulture),
                IcoPath = iconPath,
                Score = 300,
                SubTitle = Properties.Resources.wox_plugin_calculator_copy_number_to_clipboard,
                Action = c => Action(roundedResult),
            };
        }

        public static bool Action(decimal? roundedResult)
        {
            var ret = false;

            if (roundedResult != null)
            {
                var thread = new Thread(() =>
                {
                    try
                    {
                        Clipboard.SetText(roundedResult?.ToString(CultureInfo.CurrentCulture));
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
