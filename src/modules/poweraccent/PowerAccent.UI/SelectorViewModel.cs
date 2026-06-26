// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace PowerAccent.UI;

public partial class SelectorViewModel : ObservableObject
{
    // Partial properties (not [ObservableProperty] fields): the CsWinRT generators need partial
    // properties to emit correct WinRT marshalling for a WinUI 3 app (otherwise MVVMTK0045).
    // Partial properties cannot carry field initializers, so initial values are set in the ctor.
    [ObservableProperty]
    public partial ObservableCollection<string> Characters { get; set; }

    [ObservableProperty]
    public partial int SelectedIndex { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; }

    // Bound directly as a Visibility (no value converter). The Selector's XAML root is a
    // Window (TransparentWindow), and x:Bind's {StaticResource} converter resolution requires
    // a FrameworkElement root, so a BoolToVisibilityConverter cannot be used here.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DescriptionVisibility))]
    public partial bool ShowDescription { get; set; }

    public SelectorViewModel()
    {
        Characters = new ObservableCollection<string>();
        SelectedIndex = -1;
        Description = string.Empty;
    }

    public Visibility DescriptionVisibility => ShowDescription ? Visibility.Visible : Visibility.Collapsed;
}
