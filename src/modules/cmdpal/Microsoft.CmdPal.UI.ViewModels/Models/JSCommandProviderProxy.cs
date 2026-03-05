// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable SA1300 // Element should begin with upper-case letter (private event backing fields)
#pragma warning disable CS4014 // Because this call is not awaited

using System.Text.Json;
using System.Text.Json.Nodes;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Proxy that implements ICommandProvider by forwarding calls to a Node.js extension via JSON-RPC.
/// </summary>
public sealed class JSCommandProviderProxy : ICommandProvider, IDisposable
{
    private readonly JsonRpcConnection _rpcConnection;
    private readonly JSExtensionManifest _manifest;
    private readonly IconInfo _defaultIcon;
    private readonly Lock _eventLock = new();
    private readonly Dictionary<(string Message, int State), StatusMessage> _shownStatusMessages = new();
    private IExtensionHost? _host;
    private bool _isDisposed;
    private ICommandSettings? _settingsCache;
    private bool _settingsQueried;

    private event TypedEventHandler<object, IItemsChangedEventArgs>? _itemsChanged;

    public JSCommandProviderProxy(JsonRpcConnection rpcConnection, JSExtensionManifest manifest)
    {
        _rpcConnection = rpcConnection ?? throw new ArgumentNullException(nameof(rpcConnection));
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _defaultIcon = new IconInfo(string.Empty);

        RegisterNotificationHandlers();
    }

    public string Id => _manifest.Name ?? "unknown";

    public string DisplayName => _manifest.DisplayName ?? _manifest.Name ?? "Unknown";

    public IIconInfo Icon => _defaultIcon;

    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged
    {
        add
        {
            lock (_eventLock)
            {
                _itemsChanged += value;
            }
        }

        remove
        {
            lock (_eventLock)
            {
                _itemsChanged -= value;
            }
        }
    }

