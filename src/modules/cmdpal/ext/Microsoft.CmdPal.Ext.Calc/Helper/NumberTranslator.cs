// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

/// <summary>
/// Tries to convert all numbers in a text from one culture format to another.
/// </summary>
public class NumberTranslator
{
    private const string ProtectedListSeparatorToken = "\uE000";

    /// <summary>
    /// ExprTK does not expose a public API that lets us enumerate the built-in functions accepted by
    /// this calculator together with their arity. Keep this table in sync with the functions allowed
    /// by <see cref="CalculateHelper.InputValid"/> and extend the translator/query tests when adding
    /// new functions.
    /// </summary>
    private static readonly Dictionary<string, int> SupportedFunctionArgumentCounts = new(StringComparer.OrdinalIgnoreCase)
    {
        { "ceil", 1 },
        { "floor", 1 },
        { "exp", 1 },
        { "max", 2 },
        { "min", 2 },
        { "abs", 1 },
        { "log", 1 },
        { "log2", 1 },
        { "log10", 1 },
        { "ln", 1 },
        { "sqrt", 1 },
        { "pow", 2 },
        { "factorial", 1 },
        { "sign", 1 },
        { "round", 1 },
        { "rand", 0 },
        { "randi", 1 },
        { "sin", 1 },
        { "cos", 1 },
        { "tan", 1 },
        { "arcsin", 1 },
        { "arccos", 1 },
        { "arctan", 1 },
        { "sinh", 1 },
        { "cosh", 1 },
        { "tanh", 1 },
        { "arsinh", 1 },
        { "arcosh", 1 },
        { "artanh", 1 },
        { "rad", 1 },
        { "deg", 1 },
        { "grad", 1 },
    };

    private readonly CultureInfo sourceCulture;
    private readonly CultureInfo targetCulture;
    private readonly Regex splitRegexForSource;
    private readonly Regex splitRegexForTarget;

    private NumberTranslator(CultureInfo sourceCulture, CultureInfo targetCulture)
    {
        this.sourceCulture = sourceCulture;
        this.targetCulture = targetCulture;

        splitRegexForSource = GetSplitRegex(this.sourceCulture);
        splitRegexForTarget = GetSplitRegex(this.targetCulture);
    }

    /// <summary>
    /// Create a new <see cref="NumberTranslator"/>.
    /// </summary>
    /// <param name="sourceCulture">source culture</param>
    /// <param name="targetCulture">target culture</param>
    /// <returns>Number translator for target culture</returns>
    public static NumberTranslator Create(CultureInfo sourceCulture, CultureInfo targetCulture)
    {
        ArgumentNullException.ThrowIfNull(sourceCulture);

        ArgumentNullException.ThrowIfNull(targetCulture);

        return new NumberTranslator(sourceCulture, targetCulture);
    }

    /// <summary>
    /// Translate from source to target culture.
    /// </summary>
    /// <param name="input">input string to translate</param>
    /// <returns>translated string</returns>
    public string Translate(string input)
    {
        return Translate(input, sourceCulture, targetCulture, splitRegexForSource);
    }

    /// <summary>
    /// Translate from target to source culture.
    /// </summary>
    /// <param name="input">input string to translate back to source culture</param>
    /// <returns>source culture string</returns>
    public string TranslateBack(string input)
    {
        return Translate(input, targetCulture, sourceCulture, splitRegexForTarget);
    }

