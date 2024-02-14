// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FileActionsMenu.Interfaces;

namespace PowerToys.FileActionsMenu.Plugins.MoveCopyActions
{
    public class Program : IFileActionsMenuPlugin
    {
        public string Name => "Move & Copy actions";

        public string Description => string.Empty;

        public string Author => "Microsoft Corporation";

        public IAction[] TopLevelMenuActions => throw new NotImplementedException();
    }
}
