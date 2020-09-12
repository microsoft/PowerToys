// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Wox.Plugin;

namespace Microsoft.Plugin.Folder.Sources.Result
{
    public class CreateOpenCurrentFolderResult : IItemResult
    {
        private readonly IExplorerAction _explorerAction;

        public string Search { get; set;  }

        public CreateOpenCurrentFolderResult(string search)
            : this(search, new ExplorerAction())
        {
        }

        public CreateOpenCurrentFolderResult(string search, IExplorerAction explorerAction)
        {
            Search = search;
            _explorerAction = explorerAction;
        }

        public Wox.Plugin.Result Create(IPublicAPI contextApi)
        {
            return new Wox.Plugin.Result
            {
                Title = $"Open {Search}",
                QueryTextDisplay = Search,
                SubTitle = $"Folder: Use > to search within the directory. Use * to search for file extensions. Or use both >*.",
                IcoPath = Search,
                Score = 500,
                Action = c => _explorerAction.ExecuteSanitized(Search, contextApi),
            };
        }
    }
}
