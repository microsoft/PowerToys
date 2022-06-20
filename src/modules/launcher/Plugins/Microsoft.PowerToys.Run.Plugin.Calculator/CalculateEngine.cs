// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Mages.Core;

namespace Microsoft.PowerToys.Run.Plugin.Calculator
{
    public class CalculateEngine
    {
        private readonly Engine _magesEngine = new Engine(new Configuration
        {
            Scope = new Dictionary<string, object>
            {
                { "e", Math.E }, // e is not contained in the default mages engine
            },
        });

        public const int RoundingDigits = 10;

        /// <summary>
        /// Interpret
        /// </summary>
        /// <param name="cultureInfo">Use CultureInfo.CurrentCulture if something is user facing</param>
        public CalculateResult Interpret(string input, CultureInfo cultureInfo, out string error)
        {
            error = default;

            if (!CalculateHelper.InputValid(input))
            {
                return default;
            }

            // mages has quirky log representation
            // mage has log == ln vs log10
            input = input.
                        Replace("log(", "log10(", true, CultureInfo.CurrentCulture).
                        Replace("ln(", "log(", true, CultureInfo.CurrentCulture);

            var result = _magesEngine.Interpret(input);

            // This could happen for some incorrect queries, like pi(2)
            if (result == null)
            {
                error = Properties.Resources.wox_plugin_calculator_expression_not_complete;
                return default;
            }

            result = TransformResult(result);
            if (result is string)
            {
                error = result as string;
                return default;
            }

            if (string.IsNullOrEmpty(result?.ToString()))
            {
                return default;
            }

            var decimalResult = Convert.ToDecimal(result, cultureInfo);
            var roundedResult = Round(decimalResult);

            return new CalculateResult()
            {
                Result = decimalResult,
                RoundedResult = roundedResult,
            };
        }

        public static decimal Round(decimal value)
        {
            return Math.Round(value, RoundingDigits, MidpointRounding.AwayFromZero);
        }

        private static dynamic TransformResult(object result)
        {
            if (result.ToString() == "NaN")
            {
                return Properties.Resources.wox_plugin_calculator_not_a_number;
            }

            if (result is Function)
            {
                return Properties.Resources.wox_plugin_calculator_expression_not_complete;
            }

            if (result is double[,])
            {
                // '[10,10]' is interpreted as array by mages engine
                return Properties.Resources.wox_plugin_calculator_double_array_returned;
            }

            return result;
        }
    }
}
