// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CmdPal.UI.Dock;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Dashboard;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Dashboard;

/// <summary>
/// One pinned page on the dashboard. Hosts the page the same way a dock flyout does, with the
/// pinned page as the root of its own navigation stack, so it can't navigate back to home.
/// </summary>
public sealed partial class DashboardTileControl : UserControl, IDisposable
{
    private static readonly Microsoft.Windows.ApplicationModel.Resources.ResourceLoader ResourceLoader = new();

    private CommandResolution? _resolution;
    private DockPageNavigationViewModel? _navigation;
    private DockPageControl? _pageControl;
    private bool _isEditing;
    private bool _isDisposed;

    internal DashboardTileControl(DashboardTileSettings settings)
    {
        ProviderId = settings.ProviderId;
        CommandId = settings.CommandId;
        InitializeComponent();
        SetTitle(settings.CommandId);
    }

    internal string ProviderId { get; }

    internal string CommandId { get; }

    internal string Title { get; private set; } = string.Empty;

    /// <summary>
    /// Gets or sets where the tile currently sits on the dashboard grid.
    /// </summary>
    internal DashboardTileRect Rect { get; set; }

    internal DockPageNavigationViewModel? Navigation => _navigation;

    internal bool HasOpenTransientUi => _pageControl?.HasOpenTransientUi ?? false;

    internal bool HasPage => _navigation?.CurrentPage is not null;

    internal bool IsLoading { get; private set; }

    internal event EventHandler? DismissRequested;

    internal event EventHandler? UnpinRequested;

    internal event EventHandler<DashboardTileEditKind>? EditStarted;

    internal event EventHandler<DashboardTileEditEventArgs>? EditDelta;

    internal event EventHandler<DashboardTileEditKind>? EditCompleted;

    internal bool IsEditing
    {
        get => _isEditing;
        set
        {
            _isEditing = value;
            EditOverlay.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    internal bool Matches(DashboardTileSettings settings) => settings.Matches(ProviderId, CommandId);

    /// <summary>
    /// Resolves the pinned command and opens its page.
    /// </summary>
    internal async Task LoadAsync(
        IntPtr ownerHwnd,
        TopLevelCommandManager commandManager,
        TaskScheduler uiScheduler,
        IPageViewModelFactoryService pageFactory,
        IAppHostService appHostService,
        CancellationToken cancellationToken)
    {
        ShowLoading();
        IsLoading = true;
        try
        {
            await commandManager.WaitForCurrentLoadAsync(cancellationToken);
            var resolution = await commandManager.ResolveCommandAsync(ProviderId, CommandId, cancellationToken);
            if (_isDisposed || cancellationToken.IsCancellationRequested)
            {
                resolution?.Dispose();
                return;
            }

            if (resolution is null)
            {
                ShowMessage(ResourceLoader.GetString("Dashboard_Tile_Unavailable"));
                return;
            }

            ReleasePage();
            _resolution = resolution;
            var command = resolution.Command;
            SetTitle(string.IsNullOrEmpty(command.Title) ? CommandId : command.Title);
            PlaceholderIcon.SourceKey = command.IconViewModel;

            if (!command.CommandViewModel.IsPage)
            {
                ShowMessage(ResourceLoader.GetString("Dashboard_Tile_NotAPage"));
                return;
            }

            var route = new DockCommandRoute(ownerHwnd, Guid.NewGuid());
            var navigation = new DockPageNavigationViewModel(route, uiScheduler, pageFactory, appHostService);
            navigation.CloseRequested += Navigation_CloseRequested;
            _navigation = navigation;

            var message = DockItemActions.CreateInvocationMessage(command.ItemViewModel);
            message.DockRoute = route;
            var opened = await navigation.NavigateAsync(message, cancellationToken);
            if (_isDisposed || !ReferenceEquals(_navigation, navigation))
            {
                return;
            }

            if (!opened || navigation.CurrentPage is null)
            {
                ReleasePage();
                ShowMessage(ResourceLoader.GetString("Dashboard_Tile_Unavailable"));
                return;
            }

            var pageControl = new DockPageControl(navigation) { FocusOnPageChange = false };
            pageControl.FillContainer();
            pageControl.CloseRequested += PageControl_CloseRequested;
            _pageControl = pageControl;
            PageHost.Content = pageControl;
            Placeholder.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load dashboard tile '{CommandId}' from '{ProviderId}'.", ex);
            if (!_isDisposed)
            {
                ReleasePage();
                ShowMessage(ResourceLoader.GetString("Dashboard_Tile_Unavailable"));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Returns to the pinned page, e.g. when the dashboard is dismissed.
    /// </summary>
    internal async Task GoHomeAsync()
    {
        if (_navigation is { CanGoBack: true } navigation)
        {
            try
            {
                await navigation.GoHomeAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to reset a dashboard tile.", ex);
            }
        }
    }

    internal bool TryFocusPage()
    {
        if (_pageControl is null)
        {
            return false;
        }

        _pageControl.FocusSearch();
        return true;
    }

    internal bool ContainsFocus() => _pageControl?.ContainsFocus() ?? false;

    private void SetTitle(string title)
    {
        Title = title;
        PlaceholderTitle.Text = title;
        EditTitle.Text = title;
        AutomationProperties.SetName(this, title);
    }

    private void ShowLoading()
    {
        Placeholder.Visibility = Visibility.Visible;
        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;
        PlaceholderMessage.Visibility = Visibility.Collapsed;
    }

    private void ShowMessage(string message)
    {
        Placeholder.Visibility = Visibility.Visible;
        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
        PlaceholderMessage.Text = message;
        PlaceholderMessage.Visibility = Visibility.Visible;
    }

    private void Navigation_CloseRequested(object? sender, EventArgs e) => DismissRequested?.Invoke(this, EventArgs.Empty);

    private void PageControl_CloseRequested(object? sender, EventArgs e) => DismissRequested?.Invoke(this, EventArgs.Empty);

    private void UnpinButton_Click(object sender, RoutedEventArgs e) => UnpinRequested?.Invoke(this, EventArgs.Empty);

    private void EditOverlay_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
    {
        e.Handled = true;
        EditStarted?.Invoke(this, DashboardTileEditKind.Move);
    }

    private void EditOverlay_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        e.Handled = true;
        EditDelta?.Invoke(this, new(DashboardTileEditKind.Move, e.Cumulative.Translation));
    }

    private void EditOverlay_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        e.Handled = true;
        EditCompleted?.Invoke(this, DashboardTileEditKind.Move);
    }

    private void ResizeGrip_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
    {
        e.Handled = true;
        EditStarted?.Invoke(this, DashboardTileEditKind.Resize);
    }

    private void ResizeGrip_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        e.Handled = true;
        EditDelta?.Invoke(this, new(DashboardTileEditKind.Resize, e.Cumulative.Translation));
    }

    private void ResizeGrip_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        e.Handled = true;
        EditCompleted?.Invoke(this, DashboardTileEditKind.Resize);
    }

    private void ReleasePage()
    {
        PageHost.Content = null;
        if (_pageControl is not null)
        {
            _pageControl.CloseRequested -= PageControl_CloseRequested;
            _pageControl.Dispose();
            _pageControl = null;
        }

        if (_navigation is not null)
        {
            _navigation.CloseRequested -= Navigation_CloseRequested;
            _navigation.Dispose();
            _navigation = null;
        }

        _resolution?.Dispose();
        _resolution = null;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        ReleasePage();
    }
}
