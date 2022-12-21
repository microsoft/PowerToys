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
        private readonly MatchType _matchType;
        private readonly FileAttributes _attributesToSkip;

        public QueryFileSystemInfo(IDirectoryInfoFactory directoryInfoFactory, MatchType matchType = MatchType.Win32, FileAttributes attributesToSkip = FileAttributes.Hidden)
        {
            _directoryInfoFactory = directoryInfoFactory;
            _matchType = matchType;
            _attributesToSkip = attributesToSkip;
        }

        public IEnumerable<DisplayFileInfo> MatchFileSystemInfo(string search, string incompleteName, bool isRecursive)
        {
            // search folder and add results
            var directoryInfo = _directoryInfoFactory.FromDirectoryName(search);
            var fileSystemInfos = directoryInfo.EnumerateFileSystemInfos(incompleteName, new EnumerationOptions
            {
                MatchType = _matchType,
                RecurseSubdirectories = isRecursive,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = _attributesToSkip,
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
