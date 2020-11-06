// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Text.RegularExpressions;
using Wox.Plugin;

namespace Microsoft.Plugin.Program
{
    public class InferredProgramArgumentParser : IProgramArgumentParser
    {
        private static readonly Regex ArgumentPrefixRegex = new Regex("^(-|--|/)[a-zA-Z]+", RegexOptions.Compiled);

        public bool Enabled { get; } = true;

        public bool TryParse(Query query, out string program, out string programArguments)
        {
            if (!string.IsNullOrEmpty(query?.Search))
            {
                // First Argument is always (part of) the program, 2nd term is possibly  a Program Argument
                if (query.Terms.Count > 1)
                {
                    for (var i = 1; i < query.Terms.Count; i++)
                    {
                        if (!ArgumentPrefixRegex.IsMatch(query.Terms[i]))
                        {
                            continue;
                        }

                        program = string.Join(Query.TermSeparator, query.Terms.Take(i));
                        programArguments = string.Join(Query.TermSeparator, query.Terms.Skip(i));
                        return true;
                    }
                }
            }

            program = null;
            programArguments = null;
            return false;
        }
    }
}
