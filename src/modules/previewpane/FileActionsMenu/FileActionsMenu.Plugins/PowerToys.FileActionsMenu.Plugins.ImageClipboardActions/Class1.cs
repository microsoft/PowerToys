// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Interfaces;

namespace PowerToys.FileActionsMenu.Plugins.ImageClipboardActions
{
    public class Class1 : IFileActionsMenuPlugin
    {
        public string Name => "Image clipboard";

        public string Description => "Adds actions for copying/pasting images to/from the clipboard.";

        public string Author => "Microsoft Corporation";

        public IAction[] TopLevelMenuActions =>
        [
            new CopyImageToClipboard(),
            new CopyImageFromClipboardToFolder(),
        ];
    }
}
