// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Translates a JSON-RPC command result payload into a toolkit
/// <see cref="ICommandResult"/>, mapping every <c>Kind</c> value (0-7) and its
/// associated arguments. Accepts both PascalCase and camelCase keys.
/// </summary>
internal static class JSCommandResultParser
{
    internal static ICommandResult ParseCommandResult(JsonElement? result, JsonRpcConnection? connection)
    {
        if (!result.HasValue || result.Value.ValueKind != JsonValueKind.Object)
        {
            return CommandResult.Dismiss();
        }

        var value = result.Value;
        var kindValue = 0;
        if (value.TryGetProperty("Kind", out var kindProp) || value.TryGetProperty("kind", out kindProp))
        {
            if (kindProp.ValueKind == JsonValueKind.Number)
            {
                kindValue = kindProp.GetInt32();
            }
        }

        var kind = (CommandResultKind)kindValue;
        var hasArgs = (value.TryGetProperty("Args", out var argsProp) || value.TryGetProperty("args", out argsProp)) &&
            argsProp.ValueKind == JsonValueKind.Object;

        switch (kind)
        {
            case CommandResultKind.GoHome:
                return CommandResult.GoHome();

            case CommandResultKind.GoBack:
                return CommandResult.GoBack();

            case CommandResultKind.Hide:
                return CommandResult.Hide();

            case CommandResultKind.KeepOpen:
                return CommandResult.KeepOpen();

            case CommandResultKind.GoToPage:
                return ParseGoToPage(hasArgs ? argsProp : default);

            case CommandResultKind.ShowToast:
                return ParseShowToast(hasArgs ? argsProp : default, connection);

            case CommandResultKind.Confirm:
                return ParseConfirm(hasArgs ? argsProp : default, connection);

            default:
                return CommandResult.Dismiss();
        }
    }

    private static ICommandResult ParseGoToPage(JsonElement args)
    {
        var pageId = string.Empty;
        if (args.ValueKind == JsonValueKind.Object)
        {
            pageId = JSModelMapper.GetString(args, "pageId") ?? JSModelMapper.GetString(args, "PageId") ?? string.Empty;
        }

        var navigationMode = NavigationMode.Push;
        if (args.ValueKind == JsonValueKind.Object &&
            (JSModelMapper.TryGetAnyCase(args, "navigationMode", "NavigationMode", out var modeProp) ||
             JSModelMapper.TryGetAnyCase(args, "mode", "Mode", out modeProp)))
        {
            navigationMode = ReadNavigationMode(modeProp);
        }

        return CommandResult.GoToPage(new GoToPageArgs { PageId = pageId, NavigationMode = navigationMode });
    }

    private static ICommandResult ParseShowToast(JsonElement args, JsonRpcConnection? connection)
    {
        var message = string.Empty;
        if (args.ValueKind == JsonValueKind.Object)
        {
            message = JSModelMapper.GetString(args, "message") ?? JSModelMapper.GetString(args, "Message") ?? string.Empty;
        }

        var toastArgs = new ToastArgs { Message = message };

        // A toast can carry a nested continuation result that the shell executes
        // after the toast is shown. Parse it recursively so every nested kind
        // (including confirm, which needs the connection for its primary command,
        // and even another toast) round-trips faithfully.
        if (args.ValueKind == JsonValueKind.Object &&
            JSModelMapper.TryGetAnyCase(args, "result", "Result", out var resultProp) &&
            resultProp.ValueKind == JsonValueKind.Object)
        {
            toastArgs.Result = ParseCommandResult(resultProp, connection);
        }

        return CommandResult.ShowToast(toastArgs);
    }

    private static ICommandResult ParseConfirm(JsonElement args, JsonRpcConnection? connection)
    {
        if (args.ValueKind != JsonValueKind.Object)
        {
            return CommandResult.Confirm(new ConfirmationArgs());
        }

        ICommand? primaryCommand = null;
        if (connection != null &&
            JSModelMapper.TryGetAnyCase(args, "primaryCommand", "PrimaryCommand", out var cmdProp) &&
            cmdProp.ValueKind == JsonValueKind.Object)
        {
            primaryCommand = JSCommandFactory.CreateCommandFromJson(cmdProp, connection);
        }

        return CommandResult.Confirm(new ConfirmationArgs
        {
            Title = JSModelMapper.GetString(args, "title") ?? JSModelMapper.GetString(args, "Title") ?? string.Empty,
            Description = JSModelMapper.GetString(args, "description") ?? JSModelMapper.GetString(args, "Description") ?? string.Empty,
            PrimaryCommand = primaryCommand,
            IsPrimaryCommandCritical = JSModelMapper.GetBool(args, "isPrimaryCommandCritical", false) ||
                JSModelMapper.GetBool(args, "IsPrimaryCommandCritical", false),
        });
    }

    private static NavigationMode ReadNavigationMode(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return (NavigationMode)element.GetInt32();
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return (element.GetString() ?? string.Empty).ToLowerInvariant() switch
            {
                "goback" => NavigationMode.GoBack,
                "gohome" => NavigationMode.GoHome,
                _ => NavigationMode.Push,
            };
        }

        return NavigationMode.Push;
    }
}
