// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.UI.Dispatching;

namespace Microsoft.CmdPal.UI.Services;

internal sealed partial class ExternalCommandLinkCoordinator : IDisposable
{
    private const int MaximumQueuedLinks = 16;
    private static readonly TimeSpan ResolutionTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan LoadingDelay = TimeSpan.FromMilliseconds(250);

    private readonly ISettingsService _settingsService;
    private readonly IExternalCommandPermissionStore _permissionStore;
    private readonly TopLevelCommandManager _topLevelCommandManager;
    private readonly IExternalCommandLinkPresenter _presenter;
    private readonly IMessenger _messenger;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ExternalCommandPermission _reloadPermission;
    private readonly Queue<CmdPalProtocolRoute> _queue = [];
    private readonly HashSet<CmdPalProtocolRoute> _pendingLinks = [];
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly CancellationToken _lifetimeToken;
    private bool _isProcessing;
    private bool _isDisposed;

    internal ExternalCommandLinkCoordinator(
        ISettingsService settingsService,
        IExternalCommandPermissionStore permissionStore,
        TopLevelCommandManager topLevelCommandManager,
        IExternalCommandLinkPresenter presenter,
        IMessenger messenger,
        DispatcherQueue dispatcherQueue,
        ExternalCommandPermission reloadPermission)
    {
        _settingsService = settingsService;
        _permissionStore = permissionStore;
        _topLevelCommandManager = topLevelCommandManager;
        _presenter = presenter;
        _messenger = messenger;
        _dispatcherQueue = dispatcherQueue;
        _reloadPermission = reloadPermission;
        _lifetimeToken = _lifetimeCts.Token;
    }

    internal void Enqueue(CmdPalProtocolRoute route)
    {
        _ = _dispatcherQueue.TryEnqueue(() => EnqueueOnUiThread(route));
    }

    private void EnqueueOnUiThread(CmdPalProtocolRoute route)
    {
        if (_isDisposed)
        {
            return;
        }

        if (_queue.Count >= MaximumQueuedLinks)
        {
            Logger.LogWarning("Dropping an external command link because the consent queue is full.");
            return;
        }

        if (!_pendingLinks.Add(route))
        {
            Logger.LogInfo("Ignoring a duplicate external command link that is already being handled.");
            return;
        }

        _queue.Enqueue(route);
        _ = ProcessQueueAsync();
    }

