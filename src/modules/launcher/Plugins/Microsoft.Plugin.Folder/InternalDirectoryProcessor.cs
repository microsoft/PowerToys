// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Plugin.Folder.Sources;
using Microsoft.Plugin.Folder.Sources.Result;

namespace Microsoft.Plugin.Folder
{
    public class InternalDirectoryProcessor : IFolderProcessor
    {
        private readonly IFolderHelper _folderHelper;
        private readonly IQueryInternalDirectory _internalDirectory;

        public InternalDirectoryProcessor(IFolderHelper folderHelper, IQueryInternalDirectory internalDirectory)
        {
            _folderHelper = folderHelper;
            _internalDirectory = internalDirectory;
        }

        public IEnumerable<IItemResult> Results(string actionKeyword, string search)
        {
            if (!_folderHelper.IsDriveOrSharedFolder(search))
            {
                return Enumerable.Empty<IItemResult>();
            }

            return _internalDirectory.Query(search);
        }
    }
}
