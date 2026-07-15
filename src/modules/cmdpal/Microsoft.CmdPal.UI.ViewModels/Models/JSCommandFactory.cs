// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Creates the appropriate <see cref="ICommand"/> implementation from a JSON
/// command payload. A <c>pageType</c> (or legacy <c>_type</c>) discriminator
/// selects a page proxy; otherwise an invokable command adapter is returned.
/// </summary>
internal static class JSCommandFactory
{
    internal static ICommand CreateCommandFromJson(JsonElement data, JsonRpcConnection connection)
    {
        var pageType = ReadPageType(data);
        var commandId = JSModelMapper.GetString(data, "id") ?? string.Empty;

        return pageType switch
        {
            "dynamicListPage" => new JSDynamicListPageProxy(commandId, connection, data),
            "listPage" => new JSListPageProxy(commandId, connection, data),
            "contentPage" => new JSContentPageProxy(commandId, connection, data),
            _ => new JSInvokableCommandAdapter(data, connection),
        };
    }

    internal static string ReadPageType(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        if (data.TryGetProperty("pageType", out var pageTypeProp) && pageTypeProp.ValueKind == JsonValueKind.String)
        {
            return pageTypeProp.GetString() ?? string.Empty;
        }

        if (data.TryGetProperty("_type", out var legacyProp) && legacyProp.ValueKind == JsonValueKind.String)
        {
            return legacyProp.GetString() ?? string.Empty;
        }

        return string.Empty;
    }
}
