// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Microsoft.PowerToys.Run.Plugin.Calculator
{
    public static class CalculateHelper
    {
        private static readonly Regex RegValidExpressChar = new Regex(
            @"^(" +
            @"%|" +
            @"ceil\s*\(|floor\s*\(|exp\s*\(|max\s*\(|min\s*\(|abs\s*\(|log(?:2|10)?\s*\(|ln\s*\(|sqrt\s*\(|pow\s*\(|" +
            @"factorial\s*\(|sign\s*\(|round\s*\(|rand\s*\(|" +
            @"sin\s*\(|cos\s*\(|tan\s*\(|arcsin\s*\(|arccos\s*\(|arctan\s*\(|" +
            @"sinh\s*\(|cosh\s*\(|tanh\s*\(|arsinh\s*\(|arcosh\s*\(|artanh\s*\(|" +
            @"pi|" +
            @"==|~=|&&|\|\||" +
            @"((-?(\d+(\.\d*)?)|-?(\.\d+))[E](-?\d+))|" + /* expression from CheckScientificNotation between parenthesis */
            @"e|[0-9]|0x[0-9a-fA-F]+|0b[01]+|[\+\-\*\/\^\., ""]|[\(\)\|\!\[\]]" +
            @")+$",
            RegexOptions.Compiled);

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
             * E: Captures capital 'E'
             * (-?\d+): Captures an integer number (e.g. "-1" or "23")
             */
            var p = @"(-?(\d+(\.\d*)?)|-?(\.\d+))E(-?\d+)";
            return Regex.Replace(input, p, "($1 * 10^($5))");
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
    }
}
