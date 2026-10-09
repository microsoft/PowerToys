// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Dashboard;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class DashboardSettingsPage : Page, IDisposable, INotifyPropertyChanged
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    internal DashboardSettingsViewModel ViewModel { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal Visibility NoTilesVisibility => ViewModel.HasPinnedTiles ? Visibility.Collapsed : Visibility.Visible;

    public DashboardSettingsPage()
    {
        var settingsService = App.Current.Services.GetRequiredService<ISettingsService>();
        var topLevelCommandManager = App.Current.Services.GetService<TopLevelCommandManager>();
        ViewModel = new DashboardSettingsViewModel(settingsService, topLevelCommandManager, _mainTaskScheduler);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        InitializeComponent();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DashboardSettingsViewModel.HasPinnedTiles))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NoTilesVisibility)));
        }
    }

    private void UnpinButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DashboardPinnedTileViewModel tile })
        {
            ViewModel.UnpinCommand.Execute(tile);
        }
    }

    public void Dispose()
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ViewModel.Dispose();
    }
}
