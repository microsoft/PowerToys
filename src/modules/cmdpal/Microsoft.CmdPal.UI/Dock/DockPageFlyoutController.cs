// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Microsoft.CmdPal.UI.Dock;

/// <summary>
/// Shows dock pages in a flyout. Each flyout session owns one
/// <see cref="DockPageNavigationViewModel"/> and a matching <see cref="DockCommandRoute"/>.
/// Commands sent by its pages carry the route; this controller navigates page commands
/// and forwards other commands to the shell, routing their results back to the flyout.
/// </summary>
internal sealed partial class DockPageFlyoutController :
    IRecipient<PerformCommandMessage>,
    IRecipient<HandleCommandResultMessage>,
    IDisposable
{
    private sealed record PageRequest(PerformCommandMessage Message, FrameworkElement Anchor, Point Position);

    // Where the latest flyout opened. Kept after it closes, so a confirmation that arrives
    // late still surfaces the palette at the right dock item.
    private sealed record ShownFlyout(DockCommandRoute Route, Point Position);

    private readonly Flyout _flyout;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly TaskScheduler _uiScheduler;
    private readonly IPageViewModelFactoryService _pageFactory;
    private readonly IAppHostService _appHostService;
    private readonly Func<IntPtr> _ownerHwnd;
    private readonly Func<DockSide> _dockSide;
    private readonly Func<bool> _shouldRestoreFocus;
    private readonly Action _restoreFocus;
    private DockPageNavigationViewModel? _navigation;
    private DockPageControl? _control;
    private PageRequest? _openRequest;
    private PageRequest? _pendingRequest;
    private ShownFlyout? _lastShown;
    private bool _isActive;
    private bool _isDisposed;

    internal DockPageFlyoutController(
        Flyout flyout,
        DispatcherQueue dispatcherQueue,
        TaskScheduler uiScheduler,
        IPageViewModelFactoryService pageFactory,
        IAppHostService appHostService,
        Func<IntPtr> ownerHwnd,
        Func<DockSide> dockSide,
        Func<bool> shouldRestoreFocus,
        Action restoreFocus)
    {
        _flyout = flyout;
        _dispatcherQueue = dispatcherQueue;
        _uiScheduler = uiScheduler;
        _pageFactory = pageFactory;
        _appHostService = appHostService;
        _ownerHwnd = ownerHwnd;
        _dockSide = dockSide;
        _shouldRestoreFocus = shouldRestoreFocus;
        _restoreFocus = restoreFocus;

        _flyout.Opened += Flyout_Opened;
        _flyout.Closed += Flyout_Closed;
    }

    internal bool HasOpenTransientUi =>
        _flyout.IsOpen ||
        (_control?.HasOpenTransientUi ?? false);

    internal void Activate()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _isActive = true;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        WeakReferenceMessenger.Default.Register<PerformCommandMessage>(this);
        WeakReferenceMessenger.Default.Register<HandleCommandResultMessage>(this);
    }

    internal void Deactivate()
    {
        _isActive = false;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _pendingRequest = null;
        if (_flyout.IsOpen)
        {
            _flyout.Hide();
        }

        Cleanup();
    }

    /// <summary>
    /// Shows the page in <paramref name="message"/> in a flyout at <paramref name="anchor"/>.
    /// This method owns delivery of the message: if the flyout can't be shown, it sends the
    /// message to the palette instead. Invoking the open flyout's command again closes it.
    /// </summary>
    internal void OpenPage(PerformCommandMessage message, FrameworkElement anchor, Point position)
    {
        if (_flyout.IsOpen)
        {
            if (_openRequest is { } open &&
                ReferenceEquals(anchor, open.Anchor) &&
                ReferenceEquals(message.Command.Unsafe, open.Message.Command.Unsafe))
            {
                Close();
                return;
            }

            // A flyout can't move to another anchor while it's open. Show the next page
            // after this one closes.
            _pendingRequest = new(message, anchor, position);
            _flyout.Hide();
            return;
        }

        Cleanup();
        StartRequest(new(message, anchor, position));
    }

    public void Receive(PerformCommandMessage message)
    {
        if (!IsForThisDock(message.DockRoute))
        {
            return;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            var navigation = _navigation;
            if (message.Command.Unsafe is IPage)
            {
                if (navigation is not null && message.DockRoute == navigation.Route)
                {
                    _ = NavigateAsync(navigation, message);
                }

                return;
            }

            // The user asked for this command, so run it even if its flyout has closed.
            ForwardToShell(message, navigation);
        });
    }

    public void Receive(HandleCommandResultMessage message)
    {
        if (!IsForThisDock(message.DockRoute))
        {
            return;
        }

        _dispatcherQueue.TryEnqueue(() => ForwardToShell(message, _navigation));
    }

    private bool IsForThisDock(DockCommandRoute? route) =>
        route is { } value && value.OwnerHwnd == _ownerHwnd();

    private void ForwardToShell(PerformCommandMessage message, DockPageNavigationViewModel? navigation) =>
        WeakReferenceMessenger.Default.Send(message with
        {
            DockRoute = null,
            OnBeforeShowConfirmation = CreateConfirmationCallback(message.DockRoute, message.OnBeforeShowConfirmation),
            ResultHandler = CreateResultHandler(message.ResultHandler, navigation, message.DockRoute, message.Context?.Page),
        });

    private void ForwardToShell(HandleCommandResultMessage message, DockPageNavigationViewModel? navigation) =>
        WeakReferenceMessenger.Default.Send(message with
        {
            DockRoute = null,
            OnBeforeShowConfirmation = CreateConfirmationCallback(message.DockRoute, message.OnBeforeShowConfirmation),
            ResultHandler = CreateResultHandler(message.ResultHandler, navigation, message.DockRoute, message.Context?.Page),
        });

    // Confirmation dialogs belong to the palette, so surface it at the dock item first.
    private Action CreateConfirmationCallback(DockCommandRoute? route, Action? existingCallback)
    {
        var position = _lastShown is { } shown && shown.Route == route ? shown.Position : (Point?)null;
        var hwnd = _ownerHwnd();
        return () =>
        {
            existingCallback?.Invoke();
            if (position is Point value)
            {
                WeakReferenceMessenger.Default.Send<RequestShowPaletteAtMessage>(new(value, hwnd));
            }
        };
    }

    /// <summary>
    /// Routes results from the current page back to its flyout. Results from a closed
    /// flyout or an old page keep only shell UI, so they can't move the palette.
    /// </summary>
    private static Func<ICommandResult, bool> CreateResultHandler(
        Func<ICommandResult, bool>? existingHandler,
        DockPageNavigationViewModel? navigation,
        DockCommandRoute? route,
        PageViewModel? sourcePage)
    {
        if (navigation is not null &&
            route == navigation.Route &&
            sourcePage is not null &&
            navigation.OwnsSourcePage(sourcePage))
        {
            return result => existingHandler?.Invoke(result) == true ||
                navigation.HandleCommandResult(sourcePage, result);
        }

        return result => existingHandler?.Invoke(result) == true ||
            !DockPageNavigationViewModel.IsShellResult(result.Kind);
    }

    private void StartRequest(PageRequest request)
    {
        var route = new DockCommandRoute(_ownerHwnd(), Guid.NewGuid());
        var message = request.Message;
        if (TryShowFlyout(request, route) is { } navigation)
        {
            message.DockRoute = route;
            _ = NavigateAsync(navigation, message);
            return;
        }

        message.DockRoute = null;
        WeakReferenceMessenger.Default.Send<RequestShowPaletteAtMessage>(new(request.Position, _ownerHwnd()));
        WeakReferenceMessenger.Default.Send(message);
    }

    private DockPageNavigationViewModel? TryShowFlyout(PageRequest request, DockCommandRoute route)
    {
        if (request.Anchor.XamlRoot is null)
        {
            return null;
        }

        try
        {
            _lastShown = new(route, request.Position);
            _openRequest = request;
            _navigation = new DockPageNavigationViewModel(route, _uiScheduler, _pageFactory, _appHostService);
            _navigation.CloseRequested += Navigation_CloseRequested;
            _control = new DockPageControl(_navigation);
            _control.CloseRequested += Control_CloseRequested;
            _flyout.Content = _control;

            // A windowed popup only receives pointer input when its owner is active.
            var ownerHwnd = new HWND(_ownerHwnd());
            PInvoke.SetForegroundWindow(ownerHwnd);
            PInvoke.SetActiveWindow(ownerHwnd);

            UIHelper.PreparePopupForShow(_flyout, request.Anchor);
            _flyout.ShowAt(
                request.Anchor,
                new FlyoutShowOptions
                {
                    ShowMode = FlyoutShowMode.Standard,
                    Placement = GetPlacement(),
                });
            return _navigation;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to show a dock page.", ex);
            Cleanup();
            return null;
        }
    }

    private async Task NavigateAsync(DockPageNavigationViewModel navigation, PerformCommandMessage message)
    {
        try
        {
            await navigation.NavigateAsync(message);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to open a dock page.", ex);
        }

        // Don't leave an empty flyout open when the first page couldn't be created.
        if (ReferenceEquals(_navigation, navigation) && navigation.CurrentPage is null)
        {
            Close();
        }
    }

    private FlyoutPlacementMode GetPlacement()
    {
        return _dockSide() switch
        {
            DockSide.Top => FlyoutPlacementMode.Bottom,
            DockSide.Bottom => FlyoutPlacementMode.Top,
            DockSide.Left => FlyoutPlacementMode.RightEdgeAlignedTop,
            DockSide.Right => FlyoutPlacementMode.LeftEdgeAlignedTop,
            _ => FlyoutPlacementMode.Bottom,
        };
    }

    private void Close()
    {
        _pendingRequest = null;
        if (_flyout.IsOpen)
        {
            _flyout.Hide();
        }
        else
        {
            Cleanup();
        }
    }

    private void Flyout_Opened(object? sender, object e) =>
        _dispatcherQueue.TryEnqueue(
            DispatcherQueuePriority.Low,
            () => _control?.FocusSearch());

    private void Flyout_Closed(object? sender, object e)
    {
        Cleanup();

        var pending = _pendingRequest;
        _pendingRequest = null;
        if (pending is not null)
        {
            StartRequest(pending);
        }
        else if (_isActive && _shouldRestoreFocus())
        {
            _restoreFocus();
        }
    }

    private void Navigation_CloseRequested(object? sender, EventArgs e) => Close();

    private void Control_CloseRequested(object? sender, EventArgs e) => Close();

    private void Cleanup()
    {
        _openRequest = null;
        _flyout.Content = null;

        if (_control is not null)
        {
            _control.CloseRequested -= Control_CloseRequested;
            _control.Dispose();
            _control = null;
        }

        if (_navigation is not null)
        {
            _navigation.CloseRequested -= Navigation_CloseRequested;
            _navigation.Dispose();
            _navigation = null;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Deactivate();
        _flyout.Opened -= Flyout_Opened;
        _flyout.Closed -= Flyout_Closed;
        GC.SuppressFinalize(this);
    }
}
