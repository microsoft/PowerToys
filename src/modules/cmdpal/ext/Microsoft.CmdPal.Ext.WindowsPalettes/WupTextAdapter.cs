// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.UI.Palettes;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

internal static class WupTextAdapter
{
    internal static string CreateSubtitle(IReadOnlyList<object>? values)
    {
        if (values is null || values.Count == 0)
        {
            return string.Empty;
        }

        List<string> parts = [];
        foreach (var value in values)
        {
            switch (value)
            {
                case string text when !string.IsNullOrWhiteSpace(text):
                    parts.Add(text);
                    break;
                case PaletteItemView item when !string.IsNullOrWhiteSpace(item.Title):
                    parts.Add(item.Title);
                    break;
            }
        }

        return string.Join(" ", parts);
    }

    internal static ITag[] CreateTags(IReadOnlyList<PaletteTag>? tags, string sourceDisplayName)
    {
        List<ITag> adapted = [];
        if (tags is not null)
        {
            foreach (var tag in tags)
            {
                adapted.Add(new Tag(tag.Text ?? string.Empty)
                {
                    ToolTip = tag.ToolTip ?? string.Empty,
                    Icon = WupIconAdapter.Create(tag.Icon),
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(sourceDisplayName))
        {
            adapted.Add(new Tag(sourceDisplayName));
        }

        return [.. adapted];
    }

    internal static ITag[] CreateStatusTags(PaletteStatus? status)
    {
        if (status is null || string.IsNullOrWhiteSpace(status.Message))
        {
            return [];
        }

        var glyph = status.Severity switch
        {
            PaletteSeverity.Success => "\uE73E",
            PaletteSeverity.Warning => "\uE7BA",
            PaletteSeverity.Error => "\uEA39",
            _ => "\uE946",
        };
        return
        [
            new Tag(status.Message)
            {
                Icon = new IconInfo(glyph),
                ToolTip = status.Message,
            },
        ];
    }
}
