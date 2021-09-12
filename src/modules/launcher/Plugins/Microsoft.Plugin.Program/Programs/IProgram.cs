// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Wox.Plugin;

namespace Microsoft.Plugin.Program.Programs
{
    public interface IProgram
    {
        List<ContextMenuResult> ContextMenus(string queryArguments, IPublicAPI api);

        Result Result(string query, string queryArguments, IPublicAPI api);

        string UniqueIdentifier { get; set; }

        string Name { get; }

        string Description { get; set; }

        string Location { get; }

        bool Enabled { get; set; }
    }
}
