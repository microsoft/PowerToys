// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

namespace PowerAccent.UI;

public partial class SelectorViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<string> _characters = new();

    [ObservableProperty]
    private int _selectedIndex = -1;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _showDescription;
}
