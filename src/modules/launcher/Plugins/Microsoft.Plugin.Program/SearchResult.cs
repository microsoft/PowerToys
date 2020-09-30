// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Plugin.Program.Programs;

namespace Microsoft.Plugin.Program
{
    internal class SearchResult
    {
        public IProgram Program { get; set; }

        public string ProgramAguments { get; set; }
    }
}
