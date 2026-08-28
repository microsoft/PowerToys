// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
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

internal sealed partial class DockPageFlyoutController :
    IRecipient<PerformCommandMessage>,
    IRecipient<HandleCommandResultMessage>,
    IDisposable
{
    internal enum RequestResult
    {
        Started,
        Deferred,
        Failed,
    }

    private sealed record PendingRequest(
        PerformCommandMessage Message,
        FrameworkElement Anchor,
        Point Position,
        DockCommandRoute Route);

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
    private PendingRequest? _pendingRequest;
    private Point? _palettePosition;
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

    internal RequestResult Open(PerformCommandMessage message, FrameworkElement anchor, Point position)
    {
        var route = new DockCommandRoute(_ownerHwnd(), Guid.NewGuid());
        message.DockRoute = route;
        var request = new PendingRequest(message, anchor, position, route);

        if (_flyout.IsOpen)
        {
            _pendingRequest = request;
            _flyout.Hide();
            return RequestResult.Deferred;
        }

        Cleanup();
        if (TryStartRequest(request))
        {
            return RequestResult.Started;
        }

        message.DockRoute = null;
        return RequestResult.Failed;
    }

    internal void PreparePaletteFallback(PerformCommandMessage message, Point position)
    {
        message.DockRoute = null;
        WeakReferenceMessenger.Default.Send<RequestShowPaletteAtMessage>(new(position, _ownerHwnd()));
    }

    public void Receive(PerformCommandMessage message)
    {
        var route = message.DockRoute;
        if (route is null ||
            route.Value.OwnerHwnd != _ownerHwnd())
        {
            return;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            var navigation = _navigation;
            var commandRoute = message.DockRoute;
            if (navigation is null || commandRoute != navigation.Route)
            {
                return;
            }

            if (message.Command.Unsafe is IPage)
            {
                _ = ObserveNavigationAsync(navigation.NavigateAsync(message));
            }
            else if (message.Command.Unsafe is IInvokableCommand)
            {
                ForwardInvokableCommand(message, navigation, commandRoute.Value);
            }
        });
    }

    public void Receive(HandleCommandResultMessage message)
    {
        var route = message.DockRoute;
        if (route is null ||
            route.Value.OwnerHwnd != _ownerHwnd())
        {
            return;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            var navigation = _navigation;
            var sourcePage = message.SourcePage;
            var commandRoute = message.DockRoute;
            if (navigation is null ||
                commandRoute != navigation.Route ||
                sourcePage is null ||
                !navigation.OwnsSourcePage(sourcePage))
            {
                return;
            }

            var forwarded = message with { DockRoute = null };
            AddPaletteConfirmationCallback(forwarded);

            var existingHandler = forwarded.ResultHandler;
            forwarded.ResultHandler = result =>
            {
                if (existingHandler?.Invoke(result) == true)
                {
                    return true;
                }

                return HandleCommandResult(navigation, commandRoute.Value, sourcePage, result);
            };
            WeakReferenceMessenger.Default.Send(forwarded);
        });
    }

    private void ForwardInvokableCommand(
        PerformCommandMessage message,
        DockPageNavigationViewModel navigation,
        DockCommandRoute route)
    {
        var sourcePage = message.SourcePage ?? navigation.CurrentPage;
        if (sourcePage is null || !navigation.OwnsSourcePage(sourcePage))
        {
            return;
        }

        var forwarded = message with
        {
            DockRoute = null,
            SourcePage = sourcePage,
            SourceExtensionHost = message.SourceExtensionHost ?? navigation.CurrentPage?.ExtensionHost,
            SourceProviderContext = message.SourceProviderContext ?? navigation.CurrentPage?.ProviderContext,
        };
        AddPaletteConfirmationCallback(forwarded);

        var existingHandler = forwarded.ResultHandler;
        forwarded.ResultHandler = result =>
        {
            if (existingHandler?.Invoke(result) == true)
            {
                return true;
            }

            return HandleCommandResult(navigation, route, sourcePage, result);
        };
        WeakReferenceMessenger.Default.Send(forwarded);
    }

    private void AddPaletteConfirmationCallback(PerformCommandMessage message)
    {
        var existingCallback = message.OnBeforeShowConfirmation;
        var capturedPosition = _palettePosition;
        var hwnd = _ownerHwnd();
        message.OnBeforeShowConfirmation = () =>
        {
            existingCallback?.Invoke();
            if (capturedPosition is Point position)
            {
                WeakReferenceMessenger.Default.Send<RequestShowPaletteAtMessage>(new(position, hwnd));
            }
        };
    }

    private void AddPaletteConfirmationCallback(HandleCommandResultMessage message)
    {
        var existingCallback = message.OnBeforeShowConfirmation;
        var capturedPosition = _palettePosition;
        var hwnd = _ownerHwnd();
        message.OnBeforeShowConfirmation = () =>
        {
            existingCallback?.Invoke();
            if (capturedPosition is Point position)
            {
                WeakReferenceMessenger.Default.Send<RequestShowPaletteAtMessage>(new(position, hwnd));
            }
        };
    }

    private bool HandleCommandResult(
        DockPageNavigationViewModel navigation,
        DockCommandRoute route,
        PageViewModel sourcePage,
        ICommandResult result)
    {
        if (!ReferenceEquals(Volatile.Read(ref _navigation), navigation) ||
            navigation.Route != route ||
            !navigation.OwnsSourcePage(sourcePage))
        {
            return true;
        }

        if (result.Kind is CommandResultKind.ShowToast or CommandResultKind.Confirm)
        {
            return false;
        }

        if (_dispatcherQueue.HasThreadAccess)
        {
            ApplyCommandResult(navigation, route, sourcePage, result.Kind);
        }
        else
        {
            _dispatcherQueue.TryEnqueue(
                () => ApplyCommandResult(navigation, route, sourcePage, result.Kind));
        }

        return true;
    }

    private void ApplyCommandResult(
        DockPageNavigationViewModel navigation,
        DockCommandRoute route,
        PageViewModel sourcePage,
        CommandResultKind kind)
    {
        if (!ReferenceEquals(_navigation, navigation) ||
            navigation.Route != route ||
            !navigation.OwnsSourcePage(sourcePage))
        {
            return;
        }

        switch (kind)
        {
            case CommandResultKind.Dismiss:
            case CommandResultKind.Hide:
                Close();
                break;
            case CommandResultKind.GoHome:
                _ = ObserveNavigationAsync(navigation.GoHomeAsync());
                break;
            case CommandResultKind.GoBack:
                if (navigation.CanGoBack)
                {
                    _ = ObserveNavigationAsync(navigation.GoBackAsync());
                }
                else
                {
                    Close();
                }

                break;
        }
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

    private bool TryStartRequest(PendingRequest request)
    {
        if (request.Anchor.XamlRoot is null || request.Route.OwnerHwnd != _ownerHwnd())
        {
            return false;
        }

        try
        {
            _palettePosition = request.Position;
            _navigation = new DockPageNavigationViewModel(
                request.Route,
                _uiScheduler,
                _pageFactory,
                _appHostService);
            _control = new DockPageControl(_navigation);
            _control.CloseRequested += Control_CloseRequested;
            _flyout.Content = _control;

            // A windowed popup only receives pointer input when its owner is active.
            var ownerHwnd = new HWND(_ownerHwnd());
            PInvoke.SetForegroundWindow(ownerHwnd);
            PInvoke.SetActiveWindow(ownerHwnd);

            if (request.Anchor is Control anchorControl)
            {
                anchorControl.Focus(FocusState.Programmatic);
            }

            PreparePopupForShow(request.Anchor);
            _flyout.ShowAt(
                request.Anchor,
                new FlyoutShowOptions
                {
                    ShowMode = FlyoutShowMode.Standard,
                    Placement = GetPlacement(),
                });
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to show a dock page.", ex);
            Cleanup();
            return false;
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

    private void PreparePopupForShow(FrameworkElement placementTarget)
    {
        if (placementTarget.XamlRoot is not null && _flyout.XamlRoot != placementTarget.XamlRoot)
        {
            _flyout.XamlRoot = placementTarget.XamlRoot;
        }
    }

    private void Flyout_Opened(object? sender, object e) =>
        _dispatcherQueue.TryEnqueue(
            DispatcherQueuePriority.Low,
            () => _control?.FocusSearch());

    private void Flyout_Closed(object? sender, object e)
    {
        Cleanup();
        if (_isActive && _shouldRestoreFocus())
        {
            _restoreFocus();
        }

        var pending = _pendingRequest;
        _pendingRequest = null;
        if (pending is not null)
        {
            if (!TryStartRequest(pending))
            {
                PreparePaletteFallback(pending.Message, pending.Position);
            }

            WeakReferenceMessenger.Default.Send(pending.Message);
        }
    }

    private void Control_CloseRequested(object? sender, EventArgs e)
    {
        if (_flyout.IsOpen)
        {
            _flyout.Hide();
        }
    }

    private void Cleanup()
    {
        _palettePosition = null;
        _flyout.Content = null;

        if (_control is not null)
        {
            _control.CloseRequested -= Control_CloseRequested;
            _control.Dispose();
            _control = null;
            _navigation = null;
        }
        else
        {
            _navigation?.Dispose();
            _navigation = null;
        }
    }

    private static async Task ObserveNavigationAsync(Task<bool> navigationTask)
    {
        try
        {
            await navigationTask;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to open a dock page.", ex);
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
