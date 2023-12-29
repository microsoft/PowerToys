// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO.Abstractions;
using System.Linq;
using ManagedCommon;
using Microsoft.Plugin.Folder.Sources.Result;

namespace Microsoft.Plugin.Folder.Sources
{
    public class QueryInternalDirectory : IQueryInternalDirectory
    {
        private static readonly SearchValues<char> PathChars = SearchValues.Create("\\/");
        private readonly FolderSettings _settings;
        private readonly IQueryFileSystemInfo _queryFileSystemInfo;
        private readonly IDirectory _directory;

        private static readonly HashSet<char> SpecialSearchChars = new HashSet<char>
        {
            '?', '*', '>',
        };

        private static string _warningIconPath;

        public QueryInternalDirectory(FolderSettings folderSettings, IQueryFileSystemInfo queryFileSystemInfo, IDirectory directory)
        {
            _settings = folderSettings;
            _queryFileSystemInfo = queryFileSystemInfo;
            _directory = directory;
        }

        private static bool HasSpecialChars(string search)
        {
            return search.Any(c => SpecialSearchChars.Contains(c));
        }

        public static bool RecursiveSearch(string query)
        {
            // give the ability to search all folder when it contains a >
            return query.Any(c => c.Equals('>'));
        }

        private (string Search, string IncompleteName) Process(string search)
        {
            string incompleteName = string.Empty;
            if (HasSpecialChars(search) || !_directory.Exists($@"{search}\"))
            {
                // if folder doesn't exist, we want to take the last part and use it afterwards to help the user
                // find the right folder.
                int index = search.AsSpan().LastIndexOfAny(PathChars);

                // No slashes found, so probably not a folder
                if (index <= 0 || index >= search.Length - 1)
                {
                    return default;
                }

                // Remove everything after the last \ and add *
                // Using InvariantCulture since this is internal
                incompleteName = search.Substring(index + 1)
                    .ToLower(CultureInfo.InvariantCulture) + "*";
                search = search.Substring(0, index + 1);
                if (!_directory.Exists(search))
                {
                    return default;
                }
            }
            else
            {
                // folder exist, add \ at the end of doesn't exist
                // Using Ordinal since this is internal and is used for a symbol
                if (!search.EndsWith('\\'))
                {
                    search += @"\";
                }
            }

            return (search, incompleteName);
        }

        public IEnumerable<IItemResult> Query(string search)
        {
            ArgumentNullException.ThrowIfNull(search);

            var processed = Process(search);

            if (processed == default)
            {
                yield break;
            }

            var (querySearch, incompleteName) = processed;
            var isRecursive = RecursiveSearch(incompleteName);

            if (isRecursive)
            {
                // match everything before and after search term using supported wildcard '*', ie. *searchterm*
                if (string.IsNullOrEmpty(incompleteName))
                {
                    incompleteName = "*";
                }
                else
                {
                    incompleteName = string.Concat("*", incompleteName.AsSpan(1));
                }
            }

            yield return new CreateOpenCurrentFolderResult(querySearch);

            // Note: Take 1000 is so that you don't search the whole system before you discard
            var lookup = _queryFileSystemInfo.MatchFileSystemInfo(querySearch, incompleteName, isRecursive)
                .Take(1000)
                .ToLookup(r => r.Type);

            var folderList = lookup[DisplayType.Directory].ToImmutableArray();
            var fileList = lookup[DisplayType.File].ToImmutableArray();

            var fileSystemResult = GenerateFolderResults(querySearch, folderList)
                .Concat<IItemResult>(GenerateFileResults(querySearch, fileList))
                .ToImmutableArray();

            foreach (var result in fileSystemResult)
            {
                yield return result;
            }

            // Show warning message if result has been truncated
            if (folderList.Length > _settings.MaxFolderResults || fileList.Length > _settings.MaxFileResults)
            {
                yield return GenerateTruncatedItemResult(folderList.Length + fileList.Length, fileSystemResult.Length);
            }
        }

        private IEnumerable<FileItemResult> GenerateFileResults(string search, IEnumerable<DisplayFileInfo> fileList)
        {
            return fileList
                .Select(fileSystemInfo => new FileItemResult()
                {
                    FilePath = fileSystemInfo.FullName,
                    Search = search,
                })
                .OrderBy(x => x.Title)
                .Take(_settings.MaxFileResults);
        }

        private IEnumerable<FolderItemResult> GenerateFolderResults(string search, IEnumerable<DisplayFileInfo> folderList)
        {
            return folderList
                .Select(fileSystemInfo => new FolderItemResult(fileSystemInfo)
                {
                    Search = search,
                })
                .OrderBy(x => x.Title)
                .Take(_settings.MaxFolderResults);
        }

        private static TruncatedItemResult GenerateTruncatedItemResult(int preTruncationCount, int postTruncationCount)
        {
            return new TruncatedItemResult()
            {
                PreTruncationCount = preTruncationCount,
                PostTruncationCount = postTruncationCount,
                WarningIconPath = _warningIconPath,
            };
        }

        public static void SetWarningIcon(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                _warningIconPath = "Images/Warning.light.png";
            }
            else
            {
                _warningIconPath = "Images/Warning.dark.png";
            }
        }
    }
}
