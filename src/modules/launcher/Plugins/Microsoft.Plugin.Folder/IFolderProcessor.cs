// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.Plugin.Folder.Sources.Result;

namespace Microsoft.Plugin.Folder
{
    internal interface IFolderProcessor
    {
        IEnumerable<IItemResult> Results(string actionKeyword, string search);
    }
}
