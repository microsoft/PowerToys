// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Wox.Plugin;

namespace Microsoft.Plugin.Calculator
{
    public static class ResultHelper
    {
        public static Result CreateResult(CalculateResult result, string iconPath)
        {
            return CreateResult(result.Result, result.RoundedResult, iconPath);
        }

        public static Result CreateResult(decimal result, decimal roundedResult, string iconPath)
        {
            return new Result
            {
                Title = roundedResult.ToString(CultureInfo.CurrentCulture),
                IcoPath = iconPath,
                Score = 300,
                SubTitle = Properties.Resources.wox_plugin_calculator_copy_number_to_clipboard,
                Action = c => Action(result),
            };
        }

        public static bool Action(decimal result)
        {
            var ret = false;
            var thread = new Thread(() =>
            {
                try
                {
                    Clipboard.SetText(result.ToString(CultureInfo.CurrentUICulture.NumberFormat));
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
            return ret;
        }
    }
}
