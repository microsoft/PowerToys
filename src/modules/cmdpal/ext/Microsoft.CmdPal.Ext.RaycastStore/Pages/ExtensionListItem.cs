// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.CmdPal.Ext.RaycastStore.GitHub;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RaycastStore.Pages;

internal sealed partial class ExtensionListItem : ListItem
{
    private readonly RaycastExtensionInfo _extension;
    private readonly Lazy<Details?> _details;

    public override IDetails? Details
    {
        get => _details.Value;
        set => base.Details = value;
    }

    public ExtensionListItem(RaycastExtensionInfo extension, InstalledExtensionTracker? tracker = null)
        : base(BuildPrimaryCommand(extension, tracker))
    {
        _extension = extension;
        Title = extension.Title;
        Subtitle = string.IsNullOrEmpty(extension.Author) ? extension.Name : "by " + extension.Author;
        Icon = !string.IsNullOrEmpty(extension.IconUrl) ? new IconInfo(extension.IconUrl) : Icons.ExtensionIcon;

        List<Tag> tagList = new();
        var isInstalled = tracker?.IsInstalled(extension.DirectoryName) ?? false;

        if (isInstalled)
        {
            tagList.Add(new Tag
            {
                Text = "Installed ✓",
                Foreground = ColorHelpers.FromRgb(0, 128, 0),
            });
        }

        if (!string.IsNullOrEmpty(extension.Version))
        {
            tagList.Add(new Tag
            {
                Text = "v" + extension.Version,
            });
        }

        for (var i = 0; i < extension.Categories.Count && i < 2; i++)
        {
            tagList.Add(new Tag
            {
                Text = extension.Categories[i],
            });
        }

        Tags = tagList.ToArray();

        MoreCommands = new IContextItem[]
        {
            new CommandContextItem(new ExtensionDetailPage(extension, tracker))
            {
                Title = "View Details",
            },
        };

        _details = new Lazy<Details?>(() => BuildDetails(isInstalled));
    }

    private static ICommand BuildPrimaryCommand(RaycastExtensionInfo extension, InstalledExtensionTracker? tracker)
    {
        if (tracker == null)
        {
            return new ExtensionDetailPage(extension, tracker);
        }

        return new InstallExtensionCommand(extension, tracker);
    }

    private Details? BuildDetails(bool isInstalled)
    {
        var body = _extension.Description;
        if (string.IsNullOrEmpty(body))
        {
            body = "No description available.";
        }

        List<IDetailsElement> metadata = new();

        if (!string.IsNullOrEmpty(_extension.Author))
        {
            metadata.Add(new DetailsElement
            {
                Key = "Author",
                Data = new DetailsLink { Text = _extension.Author },
            });
        }

        if (!string.IsNullOrEmpty(_extension.Version))
        {
            metadata.Add(new DetailsElement
            {
                Key = "Version",
                Data = new DetailsLink { Text = _extension.Version },
            });
        }

        if (_extension.Commands.Count > 0)
        {
            metadata.Add(new DetailsElement
            {
                Key = "Commands",
                Data = new DetailsLink { Text = _extension.Commands.Count.ToString(CultureInfo.InvariantCulture) },
            });
        }

        if (!string.IsNullOrEmpty(_extension.License))
        {
            metadata.Add(new DetailsElement
            {
                Key = "License",
                Data = new DetailsLink { Text = _extension.License },
            });
        }

        metadata.Add(new DetailsElement
        {
            Key = "Status",
            Data = new DetailsLink { Text = isInstalled ? "Installed ✓" : "Not installed" },
        });

        if (_extension.Categories.Count > 0)
        {
            Tag[] catTags = new Tag[_extension.Categories.Count];
            for (var i = 0; i < _extension.Categories.Count; i++)
            {
                catTags[i] = new Tag(_extension.Categories[i]);
            }

            metadata.Add(new DetailsElement
            {
                Key = "Categories",
                Data = new DetailsTags { Tags = catTags },
            });
        }

        return new Details
        {
            Body = body,
            Title = _extension.Title,
            HeroImage = new IconInfo(!string.IsNullOrEmpty(_extension.IconUrl) ? _extension.IconUrl : null),
            Metadata = metadata.ToArray(),
        };
    }
}
