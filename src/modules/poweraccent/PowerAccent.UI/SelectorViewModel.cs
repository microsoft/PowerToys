// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace PowerAccent.UI;

public partial class SelectorViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<string> _characters = new();

    [ObservableProperty]
    private int _selectedIndex = -1;

    [ObservableProperty]
    private string _description = string.Empty;

    // Bound directly as a Visibility (no value converter). The Selector's XAML root is a
    // Window (TransparentWindow), and x:Bind's {StaticResource} converter resolution requires
    // a FrameworkElement root, so a BoolToVisibilityConverter cannot be used here.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DescriptionVisibility))]
    private bool _showDescription;

    public Visibility DescriptionVisibility => ShowDescription ? Visibility.Visible : Visibility.Collapsed;
}
