// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using static Microsoft.PowerToys.Run.Plugin.Calculator.CalculateEngine;

namespace Microsoft.PowerToys.Run.Plugin.Calculator
{
    public static class CalculateHelper
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
            @"((-?(\d+(\.\d*)?)|-?(\.\d+))[Ee](-?\d+))|" + /* expression from CheckScientificNotation between parenthesis */
            @"e|[0-9]|0[xX][0-9a-fA-F]+|0[bB][01]+|0[oO][0-7]+|[\+\-\*\/\^\., ""]|[\(\)\|\!\[\]]" +
            @")+$",
            RegexOptions.Compiled);

        private const string DegToRad = "(pi / 180) * ";
        private const string DegToGrad = "(10 / 9) * ";
        private const string GradToRad = "(pi / 200) * ";
        private const string GradToDeg = "(9 / 10) * ";
        private const string RadToDeg = "(180 / pi) * ";
        private const string RadToGrad = "(200 / pi) * ";

        public static bool InputValid(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentNullException(paramName: nameof(input));
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
            string trimmedInput = input.TrimEnd();
            if (trimmedInput.EndsWith('+') || trimmedInput.EndsWith('-') || trimmedInput.EndsWith('*') || trimmedInput.EndsWith('|') || trimmedInput.EndsWith('\\') || trimmedInput.EndsWith('^') || trimmedInput.EndsWith('=') || trimmedInput.EndsWith('&') || trimmedInput.EndsWith('/') || trimmedInput.EndsWith('%'))
            {
                return false;
            }

            return true;
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
            /**
             * NOTE: By the time the expression gets to us, it's already in English format.
             *
             * Regex explanation:
             * (-?(\d+({0}\d*)?)|-?({0}\d+)): Used to capture one of two types:
             * -?(\d+({0}\d*)?): Captures a decimal number starting with a number (e.g. "-1.23")
             * -?({0}\d+): Captures a decimal number without leading number (e.g. ".23")
             * e: Captures 'e' or 'E'
             * (-?\d+): Captures an integer number (e.g. "-1" or "23")
             */
            var p = @"(-?(\d+(\.\d*)?)|-?(\.\d+))e(-?\d+)";
            return Regex.Replace(input, p, "($1 * 10^($5))", RegexOptions.IgnoreCase);
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
            int bracketCount = 0;    // Set count to zero
            for (int i = start; i < input.Length; i++)
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
            string pattern = function.StartsWith("arc", StringComparison.Ordinal) ? string.Empty : @"(?<!c)";
            pattern += $@"{function}\s*\(";

            int index = 0;    // Index for match to ensure that the same match is not found twice

            Regex regex = new Regex(pattern);
            Match match;

            while ((match = regex.Match(input, index)).Success)
            {
                index = match.Index + match.Groups[0].Length + modification.Length;    // Get the next index to look from for further matches

                int endIndex = FindClosingBracketIndex(input, match.Index + match.Groups[0].Length - 1);    // Find the index of the closing bracket of the function

                // If no valid bracket index was found, try the next match
                if (endIndex == -1)
                {
                    continue;
                }

                string argument = input.Substring(match.Index + match.Groups[0].Length, endIndex - (match.Index + match.Groups[0].Length));  // Extract the argument between the brackets
                string replaced = function.StartsWith("arc", StringComparison.Ordinal) ? $"{modification}({match.Groups[0].Value}{argument}))" : $"{match.Groups[0].Value}{modification}({argument}))";  // The string to substitute in, handles differing formats of inverse functions

                input = input.Remove(match.Index, endIndex - match.Index + 1);    // Remove the match from the input
                input = input.Insert(match.Index, replaced);    // Substitute with the new string
            }

            return input;
        }

        public static string UpdateTrigFunctions(string input, TrigMode mode)
        {
            string modifiedInput = input;
            if (mode == TrigMode.Degrees)
            {
                modifiedInput = ModifyTrigFunction(modifiedInput, "sin", DegToRad);
                modifiedInput = ModifyTrigFunction(modifiedInput, "cos", DegToRad);
                modifiedInput = ModifyTrigFunction(modifiedInput, "tan", DegToRad);
                modifiedInput = ModifyTrigFunction(modifiedInput, "arcsin", RadToDeg);
                modifiedInput = ModifyTrigFunction(modifiedInput, "arccos", RadToDeg);
                modifiedInput = ModifyTrigFunction(modifiedInput, "arctan", RadToDeg);
            }
            else if (mode == TrigMode.Gradians)
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

        private static string ModifyMathFunction(string input, string function, string modification)
        {
            // Create the pattern to match the function, opening bracket, and any spaces in between
            string pattern = $@"{function}\s*\(";
            return Regex.Replace(input, pattern, modification + "(");
        }

        public static string ExpandTrigConversions(string input, TrigMode mode)
        {
            string modifiedInput = input;

            // Expand "rad", "deg" and "grad" to their respective conversions for the current trig unit
            if (mode == TrigMode.Radians)
            {
                modifiedInput = ModifyMathFunction(modifiedInput, "deg", DegToRad);
                modifiedInput = ModifyMathFunction(modifiedInput, "grad", GradToRad);
                modifiedInput = ModifyMathFunction(modifiedInput, "rad", string.Empty);
            }
            else if (mode == TrigMode.Degrees)
            {
                modifiedInput = ModifyMathFunction(modifiedInput, "deg", string.Empty);
                modifiedInput = ModifyMathFunction(modifiedInput, "grad", GradToDeg);
                modifiedInput = ModifyMathFunction(modifiedInput, "rad", RadToDeg);
            }
            else if (mode == TrigMode.Gradians)
            {
                modifiedInput = ModifyMathFunction(modifiedInput, "deg", DegToGrad);
                modifiedInput = ModifyMathFunction(modifiedInput, "grad", string.Empty);
                modifiedInput = ModifyMathFunction(modifiedInput, "rad", RadToGrad);
            }

            return modifiedInput;
        }
    }
}
