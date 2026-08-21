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
/// Lets Command Palette treat a Node.js extension as an <see cref="ICommandProvider"/>.
/// Provider calls go over JSON-RPC. Fallback titles, host status, log messages,
/// and clipboard requests from the extension are handled here.
/// </summary>
public sealed partial class JSCommandProviderProxy : ICommandProvider4, IDisposable
{
    private readonly JsonRpcConnection _connection;
    private readonly JSExtensionManifest _manifest;
    private readonly JsonElement _providerMetadata;
    private readonly string _id;
    private readonly string _displayName;
    private readonly IIconInfo _icon;

    // Host status messages use the statusId minted by the client. That lets an
    // update refresh the same message and lets hide target the right one.
    private readonly Dictionary<string, StatusMessage> _shownStatusMessages = new();

    // Guards _shownStatusMessages. Host status notifications and Dispose can run
    // on different threads, so reads, writes, and enumeration share one gate.
    private readonly object _statusLock = new();
    private readonly ConcurrentDictionary<string, JSFallbackCommandItemAdapter> _fallbackAdapters = new();

    // Host notifications can arrive after this proxy subscribes but before
    // InitializeWithHost attaches the host. Buffer them in arrival order so
    // startup status and log messages are not dropped. A null buffer means the
    // host is attached and notifications can run inline.
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

        // Prefer the identity and icon from the initialize handshake when they are
        // present. If the extension omits a field, use the package manifest value.
        _id = ReadHandshakeString(providerMetadata, "id", "Id") ?? _manifest.Name ?? "unknown";
        _displayName = ReadHandshakeString(providerMetadata, "displayName", "DisplayName") ?? _manifest.EffectiveDisplayName;
        _icon = ReadHandshakeIcon(providerMetadata) ?? new IconInfo(_manifest.Icon ?? string.Empty);

        RegisterNotificationHandlers();
    }

    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged;

    public string Id => _id;

    public string DisplayName => _displayName;

    public IIconInfo Icon => _icon;

    // True means the provider's top-level command set is fixed. If the extension
    // leaves it out of the handshake, the wire default is true.
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

    public ICommandItem? GetCommandItem(string id)
    {
        try
        {
            var response = _connection.SendRequestAsync(
                "provider/getCommandItem",
                new JsonObject { ["commandId"] = id },
                CancellationToken.None).GetAwaiter().GetResult();

            if (response.Error != null)
            {
                Logger.LogWarning($"GetCommandItem error for {id}: {response.Error.Message}");
                return null;
            }

            if (!response.Result.HasValue || response.Result.Value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new JSCommandItemAdapter(response.Result.Value, _connection);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to get command item {id}: {ex.Message}");
            return null;
        }
    }

    public object[] GetApiExtensionStubs() => [];

    public ICommandItem[]? GetDockBands() => null;

    public void InitializeWithHost(IExtensionHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        List<BufferedHostNotification> buffered;
        lock (_preInitLock)
        {
            _host = host;

            // Swap the buffer to null under the same lock used by notification handlers.
            // _host is written first so any handler that now runs inline sees the host,
            // not a stale null.
            buffered = _preInitNotifications ?? new List<BufferedHostNotification>();
            _preInitNotifications = null;
        }

        Logger.LogDebug($"JSCommandProviderProxy initialized with host for {DisplayName}");

        // Replay startup notifications in arrival order now that the host can receive them.
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

        // Detach this proxy's handlers so late connection notifications stop here.
        // The extension service owns process teardown and protocol dispose, so this
        // proxy only releases its subscriptions and host references.
        foreach (var method in RegisteredNotificationMethods)
        {
            _connection.UnregisterNotificationHandler(method);
        }

        // Hide any status messages still on screen. Snapshot and clear under the lock
        // so a status notification racing Dispose cannot change the map during enumeration.
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

    // Buffers a host notification until InitializeWithHost attaches the host.
    // The params element is cloned because the connection may recycle the source document.
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

    // Runs the buffered notification now that the host is attached. The open gate
    // keeps this pass from buffering the same notification again.
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
            JSPropertyChangeRegistry.Dispatch(_connection, paramsElement);

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
                    // Same statusId again. Update the existing message instead of
                    // stacking another one.
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

                // Keep the map update and ShowStatus under one lock. Dispose uses
                // this same lock to hide tracked statuses, so it either runs before
                // this show starts or after the shown status is tracked. Releasing
                // the lock between those steps would let Dispose hide a status that
                // was not shown yet.
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

    // Turns the wire progress payload into the toolkit shape. Null means no
    // progress was reported.
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

    // Blank or missing handshake fields fall back to the package manifest value.
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

    // Missing handshake icons fall back to the manifest icon, not an empty glyph.
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

        // The wire default is frozen when the extension leaves the flag out.
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

            if (contextProp.ValueKind == JsonValueKind.String)
            {
                return contextProp.GetString() switch
                {
                    "page" => StatusContext.Page,
                    "extension" => StatusContext.Extension,
                    _ => StatusContext.Extension,
                };
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
