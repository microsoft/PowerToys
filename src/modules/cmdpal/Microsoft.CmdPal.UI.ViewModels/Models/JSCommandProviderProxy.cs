// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Presents a Node.js extension as an <see cref="ICommandProvider"/> by forwarding
/// provider calls over JSON-RPC. Fallback display titles, host status messages,
/// log messages and clipboard requests raised by the extension are handled here.
/// </summary>
public sealed partial class JSCommandProviderProxy : ICommandProvider, IDisposable
{
    private readonly JsonRpcConnection _connection;
    private readonly JSExtensionManifest _manifest;
    private readonly IconInfo _icon;

    // Guards the provider metadata, which is set once from the initialize handshake
    // after the proxy is constructed (the proxy is created before the handshake so its
    // notification handlers are registered in time to receive startup notifications).
    private readonly Lock _metadataLock = new();

    // Host status messages are tracked by their client-minted statusId so an
    // update to the same status refreshes the existing message in place instead
    // of creating a duplicate, and a hide targets exactly the right message.
    private readonly Dictionary<string, StatusMessage> _shownStatusMessages = new();
    private readonly ConcurrentDictionary<string, JSFallbackCommandItemAdapter> _fallbackAdapters = new();

    // Guards the host reference, the shown-status bookkeeping, and the buffer of host
    // actions emitted before the host is attached. Notifications an extension raises
    // while it activates (during the initialize handshake) arrive before the host is
    // set; they are buffered here and flushed in order once the host attaches so those
    // startup logs, statuses, and clipboard requests are not lost.
    private readonly Lock _hostLock = new();
    private readonly List<Action<IExtensionHost>> _pendingHostActions = [];

    private JsonElement _providerMetadata;
    private IExtensionHost? _host;
    private ICommandSettings? _settingsCache;
    private bool _settingsQueried;
    private bool _isDisposed;

    public JSCommandProviderProxy(JsonRpcConnection connection, JSExtensionManifest manifest, JsonElement providerMetadata = default)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _providerMetadata = providerMetadata;
        _icon = new IconInfo(_manifest.Icon ?? string.Empty);

        RegisterNotificationHandlers();

