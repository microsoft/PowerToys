// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Mages.Core;

namespace Microsoft.Plugin.Calculator
{
    public class CalculateEngine
    {
        private readonly Engine _magesEngine = new Engine();

        public CalculateResult Interpret(string input)
        {
            return Interpret(input, CultureInfo.CurrentCulture);
        }

        public CalculateResult Interpret(string input, CultureInfo cultureInfo)
        {
            if (input == null)
            {
                throw new ArgumentNullException(paramName: nameof(input));
            }

            if (!CalculateHelper.InputValid(input))
            {
                return default;
            }

            var result = _magesEngine.Interpret(input);

            // This could happen for some incorrect queries, like pi(2)
            if (result == null)
            {
                return default;
            }

            result = TransformResult(result);

            if (string.IsNullOrEmpty(result?.ToString()))
            {
                return default;
            }

            var decimalResult = Convert.ToDecimal(result, cultureInfo);
            var roundedResult = Math.Round(decimalResult, 10, MidpointRounding.AwayFromZero);

            return new CalculateResult()
            {
                Result = decimalResult,
                RoundedResult = roundedResult,
            };
        }

        private static object TransformResult(object result)
        {
            if (result.ToString() == "NaN")
            {
                return Properties.Resources.wox_plugin_calculator_not_a_number;
            }

            if (result is Function)
            {
                return Properties.Resources.wox_plugin_calculator_expression_not_complete;
            }

            return result;
        }
    }
}
