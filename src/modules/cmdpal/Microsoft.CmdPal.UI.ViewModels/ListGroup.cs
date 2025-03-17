// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ListGroup : ObservableObject
{
    public string Key { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<ListItemViewModel> Items { get; set; } = [];
}