        // Clean up any active host statuses when the extension disconnects or its process
        // exits, not only when it explicitly hides them. The wrapper also disposes this
        // proxy during teardown; both paths are idempotent.
        _connection.Disconnected += OnConnectionDisconnected;
    }

    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged;

    public string Id => _manifest.Name ?? "unknown";

    public string DisplayName => _manifest.EffectiveDisplayName;

    public IIconInfo Icon => _icon;

    // Whether the provider's top-level command set is fixed. The value is carried
    // from the initialize handshake metadata; the wire default is true when the
    // extension does not specify it.
    public bool Frozen
    {
        get
        {
            lock (_metadataLock)
            {
                return ReadFrozen(_providerMetadata);
            }
        }
    }

    public ICommandSettings? Settings
    {
        get
        {
            if (_settingsQueried)
            {
                return _settingsCache;
            }

            _settingsQueried = true;

            try
            {
                var response = _connection.SendRequestAsync(
                    "provider/getSettings",
                    null,
                    CancellationToken.None).GetAwaiter().GetResult();

                if (response.Error != null ||
                    !response.Result.HasValue ||
                    response.Result.Value.ValueKind != JsonValueKind.Object)
                {
                    return _settingsCache;
                }

                var pageId = JSModelMapper.GetString(response.Result.Value, "id") ?? string.Empty;
                if (!string.IsNullOrEmpty(pageId))
                {
                    _settingsCache = new JSCommandSettingsProxy(pageId, _connection, response.Result.Value.Clone());
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Failed to get settings for {DisplayName}: {ex.Message}");
            }

            return _settingsCache;
        }
    }

    public ICommandItem[] TopLevelCommands()
    {
        try
        {
            var response = _connection.SendRequestAsync(
                "provider/getTopLevelCommands",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            if (response.Error != null)
            {
                Logger.LogError($"TopLevelCommands error: {response.Error.Message}");
                return [];
            }

            return ParseCommandItems(response.Result);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to get top-level commands: {ex.Message}");
            return [];
        }
    }

    public IFallbackCommandItem[]? FallbackCommands()
    {
        try
        {
            var response = _connection.SendRequestAsync(
                "provider/getFallbackCommands",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

            if (response.Error != null)
            {
                Logger.LogWarning($"FallbackCommands error: {response.Error.Message}");
                return null;
            }

            return ParseFallbackCommandItems(response.Result);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to get fallback commands: {ex.Message}");
            return null;
        }
    }

    public ICommand? GetCommand(string id)
    {
        try
        {
            var response = _connection.SendRequestAsync(
                "provider/getCommand",
                new JsonObject { ["commandId"] = id },
                CancellationToken.None).GetAwaiter().GetResult();

            if (response.Error != null)
            {
                Logger.LogWarning($"GetCommand error for {id}: {response.Error.Message}");
                return null;
            }

            if (!response.Result.HasValue || response.Result.Value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return JSCommandFactory.CreateCommandFromJson(response.Result.Value, _connection);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to get command {id}: {ex.Message}");
            return null;
        }
    }

    public void InitializeWithHost(IExtensionHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        List<Action<IExtensionHost>> pending;
        lock (_hostLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _host = host;
            pending = [.. _pendingHostActions];
            _pendingHostActions.Clear();
        }

        // Deliver, in arrival order, the host actions that the extension emitted while it
        // was activating (before the host was attached), such as startup logs, statuses,
        // and clipboard requests.
        foreach (var action in pending)
        {
            try
            {
                action(host);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error flushing buffered host action for {DisplayName}: {ex.Message}");
            }
        }

        Logger.LogDebug($"JSCommandProviderProxy initialized with host for {DisplayName}");
    }

    /// <summary>
    /// Sets the provider metadata captured from the initialize handshake so that the
    /// author-specified <see cref="Frozen"/> value flows through instead of the wire
    /// default. Called by the extension wrapper after the handshake completes.
    /// </summary>
    /// <param name="providerMetadata">The provider metadata returned during initialize.</param>
    internal void SetProviderMetadata(JsonElement providerMetadata)
    {
        lock (_metadataLock)
        {
            _providerMetadata = providerMetadata;
        }
    }

    /// <summary>
    /// Runs a host action now when the host is attached, or buffers it in arrival order to
    /// be replayed once <see cref="InitializeWithHost"/> attaches the host. This keeps
    /// notifications emitted during activation (before the host is set) from being dropped.
    /// </summary>
    private void RunWithHost(Action<IExtensionHost> action)
    {
        IExtensionHost? host;
        lock (_hostLock)
        {
            if (_isDisposed)
            {
                return;
            }

            host = _host;
            if (host is null)
            {
                _pendingHostActions.Add(action);
                return;
            }
        }

        action(host);
    }

    public void Dispose()
    {
        IExtensionHost? host;
        List<StatusMessage> activeStatuses;
        lock (_hostLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            host = _host;
            _host = null;
            _pendingHostActions.Clear();
            activeStatuses = [.. _shownStatusMessages.Values];
            _shownStatusMessages.Clear();
        }

        _connection.Disconnected -= OnConnectionDisconnected;

        // Detach every notification handler this proxy registered so late
        // notifications from the connection are no longer routed here. Process
        // teardown and the protocol dispose request are owned by the extension
        // service, so this proxy only releases its own subscriptions and host
        // references. See the W4 coordination note in the remediation report.
        foreach (var method in RegisteredNotificationMethods)
        {
            _connection.UnregisterNotificationHandler(method);
        }

        // Hide any status messages that are still visible so a disposed provider, or an
        // extension whose process has exited, does not leave stale status in the host UI.
        HideStatuses(host, activeStatuses);
    }

    private void OnConnectionDisconnected(object? sender, EventArgs e)
    {
        // The extension disconnected or its process exited. Clear any active statuses so
        // they do not linger in the host UI even though no explicit hide arrived. The
        // notification handlers stay registered; the connection is gone so they cannot
        // fire again, and Dispose still unregisters them during full teardown.
        IExtensionHost? host;
        List<StatusMessage> activeStatuses;
        lock (_hostLock)
        {
            if (_isDisposed || _shownStatusMessages.Count == 0)
            {
                return;
            }

            host = _host;
            activeStatuses = [.. _shownStatusMessages.Values];
            _shownStatusMessages.Clear();
        }

        HideStatuses(host, activeStatuses);
    }

    private void HideStatuses(IExtensionHost? host, List<StatusMessage> statuses)
    {
        if (host is null)
        {
            return;
        }

        foreach (var status in statuses)
        {
            try
            {
                _ = host.HideStatus(status);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error hiding status for {DisplayName}: {ex.Message}");
            }
        }
    }

    private static readonly string[] RegisteredNotificationMethods =
    [
        "provider/itemsChanged",
        "command/propChanged",
        "host/logMessage",
        "host/showStatus",
        "host/hideStatus",
        "host/copyText",
    ];

    private void RegisterNotificationHandlers()
    {
        _connection.RegisterNotificationHandler("provider/itemsChanged", HandleItemsChangedNotification);
        _connection.RegisterNotificationHandler("command/propChanged", HandleCommandPropChangedNotification);
        _connection.RegisterNotificationHandler("host/logMessage", HandleLogMessageNotification);
        _connection.RegisterNotificationHandler("host/showStatus", HandleShowStatusNotification);
        _connection.RegisterNotificationHandler("host/hideStatus", HandleHideStatusNotification);
        _connection.RegisterNotificationHandler("host/copyText", HandleCopyTextNotification);
    }

    private void HandleItemsChangedNotification(JsonElement paramsElement)
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            var totalItems = -1;
            if (paramsElement.ValueKind == JsonValueKind.Object &&
                paramsElement.TryGetProperty("totalItems", out var totalItemsProp) &&
                totalItemsProp.ValueKind == JsonValueKind.Number)
            {
                totalItems = totalItemsProp.GetInt32();
            }

            ItemsChanged?.Invoke(this, new ItemsChangedEventArgs(totalItems));
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error handling provider/itemsChanged notification: {ex.Message}");
        }
    }

    private void HandleCommandPropChangedNotification(JsonElement paramsElement)
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            var commandId = JSModelMapper.GetString(paramsElement, "commandId") ?? string.Empty;
            if (string.IsNullOrEmpty(commandId) ||
                !_fallbackAdapters.TryGetValue(commandId, out var fallbackAdapter))
            {
                return;
            }

            if (paramsElement.TryGetProperty("properties", out var propsProp) &&
                propsProp.ValueKind == JsonValueKind.Object)
            {
                var displayTitle = JSModelMapper.GetString(propsProp, "displayTitle");
                if (displayTitle != null)
                {
                    fallbackAdapter.UpdateDisplayTitle(displayTitle);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error handling command/propChanged notification: {ex.Message}");
        }
    }

    private void HandleLogMessageNotification(JsonElement paramsElement)
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            var message = JSModelMapper.GetString(paramsElement, "message");
            if (message == null)
            {
                return;
            }

            var state = ReadState(paramsElement);
            switch (state)
            {
                case 0:
                    Logger.LogInfo($"[{DisplayName}] {message}");
                    break;
                case 1:
                    Logger.LogInfo($"[{DisplayName}] {message}");
                    break;
                case 2:
                    Logger.LogWarning($"[{DisplayName}] {message}");
                    break;
                case 3:
                    Logger.LogError($"[{DisplayName}] {message}");
                    break;
                default:
                    Logger.LogInfo($"[{DisplayName}] {message}");
                    break;
            }

            var logMessage = new LogMessage { Message = message, State = (MessageState)state };
            RunWithHost(host => _ = host.LogMessage(logMessage));
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error handling host/logMessage notification: {ex.Message}");
        }
    }

    private void HandleShowStatusNotification(JsonElement paramsElement)
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            var (message, state) = ReadStatusMessage(paramsElement);
            if (message.Length == 0)
            {
                return;
            }

            var statusId = ReadStatusId(paramsElement);
            var progress = ReadProgress(paramsElement);
            var context = ReadStatusContext(paramsElement);

            StatusMessage statusMessage;
            lock (_hostLock)
            {
                if (_isDisposed)
                {
                    return;
                }

                if (!string.IsNullOrEmpty(statusId) &&
                    _shownStatusMessages.TryGetValue(statusId, out var existing))
                {
                    // Same status shown again: refresh it in place so the host keeps a
                    // single message rather than stacking duplicates. The buffered or
                    // already-delivered ShowStatus references this same object.
                    existing.Message = message;
                    existing.State = (MessageState)state;
                    existing.Progress = progress;
                    return;
                }

                statusMessage = new StatusMessage
                {
                    Message = message,
                    State = (MessageState)state,
                    Progress = progress,
                };

                if (!string.IsNullOrEmpty(statusId))
                {
                    _shownStatusMessages[statusId] = statusMessage;
                }
            }

            RunWithHost(host => _ = host.ShowStatus(statusMessage, context));
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error handling host/showStatus notification: {ex.Message}");
        }
    }

    private void HandleHideStatusNotification(JsonElement paramsElement)
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            var statusId = ReadStatusId(paramsElement);
            if (string.IsNullOrEmpty(statusId))
            {
                return;
            }

            StatusMessage? statusMessage = null;
            lock (_hostLock)
            {
                if (_isDisposed)
                {
                    return;
                }

                if (!_shownStatusMessages.TryGetValue(statusId, out var existing))
                {
                    return;
                }

                _shownStatusMessages.Remove(statusId);
                statusMessage = existing;
            }

            var toHide = statusMessage!;
            RunWithHost(host => _ = host.HideStatus(toHide));
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error handling host/hideStatus notification: {ex.Message}");
        }
    }

    private void HandleCopyTextNotification(JsonElement paramsElement)
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            var text = JSModelMapper.GetString(paramsElement, "text");
            if (text != null)
            {
                ClipboardHelper.SetText(text);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error handling host/copyText notification: {ex.Message}");
        }
    }

    private static int ReadState(JsonElement paramsElement)
    {
        if (paramsElement.ValueKind == JsonValueKind.Object &&
            JSModelMapper.TryGetAnyCase(paramsElement, "state", "State", out var stateProp) &&
            stateProp.ValueKind == JsonValueKind.Number)
        {
            return stateProp.GetInt32();
        }

        return 0;
    }

    private static string ReadStatusId(JsonElement paramsElement)
    {
        if (paramsElement.ValueKind == JsonValueKind.Object &&
            JSModelMapper.TryGetAnyCase(paramsElement, "statusId", "StatusId", out var idProp) &&
            idProp.ValueKind == JsonValueKind.String)
        {
            return idProp.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    // Maps a status progress payload (indeterminate spinner or a percentage) onto
    // a toolkit progress state. Returns null when no progress is reported.
    private static IProgressState? ReadProgress(JsonElement paramsElement)
    {
        if (paramsElement.ValueKind != JsonValueKind.Object ||
            !JSModelMapper.TryGetAnyCase(paramsElement, "progress", "Progress", out var progressProp) ||
            progressProp.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var isIndeterminate =
            JSModelMapper.TryGetAnyCase(progressProp, "isIndeterminate", "IsIndeterminate", out var indeterminateProp) &&
            indeterminateProp.ValueKind == JsonValueKind.True;

        var progress = new ProgressState { IsIndeterminate = isIndeterminate };

        if (JSModelMapper.TryGetAnyCase(progressProp, "progressPercent", "ProgressPercent", out var percentProp) &&
            percentProp.ValueKind == JsonValueKind.Number &&
            percentProp.TryGetDouble(out var percent) &&
            percent >= 0)
        {
            progress.ProgressPercent = percent >= uint.MaxValue ? uint.MaxValue : (uint)percent;
        }

        return progress;
    }

    private static bool ReadFrozen(JsonElement metadata)
    {
        if (metadata.ValueKind == JsonValueKind.Object &&
            JSModelMapper.TryGetAnyCase(metadata, "frozen", "Frozen", out var frozenProp))
        {
            if (frozenProp.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (frozenProp.ValueKind == JsonValueKind.True)
            {
                return true;
            }
        }

        // The wire default when the extension omits the flag is frozen.
        return true;
    }

    private static (string Message, int State) ReadStatusMessage(JsonElement paramsElement)
    {
        if (paramsElement.ValueKind != JsonValueKind.Object ||
            !paramsElement.TryGetProperty("message", out var messageProp))
        {
            return (string.Empty, 0);
        }

        if (messageProp.ValueKind == JsonValueKind.String)
        {
            return (messageProp.GetString() ?? string.Empty, 0);
        }

        if (messageProp.ValueKind == JsonValueKind.Object)
        {
            var text = JSModelMapper.GetString(messageProp, "message") ?? JSModelMapper.GetString(messageProp, "Message") ?? string.Empty;
            var state = ReadState(messageProp);
            return (text, state);
        }

        return (string.Empty, 0);
    }

    private static StatusContext ReadStatusContext(JsonElement paramsElement)
    {
        if (paramsElement.ValueKind == JsonValueKind.Object &&
            paramsElement.TryGetProperty("context", out var contextProp))
        {
            if (contextProp.ValueKind == JsonValueKind.Number)
            {
                return (StatusContext)contextProp.GetInt32();
            }

            if (contextProp.ValueKind == JsonValueKind.String && contextProp.GetString() == "page")
            {
                return StatusContext.Page;
            }
        }

        return StatusContext.Extension;
    }

    private ICommandItem[] ParseCommandItems(JsonElement? result)
    {
        if (!result.HasValue || result.Value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<ICommandItem>();
        foreach (var element in result.Value.EnumerateArray())
        {
            items.Add(new JSCommandItemAdapter(element, _connection));
        }

        return items.ToArray();
    }

    private IFallbackCommandItem[]? ParseFallbackCommandItems(JsonElement? result)
    {
        if (!result.HasValue || result.Value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = new List<IFallbackCommandItem>();
        foreach (var element in result.Value.EnumerateArray())
        {
            var adapter = new JSFallbackCommandItemAdapter(element, _connection);
            items.Add(adapter);

            var id = adapter.Id;
            if (!string.IsNullOrEmpty(id))
            {
                _fallbackAdapters[id] = adapter;
            }
        }

        return items.ToArray();
    }
}
