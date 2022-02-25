// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerToys.Run.Plugin.Guid.Properties;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.Guid.Helper
{
    /// <summary>
    /// Helper class to easier work with results
    /// </summary>
    internal static class ResultHelper
    {
        /// <summary>
        /// Return a list with <see cref="Result"/>s, based on the given list.
        /// </summary>
        /// <param name="iconPath">The path to the icon of each entry.</param>
        /// <returns>A list with <see cref="Result"/>.</returns>
        internal static IEnumerable<Result> GetResults(Query query, in string iconPath)
        {
            var guid = System.Guid.NewGuid();

            var reverseSearch = string.IsNullOrEmpty(query?.Search)
                ? string.Empty
                : string.Join(string.Empty, query.Search.Reverse());

            var prefix = query?.Search
                .Replace('}', '{')
                .Replace(')', '(')
                .Replace(']', '[');

            var suffix = reverseSearch
                .Replace('{', '}')
                .Replace('(', ')')
                .Replace('[', ']');

            var guidHexValues = guid.ToString("X").Replace("{", string.Empty).Replace("}", string.Empty).Replace(",", ", ");

            var formatDigitsOnly = $"{prefix}{guid.ToString("N").ToLower()}{suffix}";
            var formatDigitsInGroups = $"{prefix}{guid.ToString("D").ToLower()}{suffix}";
            var formatHexValues = $"{prefix}{guidHexValues.ToLower()}{suffix}";
            var formatDigitsInUrn = $"{prefix}urn:uuid:{guid.ToString("D").ToLower()}{suffix}";

            var resultList = new List<Result>
            {
                new Result
                {
                    IcoPath = iconPath,
                    Title = formatDigitsOnly,
                    SubTitle = $"{Resources.Format}:{Resources.FormatDigitsOnly}",
                    ContextData = formatDigitsOnly,
                },
                new Result
                {
                    IcoPath = iconPath,
                    Title = formatDigitsInGroups,
                    SubTitle = $"{Resources.Format}:{Resources.FormatDigitsInGroups}",
                    ContextData = formatDigitsInGroups,
                },

                new Result
                {
                    IcoPath = iconPath,
                    Title = formatHexValues,
                    SubTitle = $"{Resources.Format}:{Resources.FormatHexValues}",
                    ContextData = formatHexValues,
                },

                new Result
                {
                    IcoPath = iconPath,
                    Title = formatDigitsInUrn,
                    SubTitle = $"{Resources.Format}:{Resources.FormatDigitsInUrn}",
                    ContextData = formatDigitsInUrn,
                },
            };

            return resultList;
        }
    }
}
