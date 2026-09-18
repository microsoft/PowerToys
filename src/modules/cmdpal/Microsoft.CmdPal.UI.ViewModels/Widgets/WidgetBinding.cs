// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.Json.Nodes;

namespace Microsoft.CmdPal.UI.ViewModels.Widgets;

public readonly record struct WidgetBinding(string ExtensionId, string ProviderId, string WidgetId)
{
    public string Serialize() => new JsonObject
    {
        ["version"] = 1,
        ["extensionId"] = ExtensionId,
        ["providerId"] = ProviderId,
        ["widgetId"] = WidgetId,
    }.ToJsonString();

    public string Encode()
    {
        var bytes = Encoding.UTF8.GetBytes(Serialize());
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool TryParse(string? json, out WidgetBinding binding)
    {
        binding = default;
        try
        {
            var root = JsonNode.Parse(json ?? string.Empty)?.AsObject();
            if (root?["version"]?.GetValue<int>() != 1)
            {
                return false;
            }

            var extensionId = root["extensionId"]?.GetValue<string>() ?? string.Empty;
            var providerId = root["providerId"]?.GetValue<string>() ?? string.Empty;
            var widgetId = root["widgetId"]?.GetValue<string>() ?? string.Empty;
            if ((string.IsNullOrWhiteSpace(extensionId) && string.IsNullOrWhiteSpace(providerId)) ||
                string.IsNullOrWhiteSpace(widgetId))
            {
                return false;
            }

            binding = new(extensionId, providerId, widgetId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryDecode(string? encoded, out WidgetBinding binding)
    {
        binding = default;
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return false;
        }

        try
        {
            var base64 = encoded.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
            return TryParse(Encoding.UTF8.GetString(Convert.FromBase64String(base64)), out binding);
        }
        catch
        {
            return false;
        }
    }
}
