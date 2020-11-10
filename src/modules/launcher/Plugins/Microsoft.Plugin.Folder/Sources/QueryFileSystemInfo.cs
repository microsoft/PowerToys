// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;

namespace Microsoft.Plugin.Folder.Sources
{
    public class QueryFileSystemInfo : IQueryFileSystemInfo
    {
        private readonly IDirectoryInfoFactory _directoryInfoFactory;

        public QueryFileSystemInfo(IDirectoryInfoFactory directoryInfoFactory)
        {
            _directoryInfoFactory = directoryInfoFactory;
        }

        public IEnumerable<DisplayFileInfo> MatchFileSystemInfo(string search, string incompleteName, bool isRecursive)
        {
            // search folder and add results
            var directoryInfo = _directoryInfoFactory.FromDirectoryName(search);
            var fileSystemInfos = directoryInfo.EnumerateFileSystemInfos(incompleteName, new EnumerationOptions()
            {
                MatchType = MatchType.Win32,
                RecurseSubdirectories = isRecursive,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.Hidden,
                MatchCasing = MatchCasing.PlatformDefault,
            });

            return fileSystemInfos
                .Select(CreateDisplayFileInfo);
        }

        private static DisplayFileInfo CreateDisplayFileInfo(IFileSystemInfo fileSystemInfo)
        {
            return new DisplayFileInfo()
            {
                Name = fileSystemInfo.Name,
                FullName = fileSystemInfo.FullName,
                Type = GetDisplayType(fileSystemInfo),
            };
        }

        private static DisplayType GetDisplayType(IFileSystemInfo fileSystemInfo)
        {
            if (fileSystemInfo is IDirectoryInfo)
            {
                return DisplayType.Directory;
            }
            else
            {
                return DisplayType.File;
            }
        }
    }
}
