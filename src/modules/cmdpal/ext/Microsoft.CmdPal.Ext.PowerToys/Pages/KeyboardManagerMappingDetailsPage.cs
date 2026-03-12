// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using KeyboardManager.ModuleServices;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension.Pages;

internal sealed partial class KeyboardManagerMappingDetailsPage : ContentPage
{
    private readonly KeyboardManagerMappingRecord _mapping;

    public KeyboardManagerMappingDetailsPage(KeyboardManagerMappingRecord mapping, IconInfo icon)
    {
        _mapping = mapping;
        Icon = icon;
        Name = mapping.TriggerDisplay;

        Details = new Details
        {
            HeroImage = icon,
            Title = mapping.TriggerDisplay,
            Body = mapping.Subtitle,
            Metadata = BuildMetadata(mapping),
        };
    }

    public override IContent[] GetContent()
    {
        return
        [
            new MarkdownContent(
$"""
# {EscapeMarkdown(_mapping.TriggerDisplay)}

{EscapeMarkdown(_mapping.Subtitle)}
"""),
        ];
    }

    private static IDetailsElement[] BuildMetadata(KeyboardManagerMappingRecord mapping)
    {
        var metadata = new List<IDetailsElement>
        {
            DetailText("Type", mapping.Kind.ToString()),
            DetailText("Scope", mapping.IsAppSpecific ? $"App-specific ({mapping.TargetApp})" : "Global"),
            DetailText("Target", mapping.TargetDisplay),
        };

        if (!string.IsNullOrWhiteSpace(mapping.ProgramPath))
        {
            metadata.Add(DetailText("Program", mapping.ProgramPath));
        }

        if (!string.IsNullOrWhiteSpace(mapping.ProgramArgs))
        {
            metadata.Add(DetailText("Args", mapping.ProgramArgs));
        }

        if (!string.IsNullOrWhiteSpace(mapping.StartInDirectory))
        {
            metadata.Add(DetailText("Start in", mapping.StartInDirectory));
        }

        if (!string.IsNullOrWhiteSpace(mapping.UriToOpen))
        {
            metadata.Add(DetailText("URI", mapping.UriToOpen));
        }

        if (!string.IsNullOrWhiteSpace(mapping.TargetText))
        {
            metadata.Add(DetailText("Text", mapping.TargetText));
        }

        return metadata.ToArray();
    }

    private static DetailsElement DetailText(string key, string value)
    {
        return new DetailsElement
        {
            Key = key,
            Data = new DetailsLink { Text = value },
        };
    }

    private static string EscapeMarkdown(string value)
    {
        return value.Replace("\\", "\\\\").Replace("*", "\\*").Replace("_", "\\_");
    }
}
