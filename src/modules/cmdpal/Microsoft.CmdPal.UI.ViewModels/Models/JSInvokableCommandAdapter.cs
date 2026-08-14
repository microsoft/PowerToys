// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Adapts a JSON command payload to <see cref="ICommand"/> and
/// <see cref="IInvokableCommand"/>. Invoking sends a <c>command/invoke</c>
/// request and maps the response to a toolkit command result.
/// </summary>
internal sealed partial class JSInvokableCommandAdapter : JSObservableProxyBase, IInvokableCommand
{
    public JSInvokableCommandAdapter(JsonElement data, JsonRpcConnection connection)
        : base(JSModelMapper.GetString(data, "id") ?? string.Empty, connection, data)
    {
    }

    public string Name => JSModelMapper.GetString(Data, "displayName") ?? JSModelMapper.GetString(Data, "name") ?? string.Empty;

    public string Id => JSModelMapper.GetString(Data, "id") ?? string.Empty;

    public IIconInfo Icon => JSModelMapper.GetIcon(Data, "icon", "Icon");

    public ICommandResult Invoke(object? sender)
    {
        try
        {
            var response = Connection.SendRequestAsync(
                "command/invoke",
                new JsonObject { ["commandId"] = Id },
                CancellationToken.None).GetAwaiter().GetResult();

            if (response.Error != null)
            {
                Logger.LogError($"Command invoke error: {response.Error.Message}");
                return CommandResult.KeepOpen();
            }

            return JSCommandResultParser.ParseCommandResult(response.Result, Connection);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to invoke command {Id}: {ex.Message}");
            return CommandResult.KeepOpen();
        }
    }

    public ICommandResult Invoke() => Invoke(this);

    protected override bool SupportsProperty(string propertyName) => propertyName switch
    {
        "id" or "name" or "icon" => true,
        _ => false,
    };
}
