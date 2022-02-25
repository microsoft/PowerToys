// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
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

            var reverseSearch = string.IsNullOrEmpty(query.Search)
                ? string.Empty
                : string.Join(string.Empty, query.Search.Reverse());

            var left = query.Search
                .Replace('}', '{')
                .Replace(')', '(')
                .Replace(']', '[');

            var right = reverseSearch
                .Replace('{', '}')
                .Replace('(', ')')
                .Replace('[', ']');

            var resultList = new List<Result>
            {
                new Result
                {
                    IcoPath = iconPath,
                    Title = $"{left}{guid.ToString("N").ToUpper()}{right}",
                    SubTitle = $"{left}{guid.ToString("N").ToLower()}{right}",
                    ContextData = $"{left}{guid.ToString("N").ToUpper()}{right}",
                },
                new Result
                {
                    IcoPath = iconPath,
                    Title = $"{left}{guid.ToString("D").ToUpper()}{right}",
                    SubTitle = $"{left}{guid.ToString("D").ToLower()}{right}",
                    ContextData = $"{left}{guid.ToString("D").ToUpper()}{right}",
                },

                new Result
                {
                    IcoPath = iconPath,
                    Title = $"{left}urn:uuid:{guid.ToString("D").ToUpper()}{right}",
                    SubTitle = $"Uniform Resource Name (URN)",
                    ContextData = $"{left}urn:uuid:{guid.ToString("D").ToUpper()}{right}",
                },
            };

            return resultList;
        }
    }
}
