// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.IO.Abstractions;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Microsoft.Plugin.Folder.Sources.Result
{
    public class FileItemResult : IItemResult
    {
        private static readonly IShellAction ShellAction = new ShellAction();

        public string FilePath { get; set; }

        public string Title => _fileSystem.Path.GetFileName(FilePath);

        public string Search { get; set; }

        public Wox.Plugin.Result Create(IPublicAPI contextApi)
        {
            var result = new Wox.Plugin.Result
            {
                Title = Title,
                SubTitle = string.Format(CultureInfo.CurrentCulture, Properties.Resources.wox_plugin_folder_select_file_result_subtitle, FilePath),
                IcoPath = FilePath,
                TitleHighlightData = StringMatcher.FuzzySearch(Search, Path.GetFileName(FilePath)).MatchData,
                Action = c => ShellAction.Execute(FilePath, contextApi),
                ContextData = new SearchResult { Type = ResultType.File, FullPath = FilePath },
            };
            return result;
        }
    }
}
