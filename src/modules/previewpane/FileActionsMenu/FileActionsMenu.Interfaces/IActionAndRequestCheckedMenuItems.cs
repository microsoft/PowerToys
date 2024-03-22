// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FileActionsMenu.Interfaces
{
    /// <summary>
    /// <inheritdoc cref="IAction"/> In addition to the action, it also provides a dictionary of checked menu items.
    /// </summary>
    public interface IActionAndRequestCheckedMenuItems : IAction
    {
        /// <summary>
        /// Gets or sets the dictionary of checked menu items.
        /// </summary>
        public CheckedMenuItemsDictionary CheckedMenuItemsDictionary { get; set; }
    }
}
