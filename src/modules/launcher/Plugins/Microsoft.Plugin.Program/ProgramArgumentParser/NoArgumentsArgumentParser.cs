// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Wox.Plugin;

namespace Microsoft.Plugin.Program.ProgramArgumentParser
{
    public class NoArgumentsArgumentParser : IProgramArgumentParser
    {
        public bool Enabled { get; } = true;

        public bool TryParse(Query query, out string program, out string programArguments)
        {
            program = query?.Search;
            programArguments = null;
            return true;
        }
    }
}
