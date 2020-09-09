// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.RegularExpressions;

namespace Microsoft.Plugin.Calculator
{
    public static class CalculateHelper
    {
        private static readonly Regex RegBrackets = new Regex(@"[\(\)\[\]]", RegexOptions.Compiled);
        private static readonly Regex RegValidExpressChar = new Regex(
            @"^(" +
            @"ceil|floor|exp|pi|e|max|min|det|abs|log|ln|sqrt|" +
            @"sin|cos|tan|arcsin|arccos|arctan|" +
            @"eigval|eigvec|eig|sum|polar|plot|round|sort|real|zeta|" +
            @"bin2dec|hex2dec|oct2dec|" +
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

            if (!IsBracketComplete(input))
            {
                return false;
            }

            return true;
        }

        public static bool IsBracketComplete(string query)
        {
            var matchs = RegBrackets.Matches(query);
            var leftBracketCount = 0;
            foreach (Match match in matchs)
            {
                if (match.Value == "(" || match.Value == "[")
                {
                    leftBracketCount++;
                }
                else
                {
                    leftBracketCount--;
                }
            }

            return leftBracketCount == 0;
        }
    }
}
