// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using KeyboardManager.ModuleServices;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;
using PowerToysExtension.Pages;

namespace PowerToysExtension.Commands;

internal sealed partial class KeyboardManagerMappingListItem : ListItem
{
    public KeyboardManagerMappingListItem(KeyboardManagerMappingRecord mapping, IconInfo icon)
        : base(new CommandItem(new KeyboardManagerMappingDetailsPage(mapping, icon)))
    {
        Title = mapping.TriggerDisplay;
        Subtitle = mapping.Subtitle;
        Icon = icon;
        Details = BuildDetails(mapping, icon);
    }

    private static Details BuildDetails(KeyboardManagerMappingRecord mapping, IconInfo icon)
    {
        var metadata = new List<IDetailsElement>
        {
            DetailText("Type", mapping.Kind.ToString()),
            DetailText("Target", mapping.TargetDisplay),
            DetailText("Scope", mapping.IsAppSpecific ? $"App-specific ({mapping.TargetApp})" : "Global"),
        };

        if (!string.IsNullOrWhiteSpace(mapping.ProgramArgs))
        {
            metadata.Add(DetailText("Args", mapping.ProgramArgs));
        }

        if (!string.IsNullOrWhiteSpace(mapping.StartInDirectory))
        {
            metadata.Add(DetailText("Start in", mapping.StartInDirectory));
        }

        if (!string.IsNullOrWhiteSpace(mapping.TargetText))
        {
            metadata.Add(DetailText("Text", mapping.TargetText));
        }

        if (!string.IsNullOrWhiteSpace(mapping.UriToOpen))
        {
            metadata.Add(DetailText("URI", mapping.UriToOpen));
        }

        return new Details
        {
            HeroImage = icon,
            Title = mapping.TriggerDisplay,
            Body = mapping.Subtitle,
            Metadata = metadata.ToArray(),
        };
    }

    private static DetailsElement DetailText(string key, string value)
    {
        return new DetailsElement
        {
            Key = key,
            Data = new DetailsLink { Text = value },
        };
    }
}
