// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable SA1300 // Element should begin with upper-case letter (private event backing fields)
#pragma warning disable SA1516 // Elements should be separated by blank line

using System.Text.Json;
using System.Text.Json.Nodes;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Adapts JSON command data to ICommand and IInvokableCommand interfaces.
/// </summary>
internal sealed class JSCommandAdapter : ICommand, IInvokableCommand
{
    private readonly JsonElement _data;
    private readonly JsonRpcConnection _connection;
    private readonly Lock _eventLock = new();
    private event TypedEventHandler<object, IPropChangedEventArgs>? _propChanged;

    public JSCommandAdapter(JsonElement data, JsonRpcConnection connection)
    {
        _data = data;
        _connection = connection;
    }

    public string Name => GetStringProperty("displayName") ?? GetStringProperty("name") ?? string.Empty;

    public string Id => GetStringProperty("id") ?? string.Empty;

    public IIconInfo Icon => GetIconInfo();

    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add
        {
            lock (_eventLock)
            {
                _propChanged += value;
            }
        }

        remove
        {
            lock (_eventLock)
            {
                _propChanged -= value;
            }
        }
    }

    public void RaisePropChanged(string propertyName)
    {
        lock (_eventLock)
        {
            _propChanged?.Invoke(this, new PropChangedEventArgs(propertyName));
        }
    }

    public ICommandResult Invoke(object sender)
    {
        try
        {
            var response = _connection.SendRequestAsync(
                "command/invoke",
                new JsonObject { ["commandId"] = Id },
                CancellationToken.None).GetAwaiter().GetResult();

            if (response.Error != null)
            {
                Logger.LogError($"Command invoke error: {response.Error.Message}");
                return CommandResult.KeepOpen();
            }

            return ParseCommandResult(response.Result);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to invoke command {Id}: {ex.Message}");
            return CommandResult.KeepOpen();
        }
    }

    private string? GetStringProperty(string name)
    {
        if (_data.ValueKind == JsonValueKind.Object &&
            _data.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private IIconInfo GetIconInfo()
    {
        if (_data.ValueKind == JsonValueKind.Object &&
            _data.TryGetProperty("icon", out var iconProp))
        {
            return JSIconInfoAdapter.FromJson(iconProp);
        }

        return new IconInfo(string.Empty);
    }

    private static ICommandResult ParseCommandResult(JsonElement? result)
    {
        return JSCommandResultAdapter.ParseCommandResult(result);
    }
}

/// <summary>
/// Adapts JSON command item data to ICommandItem interface.
/// </summary>
internal sealed class JSCommandItemAdapter : ICommandItem
{
    private readonly JsonElement _data;
    private readonly JsonRpcConnection _connection;
    private readonly Lock _eventLock = new();
    private ICommand? _command;
    private event TypedEventHandler<object, IPropChangedEventArgs>? _propChanged;

    public JSCommandItemAdapter(JsonElement data, JsonRpcConnection connection)
    {
        _data = data;
        _connection = connection;
    }

    public ICommand Command
    {
        get
        {
            if (_command == null)
            {
                // The command data is nested inside the "command" property of the CommandItem
                var commandData = _data;
                if (_data.ValueKind == JsonValueKind.Object &&
                    _data.TryGetProperty("command", out var commandElement) &&
                    commandElement.ValueKind == JsonValueKind.Object)
                {
                    commandData = commandElement;
                }

                _command = JSCommandFactory.CreateCommandFromJson(commandData, _connection);
            }

            return _command;
        }
    }

    public IContextItem[] MoreCommands => [];

    public IIconInfo Icon => GetIconInfo();

    public string Title => GetStringProperty("displayName") ?? GetStringProperty("title") ?? string.Empty;

    public string Subtitle => GetStringProperty("description") ?? GetStringProperty("subtitle") ?? string.Empty;

    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add
        {
            lock (_eventLock)
            {
                _propChanged += value;
            }
        }

        remove
        {
            lock (_eventLock)
            {
                _propChanged -= value;
            }
        }
    }

    public void RaisePropChanged(string propertyName)
    {
        lock (_eventLock)
        {
            _propChanged?.Invoke(this, new PropChangedEventArgs(propertyName));
        }
    }

    private string? GetStringProperty(string name)
    {
        if (_data.ValueKind == JsonValueKind.Object &&
            _data.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private IIconInfo GetIconInfo()
    {
        if (_data.ValueKind == JsonValueKind.Object &&
            _data.TryGetProperty("icon", out var iconProp))
        {
            return JSIconInfoAdapter.FromJson(iconProp);
        }

        return new IconInfo(string.Empty);
    }
}

/// <summary>
/// Utility for creating the appropriate ICommand implementation from JSON data,
/// inspecting the _type discriminator to return page proxies for pages.
/// </summary>
internal static class JSCommandFactory
{
    internal static ICommand CreateCommandFromJson(JsonElement data, JsonRpcConnection connection)
    {
        if (data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("_type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
        {
            var commandId = string.Empty;
            if (data.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
            {
                commandId = idProp.GetString() ?? string.Empty;
            }

            var pageType = typeProp.GetString();

            if (pageType == "dynamicListPage")
            {
                return new JSDynamicListPageProxy(commandId, connection, data);
            }

            if (pageType == "listPage")
            {
                return new JSListPageProxy(commandId, connection, data);
            }

            if (pageType == "contentPage")
            {
                return new JSContentPageProxy(commandId, data, connection);
            }
        }

        return new JSCommandAdapter(data, connection);
    }
}

/// <summary>
/// Adapts JSON fallback command item data to IFallbackCommandItem interface.
/// </summary>
internal sealed class JSFallbackCommandItemAdapter : IFallbackCommandItem, IFallbackCommandItem2
{
    private readonly JsonElement _data;
    private readonly JsonRpcConnection _connection;
    private readonly Lock _eventLock = new();
    private ICommand? _command;
    private JSFallbackHandler? _fallbackHandler;
    private event TypedEventHandler<object, IPropChangedEventArgs>? _propChanged;

    public JSFallbackCommandItemAdapter(JsonElement data, JsonRpcConnection connection)
    {
        _data = data;
        _connection = connection;
    }

    public ICommand Command
    {
        get
        {
            if (_command == null)
            {
                var commandData = _data;
                if (_data.ValueKind == JsonValueKind.Object &&
                    _data.TryGetProperty("command", out var commandElement) &&
                    commandElement.ValueKind == JsonValueKind.Object)
                {
                    commandData = commandElement;
                }

                _command = JSCommandFactory.CreateCommandFromJson(commandData, _connection);
            }

            return _command;
        }
    }

    public IContextItem[] MoreCommands => [];

    public IIconInfo Icon => GetIconInfo();

    public string Title => GetStringProperty("displayName") ?? GetStringProperty("title") ?? string.Empty;

    public string Subtitle => GetStringProperty("description") ?? GetStringProperty("subtitle") ?? string.Empty;

    public string DisplayTitle => GetStringProperty("displayTitle") ?? Title;

    public string Id => GetStringProperty("id") ?? string.Empty;

    public IFallbackHandler FallbackHandler
    {
        get
        {
            if (_fallbackHandler == null)
            {
                _fallbackHandler = new JSFallbackHandler(_connection, Id);
            }

            return _fallbackHandler;
        }
    }

    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add
        {
            lock (_eventLock)
            {
                _propChanged += value;
            }
        }

        remove
        {
            lock (_eventLock)
            {
                _propChanged -= value;
            }
        }
    }

    public void RaisePropChanged(string propertyName)
    {
        lock (_eventLock)
        {
            _propChanged?.Invoke(this, new PropChangedEventArgs(propertyName));
        }
    }

    private string? GetStringProperty(string name)
    {
        if (_data.ValueKind == JsonValueKind.Object &&
            _data.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private IIconInfo GetIconInfo()
    {
        if (_data.ValueKind == JsonValueKind.Object &&
            _data.TryGetProperty("icon", out var iconProp))
        {
            return JSIconInfoAdapter.FromJson(iconProp);
        }

        return new IconInfo(string.Empty);
    }

    private sealed class JSFallbackHandler : IFallbackHandler
    {
        private readonly JsonRpcConnection _connection;
        private readonly string _commandId;

        public JSFallbackHandler(JsonRpcConnection connection, string commandId)
        {
            _connection = connection;
            _commandId = commandId;
        }

        public void UpdateQuery(string query)
        {
            try
            {
                _connection.SendNotificationAsync(
                    "fallback/updateQuery",
                    new JsonObject { ["commandId"] = _commandId, ["query"] = query },
                    CancellationToken.None).Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to send fallback query update: {ex.Message}");
            }
        }
    }
}

/// <summary>
/// Adapts JSON icon data to IIconInfo and IIconData interfaces.
/// </summary>
internal sealed class JSIconInfoAdapter : IIconInfo
{
    private readonly JSIconDataAdapter? _light;
    private readonly JSIconDataAdapter? _dark;

    private JSIconInfoAdapter(JSIconDataAdapter? light, JSIconDataAdapter? dark)
    {
        _light = light;
        _dark = dark;
    }

    public IIconData Light => _light ?? new JSIconDataAdapter(string.Empty, null);

    public IIconData Dark => _dark ?? new JSIconDataAdapter(string.Empty, null);

    public static JSIconInfoAdapter FromJson(JsonElement iconJson)
    {
#pragma warning disable CA1507 // JSON property names, not C# members
        JSIconDataAdapter? light = null;
        JSIconDataAdapter? dark = null;

        if (iconJson.TryGetProperty("Light", out var lightProp) || iconJson.TryGetProperty("light", out lightProp))
        {
            light = ParseIconData(lightProp);
        }

        if (iconJson.TryGetProperty("Dark", out var darkProp) || iconJson.TryGetProperty("dark", out darkProp))
        {
            dark = ParseIconData(darkProp);
        }

        // If only "icon"/"Icon" property exists, use it for both light and dark
        if (light == null && dark == null)
        {
            if (iconJson.TryGetProperty("Icon", out var iconProp) || iconJson.TryGetProperty("icon", out iconProp))
            {
                var iconData = ParseIconData(iconProp);
                light = iconData;
                dark = iconData;
            }
        }
#pragma warning restore CA1507

        return new JSIconInfoAdapter(light, dark);
    }

    private static JSIconDataAdapter? ParseIconData(JsonElement iconDataJson)
    {
#pragma warning disable CA1507 // JSON property names, not C# members
        string? iconPath = null;
        string? base64Data = null;

        if (iconDataJson.ValueKind == JsonValueKind.String)
        {
            iconPath = iconDataJson.GetString();
        }
        else if (iconDataJson.ValueKind == JsonValueKind.Object)
        {
            if ((iconDataJson.TryGetProperty("Icon", out var iconProp) || iconDataJson.TryGetProperty("icon", out iconProp)) &&
                iconProp.ValueKind == JsonValueKind.String)
            {
                iconPath = iconProp.GetString();
            }

            if ((iconDataJson.TryGetProperty("Data", out var dataProp) || iconDataJson.TryGetProperty("data", out dataProp)) &&
                dataProp.ValueKind == JsonValueKind.String)
            {
                base64Data = dataProp.GetString();
            }
        }
#pragma warning restore CA1507

        if (!string.IsNullOrEmpty(iconPath) || !string.IsNullOrEmpty(base64Data))
        {
            return new JSIconDataAdapter(iconPath ?? string.Empty, base64Data);
        }

        return null;
    }

    private sealed class JSIconDataAdapter : IIconData
    {
        private readonly string _icon;
        private readonly string? _base64Data;

        public JSIconDataAdapter(string icon, string? base64Data)
        {
            _icon = icon;
            _base64Data = base64Data;
        }

        public string Icon => _icon;

        public IRandomAccessStreamReference? Data
        {
            get
            {
                if (string.IsNullOrEmpty(_base64Data))
                {
                    return null;
                }

                // TODO: Convert base64 to IRandomAccessStreamReference if needed
                // For now, return null and rely on Icon path
                return null;
            }
        }
    }
}
