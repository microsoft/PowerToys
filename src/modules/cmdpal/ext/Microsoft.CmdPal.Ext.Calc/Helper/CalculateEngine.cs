// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using CalculatorEngineCommon;
using Windows.Foundation.Collections;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public static class CalculateEngine
{
    private static readonly PropertySet _constants = new()
    {
        { "pi", Math.PI },
        { "e", Math.E },
    };

    private static readonly Calculator _calculator = new Calculator(_constants);

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
    public static CalculateResult Interpret(SettingsManager settings, string input, CultureInfo cultureInfo, out string error)
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
            error = Properties.Resources.calculator_division_by_zero;
            return default;
        }

        // mages has quirky log representation
        // mage has log == ln vs log10
        input = input.
                    Replace("log(", "log10(", true, CultureInfo.CurrentCulture).
                    Replace("ln(", "log(", true, CultureInfo.CurrentCulture);

        input = CalculateHelper.FixHumanMultiplicationExpressions(input);

        // Get the user selected trigonometry unit
        TrigMode trigMode = settings.TrigUnit;

        // Modify trig functions depending on angle unit setting
        input = CalculateHelper.UpdateTrigFunctions(input, trigMode);

        // Expand conversions between trig units
        input = CalculateHelper.ExpandTrigConversions(input, trigMode);

        var result = _calculator.EvaluateExpression(input);

        // This could happen for some incorrect queries, like pi(2)
        if (result == "NaN")
        {
            error = Properties.Resources.calculator_expression_not_complete;
            return default;
        }

        if (string.IsNullOrEmpty(result))
        {
            return default;
        }

        var decimalResult = Convert.ToDecimal(result, cultureInfo);

        // Remove trailing zeros from the decimal string representation (e.g., "1.2300" -> "1.23")
        // This is necessary because the value extracted from exprtk may contain unnecessary trailing zeros.
        var formatted = decimalResult.ToString("G29", cultureInfo);
        decimalResult = Convert.ToDecimal(formatted, cultureInfo);
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
}
