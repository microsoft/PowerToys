// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FileActionsMenu.Interfaces
{
    public interface IActionAndRequestCheckedMenuItems : IAction
    {
        public CheckedMenuItemsDictionary CheckedMenuItemsDictionary { get; set; }
    }
}
