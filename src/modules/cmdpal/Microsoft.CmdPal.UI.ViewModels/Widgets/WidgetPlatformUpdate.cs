// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels.Widgets;

public sealed record WidgetPlatformUpdate(
    string WidgetId,
    string TemplateJson,
    string DataJson,
    string CustomState,
    bool IsPlaceholder = false,
    string? HeaderTitle = null,
    IIconInfo? HeaderIcon = null)
{
    public string TemplateWithHeader(string title, string? iconUrl)
    {
        var template = JsonNode.Parse(TemplateJson)!.AsObject();
        var header = new JsonObject { ["text"] = title };
        if (!string.IsNullOrEmpty(iconUrl))
        {
            header["iconUrl"] = iconUrl;
        }

        template["header"] = header;
        return template.ToJsonString();
    }
}
