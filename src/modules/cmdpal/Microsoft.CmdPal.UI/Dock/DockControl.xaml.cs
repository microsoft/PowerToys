// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Dock;

public sealed partial class DockControl : UserControl, INotifyPropertyChanged
{
    private DockViewModel _viewModel;

    internal DockViewModel ViewModel => _viewModel;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Orientation ItemsOrientation
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                PropertyChanged?.Invoke(this, new(nameof(ItemsOrientation)));
            }
        }
    }

    public bool ShowSearchButton
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                PropertyChanged?.Invoke(this, new(nameof(ShowSearchButton)));
            }
        }
    }

    internal DockControl(DockViewModel viewModel)
    {
        // MainViewModel mainModel = (MainViewModel)DataContext;
        _viewModel = viewModel;
        InitializeComponent();
    }

    internal void UpdateSettings(DockSettings settings)
    {
        var isHorizontal = settings.Side == DockSide.Top || settings.Side == DockSide.Bottom;

        // _viewModel.UpdateSettings(); // TODO!
        ItemsOrientation = isHorizontal ? Orientation.Horizontal : Orientation.Vertical;
        ShowSearchButton = settings.ShowSearchButton;
        SearchColumn.Width = ShowSearchButton
            ? new Microsoft.UI.Xaml.GridLength(1, Microsoft.UI.Xaml.GridUnitType.Star)
            : new Microsoft.UI.Xaml.GridLength(0, Microsoft.UI.Xaml.GridUnitType.Star);

        EndColumn.Width = ShowSearchButton
            ? new Microsoft.UI.Xaml.GridLength(2, Microsoft.UI.Xaml.GridUnitType.Star)
            : new Microsoft.UI.Xaml.GridLength(1, Microsoft.UI.Xaml.GridUnitType.Auto);
    }

    [RelayCommand]
    private void SearchOpenCmdPal(object? whatever)
    {
        // URI invoke "x-cmdpal://"
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "x-cmdpal://",
            UseShellExecute = true,
        });
    }
}
