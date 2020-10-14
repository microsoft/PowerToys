// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Microsoft.Plugin.Folder.Sources.Result
{
    public class FolderItemResult : IItemResult
    {
        private static readonly IShellAction ExplorerAction = new ShellAction();

        public FolderItemResult()
        {
        }

        public FolderItemResult(DisplayFileInfo fileSystemInfo)
        {
            Title = fileSystemInfo.Name;
            Subtitle = fileSystemInfo.FullName;
            Path = fileSystemInfo.FullName;
        }

        public string Title { get; set; }

        public string Subtitle { get; set; }

        public string Path { get; set; }

        public string Search { get; set; }

        public Wox.Plugin.Result Create(IPublicAPI contextApi)
        {
            return new Wox.Plugin.Result
            {
                Title = Title,
                IcoPath = Path,
                SubTitle = string.Format(CultureInfo.CurrentCulture, Properties.Resources.wox_plugin_folder_select_folder_result_subtitle, Subtitle),
                QueryTextDisplay = Path,
                TitleHighlightData = StringMatcher.FuzzySearch(Search, Title).MatchData,
                ContextData = new SearchResult { Type = ResultType.Folder, FullPath = Path },
                Action = c => ExplorerAction.Execute(Path, contextApi),
            };
        }
    }
}
