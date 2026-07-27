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
    private readonly JsonElement _providerMetadata;
    private readonly string _id;
    private readonly string _displayName;
    private readonly IIconInfo _icon;

    // Host status messages are tracked by their client-minted statusId so an
    // update to the same status refreshes the existing message in place instead
    // of creating a duplicate, and a hide targets exactly the right message.
    private readonly Dictionary<string, StatusMessage> _shownStatusMessages = new();

    // Guards _shownStatusMessages. Host status notifications and Dispose can run
    // on different threads, so every read, mutation and enumeration of the map
    // is serialized to avoid enumerating it while another thread mutates it.
    private readonly object _statusLock = new();
    private readonly ConcurrentDictionary<string, JSFallbackCommandItemAdapter> _fallbackAdapters = new();

    // Host notifications can arrive between this proxy subscribing (in the
    // constructor) and the host being attached via InitializeWithHost. Until the
    // host is attached they are buffered here in arrival order and replayed once
    // the host is bound, so a status or log raised during startup is not dropped.
    // A null buffer means the host is attached and notifications run inline.
    private readonly object _preInitLock = new();
    private List<BufferedHostNotification>? _preInitNotifications = new();
    private IExtensionHost? _host;
    private ICommandSettings? _settingsCache;
    private bool _settingsQueried;
    private bool _isDisposed;

    public JSCommandProviderProxy(JsonRpcConnection connection, JSExtensionManifest manifest, JsonElement providerMetadata = default)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _providerMetadata = providerMetadata;

        // The initialize handshake response carries the extension's declared
        // identity and icon. Prefer those when present so the palette reflects
        // what the extension reports at runtime, falling back to the static
        // package manifest values when the handshake omits a field.
        _id = ReadHandshakeString(providerMetadata, "id", "Id") ?? _manifest.Name ?? "unknown";
        _displayName = ReadHandshakeString(providerMetadata, "displayName", "DisplayName") ?? _manifest.EffectiveDisplayName;
        _icon = ReadHandshakeIcon(providerMetadata) ?? new IconInfo(_manifest.Icon ?? string.Empty);

        RegisterNotificationHandlers();
    }

    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged;

    public string Id => _id;

    public string DisplayName => _displayName;

    public IIconInfo Icon => _icon;

    // Whether the provider's top-level command set is fixed. The value is carried
    // from the initialize handshake metadata; the wire default is true when the
    // extension does not specify it.
    public bool Frozen => ReadFrozen(_providerMetadata);

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

        List<BufferedHostNotification> buffered;
        lock (_preInitLock)
        {
            _host = host;

            // Flip the buffer to null under the same lock that the notification
            // handlers use to decide whether to buffer. Writing _host before
            // clearing the buffer (and readers taking _preInitLock before reading
            // _host) guarantees a handler that runs inline observes the attached
            // host rather than a stale null.
            buffered = _preInitNotifications ?? new List<BufferedHostNotification>();
            _preInitNotifications = null;
        }

        Logger.LogDebug($"JSCommandProviderProxy initialized with host for {DisplayName}");

        // Replay the host notifications that arrived before the host was attached,
        // in their original arrival order, now that the host can receive them.
        foreach (var notification in buffered)
        {
            DispatchBufferedHostNotification(notification.Method, notification.Parameters);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        // Detach every notification handler this proxy registered so late
        // notifications from the connection are no longer routed here. Process
        // teardown and the protocol dispose request are owned by the extension
        // service, so this proxy only releases its own subscriptions and host
        // references. See the W4 coordination note in the remediation report.
        foreach (var method in RegisteredNotificationMethods)
        {
            _connection.UnregisterNotificationHandler(method);
        }

        // Hide any status messages that are still visible so a disposed provider
        // does not leave stale status in the host UI. Snapshot and clear the map
        // under the lock so a status notification racing dispose cannot mutate it
        // while it is being enumerated.
        var host = _host;
        List<StatusMessage> pendingStatuses;
        lock (_statusLock)
        {
            pendingStatuses = new List<StatusMessage>(_shownStatusMessages.Values);
            _shownStatusMessages.Clear();
        }

        if (host != null)
        {
            foreach (var status in pendingStatuses)
            {
                try
                {
                    _ = host.HideStatus(status);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error hiding status during dispose for {DisplayName}: {ex.Message}");
                }
            }
        }

        _host = null;
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

    // Buffers a host notification that arrived before InitializeWithHost attached
    // the host. Returns true when the notification was buffered (the caller must
    // stop) and false when the host is already attached and the caller should
    // handle it inline. The params element is cloned so the buffered copy stays
    // valid after the connection recycles the source document.
    private bool TryBufferUntilHostAttached(string method, JsonElement paramsElement)
    {
        lock (_preInitLock)
        {
            if (_preInitNotifications == null)
            {
                return false;
            }

            _preInitNotifications.Add(new BufferedHostNotification(method, paramsElement.Clone()));
            return true;
        }
    }

    // Replays a buffered host notification through its handler once the host is
    // attached. The gate is already open, so the handler runs inline instead of
    // buffering again.
    private void DispatchBufferedHostNotification(string method, JsonElement paramsElement)
    {
        switch (method)
        {
            case "host/showStatus":
                HandleShowStatusNotification(paramsElement);
                break;
            case "host/hideStatus":
                HandleHideStatusNotification(paramsElement);
                break;
            case "host/logMessage":
                HandleLogMessageNotification(paramsElement);
                break;
        }
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

        if (TryBufferUntilHostAttached("host/logMessage", paramsElement))
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

            if (_host != null)
            {
                var logMessage = new LogMessage { Message = message, State = (MessageState)state };
                _ = _host.LogMessage(logMessage);
            }
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

        if (TryBufferUntilHostAttached("host/showStatus", paramsElement))
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

            lock (_statusLock)
            {
                if (_isDisposed)
                {
                    return;
                }

                var host = _host;
                if (host == null)
                {
                    return;
                }

                if (!string.IsNullOrEmpty(statusId) &&
                    _shownStatusMessages.TryGetValue(statusId, out var existing))
                {
                    // Same status shown again: refresh it in place so the host
                    // keeps a single message rather than stacking duplicates.
                    existing.Message = message;
                    existing.State = (MessageState)state;
                    existing.Progress = progress;
                    return;
                }

                var statusMessage = new StatusMessage
                {
                    Message = message,
                    State = (MessageState)state,
                    Progress = progress,
                };

                if (!string.IsNullOrEmpty(statusId))
                {
                    _shownStatusMessages[statusId] = statusMessage;
                }

                // Dispatch the show while still holding the status lock. Dispose
                // hides every tracked status and must acquire this same lock, so
                // keeping the map insertion and the ShowStatus call atomic
                // guarantees a racing dispose either runs entirely before this
                // show (and the disposed guard above cancels it) or entirely
                // after (and hides the status the show has already dispatched).
                // Releasing the lock between the insertion and the call would let
                // dispose observe the status and hide it before it was ever shown.
                _ = host.ShowStatus(statusMessage, ReadStatusContext(paramsElement));
            }
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

        if (TryBufferUntilHostAttached("host/hideStatus", paramsElement))
        {
            return;
        }

        try
        {
            if (_host == null)
            {
                return;
            }

            var statusId = ReadStatusId(paramsElement);
            StatusMessage statusMessage;
            lock (_statusLock)
            {
                if (string.IsNullOrEmpty(statusId) ||
                    !_shownStatusMessages.TryGetValue(statusId, out statusMessage!))
                {
                    return;
                }

                _shownStatusMessages.Remove(statusId);
            }

            _ = _host.HideStatus(statusMessage);
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

        var progress = new ProgressState();

        if (JSModelMapper.TryGetAnyCase(progressProp, "isIndeterminate", "IsIndeterminate", out var indeterminateProp))
        {
            progress.IsIndeterminate = indeterminateProp.ValueKind == JsonValueKind.True;
        }

        if (JSModelMapper.TryGetAnyCase(progressProp, "progressPercent", "ProgressPercent", out var percentProp) &&
            percentProp.ValueKind == JsonValueKind.Number &&
            percentProp.TryGetUInt32(out var percent))
        {
            progress.ProgressPercent = percent;
        }

        return progress;
    }

    // Reads a non-empty string field (id or displayName) from the initialize
    // handshake metadata. Returns null when the field is absent or blank so the
    // caller falls back to the static package manifest value.
    private static string? ReadHandshakeString(JsonElement metadata, string camel, string pascal)
    {
        if (metadata.ValueKind == JsonValueKind.Object &&
            JSModelMapper.TryGetAnyCase(metadata, camel, pascal, out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            var value = prop.GetString();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        return null;
    }

    // Reads the icon declared in the initialize handshake metadata. Returns null
    // when the handshake omits an icon so the caller falls back to the manifest
    // icon rather than replacing it with an empty glyph.
    private static IIconInfo? ReadHandshakeIcon(JsonElement metadata)
    {
        if (metadata.ValueKind == JsonValueKind.Object &&
            JSModelMapper.TryGetIcon(metadata, "icon", "Icon", out var icon))
        {
            return icon;
        }

        return null;
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

    // An arrival-ordered snapshot of a host notification that reached this proxy
    // before the host was attached, held until InitializeWithHost replays it.
    private readonly record struct BufferedHostNotification(string Method, JsonElement Parameters);
}
