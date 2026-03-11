// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class FallbackRanker : UserControl, IDisposable
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
    private SettingsViewModel? viewModel;
    private bool _disposed;

    public FallbackRanker()
    {
        this.InitializeComponent();

        var settingsService = App.Current.Services.GetService<SettingsService>()!;
        var topLevelCommandManager = App.Current.Services.GetService<TopLevelCommandManager>()!;
        var themeService = App.Current.Services.GetService<IThemeService>()!;
        viewModel = new SettingsViewModel(settingsService, topLevelCommandManager, _mainTaskScheduler, themeService);
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        Unloaded -= OnUnloaded;
        viewModel?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        viewModel?.ApplyFallbackSort();
    }
}
