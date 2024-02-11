// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CheckedMenuItemsDictionairy = System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<(Wpf.Ui.Controls.MenuItem, FileActionsMenu.Ui.Actions.IAction)>>;

namespace FileActionsMenu.Ui.Actions
{
    internal interface IActionAndRequestCheckedMenuItems : IAction
    {
        public CheckedMenuItemsDictionairy CheckedMenuItemsDictionary { get; set; }
    }
}
