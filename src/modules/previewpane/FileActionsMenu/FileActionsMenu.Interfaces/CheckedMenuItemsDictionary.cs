// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using FileActionsMenu.Interfaces;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Interfaces
{
    public sealed class CheckedMenuItemsDictionary : Dictionary<string, List<(MenuItem, IAction)>>
    {
    }
}
