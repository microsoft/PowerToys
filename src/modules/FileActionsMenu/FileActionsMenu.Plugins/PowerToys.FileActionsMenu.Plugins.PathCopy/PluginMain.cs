// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Helpers;
using FileActionsMenu.Interfaces;

namespace PowerToys.FileActionsMenu.Plugins.PathCopy
{
    public class PluginMain : IFileActionsMenuPlugin
    {
        public string Name => ResourceHelper.GetResource("Path_Copy.Name");

        public string Description => ResourceHelper.GetResource("Path_Copy.Description");

        public string Author => ResourceHelper.GetResource("PluginPublisher");

        public IAction[] TopLevelMenuActions =>
        [
            new CopyPath(),
            new CopyPathSeparatedBy(),
        ];
    }
}
