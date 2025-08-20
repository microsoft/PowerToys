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
        var outputBuilder = new StringBuilder();

        // Match numbers in hexadecimal (0x..), binary (0b..), or octal (0o..) format,
        // and convert them to decimal form for compatibility with ExprTk (which only supports decimal input).
        var baseNumberRegex = new Regex(@"(0[xX][\da-fA-F]+|0[bB][0-9]+|0[oO][0-9]+)");

        var tokens = baseNumberRegex.Split(input);

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
                    ? (new string('0', leadingZeroCount) + number.ToString(cultureTo))
                    : inner.Replace(cultureFrom.TextInfo.ListSeparator, cultureTo.TextInfo.ListSeparator));
            }
        }

        return outputBuilder.ToString();
    }

    private static Regex GetSplitRegex(CultureInfo culture)
    {
        var groupSeparator = culture.NumberFormat.NumberGroupSeparator;

        // if the group separator is a no-break space, we also add a normal space to the regex
        if (groupSeparator == "\u00a0")
        {
            groupSeparator = "\u0020\u00a0";
        }

        var splitPattern = $"([0-9{Regex.Escape(culture.NumberFormat.NumberDecimalSeparator)}" +
            $"{Regex.Escape(groupSeparator)}]+)";
        return new Regex(splitPattern);
    }
}
