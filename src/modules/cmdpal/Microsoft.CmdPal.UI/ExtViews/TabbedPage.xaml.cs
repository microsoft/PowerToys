// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// Hosts an <see cref="TabbedPageViewModel"/>: a tab strip on top and the active
/// tab's page below. Each tab's page is rendered by reusing the existing host
/// views (<see cref="ListPage"/>, <see cref="ContentPage"/>, ...) inside an inner
/// <see cref="Frame"/>. Because forward navigation within that frame gives the
/// outgoing page <see cref="NavigationMode.New"/>, the reused pages skip their
/// cleanup and the tabbed page's cached child view models survive tab switches.
/// </summary>
public sealed partial class TabbedPage : Page
{
    private static readonly NavigationTransitionInfo _noAnimation = new SuppressNavigationTransitionInfo();

    private PageViewModel? _currentChild;

    internal TabbedPageViewModel? ViewModel
    {
        get => (TabbedPageViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(TabbedPageViewModel), typeof(TabbedPage), new PropertyMetadata(null, OnViewModelChanged));

    public TabbedPage()
    {
        this.InitializeComponent();
        this.NavigationCacheMode = NavigationCacheMode.Disabled;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not AsyncNavigationRequest navigationRequest)
        {
            throw new InvalidOperationException($"Invalid navigation parameter: {nameof(e.Parameter)} must be {nameof(AsyncNavigationRequest)}");
        }

        if (navigationRequest.TargetViewModel is not TabbedPageViewModel tabbedPageViewModel)
        {
            throw new InvalidOperationException($"Invalid navigation target: AsyncNavigationRequest.{nameof(AsyncNavigationRequest.TargetViewModel)} must be {nameof(TabbedPageViewModel)}");
        }

        ViewModel = tabbedPageViewModel;

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);

        if (e.NavigationMode != NavigationMode.New)
        {
            ViewModel?.SafeCleanup();
            CleanupHelper.Cleanup(this);
        }

        // Clean-up event listeners (OnViewModelChanged unhooks PropertyChanged)
        ViewModel = null;
        _currentChild = null;

        GC.Collect();
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TabbedPage @this)
        {
            return;
        }

        if (e.OldValue is TabbedPageViewModel old)
        {
            old.PropertyChanged -= @this.ViewModel_PropertyChanged;
        }

        if (e.NewValue is TabbedPageViewModel newVm)
        {
            newVm.PropertyChanged += @this.ViewModel_PropertyChanged;
            @this.SyncActiveChild();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TabbedPageViewModel.ActiveChild))
        {
            SyncActiveChild();
        }
    }

    // Navigates the inner frame to the host view for the active tab's child view
    // model. Reuses the same view types the shell uses for standalone pages.
    private void SyncActiveChild()
    {
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        var child = vm.ActiveChild;
        if (child is null)
        {
            // Unsupported tab: the placeholder is shown via binding.
            TabFrame.Visibility = Visibility.Collapsed;
            _currentChild = null;
            return;
        }

        if (ReferenceEquals(_currentChild, child))
        {
            TabFrame.Visibility = Visibility.Visible;
            return;
        }

        var viewType = child switch
        {
            ListViewModel => typeof(ListPage),
            ContentPageViewModel => typeof(ContentPage),
            ParametersPageViewModel => typeof(ParametersPage),
            _ => null,
        };

        if (viewType is null)
        {
            TabFrame.Visibility = Visibility.Collapsed;
            _currentChild = null;
            return;
        }

        _currentChild = child;
        TabFrame.Visibility = Visibility.Visible;

        // Clear the command bar before swapping; the newly loaded tab page
        // re-populates it as if it had been opened on its own.
        WeakReferenceMessenger.Default.Send<UpdateCommandBarMessage>(new(null));

        TabFrame.Navigate(viewType, new AsyncNavigationRequest(child, CancellationToken.None), _noAnimation);
    }

    private void NextTab_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = CycleTab(1);
    }

    private void PreviousTab_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = CycleTab(-1);
    }

    private bool CycleTab(int direction)
    {
        var vm = ViewModel;
        if (vm is null || vm.Tabs.Count == 0)
        {
            return false;
        }

        var current = vm.SelectedTab is null ? -1 : vm.Tabs.IndexOf(vm.SelectedTab);
        var count = vm.Tabs.Count;
        var next = current < 0 ? 0 : (((current + direction) % count) + count) % count;
        vm.SelectedTab = vm.Tabs[next];
        return true;
    }
}
