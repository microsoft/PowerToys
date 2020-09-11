// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Plugin.Folder.Sources.Result;

namespace Microsoft.Plugin.Folder.Sources
{
    public interface IQueryInternalDirectory
    {
        IEnumerable<IItemResult> Query(string actionKeyword, string search);

        IEnumerable<IItemResult> Query(string querySearch, SearchOption searchOption);
    }
}
