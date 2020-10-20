// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.RegularExpressions;

namespace Microsoft.Plugin.Calculator
{
    public static class CalculateHelper
    {
        private static readonly Regex RegValidExpressChar = new Regex(
            @"^(" +
            @"ceil\s*\(|floor\s*\(|exp\s*\(|pi|e|max\s*\(|min\s*\(|det|abs\s*\(|log\s*\(|ln\s*\(|sqrt\s*\(|pow\s*\(|" +
            @"factorial\s*\(|sign\s*\(|round\s*\(|rand\s*\(|exp\s*\(|lt\s*\(|gt\s*\(|eq\s*\(|rand\s*\(|" +
            @"sin\s*\(|cos\s*\(|tan\s*\(|arcsin\s*\(|arccos\s*\(|arctan\s*\(|" +
            @"==|~=|&&|\|\||" +
            @"[ei]|[0-9]|[\+\-\*\/\^\., ""]|[\(\)\|\!\[\]]" +
            @")+$", RegexOptions.Compiled);

        public static bool InputValid(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentNullException(paramName: nameof(input));
            }

            if (input.Length <= 2)
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

            return true;
        }
    }
}