    private static string ConvertBaseLiteral(string token, CultureInfo cultureTo)
    {
        var prefixes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "0x", 16 },
            { "0b", 2 },
            { "0o", 8 },
        };

        foreach (var (prefix, numberBase) in prefixes)
        {
            if (token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var num = Convert.ToInt64(token.Substring(prefix.Length), numberBase);
                    return num.ToString(cultureTo);
                }
                catch
                {
                    return null; // fallback
                }
            }
        }

        return null;
    }

    private static string Translate(string input, CultureInfo cultureFrom, CultureInfo cultureTo, Regex splitRegex)
    {
        var protectFunctionArgumentSeparators = cultureFrom.NumberFormat.NumberGroupSeparator == cultureFrom.TextInfo.ListSeparator;

        // In cultures such as en-US, ',' can mean either digit grouping or a function argument
        // separator. Preserve separators only inside the supported multi-argument functions before
        // the regex-based number pass so expressions like max(123,456) are not collapsed to 123456,
        // while single-argument calls such as ceil(123,456.23) still keep their grouped number.
        var workingInput = protectFunctionArgumentSeparators
            ? ProtectFunctionArgumentSeparators(input, cultureFrom.TextInfo.ListSeparator, ProtectedListSeparatorToken)
            : input;

        var outputBuilder = new StringBuilder();

        // Match numbers in hexadecimal (0x..), binary (0b..), or octal (0o..) format,
        // and convert them to decimal form for compatibility with ExprTk (which only supports decimal input).
        var baseNumberRegex = new Regex(@"(0[xX][\da-fA-F]+|0[bB][0-9]+|0[oO][0-9]+)");

        var tokens = baseNumberRegex.Split(workingInput);

        foreach (var token in tokens)
        {
            // Currently, we only convert base literals (hexadecimal, binary, octal) to decimal.
            var converted = ConvertBaseLiteral(token, cultureTo);

            if (converted is not null)
            {
                outputBuilder.Append(converted);
                continue;
            }

            foreach (var inner in splitRegex.Split(token))
            {
                var leadingZeroCount = 0;

                // Count leading zero characters.
                foreach (var c in inner)
                {
                    if (c != '0')
                    {
                        break;
                    }

                    leadingZeroCount++;
                }

                // number is all zero characters. no need to add zero characters at the end.
                if (inner.Length == leadingZeroCount)
                {
                    leadingZeroCount = 0;
                }

                decimal number;

                outputBuilder.Append(
                    decimal.TryParse(inner, NumberStyles.Number, cultureFrom, out number)
                    ? ((inner.Contains(cultureFrom.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal) ? string.Empty : new string('0', leadingZeroCount)) + number.ToString(cultureTo))
                    : inner.Replace(cultureFrom.TextInfo.ListSeparator, cultureTo.TextInfo.ListSeparator));
            }
        }

        var translated = outputBuilder.ToString();

        // Restore protected argument separators after numeric translation has finished.
        return protectFunctionArgumentSeparators
            ? translated.Replace(ProtectedListSeparatorToken, cultureTo.TextInfo.ListSeparator, StringComparison.Ordinal)
            : translated;
    }

    private static string ProtectFunctionArgumentSeparators(string input, string listSeparator, string placeholder)
    {
        if (string.IsNullOrEmpty(listSeparator))
        {
            return input;
        }

        var outputBuilder = new StringBuilder();
        var parenthesisProtection = new Stack<bool>();

        for (var i = 0; i < input.Length; i++)
        {
            if (parenthesisProtection.Count > 0 && parenthesisProtection.Peek() && MatchesAt(input, listSeparator, i))
            {
                // Protect separators only for the current multi-argument function call. Nested
                // single-argument functions such as max(ceil(123,456.23), 2) must still be able to
                // treat ',' as a digit-group separator inside their own argument.
                outputBuilder.Append(placeholder);
                i += listSeparator.Length - 1;
                continue;
            }

            var current = input[i];
            outputBuilder.Append(current);

            if (current == '(')
            {
                parenthesisProtection.Push(ShouldProtectFunctionArgumentSeparators(input, i));
            }
            else if (current == ')' && parenthesisProtection.Count > 0)
            {
                parenthesisProtection.Pop();
            }
        }

        return outputBuilder.ToString();
    }

    private static bool ShouldProtectFunctionArgumentSeparators(string input, int openParenIndex)
    {
        var end = openParenIndex - 1;

        // Allow whitespace between a function name and its opening parenthesis.
        while (end >= 0 && char.IsWhiteSpace(input[end]))
        {
            end--;
        }

        if (end < 0 || !char.IsLetterOrDigit(input[end]))
        {
            return false;
        }

        var start = end;
        while (start >= 0 && char.IsLetterOrDigit(input[start]))
        {
            start--;
        }

        start++;

        // Treat identifier-like text such as max    ( as a function call, but avoid marking plain
        // grouping parentheses like (1 + 2) as function syntax. Only supported functions with more
        // than one argument need protection; single-argument functions must still allow grouping
        // separators inside their numeric inputs.
        if (start > end || !char.IsLetter(input[start]))
        {
            return false;
        }

        var functionName = input.Substring(start, end - start + 1);
        return SupportedFunctionArgumentCounts.TryGetValue(functionName, out var argumentCount) && argumentCount > 1;
    }

    private static bool MatchesAt(string input, string value, int index)
    {
        return index + value.Length <= input.Length &&
               string.Compare(input, index, value, 0, value.Length, StringComparison.Ordinal) == 0;
    }

    private static Regex GetSplitRegex(CultureInfo culture)
    {
        var listSeparator = culture.TextInfo.ListSeparator;
        var groupSeparator = culture.NumberFormat.NumberGroupSeparator;
        var hasAmbiguousNumericSeparators = groupSeparator == listSeparator;

        // Some cultures use a non-breaking space for digit grouping, but users may type a
        // normal space instead. Expand the group separator to allow for either character.
        if (groupSeparator == "\u00a0")
        {
            groupSeparator = "\u0020\u00a0";
        }

        var decimalSeparator = Regex.Escape(culture.NumberFormat.NumberDecimalSeparator);

        // Strictly match only culture-valid numbers when the group separator is also the
        // function argument separator. In cultures like en-US, a looser pattern would
        // swallow max(1,2) as if "1,2" were a single number instead of two arguments.
        var strictNumberTokenPattern =
            $@"((?:\d{{1,3}}(?:[{Regex.Escape(groupSeparator)}]\d{{3}})+|\d+)(?:{decimalSeparator}\d+)?|{decimalSeparator}\d+)";

        // Preserve the legacy looser matching in cultures where numeric grouping cannot be
        // confused with function argument separators. This keeps existing behavior for cases
        // like de-DE, where '.' is a group separator but ';' separates function arguments.
        var looseNumberTokenPattern = $"([0-9{decimalSeparator}{Regex.Escape(groupSeparator)}]+)";

        return new Regex(hasAmbiguousNumericSeparators ? strictNumberTokenPattern : looseNumberTokenPattern);
    }
}
