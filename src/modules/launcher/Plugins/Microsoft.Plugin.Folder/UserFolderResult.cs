// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.Plugin.Folder.Sources;
using Microsoft.Plugin.Folder.Sources.Result;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Microsoft.Plugin.Folder
{
    public class UserFolderResult : IItemResult
    {
        private readonly IShellAction _shellAction = new ShellAction();

        public string Search { get; set; }

        public string Title { get; set; }

        public string Path { get; set; }

        public string Subtitle { get; set; }

        public Result Create(IPublicAPI contextApi)
        {
            return new Result(StringMatcher.FuzzySearch(Search, Title).MatchData)
            {
                Title = Title,
                IcoPath = Path,

                // Using CurrentCulture since this is user facing
                SubTitle = string.Format(CultureInfo.CurrentCulture, Properties.Resources.wox_plugin_folder_select_folder_result_subtitle, Subtitle),
                QueryTextDisplay = Path,
                ContextData = new SearchResult { Type = ResultType.Folder, Path = Path },
                Action = c => _shellAction.Execute(Path, contextApi),
            };
        }
    }
}
