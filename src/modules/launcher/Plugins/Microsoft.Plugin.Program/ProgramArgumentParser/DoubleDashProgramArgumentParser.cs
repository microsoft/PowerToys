// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Wox.Plugin;

namespace Microsoft.Plugin.Program
{
    public class DoubleDashProgramArgumentParser : IProgramArgumentParser
    {
        private const string DoubleDash = "--";

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
                        // Using Ordinal since this is internal and used with a symbol
                        if (!string.Equals(query.Terms[i], DoubleDash, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        program = string.Join(Query.TermSeparator, query.Terms.Take(i));
                        programArguments = string.Join(Query.TermSeparator, query.Terms.Skip(i + 1));
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
