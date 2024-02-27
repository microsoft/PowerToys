// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Interfaces;

namespace PowerToys.FileActionsMenu.Plugins.FileContentActions
{
    public class Class1 : IFileActionsMenuPlugin
    {
        public string Name => "File content actions";

        public string Description => "Enables diverse actions for working with the contents of a file";

        public string Author => "Microsoft Corporation";

        public IAction[] TopLevelMenuActions =>
        [
            new CopyFileContent(),
        ];
    }
}
