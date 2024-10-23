// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Helpers;
using FileActionsMenu.Interfaces;

namespace PowerToys.FileActionsMenu.Plugins.MoveCopyActions
{
    public class PluginMain : IFileActionsMenuPlugin
    {
        public string Name => ResourceHelper.GetResource("Move_Copy_Actions.Title");

        public string Description => ResourceHelper.GetResource("Move_Copy_Actions.Description");

        public string Author => ResourceHelper.GetResource("PluginPublisher");

        public IAction[] TopLevelMenuActions =>
        [
            new MoveTo(),
            new CopyTo(),
            new SaveAs(),
            new NewFolderWithSelection(),
        ];
    }
}
