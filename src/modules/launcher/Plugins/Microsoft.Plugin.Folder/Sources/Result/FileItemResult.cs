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

        private readonly IPath _path;

        public FileItemResult()
            : this(new FileSystem().Path)
        {
        }

        private FileItemResult(IPath path)
        {
            _path = path;
        }

        public string FilePath { get; set; }

        public string Title => _path.GetFileName(FilePath);

        public string Search { get; set; }

        public Wox.Plugin.Result Create(IPublicAPI contextApi)
        {
            var result = new Wox.Plugin.Result(StringMatcher.FuzzySearch(Search, _path.GetFileName(FilePath)).MatchData)
            {
                Title = Title,

                // Using CurrentCulture since this is user facing
                SubTitle = string.Format(CultureInfo.CurrentCulture, Properties.Resources.wox_plugin_folder_select_file_result_subtitle, FilePath),
                ToolTipData = new ToolTipData(Title, string.Format(CultureInfo.CurrentCulture, Properties.Resources.wox_plugin_folder_select_file_result_subtitle, FilePath)),
                IcoPath = FilePath,
                Action = c => ShellAction.Execute(FilePath, contextApi),
                ContextData = new SearchResult { Type = ResultType.File, Path = FilePath },
            };
            return result;
        }
    }
}
