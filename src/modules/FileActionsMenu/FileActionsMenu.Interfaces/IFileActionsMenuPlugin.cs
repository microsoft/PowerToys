// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FileActionsMenu.Interfaces
{
    public interface IFileActionsMenuPlugin
    {
        /// <summary>
        /// Gets the name of the plugin
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the description of the plugin
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the author of the plugin
        /// </summary>
        string Author { get; }

        /// <summary>
        /// Gets the items that will be added to the top level menu
        /// </summary>
        IAction[] TopLevelMenuActions { get; }
    }
}
