// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using DirectoryWrapper = Wox.Infrastructure.FileSystemHelper.DirectoryWrapper;

namespace Microsoft.Plugin.Folder.Sources
{
    public class QueryFileSystemInfo : DirectoryWrapper,  IQueryFileSystemInfo
    {
        private readonly IFileSystem _fileSystem = new FileSystem();

        public IEnumerable<DisplayFileInfo> MatchFileSystemInfo(string search, string incompleteName, SearchOption searchOption)
        {
            // search folder and add results
            var directoryInfo = _fileSystem.DirectoryInfo.FromDirectoryName(search);
            var fileSystemInfos = directoryInfo.EnumerateFileSystemInfos(incompleteName, searchOption);

            return SafeEnumerateFileSystemInfos(fileSystemInfos)
                .Where(fileSystemInfo => (fileSystemInfo.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                .Select(CreateDisplayFileInfo);
        }

        private static IEnumerable<IFileSystemInfo> SafeEnumerateFileSystemInfos(IEnumerable<IFileSystemInfo> fileSystemInfos)
        {
            using (var enumerator = fileSystemInfos.GetEnumerator())
            {
                while (true)
                {
                    IFileSystemInfo currentFileSystemInfo;
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
