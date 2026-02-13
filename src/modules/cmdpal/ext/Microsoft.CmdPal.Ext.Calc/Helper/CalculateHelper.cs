// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public static partial class CalculateHelper
{
    private static readonly Regex RegValidExpressChar = new Regex(
        @"^(" +
        @"%|" +
        @"ceil\s*\(|floor\s*\(|exp\s*\(|max\s*\(|min\s*\(|abs\s*\(|log(?:2|10)?\s*\(|ln\s*\(|sqrt\s*\(|pow\s*\(|" +
        @"factorial\s*\(|sign\s*\(|round\s*\(|rand\s*\(\)|randi\s*\([^\)]|" +
        @"sin\s*\(|cos\s*\(|tan\s*\(|arcsin\s*\(|arccos\s*\(|arctan\s*\(|" +
        @"sinh\s*\(|cosh\s*\(|tanh\s*\(|arsinh\s*\(|arcosh\s*\(|artanh\s*\(|" +
        @"rad\s*\(|deg\s*\(|grad\s*\(|" + /* trigonometry unit conversion macros */
        @"pi|" +
        @"==|~=|&&|\|\||" +
        @"((\d+(?:\.\d*)?|\.\d+)[eE](-?\d+))|" + /* expression from CheckScientificNotation between parenthesis */
        @"e|[0-9]|0[xX][0-9a-fA-F]+|0[bB][01]+|0[oO][0-7]+|[\+\-\*\/\^\., ""]|[\(\)\|\!\[\]]" +
        @")+$",
        RegexOptions.Compiled);

    private const string DegToRad = "(pi / 180) * ";
    private const string DegToGrad = "(10 / 9) * ";
    private const string GradToRad = "(pi / 200) * ";
    private const string GradToDeg = "(9 / 10) * ";
    private const string RadToDeg = "(180 / pi) * ";
    private const string RadToGrad = "(200 / pi) * ";

    // replacements from the user input to displayed query
    private static readonly Dictionary<string, string> QueryReplacements = new()
    {
        { "％", "%" }, { "﹪", "%" },
        { "−", "-" }, { "–", "-" }, { "—", "-" },
        { "！", "!" },
        { "*", "×" }, { "∗", "×" }, { "·", "×" }, { "⊗", "×" }, { "⋅", "×" }, { "✕", "×" }, { "✖", "×" }, { "\u2062", "×" },
        { "/", "÷" }, { "∕", "÷" }, { "➗", "÷" }, { ":", "÷" },
    };

    // replacements from a query to engine input
    private static readonly Dictionary<string, string> EngineReplacements = new()
    {
        { "×", "*" },
        { "÷", "/" },
    };

    private static readonly Dictionary<string, string> SuperscriptReplacements = new()
    {
        { "²", "^2" }, { "³", "^3" },
    };

    private static readonly HashSet<char> StandardOperators = [

        // binary operators; doesn't make sense for them to be at the end of a query
        '+', '-', '*', '/', '%', '^', '=', '&', '|', '\\',

        // parentheses
        '(', '[',
    ];

    private static readonly HashSet<char> SuffixOperators = [

        // unary operators; can appear at the end of a query
        ')', ']', '!',
    ];

    private static readonly Regex ReplaceScientificNotationRegex = CreateReplaceScientificNotationRegex();

    public static char[] GetQueryOperators()
    {
        var ops = new HashSet<char>(StandardOperators);
        ops.ExceptWith(SuffixOperators);
        return [.. ops];
    }

    /// <summary>
    /// Normalizes the query for display
    /// This replaces standard operators with more visually appealing ones (e.g., '*' -> '×') if enabled.
    /// Always applies safe normalizations (standardizing variants like minus, percent, etc.).
    /// </summary>
    /// <param name="input">The query string to normalize.</param>
    public static string NormalizeCharsForDisplayQuery(string input)
    {
        // 1. Safe/Trivial replacements (Variant -> Standard)
        // These are always applied to ensure consistent behavior for non-math symbols (spaces) and
        // operator variants like minus, percent, and exclamation mark.
        foreach (var (key, value) in QueryReplacements)
        {
            input = input.Replace(key, value);
        }

        return input;
    }

    /// <summary>
    /// Normalizes the query for the calculation engine.
    /// This replaces all supported operator variants (visual or standard) with the specific
    /// ASCII operators required by the engine (e.g., '×' -> '*').
    /// It duplicates and expands upon replacements in NormalizeQuery to ensure the engine
    /// receives valid input regardless of whether NormalizeQuery was executed.
    /// </summary>
    public static string NormalizeCharsToEngine(string input)
    {
        foreach (var (key, value) in EngineReplacements)
        {
            input = input.Replace(key, value);
        }

        // Replace superscript characters with their engine equivalents (e.g., '²' -> '^2')
        foreach (var (key, value) in SuperscriptReplacements)
        {
            input = input.Replace(key, value);
        }

        return input;
    }

    public static bool InputValid(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (!RegValidExpressChar.IsMatch(input))
        {
            return false;
        }

        if (!BracketHelper.IsBracketComplete(input))
        {
            return false;
        }

        // If the input ends with a binary operator then it is not a valid input to mages and the Interpret function would throw an exception. Because we expect here that the user has not finished typing we block those inputs.
        var trimmedInput = input.TrimEnd();
        if (EndsWithBinaryOperator(trimmedInput))
        {
            return false;
        }

        return true;
    }

    private static bool EndsWithBinaryOperator(string input)
    {
        var operators = GetQueryOperators();
        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        var lastChar = input[^1];
        return Array.Exists(operators, op => op == lastChar);
    }

    public static string FixHumanMultiplicationExpressions(string input)
    {
        var output = CheckScientificNotation(input);
        output = CheckNumberOrConstantThenParenthesisExpr(output);
        output = CheckNumberOrConstantThenFunc(output);
        output = CheckParenthesisExprThenFunc(output);
        output = CheckParenthesisExprThenParenthesisExpr(output);
        output = CheckNumberThenConstant(output);
        output = CheckConstantThenConstant(output);
        return output;
    }

    private static string CheckScientificNotation(string input)
    {
        return ReplaceScientificNotationRegex.Replace(input, "($1 * 10^($2))");
    }

    /*
     * num (exp)
     * const (exp)
     */
    private static string CheckNumberOrConstantThenParenthesisExpr(string input)
    {
        var output = input;
        do
        {
            input = output;
            output = Regex.Replace(input, @"(\d+|pi|e)\s*(\()", m =>
            {
                if (m.Index > 0 && char.IsLetter(input[m.Index - 1]))
                {
                    return m.Value;
                }

                return $"{m.Groups[1].Value} * {m.Groups[2].Value}";
            });
        }
        while (output != input);

        return output;
    }

    /*
     * num func
     * const func
     */
    private static string CheckNumberOrConstantThenFunc(string input)
    {
        var output = input;
        do
        {
            input = output;
            output = Regex.Replace(input, @"(\d+|pi|e)\s*([a-zA-Z]+[0-9]*\s*\()", m =>
            {
                if (input[m.Index] == 'e' && input[m.Index + 1] == 'x' && input[m.Index + 2] == 'p')
                {
                    return m.Value;
                }

                if (m.Index > 0 && char.IsLetter(input[m.Index - 1]))
                {
                    return m.Value;
                }

                return $"{m.Groups[1].Value} * {m.Groups[2].Value}";
            });
        }
        while (output != input);

        return output;
    }

    /*
     * (exp) func
     * func func
     */
    private static string CheckParenthesisExprThenFunc(string input)
    {
        var p = @"(\))\s*([a-zA-Z]+[0-9]*\s*\()";
        var r = "$1 * $2";
        return Regex.Replace(input, p, r);
    }

    /*
     * (exp) (exp)
     * func (exp)
     */
    private static string CheckParenthesisExprThenParenthesisExpr(string input)
    {
        var p = @"(\))\s*(\()";
        var r = "$1 * $2";
        return Regex.Replace(input, p, r);
    }

    /*
     * num const
     */
    private static string CheckNumberThenConstant(string input)
    {
        var output = input;
        do
        {
            input = output;
            output = Regex.Replace(input, @"(\d+)\s*(pi|e)", m =>
            {
                if (m.Index > 0 && char.IsLetter(input[m.Index - 1]))
                {
                    return m.Value;
                }

                return $"{m.Groups[1].Value} * {m.Groups[2].Value}";
            });
        }
        while (output != input);

        return output;
    }

    /*
     * const const
     */
    private static string CheckConstantThenConstant(string input)
    {
        var output = input;
        do
        {
            input = output;
            output = Regex.Replace(input, @"(pi|e)\s*(pi|e)", m =>
            {
                if (m.Index > 0 && char.IsLetter(input[m.Index - 1]))
                {
                    return m.Value;
                }

                return $"{m.Groups[1].Value} * {m.Groups[2].Value}";
            });
        }
        while (output != input);

        return output;
    }

    // Gets the index of the closing bracket of a function
    private static int FindClosingBracketIndex(string input, int start)
    {
        var bracketCount = 0;    // Set count to zero
        for (var i = start; i < input.Length; i++)
        {
            if (input[i] == '(')
            {
                bracketCount++;
            }
            else if (input[i] == ')')
            {
                bracketCount--;
                if (bracketCount == 0)
                {
                    return i;
                }
            }
        }

        return -1;  // Unmatched brackets
    }

    private static string ModifyTrigFunction(string input, string function, string modification)
    {
        // Get the RegEx pattern to match, depending on whether the function is inverse or normal
        var pattern = function.StartsWith("arc", StringComparison.Ordinal) ? string.Empty : @"(?<!c)";
        pattern += $@"{function}\s*\(";

        var index = 0;    // Index for match to ensure that the same match is not found twice

        Regex regex = new Regex(pattern);
        Match match;

        while ((match = regex.Match(input, index)).Success)
        {
            index = match.Index + match.Groups[0].Length + modification.Length;    // Get the next index to look from for further matches

            var endIndex = FindClosingBracketIndex(input, match.Index + match.Groups[0].Length - 1);    // Find the index of the closing bracket of the function

            // If no valid bracket index was found, try the next match
            if (endIndex == -1)
            {
                continue;
            }

            var argument = input.Substring(match.Index + match.Groups[0].Length, endIndex - (match.Index + match.Groups[0].Length));  // Extract the argument between the brackets
            var replaced = function.StartsWith("arc", StringComparison.Ordinal) ? $"{modification}({match.Groups[0].Value}{argument}))" : $"{match.Groups[0].Value}{modification}({argument}))";  // The string to substitute in, handles differing formats of inverse functions

            input = input.Remove(match.Index, endIndex - match.Index + 1);    // Remove the match from the input
            input = input.Insert(match.Index, replaced);    // Substitute with the new string
        }

        return input;
    }

    public static string UpdateTrigFunctions(string input, CalculateEngine.TrigMode mode)
    {
        var modifiedInput = input;
        if (mode == CalculateEngine.TrigMode.Degrees)
        {
            modifiedInput = ModifyTrigFunction(modifiedInput, "sin", DegToRad);
            modifiedInput = ModifyTrigFunction(modifiedInput, "cos", DegToRad);
            modifiedInput = ModifyTrigFunction(modifiedInput, "tan", DegToRad);
            modifiedInput = ModifyTrigFunction(modifiedInput, "arcsin", RadToDeg);
            modifiedInput = ModifyTrigFunction(modifiedInput, "arccos", RadToDeg);
            modifiedInput = ModifyTrigFunction(modifiedInput, "arctan", RadToDeg);
        }
        else if (mode == CalculateEngine.TrigMode.Gradians)
        {
            modifiedInput = ModifyTrigFunction(modifiedInput, "sin", GradToRad);
            modifiedInput = ModifyTrigFunction(modifiedInput, "cos", GradToRad);
            modifiedInput = ModifyTrigFunction(modifiedInput, "tan", GradToRad);
            modifiedInput = ModifyTrigFunction(modifiedInput, "arcsin", RadToGrad);
            modifiedInput = ModifyTrigFunction(modifiedInput, "arccos", RadToGrad);
            modifiedInput = ModifyTrigFunction(modifiedInput, "arctan", RadToGrad);
        }

        return modifiedInput;
    }

    public static string UpdateFactorialFunctions(string input)
    {
        // Handle n! -> factorial(n)
        int startSearch = 0;
        while (true)
        {
            var index = input.IndexOf('!', startSearch);
            if (index == -1)
            {
                break;
            }

            // Ignore !=
            if (index + 1 < input.Length && input[index + 1] == '=')
            {
                startSearch = index + 2;
                continue;
            }

            if (index == 0)
            {
                startSearch = index + 1;
                continue;
            }

            // Scan backwards
            var endArg = index - 1;
            while (endArg >= 0 && char.IsWhiteSpace(input[endArg]))
            {
                endArg--;
            }

            if (endArg < 0)
            {
                startSearch = index + 1;
                continue;
            }

            var startArg = endArg;
            if (input[endArg] == ')')
            {
                // Find matching '('
                startArg = FindOpeningBracketIndexInFrontOfIndex(input, endArg);
                if (startArg == -1)
                {
                    startSearch = index + 1;
                    continue;
                }
            }
            else
            {
                // Scan back for number or word
                while (startArg >= 0 && (char.IsLetterOrDigit(input[startArg]) || input[startArg] == '.'))
                {
                    startArg--;
                }

                startArg++; // Move back to first valid char
            }

            if (startArg > endArg)
            {
                // No argument found
                startSearch = index + 1;
                continue;
            }

            // Extract argument
            var arg = input.Substring(startArg, endArg - startArg + 1);

            // Replace <arg><whitespace>! with factorial(<arg>)
            input = input.Remove(startArg, index - startArg + 1);
            input = input.Insert(startArg, $"factorial({arg})");

            startSearch = 0; // Reset search because string changed
        }

        return input;
    }

    private static string ModifyMathFunction(string input, string function, string modification)
    {
        // Create the pattern to match the function, opening bracket, and any spaces in between
        var pattern = $@"{function}\s*\(";
        return Regex.Replace(input, pattern, modification + "(");
    }

    public static string ExpandTrigConversions(string input, CalculateEngine.TrigMode mode)
    {
        var modifiedInput = input;

        // Expand "rad", "deg" and "grad" to their respective conversions for the current trig unit
        if (mode == CalculateEngine.TrigMode.Radians)
        {
            modifiedInput = ModifyMathFunction(modifiedInput, "deg", DegToRad);
            modifiedInput = ModifyMathFunction(modifiedInput, "grad", GradToRad);
            modifiedInput = ModifyMathFunction(modifiedInput, "rad", string.Empty);
        }
        else if (mode == CalculateEngine.TrigMode.Degrees)
        {
            modifiedInput = ModifyMathFunction(modifiedInput, "deg", string.Empty);
            modifiedInput = ModifyMathFunction(modifiedInput, "grad", GradToDeg);
            modifiedInput = ModifyMathFunction(modifiedInput, "rad", RadToDeg);
        }
        else if (mode == CalculateEngine.TrigMode.Gradians)
        {
            modifiedInput = ModifyMathFunction(modifiedInput, "deg", DegToGrad);
            modifiedInput = ModifyMathFunction(modifiedInput, "grad", string.Empty);
            modifiedInput = ModifyMathFunction(modifiedInput, "rad", RadToGrad);
        }

        return modifiedInput;
    }

    private static int FindOpeningBracketIndexInFrontOfIndex(string input, int end)
    {
        var bracketCount = 0;
        for (var i = end; i >= 0; i--)
        {
            switch (input[i])
            {
                case ')':
                    bracketCount++;
                    break;
                case '(':
                {
                    bracketCount--;
                    if (bracketCount == 0)
                    {
                        return i;
                    }

                    break;
                }
            }
        }

        return -1;
    }

    /*
     * NOTE: By the time that the expression gets to us, it's already in English format.
     *
     * Regex explanation:
     * (-?(\d+({0}\d*)?)|-?({0}\d+)): Used to capture one of two types:
     * -?(\d+({0}\d*)?): Captures a decimal number starting with a number (e.g. "-1.23")
     * -?({0}\d+): Captures a decimal number without leading number (e.g. ".23")
     * e: Captures 'e' or 'E'
     * (?\d+): Captures an integer number (e.g. "-1" or "23")
     */
    [GeneratedRegex(@"(\d+(?:\.\d*)?|\.\d+)e(-?\d+)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex CreateReplaceScientificNotationRegex();
}
