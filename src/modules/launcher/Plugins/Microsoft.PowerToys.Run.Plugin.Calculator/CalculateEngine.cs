// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

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

        public enum TrigMode
        {
            Radians,
            Degrees,
            Gradians,
        }

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

            // check for division by zero
            // We check if the string contains a slash followed by space (optional) and zero. Whereas the zero must not be followed by a dot, comma, 'b', 'o' or 'x' as these indicate a number with decimal digits or a binary/octal/hexadecimal value respectively. The zero must also not be followed by other digits.
            if (new Regex("\\/\\s*0(?!(?:[,\\.0-9]|[box]0*[1-9a-f]))", RegexOptions.IgnoreCase).Match(input).Success)
            {
                error = Properties.Resources.wox_plugin_calculator_division_by_zero;
                return default;
            }

            // mages has quirky log representation
            // mage has log == ln vs log10
            input = input.
                        Replace("log(", "log10(", true, CultureInfo.CurrentCulture).
                        Replace("ln(", "log(", true, CultureInfo.CurrentCulture);

            input = CalculateHelper.FixHumanMultiplicationExpressions(input);

            // Get the user selected trigonometry unit
            TrigMode trigMode = Main.GetTrigMode();

            // Modify trig functions depending on angle unit setting
            input = CalculateHelper.UpdateTrigFunctions(input, trigMode);

            // Expand conversions between trig units
            input = CalculateHelper.ExpandTrigConversions(input, trigMode);

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
