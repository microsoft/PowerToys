// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wox.Infrastructure.FileSystemHelper;

namespace Microsoft.Plugin.Folder.Sources
{
    public class QueryFileSystemInfo : DirectoryWrapper,  IQueryFileSystemInfo
    {
        public IEnumerable<DisplayFileInfo> MatchFileSystemInfo(string search, string incompleteName, SearchOption searchOption)
        {
            // search folder and add results
            var directoryInfo = new DirectoryInfo(search);
            var fileSystemInfos = directoryInfo.EnumerateFileSystemInfos(incompleteName, searchOption);

            return SafeEnumerateFileSystemInfos(fileSystemInfos)
                .Where(fileSystemInfo => (fileSystemInfo.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                .Select(CreateDisplayFileInfo);
        }

        private static IEnumerable<FileSystemInfo> SafeEnumerateFileSystemInfos(IEnumerable<FileSystemInfo> fileSystemInfos)
        {
            using (var enumerator = fileSystemInfos.GetEnumerator())
            {
                while (true)
                {
                    FileSystemInfo currentFileSystemInfo;
                    try
                    {
                        if (!enumerator.MoveNext())
                        {
                            break;
                        }

                        currentFileSystemInfo = enumerator.Current;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        continue;
                    }

                    yield return currentFileSystemInfo;
                }
            }
        }

        private static DisplayFileInfo CreateDisplayFileInfo(FileSystemInfo fileSystemInfo)
        {
            return new DisplayFileInfo()
            {
                Name = fileSystemInfo.Name,
                FullName = fileSystemInfo.FullName,
                Type = GetDisplayType(fileSystemInfo),
            };
        }

        private static DisplayType GetDisplayType(FileSystemInfo fileSystemInfo)
        {
            if (fileSystemInfo is DirectoryInfo)
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
