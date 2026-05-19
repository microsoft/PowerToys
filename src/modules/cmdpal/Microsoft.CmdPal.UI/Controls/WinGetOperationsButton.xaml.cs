// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.WinGet.Services;
using Microsoft.CmdPal.UI.ViewModels.WinGet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class WinGetOperationsButton : UserControl, IDisposable
{
    private bool _disposed;

    public WinGetOperationsViewModel ViewModel { get; }

    public WinGetOperationsButton()
    {
        var trackerService = App.Current.Services.GetRequiredService<IWinGetOperationTrackerService>();
        var uiScheduler = App.Current.Services.GetService<TaskScheduler>() ?? TaskScheduler.FromCurrentSynchronizationContext();
        ViewModel = new WinGetOperationsViewModel(trackerService, uiScheduler);

        this.InitializeComponent();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ViewModel.Dispose();
    }
}
