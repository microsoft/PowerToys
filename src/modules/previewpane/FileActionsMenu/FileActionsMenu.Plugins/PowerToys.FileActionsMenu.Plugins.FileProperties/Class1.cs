// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Interfaces;

namespace PowerToys.FileActionsMenu.Plugins.FileProperties
{
    public class Class1 : IFileActionsMenuPlugin
    {
        public string Name => "File properties";

        public string Description => "Enables actions related to the file properties";

        public string Author => "Microsoft Corporation";

        public IAction[] TopLevelMenuActions =>
        [
            new UnblockFiles(),
        ];
    }
}