    private async Task ProcessQueueAsync()
    {
        if (_isProcessing)
        {
            return;
        }

        _isProcessing = true;
        try
        {
            while (!_isDisposed && _queue.TryDequeue(out var route))
            {
                try
                {
                    await HandleLinkAsync(route);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to handle an external command link.", ex);
                }
                finally
                {
                    _pendingLinks.Remove(route);
                }
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private async Task HandleLinkAsync(CmdPalProtocolRoute route)
    {
        if (!_settingsService.Settings.EnableExternalCommandLinks)
        {
            Logger.LogInfo("Ignoring an external command link because command links are disabled.");
            return;
        }

        switch (route)
        {
            case CmdPalProtocolRoute.Reload:
                await HandleReloadAsync(route);
                break;

            case CmdPalProtocolRoute.ExecuteCommand executeCommand:
                await HandleCommandAsync(route, executeCommand);
                break;

            default:
                Logger.LogWarning("Ignoring a non-executable route sent to the external command link handler.");
                break;
        }
    }

    private async Task HandleReloadAsync(CmdPalProtocolRoute route)
    {
        var consentRequest = new ExternalCommandConsentRequest(
            route,
            _reloadPermission,
            IsPage: false,
            IsReload: true);

        if (await _permissionStore.IsAllowedAsync(_reloadPermission.Key) ||
            await RequestConsentAsync(consentRequest))
        {
            if (!_settingsService.Settings.EnableExternalCommandLinks)
            {
                return;
            }

            Logger.LogInfo("External command link triggered an extension reload.");
            _messenger.Send(new ReloadCommandsMessage());
        }
    }

    private async Task HandleCommandAsync(
        CmdPalProtocolRoute route,
        CmdPalProtocolRoute.ExecuteCommand executeCommand)
    {
        var authorization = await AuthorizeCommandAsync(route, executeCommand);
        if (authorization is not { } authorized)
        {
            return;
        }

        if (!_settingsService.Settings.EnableExternalCommandLinks)
        {
            return;
        }

        // Re-resolve so consent cannot transfer across a provider reload.
        using var refreshedResolution = await ResolveCommandAsync(executeCommand);
        if (_isDisposed)
        {
            return;
        }

        var refreshedCommand = refreshedResolution?.Command;
        var refreshedProvider = refreshedResolution?.Provider;
        var refreshedCommandViewModel = refreshedCommand?.CommandViewModel;
        if (refreshedCommand is null ||
            refreshedProvider is null ||
            refreshedCommandViewModel is null ||
            authorized.Permission.Key.PackageFamilyName != (refreshedProvider.Extension?.PackageFamilyName ?? string.Empty) ||
            refreshedCommandViewModel.IsPage != authorized.IsPage ||
            !CanExecute(refreshedCommandViewModel, executeCommand.ListPageOptions))
        {
            await _presenter.ShowUnavailableAsync(authorized.ShouldSummonForExecution);
            return;
        }

        var performMessage = refreshedCommand.GetPerformCommandMessage();
        performMessage.WithAnimation = false;
        performMessage.ShowWindowIfPage = authorized.ShouldSummonForExecution;
        performMessage.ListPageOptions = executeCommand.ListPageOptions;
        if (authorized.ShouldSummonForExecution)
        {
            performMessage.OnBeforeShowConfirmation = () =>
                _messenger.Send(new ShowWindowMessage(IntPtr.Zero));
        }

        // Avoid ResetToHome, which leaves a canceled root page on the back stack.
        _messenger.Send(performMessage);
    }

    private async Task<ExternalCommandAuthorization?> AuthorizeCommandAsync(
        CmdPalProtocolRoute route,
        CmdPalProtocolRoute.ExecuteCommand executeCommand)
    {
        using var resolutionCancellation = new CancellationTokenSource();
        var resolutionTask = ResolveCommandAsync(executeCommand, resolutionCancellation.Token);
        if (await Task.WhenAny(resolutionTask, Task.Delay(LoadingDelay, _lifetimeToken)) == resolutionTask)
        {
            using var resolution = await resolutionTask;
            return await AuthorizeResolvedCommandAsync(route, executeCommand, resolution, windowWasSummoned: false);
        }

        if (_isDisposed)
        {
            await CancelResolutionAsync(resolutionCancellation, resolutionTask);
            return null;
        }

        var dialogSession = await _presenter.PrepareLoadingAsync();
        if (dialogSession is null)
        {
            await CancelResolutionAsync(resolutionCancellation, resolutionTask);
            return null;
        }

        if (resolutionTask.IsCompleted)
        {
            await dialogSession.DisposeAsync();
            using var completedResolution = await resolutionTask;
            return await AuthorizeResolvedCommandAsync(route, executeCommand, completedResolution, windowWasSummoned: true);
        }

        await using (dialogSession)
        {
            var dialogCompletion = dialogSession.ShowLoadingAsync();
            if (await Task.WhenAny(resolutionTask, dialogCompletion) == dialogCompletion)
            {
                await CancelResolutionAsync(resolutionCancellation, resolutionTask);
                return null;
            }

            using var resolution = await resolutionTask;
            return await AuthorizeResolvedCommandAsync(
                route,
                executeCommand,
                resolution,
                windowWasSummoned: true,
                new ActiveExternalCommandDialog(dialogSession, dialogCompletion));
        }
    }

    private async Task<ExternalCommandAuthorization?> AuthorizeResolvedCommandAsync(
        CmdPalProtocolRoute route,
        CmdPalProtocolRoute.ExecuteCommand executeCommand,
        CommandResolution? resolution,
        bool windowWasSummoned,
        ActiveExternalCommandDialog? activeDialog = null)
    {
        if (_isDisposed)
        {
            return null;
        }

        if (activeDialog?.Completion.IsCompleted == true)
        {
            await activeDialog.Completion;
            return null;
        }

        if (resolution is null || !CanExecute(resolution.Command.CommandViewModel, executeCommand.ListPageOptions))
        {
            if (activeDialog is null)
            {
                await _presenter.ShowUnavailableAsync(!windowWasSummoned);
            }
            else
            {
                await activeDialog.Session.ShowUnavailableAsync();
            }

            return null;
        }

        var command = resolution.Command;
        var provider = resolution.Provider;
        var permission = new ExternalCommandPermission(
            new ExternalCommandPermissionKey(
                ExternalCommandKind.Command,
                provider.Extension?.PackageFamilyName ?? string.Empty,
                command.CommandProviderId,
                command.Id),
            string.IsNullOrWhiteSpace(command.Title) ? command.Id : command.Title,
            string.IsNullOrWhiteSpace(provider.DisplayName) ? command.CommandProviderId : provider.DisplayName);

        var isPage = command.CommandViewModel.IsPage;
        var canRemember = !(executeCommand.ListPageOptions?.RequiresOneTimeConsent ?? false);
        var isRemembered = canRemember && await _permissionStore.IsAllowedAsync(permission.Key);
        if (_isDisposed)
        {
            return null;
        }

        // The loading dialog has only a cancel action, so an early result rejects the request.
        if (activeDialog?.Completion.IsCompleted == true)
        {
            await activeDialog.Completion;
            return null;
        }

        if (!isRemembered)
        {
            var consentRequest = new ExternalCommandConsentRequest(
                route,
                permission,
                isPage,
                IsReload: false,
                executeCommand.ListPageOptions,
                canRemember,
                command.IconViewModel);
            var isAllowed = activeDialog is null
                ? await RequestConsentAsync(consentRequest, summonWindow: !windowWasSummoned)
                : await ApplyConsentResultAsync(
                    await activeDialog.Session.RequestConsentAsync(consentRequest),
                    permission,
                    canRemember);

            if (!isAllowed)
            {
                return null;
            }

            windowWasSummoned = true;
        }
        else if (activeDialog is not null)
        {
            await activeDialog.Session.CloseAsync();
        }

        return new ExternalCommandAuthorization(permission, isPage, !windowWasSummoned);
    }

    private static async Task CancelResolutionAsync(
        CancellationTokenSource cancellation,
        Task<CommandResolution?> resolutionTask)
    {
        cancellation.Cancel();
        using var resolution = await resolutionTask;
    }

    private static bool CanExecute(CommandViewModel command, ListPageLaunchOptions? options)
    {
        return (command.IsPage || command.IsInvokableCommand) &&
            (options is null || (!options.IsEmpty && command.IsListPage));
    }

    private async Task<CommandResolution?> ResolveCommandAsync(
        CmdPalProtocolRoute.ExecuteCommand route,
        CancellationToken cancellationToken = default)
    {
        using var resolutionCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeToken, cancellationToken);
        resolutionCts.CancelAfter(ResolutionTimeout);
        try
        {
            if (_topLevelCommandManager.IsLoading)
            {
                await _topLevelCommandManager.WaitForCurrentLoadAsync(resolutionCts.Token);
            }

            if (_isDisposed)
            {
                return null;
            }

            // Do not wait for late providers after the current load finishes.
            return await _topLevelCommandManager.ResolveCommandAsync(route.ProviderId, route.CommandId, resolutionCts.Token);
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested || cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("Timed out resolving an external command link.");
        }

        return null;
    }

    private async Task<bool> RequestConsentAsync(
        ExternalCommandConsentRequest request,
        bool summonWindow = true)
    {
        var result = await _presenter.RequestConsentAsync(request, summonWindow);
        return await ApplyConsentResultAsync(result, request.Permission, request.CanRemember);
    }

    private async Task<bool> ApplyConsentResultAsync(
        ExternalCommandConsentResult result,
        ExternalCommandPermission permission,
        bool canRemember)
    {
        if (result is ExternalCommandConsentResult.AllowOnce or ExternalCommandConsentResult.AlwaysAllow &&
            !_settingsService.Settings.EnableExternalCommandLinks)
        {
            return false;
        }

        if (result == ExternalCommandConsentResult.AlwaysAllow && canRemember)
        {
            if (!await _permissionStore.RememberAsync(permission))
            {
                Logger.LogWarning("The external command link permission could not be remembered; allowing this request once.");
            }

            return true;
        }

        return result == ExternalCommandConsentResult.AllowOnce;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _lifetimeCts.Cancel();
        _queue.Clear();
        _pendingLinks.Clear();
        _lifetimeCts.Dispose();
    }

    private sealed record ExternalCommandAuthorization(
        ExternalCommandPermission Permission,
        bool IsPage,
        bool ShouldSummonForExecution);

    private sealed record ActiveExternalCommandDialog(
        IExternalCommandLinkDialogSession Session,
        Task Completion);
}
