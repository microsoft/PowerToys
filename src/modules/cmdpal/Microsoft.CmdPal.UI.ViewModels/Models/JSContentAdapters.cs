// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable SA1402 // File may only contain a single type - closely related content adapters grouped together
#pragma warning disable SA1649 // File name should match first type name

using System.Text.Json;
using System.Text.Json.Nodes;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Static helper for parsing IContent arrays and enhanced ICommandResult
/// including GoToPage, ShowToast, and Confirm args.
/// </summary>
internal static class JSCommandResultAdapter
{
    internal static ICommandResult ParseCommandResult(JsonElement? result, JsonRpcConnection? connection = null)
    {
        if (!result.HasValue || result.Value.ValueKind == JsonValueKind.Null)
        {
            return CommandResult.Dismiss();
        }

        var kindValue = 0;
        if (result.Value.TryGetProperty("Kind", out var kindProp) ||
            result.Value.TryGetProperty("kind", out kindProp))
        {
            kindValue = kindProp.GetInt32();
        }

        var kind = (CommandResultKind)kindValue;

        var hasArgs = result.Value.TryGetProperty("Args", out var argsProp) ||
                      result.Value.TryGetProperty("args", out argsProp);
        if (hasArgs && argsProp.ValueKind == JsonValueKind.Object)
        {
            if (kind == CommandResultKind.GoToPage)
            {
                return ParseGoToPage(argsProp, connection);
            }

            if (kind == CommandResultKind.ShowToast)
            {
                return ParseShowToast(argsProp);
            }

            if (kind == CommandResultKind.Confirm)
            {
                return ParseConfirm(argsProp, connection);
            }
        }

        if (kind == CommandResultKind.GoHome)
        {
            return CommandResult.GoHome();
        }

        if (kind == CommandResultKind.GoBack)
        {
            return CommandResult.GoBack();
        }

        if (kind == CommandResultKind.Hide)
        {
            return CommandResult.Hide();
        }

        if (kind == CommandResultKind.KeepOpen)
        {
            return CommandResult.KeepOpen();
        }

        return CommandResult.Dismiss();
    }

