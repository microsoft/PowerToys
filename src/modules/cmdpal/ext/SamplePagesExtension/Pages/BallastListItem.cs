// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages;

/// <summary>
/// A list item wired up so every part of it is observable: a tracked payload, a
/// tracked primary command, optional tracked context items, and optionally a
/// data-backed icon.
/// </summary>
internal sealed partial class BallastListItem : ListItem
{
    private readonly TrackedPayload _payload;

    public BallastListItem(int generation, int index, int payloadBytes, int iconSide, int moreCommands)
        : base(new TrackedCommand($"Run item {index:N0}"))
    {
        _payload = new TrackedPayload(payloadBytes);

        Title = $"Ballast item {index:N0} (batch {generation})";

        if (moreCommands > 0)
        {
            var context = new IContextItem[moreCommands];
            for (var i = 0; i < moreCommands; i++)
            {
                context[i] = new TrackedContextItem($"Item {index:N0} action {i}");
            }

            MoreCommands = context;
        }

        Subtitle = BuildSubtitle(iconSide, moreCommands);

        if (iconSide > 0)
        {
            Icon = BallastIcon.Create(iconSide, index);
        }
    }

    private string BuildSubtitle(int iconSide, int moreCommands)
    {
        var parts = new List<string>(3);

        if (_payload.Size > 0)
        {
            parts.Add($"{_payload.Size / 1024:N0} KB payload");
        }

        if (iconSide > 0)
        {
            parts.Add($"{iconSide}x{iconSide} data-backed icon");
        }

        if (moreCommands > 0)
        {
            parts.Add($"{moreCommands} context commands");
        }

        return parts.Count == 0
            ? "Finalized only once the host releases this item"
            : string.Join(", ", parts);
    }
}
