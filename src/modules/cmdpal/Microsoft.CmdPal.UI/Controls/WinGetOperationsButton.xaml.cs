// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.CmdPal.Common.WinGet.Services;
using Microsoft.CmdPal.UI.ViewModels.WinGet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class WinGetOperationsButton : UserControl, IDisposable
{
    private bool _disposed;

    public WinGetOperationsViewModel ViewModel { get; }

    public bool HasVisibleOperations => ViewModel.HasVisibleOperations;

    public bool HasActiveOperations => ViewModel.HasActiveOperations;

    public string SummaryText => ViewModel.SummaryText;

    public string FlyoutHeaderText => ViewModel.FlyoutHeaderText;

    public WinGetOperationsButton()
    {
        var trackerService = App.Current.Services.GetRequiredService<IWinGetOperationTrackerService>();
        var uiScheduler = App.Current.Services.GetService<TaskScheduler>() ?? TaskScheduler.FromCurrentSynchronizationContext();
        ViewModel = new WinGetOperationsViewModel(trackerService, uiScheduler);

        this.InitializeComponent();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.Dispose();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Bindings.Update();
    }
}
