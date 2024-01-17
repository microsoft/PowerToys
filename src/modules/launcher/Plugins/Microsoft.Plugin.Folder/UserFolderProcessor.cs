// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Plugin.Folder.Sources;
using Microsoft.Plugin.Folder.Sources.Result;

namespace Microsoft.Plugin.Folder
{
    internal class UserFolderProcessor : IFolderProcessor
    {
        private readonly FolderHelper _folderHelper;

        public UserFolderProcessor(FolderHelper folderHelper)
        {
            _folderHelper = folderHelper;
        }

        public IEnumerable<IItemResult> Results(string actionKeyword, string search)
        {
            return _folderHelper.GetUserFolderResults(search)
                .Select(item => CreateFolderResult(item.Nickname, item.Path, item.Path, search));
        }

        private static UserFolderResult CreateFolderResult(string title, string subtitle, string path, string search)
        {
            return new UserFolderResult
            {
                Search = search,
                Title = title,
                Subtitle = subtitle,
                Path = path,
            };
        }
    }
}
