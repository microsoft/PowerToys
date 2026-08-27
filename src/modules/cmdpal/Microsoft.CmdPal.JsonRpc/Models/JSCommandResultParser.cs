// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.CmdPal.JsonRpc;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.JsonRpc.Models;

/// <summary>
/// Translates a JSON-RPC command result payload into a toolkit
/// <see cref="ICommandResult"/>. It maps <c>kind</c> values 0 through 7.
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
        if (value.TryGetProperty("kind", out var kindProp))
        {
            if (kindProp.ValueKind == JsonValueKind.Number)
            {
                kindValue = kindProp.GetInt32();
            }
        }

        var kind = (CommandResultKind)kindValue;
        var hasArgs = value.TryGetProperty("args", out var argsProp) &&
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
            pageId = JSModelMapper.GetString(args, "pageId") ?? string.Empty;
        }

        var navigationMode = NavigationMode.Push;
        if (args.ValueKind == JsonValueKind.Object &&
            (JSModelMapper.TryGetProperty(args, "navigationMode", out var modeProp) ||
             JSModelMapper.TryGetProperty(args, "mode", out modeProp)))
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
            message = JSModelMapper.GetString(args, "message") ?? string.Empty;
        }

        var toastArgs = new ToastArgs { Message = message };

        if (args.ValueKind == JsonValueKind.Object &&
            JSModelMapper.TryGetProperty(args, "icon", out var iconProp))
        {
            toastArgs.Icon = JSModelMapper.ParseIconInfo(iconProp);
        }

        // Toast action commands need the same live connection as the command adapter.
        // If no connection is available, keep the toast and skip only the action.
        if (connection != null &&
            args.ValueKind == JsonValueKind.Object &&
            JSModelMapper.TryGetProperty(args, "command", out var commandProp) &&
            commandProp.ValueKind == JsonValueKind.Object)
        {
            toastArgs.Command = JSCommandFactory.CreateCommandFromJson(commandProp, connection);
        }

        // A toast can carry a continuation result for the shell to run after display.
        // Parse it recursively so nested confirm and toast results keep working.
        if (args.ValueKind == JsonValueKind.Object &&
            JSModelMapper.TryGetProperty(args, "result", out var resultProp) &&
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
            JSModelMapper.TryGetProperty(args, "primaryCommand", out var cmdProp) &&
            cmdProp.ValueKind == JsonValueKind.Object)
        {
            primaryCommand = JSCommandFactory.CreateCommandFromJson(cmdProp, connection);
        }

        return CommandResult.Confirm(new ConfirmationArgs
        {
            Title = JSModelMapper.GetString(args, "title") ?? string.Empty,
            Description = JSModelMapper.GetString(args, "description") ?? string.Empty,
            PrimaryCommand = primaryCommand,
            IsPrimaryCommandCritical = JSModelMapper.GetBool(args, "isPrimaryCommandCritical", false),
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
