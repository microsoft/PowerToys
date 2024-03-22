// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;

namespace FileActionsMenu.Interfaces
{
    /// <summary>
    /// A stub type that represents a dictionary of checked menu items. The key is the uuid of the group. The list contains the corresponding menu item and the action.
    /// </summary>
    public sealed class CheckedMenuItemsDictionary : Dictionary<string, List<(MenuFlyoutItemBase, IAction)>>
    {
    }
}
