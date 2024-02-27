// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Interfaces;

namespace PowerToys.FileActionsMenu.Plugins.ExecutableActions
{
    public class Class1 : IFileActionsMenuPlugin
    {
        public string Name => "Executable actions";

        public string Description => "Adds actions for .exe and .dll files.";

        public string Author => "Microsoft Corporation";

        public IAction[] TopLevelMenuActions =>
        [
            new Uninstall(),
            new ExtractImages(),
        ];
    }
}
