// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.RegularExpressions;

namespace Microsoft.PowerToys.Run.Plugin.Calculator
{
    public static class CalculateHelper
    {
        private static readonly Regex RegValidExpressChar = new Regex(
            @"^(" +
            @"%|" +
            @"ceil\s*\(|floor\s*\(|exp\s*\(|max\s*\(|min\s*\(|abs\s*\(|log\s*\(|ln\s*\(|sqrt\s*\(|pow\s*\(|" +
            @"factorial\s*\(|sign\s*\(|round\s*\(|rand\s*\(|" +
            @"sin\s*\(|cos\s*\(|tan\s*\(|arcsin\s*\(|arccos\s*\(|arctan\s*\(|" +
            @"sinh\s*\(|cosh\s*\(|tanh\s*\(|arsinh\s*\(|arcosh\s*\(|artanh\s*\(|" +
            @"pi|" +
            @"==|~=|&&|\|\||" +
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
    }
}
