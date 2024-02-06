// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.IO;
using System.Text;
using Wox.Plugin;

namespace Microsoft.Plugin.Folder.Sources.Result
{
    public class CreateOpenCurrentFolderResult : IItemResult
    {
        private readonly IShellAction _shellAction;

        private static readonly CompositeFormat WoxPluginFolderSelectFolderFirstResultTitle = System.Text.CompositeFormat.Parse(Properties.Resources.wox_plugin_folder_select_folder_first_result_title);

        public string Search { get; set; }

        public CreateOpenCurrentFolderResult(string search)
            : this(search, new ShellAction())
        {
        }

        public CreateOpenCurrentFolderResult(string search, IShellAction shellAction)
        {
            Search = search;
            _shellAction = shellAction;
        }

        public Wox.Plugin.Result Create(IPublicAPI contextApi)
        {
            return new Wox.Plugin.Result
            {
                Title = string.Format(CultureInfo.InvariantCulture, WoxPluginFolderSelectFolderFirstResultTitle, new DirectoryInfo(Search).Name),
                QueryTextDisplay = Search,
                SubTitle = Properties.Resources.wox_plugin_folder_select_folder_first_result_subtitle,
                ToolTipData = new ToolTipData(string.Format(CultureInfo.InvariantCulture, WoxPluginFolderSelectFolderFirstResultTitle, new DirectoryInfo(Search).Name), Properties.Resources.wox_plugin_folder_select_folder_first_result_subtitle),
                IcoPath = Search,
                Score = 500,
                Action = c => _shellAction.ExecuteSanitized(Search, contextApi),
                ContextData = new SearchResult { Type = ResultType.Folder, Path = Search },
            };
        }
    }
}
