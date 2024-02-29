// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Interfaces;

namespace PowerToys.FileActionsMenu.Plugins.Hashes
{
    public class Class1 : IFileActionsMenuPlugin
    {
        public string Name => "Checksum genrator/checker";

        public string Description => "Adds actions for generating and verifying checksums of files.";

        public string Author => "Microsoft Corporation";

        public IAction[] TopLevelMenuActions =>
        [
            new Hashes(Hashes.HashCallingAction.GENERATE),
            new Hashes(Hashes.HashCallingAction.VERIFY),
        ];
    }
}
