// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using Wox.Infrastructure.FileSystemHelper;

namespace Microsoft.Plugin.Folder.Sources
{
    public interface IQueryFileSystemInfo : IDirectoryWrapper
    {
        IEnumerable<DisplayFileInfo> MatchFileSystemInfo(string search, string incompleteName, bool isRecursive);
    }
}
