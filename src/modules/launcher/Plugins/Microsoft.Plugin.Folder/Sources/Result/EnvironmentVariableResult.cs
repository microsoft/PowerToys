// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Microsoft.Plugin.Folder.Sources.Result
{
    public class EnvironmentVariableResult : IItemResult
    {
        private readonly ShellAction _shellAction = new ShellAction();

        private static readonly CompositeFormat WoxPluginFolderSelectFolderResultSubtitle = System.Text.CompositeFormat.Parse(Properties.Resources.wox_plugin_folder_select_folder_result_subtitle);

        public string Search { get; set; }

        public string Title { get; private set; }

        public string Subtitle { get; private set; }

        public string Path { get; private set; }

        public EnvironmentVariableResult(string search, string title, string path)
        {
            Search = search;
            Title = title;
            Path = path;
        }

        public Wox.Plugin.Result Create(IPublicAPI contextApi)
        {
            return new Wox.Plugin.Result(StringMatcher.FuzzySearch(Search, Title).MatchData)
            {
                Title = Title,
                IcoPath = Path,

                // Using CurrentCulture since this is user facing
                SubTitle = string.Format(CultureInfo.CurrentCulture, WoxPluginFolderSelectFolderResultSubtitle, Path),
                ToolTipData = new ToolTipData(Title, string.Format(CultureInfo.CurrentCulture, WoxPluginFolderSelectFolderResultSubtitle, Path)),
                QueryTextDisplay = Path,
                ContextData = new SearchResult { Type = ResultType.Folder, Path = Path },
                Action = c => _shellAction.Execute(Path, contextApi),
            };
        }
    }
}
