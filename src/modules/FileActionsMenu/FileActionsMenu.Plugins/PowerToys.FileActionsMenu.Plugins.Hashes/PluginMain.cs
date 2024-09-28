// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Helpers;
using FileActionsMenu.Interfaces;

namespace PowerToys.FileActionsMenu.Plugins.Hashes
{
    public class PluginMain : IFileActionsMenuPlugin
    {
        public string Name => ResourceHelper.GetResource("Hashes.Title");

        public string Description => ResourceHelper.GetResource("Hashes.Description");

        public string Author => ResourceHelper.GetResource("PluginPublisher");

        public IAction[] TopLevelMenuActions =>
        [
            new Hashes(Hashes.HashCallingAction.GENERATE),
            new Hashes(Hashes.HashCallingAction.VERIFY),
        ];
    }
}
