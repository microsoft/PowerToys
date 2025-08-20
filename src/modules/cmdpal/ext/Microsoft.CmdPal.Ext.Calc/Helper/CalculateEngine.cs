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
    public static CalculateResult Interpret(ISettingsInterface settings, string input, CultureInfo cultureInfo, out string error)
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

        var decimalResult = Convert.ToDecimal(result, new CultureInfo("en-US"));

        var roundedResult = FormatMax15Digits(decimalResult, cultureInfo);

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

    /// <summary>
    /// Format a decimal so that the output contains **at most 15 total digits**
    /// (integer + fraction, not counting the decimal point or minus sign).
    /// Any extra fractional digits are rounded using “away-from-zero” rounding.
    /// Trailing zeros in the fractional part—and a dangling decimal point—are removed.
    /// Examples
    ///   1.9999999999      → "1.9999999999"
    ///   100000.9999999999 → "100001"
    ///   1234567890123.45  → "1234567890123.45"
    /// </summary>
    public static decimal FormatMax15Digits(decimal value, CultureInfo cultureInfo)
    {
        var absValue = Math.Abs(value);
        var integerDigits = absValue >= 1 ? (int)Math.Floor(Math.Log10((double)absValue)) + 1 : 1;

        var maxDecimalDigits = Math.Max(0, 15 - integerDigits);

        var rounded = Math.Round(value, maxDecimalDigits, MidpointRounding.AwayFromZero);

        var formatted = rounded.ToString("G29", cultureInfo);

        return Convert.ToDecimal(formatted, cultureInfo);
    }
}
