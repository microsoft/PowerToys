// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Interfaces;

namespace PowerToys.FileActionsMenu.Plugins.PathCopy
{
    public class Class1 : IFileActionsMenuPlugin
    {
        public string Name => "Copy path";

        public string Description => "Adds the option to copy multiple files delimited by a delimeter or to copy certain parts of a path.";

        public string Author => "Microsoft Corporation";

        public IAction[] TopLevelMenuActions =>
        [
            new CopyPath(),
            new CopyPathSeparatedBy(),
        ];
    }
}