    public ICommandItem[] TopLevelCommands()
    {
        try
        {
            var response = _rpcConnection.SendRequestAsync(
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
            var response = _rpcConnection.SendRequestAsync(
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
            var response = _rpcConnection.SendRequestAsync(
                "provider/getCommand",
                new JsonObject { ["commandId"] = id },
                CancellationToken.None).GetAwaiter().GetResult();

            if (response.Error != null)
            {
                Logger.LogWarning($"GetCommand error for {id}: {response.Error.Message}");
                return null;
            }

            if (!response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            var data = response.Result.Value;

            // Check for page type discriminator to return the appropriate proxy
            if (data.TryGetProperty("_type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
            {
                var pageType = typeProp.GetString();
                if (pageType == "dynamicListPage")
                {
                    return new JSDynamicListPageProxy(id, _rpcConnection);
                }

                if (pageType == "listPage")
                {
                    return new JSListPageProxy(id, _rpcConnection);
                }

                if (pageType == "contentPage")
                {
                    return new JSContentPageProxy(id, _rpcConnection);
                }
            }

            return new JSCommandAdapter(data, _rpcConnection);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to get command {id}: {ex.Message}");
            return null;
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
                var response = _rpcConnection.SendRequestAsync(
                    "provider/getSettings",
                    null,
                    CancellationToken.None).GetAwaiter().GetResult();

                if (response.Error != null)
                {
                    Logger.LogDebug($"Settings not available for {DisplayName}: {response.Error.Message}");
                    return null;
                }

                if (!response.Result.HasValue || response.Result.Value.ValueKind == JsonValueKind.Null)
                {
                    return null;
                }

                var data = response.Result.Value;
                var pageId = string.Empty;

                if (data.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                {
                    pageId = idProp.GetString() ?? string.Empty;
                }

                if (!string.IsNullOrEmpty(pageId))
                {
                    _settingsCache = new JSCommandSettingsProxy(pageId, _rpcConnection);
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Failed to get settings for {DisplayName}: {ex.Message}");
            }

            return _settingsCache;
        }
    }

    public bool Frozen => true;

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

        lock (_eventLock)
        {
            _itemsChanged = null;
        }

        _host = null;
    }

    private void RegisterNotificationHandlers()
    {
        _rpcConnection.RegisterNotificationHandler("provider/itemsChanged", HandleItemsChangedNotification);
        _rpcConnection.RegisterNotificationHandler("command/propChanged", HandleCommandPropChangedNotification);
        _rpcConnection.RegisterNotificationHandler("page/propChanged", HandlePagePropChangedNotification);
        _rpcConnection.RegisterNotificationHandler("content/propChanged", HandleContentPropChangedNotification);
        _rpcConnection.RegisterNotificationHandler("provider/propChanged", HandleProviderPropChangedNotification);
        _rpcConnection.RegisterNotificationHandler("host/logMessage", HandleLogMessageNotification);
        _rpcConnection.RegisterNotificationHandler("host/showStatus", HandleShowStatusNotification);
        _rpcConnection.RegisterNotificationHandler("host/hideStatus", HandleHideStatusNotification);
    }

    private void HandleItemsChangedNotification(JsonElement paramsElement)
    {
        try
        {
            var totalItems = -1;
            if (paramsElement.TryGetProperty("totalItems", out var totalItemsProp))
            {
                totalItems = totalItemsProp.GetInt32();
            }

            lock (_eventLock)
            {
                _itemsChanged?.Invoke(this, new ItemsChangedEventArgs(totalItems));
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error handling itemsChanged notification: {ex.Message}");
        }
    }

    private void HandleCommandPropChangedNotification(JsonElement paramsElement)
    {
        try
        {
            var commandId = string.Empty;
            if (paramsElement.TryGetProperty("commandId", out var commandIdProp))
            {
                commandId = commandIdProp.GetString() ?? string.Empty;
            }

            var propertyName = string.Empty;
            if (paramsElement.TryGetProperty("propertyName", out var propertyNameProp))
            {
                propertyName = propertyNameProp.GetString() ?? string.Empty;
            }

            Logger.LogDebug($"[{DisplayName}] command/propChanged: commandId={commandId}, property={propertyName}");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error handling command/propChanged notification: {ex.Message}");
        }
    }

    private void HandlePagePropChangedNotification(JsonElement paramsElement)
    {
        try
        {
            var pageId = string.Empty;
            if (paramsElement.TryGetProperty("pageId", out var pageIdProp))
            {
                pageId = pageIdProp.GetString() ?? string.Empty;
            }

            var propertyName = string.Empty;
            if (paramsElement.TryGetProperty("propertyName", out var propertyNameProp))
            {
                propertyName = propertyNameProp.GetString() ?? string.Empty;
            }

            Logger.LogDebug($"[{DisplayName}] page/propChanged: pageId={pageId}, property={propertyName}");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error handling page/propChanged notification: {ex.Message}");
        }
    }

    private void HandleContentPropChangedNotification(JsonElement paramsElement)
    {
        try
        {
            var contentId = string.Empty;
            if (paramsElement.TryGetProperty("contentId", out var contentIdProp))
            {
                contentId = contentIdProp.GetString() ?? string.Empty;
            }

            var propertyName = string.Empty;
            if (paramsElement.TryGetProperty("propertyName", out var propertyNameProp))
            {
                propertyName = propertyNameProp.GetString() ?? string.Empty;
            }

            Logger.LogDebug($"[{DisplayName}] content/propChanged: contentId={contentId}, property={propertyName}");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error handling content/propChanged notification: {ex.Message}");
        }
    }

    private void HandleProviderPropChangedNotification(JsonElement paramsElement)
    {
        try
        {
            var propertyName = string.Empty;
            if (paramsElement.TryGetProperty("propertyName", out var propertyNameProp))
            {
                propertyName = propertyNameProp.GetString() ?? string.Empty;
            }

            Logger.LogDebug($"[{DisplayName}] provider/propChanged: property={propertyName}");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error handling provider/propChanged notification: {ex.Message}");
        }
    }

    private void HandleLogMessageNotification(JsonElement paramsElement)
    {
        try
        {
            if (!paramsElement.TryGetProperty("message", out var messageProp))
            {
                return;
            }

            var message = messageProp.GetString() ?? string.Empty;
            var state = 2; // Default to Info

            if (paramsElement.TryGetProperty("state", out var stateProp))
            {
                state = stateProp.GetInt32();
            }

            // Map MessageState enum: 0=Trace, 1=Debug, 2=Info, 3=Warning, 4=Error
            switch (state)
            {
                case 0:
                case 1:
                    Logger.LogDebug($"[{DisplayName}] {message}");
                    break;
                case 2:
                    Logger.LogInfo($"[{DisplayName}] {message}");
                    break;
                case 3:
                    Logger.LogWarning($"[{DisplayName}] {message}");
                    break;
                case 4:
                    Logger.LogError($"[{DisplayName}] {message}");
                    break;
                default:
                    Logger.LogInfo($"[{DisplayName}] {message}");
                    break;
            }

            // Forward to host if available
            if (_host != null)
            {
                var logMessage = new LogMessage { Message = message, State = (MessageState)state };
                _host.LogMessage(logMessage);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error handling logMessage notification: {ex.Message}");
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

            if (!paramsElement.TryGetProperty("message", out var messageProp))
            {
                return;
            }

            // The TypeScript side sends message as an object: { Message, State, Progress }
            var message = string.Empty;
            var state = 2; // Default to Info

            if (messageProp.ValueKind == JsonValueKind.Object)
            {
                if (messageProp.TryGetProperty("Message", out var msgText))
                {
                    message = msgText.GetString() ?? string.Empty;
                }

                if (messageProp.TryGetProperty("State", out var msgState))
                {
                    state = msgState.GetInt32();
                }
            }
            else if (messageProp.ValueKind == JsonValueKind.String)
            {
                message = messageProp.GetString() ?? string.Empty;
            }

            var statusMessage = new StatusMessage
            {
                Message = message,
                State = (MessageState)state,
            };

            // Track the reference so hideStatus can find the same object later
            _shownStatusMessages[(message, state)] = statusMessage;

            // context is sent as an int enum: 0=Page, 1=Extension
            var context = StatusContext.Extension;
            if (paramsElement.TryGetProperty("context", out var contextProp))
            {
                if (contextProp.ValueKind == JsonValueKind.Number)
                {
                    context = (StatusContext)contextProp.GetInt32();
                }
                else if (contextProp.ValueKind == JsonValueKind.String)
                {
                    var contextStr = contextProp.GetString();
                    if (contextStr == "page")
                    {
                        context = StatusContext.Page;
                    }
                }
            }

            _host.ShowStatus(statusMessage, context);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error handling showStatus notification: {ex.Message}");
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

            var message = string.Empty;
            var state = 2; // Default to Info

            if (paramsElement.TryGetProperty("message", out var messageProp) &&
                messageProp.ValueKind == JsonValueKind.Object)
            {
                if (messageProp.TryGetProperty("Message", out var msgText))
                {
                    message = msgText.GetString() ?? string.Empty;
                }

                if (messageProp.TryGetProperty("State", out var msgState))
                {
                    state = msgState.GetInt32();
                }
            }

            // Look up the original StatusMessage reference so reference equality works
            var key = (message, state);
            if (_shownStatusMessages.TryGetValue(key, out var originalMessage))
            {
                _shownStatusMessages.Remove(key);
                _host.HideStatus(originalMessage);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error handling hideStatus notification: {ex.Message}");
        }
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
            items.Add(new JSCommandItemAdapter(element, _rpcConnection));
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
            items.Add(new JSFallbackCommandItemAdapter(element, _rpcConnection));
        }

        return items.ToArray();
    }
}
