// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

internal static class SettingsAdaptiveCardBuilder
{
    /*
     * Keep this internal.
     */

    public static Dictionary<string, object> Container(List<Dictionary<string, object>> items) =>
        new()
        {
            ["type"] = "Container",
            ["items"] = items,
            ["roundedCorners"] = true,
            ["showBorder"] = true,
            ["spacing"] = "Medium",
        };

    public static Dictionary<string, object> Container(Dictionary<string, object> item) => Container([item]);

    public static Dictionary<string, object> BuildSettingsCardWithControlOnLeft(string label, string description, string errorMessage, bool isRequired, Func<Dictionary<string, object>> control) =>
        Container(new Dictionary<string, object>
        {
            ["type"] = "ColumnSet",
            ["columns"] = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["type"] = "Column",
                    ["width"] = "20px",
                    ["items"] = new List<Dictionary<string, object>> { control() },
                    ["verticalContentAlignment"] = "Center",
                },
                BuildLabelDescriptionColumn(label, description, errorMessage, isRequired),
            },
            ["spacing"] = "Medium",
        });

    public static Dictionary<string, object> BuildSettingsCardWithControlOnRight(string label, string description, string errorMessage, bool isRequired, Func<Dictionary<string, object>> control) =>
        Container(new Dictionary<string, object>
        {
            ["type"] = "ColumnSet",
            ["columns"] = new List<Dictionary<string, object>>
            {
                BuildLabelDescriptionColumn(label, description, errorMessage, isRequired),
                new()
                {
                    ["type"] = "Column",
                    ["width"] = "220px",
                    ["items"] = new List<Dictionary<string, object>> { control() },
                    ["verticalContentAlignment"] = "Center",
                },
            },
            ["spacing"] = "Medium",
        });

    public static Dictionary<string, object> BuildLabelDescriptionColumn(string label, string description, string errorMessage, bool isRequired)
    {
        var items = new List<Dictionary<string, object>>();
        if (!string.IsNullOrEmpty(label))
        {
            items.Add(
                new()
                {
                    { "type", "TextBlock" },
                    { "text", label },
                    { "wrap", true },
                });
        }

        if (!(string.IsNullOrEmpty(description) || string.Equals(description, label, StringComparison.OrdinalIgnoreCase)))
        {
            items.Add(
                new()
                {
                    { "type", "TextBlock" },
                    { "text", description },
                    { "isSubtle", true },
                    { "size", "Small" },
                    { "spacing", "Small" },
                    { "wrap", true },
                });
        }

        return new Dictionary<string, object>
        {
            { "type", "Column" },
            { "width", "stretch" },
            { "items", items },
            { "verticalContentAlignment", "Center" },
        };
    }
}
