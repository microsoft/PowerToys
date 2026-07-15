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
    private readonly Dictionary<(string Message, int State), StatusMessage> _shownStatusMessages = new();
    private readonly ConcurrentDictionary<string, JSFallbackCommandItemAdapter> _fallbackAdapters = new();
    private IExtensionHost? _host;
    private ICommandSettings? _settingsCache;
    private bool _settingsQueried;
    private bool _isDisposed;

    public JSCommandProviderProxy(JsonRpcConnection connection, JSExtensionManifest manifest)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _icon = new IconInfo(_manifest.Icon ?? string.Empty);

        RegisterNotificationHandlers();
    }

    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged;

    public string Id => _manifest.Name ?? "unknown";

    public string DisplayName => _manifest.EffectiveDisplayName;

    public IIconInfo Icon => _icon;

    public bool Frozen => true;

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
                    _settingsCache = new JSCommandSettingsProxy(pageId, _connection);
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
        _host = host ?? throw new ArgumentNullException(nameof(host));
        Logger.LogDebug($"JSCommandProviderProxy initialized with host for {DisplayName}");
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _host = null;
    }

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
        try
        {
            if (_host == null)
            {
                return;
            }

            var (message, state) = ReadStatusMessage(paramsElement);
            if (message.Length == 0)
            {
                return;
            }

            var statusMessage = new StatusMessage
            {
                Message = message,
                State = (MessageState)state,
                Progress = ReadProgress(paramsElement),
            };

            _shownStatusMessages[(message, state)] = statusMessage;
            _ = _host.ShowStatus(statusMessage, ReadStatusContext(paramsElement));
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error handling host/showStatus notification: {ex.Message}");
        }
    }

    private void HandleHideStatusNotification(JsonElement paramsElement)
    {
        try
        {
            if (_host == null)
            {
                return;
            }

            var (message, state) = ReadStatusMessage(paramsElement);
            var key = (message, state);
            if (_shownStatusMessages.TryGetValue(key, out var originalMessage))
            {
                _shownStatusMessages.Remove(key);
                _ = _host.HideStatus(originalMessage);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error handling host/hideStatus notification: {ex.Message}");
        }
    }

    private void HandleCopyTextNotification(JsonElement paramsElement)
    {
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
        if (JSModelMapper.TryGetAnyCase(paramsElement, "state", "State", out var stateProp) &&
            stateProp.ValueKind == JsonValueKind.Number)
        {
            return stateProp.GetInt32();
        }

        return 0;
    }

    private static IProgressState? ReadProgress(JsonElement paramsElement)
    {
        if (!JSModelMapper.TryGetAnyCase(paramsElement, "progress", "Progress", out var progressProp) ||
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
