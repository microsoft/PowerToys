// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.Common;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ParametersPage : Page
{
    private readonly DispatcherQueue _queue = DispatcherQueue.GetForCurrentThread();

    public ParametersPageViewModel? ViewModel
    {
        get => (ParametersPageViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for ViewModel.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(ParametersPageViewModel), typeof(ParametersPage), new PropertyMetadata(null, OnViewModelChanged));

    public ParametersPage()
    {
        this.InitializeComponent();
        this.Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Unhook from everything to ensure nothing can reach us
        // between this point and our complete and utter destruction.
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not AsyncNavigationRequest navigationRequest)
        {
            throw new InvalidOperationException($"Invalid navigation parameter: {nameof(e.Parameter)} must be {nameof(AsyncNavigationRequest)}");
        }

        if (navigationRequest.TargetViewModel is not ParametersPageViewModel ppvm)
        {
            throw new InvalidOperationException($"Invalid navigation target: AsyncNavigationRequest.{nameof(AsyncNavigationRequest.TargetViewModel)} must be {nameof(ParametersPageViewModel)}");
        }

        ViewModel = ppvm;

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);

        // Clean-up event listeners
        ViewModel = null;
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ParametersPage @this)
        {
            if (e.OldValue is ParametersPageViewModel old)
            {
                old.PropertyChanged -= @this.ViewModel_PropertyChanged;
            }

            if (e.NewValue is ParametersPageViewModel page)
            {
                page.PropertyChanged += @this.ViewModel_PropertyChanged;
            }
            else if (e.NewValue is null)
            {
                CoreLogger.LogDebug("cleared view model");
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var prop = e.PropertyName;
        if (prop == nameof(ViewModel.ShowCommand))
        {
            Debug.WriteLine($"ViewModel.ShowCommand {ViewModel?.ShowCommand}");
        }
    }
}