    internal static IContent[] ParseContentArray(JsonElement? result, string pageId, JsonRpcConnection connection)
    {
        if (!result.HasValue || result.Value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var count = result.Value.GetArrayLength();
        var items = new IContent[count];
        var i = 0;
        foreach (var item in result.Value.EnumerateArray())
        {
            items[i++] = ParseContentItem(item, pageId, connection);
        }

        return items;
    }

    internal static IContent ParseContentItem(JsonElement element, string pageId, JsonRpcConnection connection)
    {
        var type = string.Empty;
        if (element.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
        {
            type = typeProp.GetString() ?? string.Empty;
        }

        if (type == "form")
        {
            return new JSFormContentProxy(pageId, element, connection);
        }

        if (type == "tree")
        {
            return new JSTreeContentAdapter(element, pageId, connection);
        }

        if (type == "plainText")
        {
            return new JSPlainTextContentAdapter(element);
        }

        if (type == "image")
        {
            return new JSImageContentAdapter(element);
        }

        return new JSMarkdownContentAdapter(element);
    }

    internal static OptionalColor ParseOptionalColor(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        if (!element.TryGetProperty("hasValue", out var hasValueProp) || hasValueProp.ValueKind != JsonValueKind.True)
        {
            return default;
        }

        if (!element.TryGetProperty("color", out var colorProp) || colorProp.ValueKind != JsonValueKind.String)
        {
            return default;
        }

        var hex = colorProp.GetString();
        if (string.IsNullOrEmpty(hex) || hex[0] != '#')
        {
            return default;
        }

        try
        {
            if (hex.Length == 7)
            {
                var r = Convert.ToByte(hex.Substring(1, 2), 16);
                var g = Convert.ToByte(hex.Substring(3, 2), 16);
                var b = Convert.ToByte(hex.Substring(5, 2), 16);
                return ColorHelpers.FromRgb(r, g, b);
            }

            if (hex.Length == 9)
            {
                var a = Convert.ToByte(hex.Substring(1, 2), 16);
                var r = Convert.ToByte(hex.Substring(3, 2), 16);
                var g = Convert.ToByte(hex.Substring(5, 2), 16);
                var b = Convert.ToByte(hex.Substring(7, 2), 16);
                return ColorHelpers.FromArgb(a, r, g, b);
            }
        }
        catch
        {
            // Invalid hex format; fall through to default
        }

        return default;
    }

    private static ICommandResult ParseGoToPage(JsonElement args, JsonRpcConnection? connection)
    {
        var pageId = string.Empty;
        if ((args.TryGetProperty("PageId", out var pageIdProp) || args.TryGetProperty("pageId", out pageIdProp)) &&
            pageIdProp.ValueKind == JsonValueKind.String)
        {
            pageId = pageIdProp.GetString() ?? string.Empty;
        }

        var navMode = NavigationMode.Push;
        if (args.TryGetProperty("Mode", out var navModeProp) || args.TryGetProperty("mode", out navModeProp) || args.TryGetProperty("navigationMode", out navModeProp))
        {
            if (navModeProp.ValueKind == JsonValueKind.Number)
            {
                navMode = (NavigationMode)navModeProp.GetInt32();
            }
            else if (navModeProp.ValueKind == JsonValueKind.String)
            {
                var modeStr = navModeProp.GetString()?.ToLowerInvariant();
                navMode = modeStr switch
                {
                    "push" => NavigationMode.Push,
                    "goback" or "goBack" => NavigationMode.GoBack,
                    "gohome" or "goHome" => NavigationMode.GoHome,
                    _ => NavigationMode.Push,
                };
            }
        }

        // Resolve the page via JSONRPC if we have a connection and a pageId
        IPage? resolvedPage = null;
        if (connection != null && !string.IsNullOrEmpty(pageId))
        {
            try
            {
                var response = connection.SendRequestAsync(
                    "provider/getCommand",
                    new JsonObject { ["commandId"] = pageId },
                    CancellationToken.None).GetAwaiter().GetResult();

                if (response.Error == null && response.Result.HasValue)
                {
                    var command = JSCommandFactory.CreateCommandFromJson(response.Result.Value, connection);
                    if (command is IPage page)
                    {
                        resolvedPage = page;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to resolve page '{pageId}': {ex.Message}");
            }
        }

        return new JSGoToPageCommandResult
        {
            Kind = CommandResultKind.GoToPage,
            Args = new GoToPageArgs { PageId = pageId, NavigationMode = navMode },
            Page = resolvedPage,
        };
    }

    private static ICommandResult ParseShowToast(JsonElement args)
    {
        string? message = null;
        if ((args.TryGetProperty("Message", out var messageProp) || args.TryGetProperty("message", out messageProp)) &&
            messageProp.ValueKind == JsonValueKind.String)
        {
            message = messageProp.GetString();
        }

        return CommandResult.ShowToast(new ToastArgs { Message = message });
    }

    private static ICommandResult ParseConfirm(JsonElement args, JsonRpcConnection? connection)
    {
        string? title = null;
        string? description = null;
        ICommand? primaryCommand = null;
        var isCritical = false;

        if ((args.TryGetProperty("Title", out var titleProp) || args.TryGetProperty("title", out titleProp)) &&
            titleProp.ValueKind == JsonValueKind.String)
        {
            title = titleProp.GetString();
        }

        if ((args.TryGetProperty("Description", out var descProp) || args.TryGetProperty("description", out descProp)) &&
            descProp.ValueKind == JsonValueKind.String)
        {
            description = descProp.GetString();
        }

        if (args.TryGetProperty("IsPrimaryCommandCritical", out var criticalProp) ||
            args.TryGetProperty("isPrimaryCommandCritical", out criticalProp))
        {
            isCritical = criticalProp.ValueKind == JsonValueKind.True;
        }

        if (connection != null &&
            (args.TryGetProperty("PrimaryCommand", out var cmdProp) || args.TryGetProperty("primaryCommand", out cmdProp)) &&
            cmdProp.ValueKind == JsonValueKind.Object)
        {
            primaryCommand = new JSCommandAdapter(cmdProp, connection);
        }

        return CommandResult.Confirm(new ConfirmationArgs
        {
            Title = title,
            Description = description,
            PrimaryCommand = primaryCommand,
            IsPrimaryCommandCritical = isCritical,
        });
    }
}

/// <summary>
/// Custom ICommandResult for GoToPage that carries a pre-resolved IPage reference.
/// This allows UnsafeHandleCommandResult to navigate directly to the page
/// without needing access to the JsonRpcConnection.
/// </summary>
internal sealed class JSGoToPageCommandResult : ICommandResult
{
    public CommandResultKind Kind { get; init; }

    public ICommandResultArgs? Args { get; init; }

    public IPage? Page { get; init; }
}

/// <summary>
/// Adapts JSON content page data to IContentPage interface.
/// </summary>
internal sealed class JSContentPageProxy : IContentPage
{
    private readonly string _pageId;
    private readonly JsonElement _data;
    private readonly JsonRpcConnection _connection;

    public JSContentPageProxy(string pageId, JsonElement data, JsonRpcConnection connection)
    {
        _pageId = pageId;
        _data = data;
        _connection = connection;
    }

    public JSContentPageProxy(string pageId, JsonRpcConnection connection)
    {
        _pageId = pageId;
        _data = default;
        _connection = connection;
    }

    public string Name => GetStringProperty("name") ?? string.Empty;

    public string Id => _pageId;

    public IIconInfo Icon => GetIconInfo();

    public string Title => GetStringProperty("title") ?? string.Empty;

    public bool IsLoading
    {
        get
        {
            if (_data.ValueKind == JsonValueKind.Object &&
                _data.TryGetProperty("isLoading", out var prop))
            {
                return prop.ValueKind == JsonValueKind.True;
            }

            return false;
        }
    }

    public OptionalColor AccentColor
    {
        get
        {
            if (_data.ValueKind == JsonValueKind.Object &&
                _data.TryGetProperty("accentColor", out var prop))
            {
                return JSCommandResultAdapter.ParseOptionalColor(prop);
            }

            return default;
        }
    }

    public IDetails Details
    {
        get
        {
            if (_data.ValueKind == JsonValueKind.Object &&
                _data.TryGetProperty("details", out var detailsProp) && detailsProp.ValueKind == JsonValueKind.Object)
            {
                return new JSDetailsAdapter(detailsProp);
            }

            return null!;
        }
    }

    public IContextItem[] Commands
    {
        get
        {
            if (_data.ValueKind == JsonValueKind.Object &&
                _data.TryGetProperty("commands", out var commandsProp) && commandsProp.ValueKind == JsonValueKind.Array)
            {
                var count = commandsProp.GetArrayLength();
                var items = new IContextItem[count];
                var i = 0;
                foreach (var item in commandsProp.EnumerateArray())
                {
                    items[i++] = new JSContextItemAdapter(item);
                }

                return items;
            }

            return [];
        }
    }

    public IContent[] GetContent()
    {
        try
        {
            var response = _connection.SendRequestAsync(
                "contentPage/getContent",
                new JsonObject { ["pageId"] = _pageId },
                CancellationToken.None).GetAwaiter().GetResult();

            if (response.Error != null)
            {
                Logger.LogWarning($"Content page getContent error: {response.Error.Message}");
                return [];
            }

            var resultElement = response.Result;
            if (resultElement.HasValue &&
                resultElement.Value.ValueKind == JsonValueKind.Object &&
                resultElement.Value.TryGetProperty("content", out var contentArray))
            {
                resultElement = contentArray;
            }

            return JSCommandResultAdapter.ParseContentArray(resultElement, _pageId, _connection);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to get content for page {_pageId}: {ex.Message}");
            return [];
        }
    }

    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add { }
        remove { }
    }

    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged
    {
        add { }
        remove { }
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
/// Adapts JSON markdown content data to IMarkdownContent interface.
/// </summary>
internal sealed class JSMarkdownContentAdapter : IMarkdownContent
{
    private readonly JsonElement _data;

    public JSMarkdownContentAdapter(JsonElement data)
    {
        _data = data;
    }

    public string Body
    {
        get
        {
            if (_data.ValueKind == JsonValueKind.Object &&
                _data.TryGetProperty("body", out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? string.Empty;
            }

            return string.Empty;
        }
    }

    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add { }
        remove { }
    }
}

/// <summary>
/// Adapts JSON form content data to IFormContent interface.
/// </summary>
internal sealed class JSFormContentProxy : IFormContent
{
    private readonly string _pageId;
    private readonly JsonElement _data;
    private readonly JsonRpcConnection _connection;

    public JSFormContentProxy(string pageId, JsonElement data, JsonRpcConnection connection)
    {
        _pageId = pageId;
        _data = data;
        _connection = connection;
    }

    public string TemplateJson
    {
        get
        {
            if (_data.ValueKind == JsonValueKind.Object &&
                _data.TryGetProperty("templateJson", out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? string.Empty;
            }

            return string.Empty;
        }
    }

    public string DataJson
    {
        get
        {
            if (_data.ValueKind == JsonValueKind.Object &&
                _data.TryGetProperty("dataJson", out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? string.Empty;
            }

            return string.Empty;
        }
    }

    public string StateJson
    {
        get
        {
            if (_data.ValueKind == JsonValueKind.Object &&
                _data.TryGetProperty("stateJson", out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? string.Empty;
            }

            return string.Empty;
        }
    }

    public ICommandResult SubmitForm(string inputs, string data)
    {
        try
        {
            var response = _connection.SendRequestAsync(
                "form/submit",
                new JsonObject { ["pageId"] = _pageId, ["inputs"] = inputs, ["data"] = data },
                CancellationToken.None).GetAwaiter().GetResult();

            if (response.Error != null)
            {
                Logger.LogWarning($"Form submit error: {response.Error.Message}");
                return CommandResult.KeepOpen();
            }

            return JSCommandResultAdapter.ParseCommandResult(response.Result, _connection);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to submit form for page {_pageId}: {ex.Message}");
            return CommandResult.KeepOpen();
        }
    }

    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add { }
        remove { }
    }
}

/// <summary>
/// Adapts JSON tree content data to ITreeContent interface.
/// </summary>
internal sealed class JSTreeContentAdapter : ITreeContent
{
    private readonly JsonElement _data;
    private readonly string _pageId;
    private readonly JsonRpcConnection _connection;

    public JSTreeContentAdapter(JsonElement data, string pageId, JsonRpcConnection connection)
    {
        _data = data;
        _pageId = pageId;
        _connection = connection;
    }

    public IContent RootContent
    {
        get
        {
            if (_data.ValueKind == JsonValueKind.Object &&
                _data.TryGetProperty("rootContent", out var rootProp) && rootProp.ValueKind == JsonValueKind.Object)
            {
                return JSCommandResultAdapter.ParseContentItem(rootProp, _pageId, _connection);
            }

            return new JSMarkdownContentAdapter(default);
        }
    }

    public IContent[] GetChildren()
    {
        if (_data.ValueKind == JsonValueKind.Object &&
            _data.TryGetProperty("children", out var childrenProp) && childrenProp.ValueKind == JsonValueKind.Array)
        {
            var count = childrenProp.GetArrayLength();
            var items = new IContent[count];
            var i = 0;
            foreach (var child in childrenProp.EnumerateArray())
            {
                items[i++] = JSCommandResultAdapter.ParseContentItem(child, _pageId, _connection);
            }

            return items;
        }

        return [];
    }

    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add { }
        remove { }
    }

    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged
    {
        add { }
        remove { }
    }
}

/// <summary>
/// Adapts JSON context item data to IContextItem marker interface.
/// </summary>
internal sealed class JSContextItemAdapter : IContextItem
{
    private readonly JsonElement _data;

    public JSContextItemAdapter(JsonElement data)
    {
        _data = data;
    }
}

/// <summary>
/// Adapts JSON plain text content data to IPlainTextContent interface.
/// </summary>
internal sealed class JSPlainTextContentAdapter : IPlainTextContent
{
    private readonly JsonElement _data;

    public JSPlainTextContentAdapter(JsonElement data)
    {
        _data = data;
    }

    public string Text
    {
        get
        {
            if (_data.ValueKind == JsonValueKind.Object &&
                _data.TryGetProperty("text", out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? string.Empty;
            }

            return string.Empty;
        }
    }

    public FontFamily FontFamily
    {
        get
        {
            if (_data.ValueKind == JsonValueKind.Object &&
                _data.TryGetProperty("fontFamily", out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var value = prop.GetString();
                return value == "monospace" ? FontFamily.Monospace : FontFamily.UserInterface;
            }

            return FontFamily.UserInterface;
        }
    }

    public bool WrapWords
    {
        get
        {
            if (_data.ValueKind == JsonValueKind.Object &&
                _data.TryGetProperty("wrapWords", out var prop))
            {
                return prop.ValueKind == JsonValueKind.True;
            }

            return true;
        }
    }

    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add { }
        remove { }
    }
}

/// <summary>
/// Adapts JSON image content data to IImageContent interface.
/// </summary>
internal sealed class JSImageContentAdapter : IImageContent
{
    private readonly JsonElement _data;

    public JSImageContentAdapter(JsonElement data)
    {
        _data = data;
    }

    public IIconInfo Image
    {
        get
        {
            if (_data.ValueKind == JsonValueKind.Object &&
                _data.TryGetProperty("image", out var imageProp))
            {
                return JSIconInfoAdapter.FromJson(imageProp);
            }

            return new IconInfo(string.Empty);
        }
    }

    public int MaxWidth
    {
        get
        {
            if (_data.ValueKind == JsonValueKind.Object &&
                _data.TryGetProperty("maxWidth", out var prop) && prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetInt32();
            }

            return -1; // Unlimited
        }
    }

    public int MaxHeight
    {
        get
        {
            if (_data.ValueKind == JsonValueKind.Object &&
                _data.TryGetProperty("maxHeight", out var prop) && prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetInt32();
            }

            return -1; // Unlimited
        }
    }

    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add { }
        remove { }
    }
}
