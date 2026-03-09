// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text;
using Microsoft.CmdPal.Ext.RaycastStore.GitHub;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RaycastStore.Pages;

internal sealed partial class ExtensionDetailPage : ContentPage
{
    private readonly RaycastExtensionInfo _extension;
    private readonly InstalledExtensionTracker? _tracker;
    private readonly MarkdownContent _markdownContent;

    public ExtensionDetailPage(RaycastExtensionInfo extension, InstalledExtensionTracker? tracker = null)
    {
        _extension = extension;
        _tracker = tracker;
        Icon = !string.IsNullOrEmpty(extension.IconUrl) ? new IconInfo(extension.IconUrl) : Icons.ExtensionIcon;
        Name = extension.Title;
        Title = extension.Title;
        _markdownContent = new MarkdownContent
        {
            Body = BuildDetailMarkdown(),
        };
        BuildCommands();
    }

    private void BuildCommands()
    {
        var target = "https://github.com/raycast/extensions/tree/main/extensions/" + _extension.DirectoryName;
        var isInstalled = _tracker?.IsInstalled(_extension.DirectoryName) ?? false;

        List<ICommandContextItem> list = new();
        if (isInstalled)
        {
            list.Add(new CommandContextItem(new UninstallExtensionCommand(_extension, _tracker!, OnInstallStateChanged))
            {
                Title = "Uninstall",
                Subtitle = "Remove this Raycast extension from Command Palette",
            });
        }
        else
        {
            list.Add(new CommandContextItem(new InstallExtensionCommand(_extension, _tracker!, OnInstallStateChanged))
            {
                Title = "Install",
                Subtitle = "Install this Raycast extension for Command Palette",
            });
        }

        list.Add(new CommandContextItem(new OpenUrlCommand(target))
        {
            Title = "Open on GitHub",
            Subtitle = "Open extension source on GitHub",
        });

        Commands = list.ToArray();
    }

    private void OnInstallStateChanged()
    {
        BuildCommands();
    }

    public override IContent[] GetContent()
    {
        return new IContent[] { _markdownContent };
    }

    private string BuildDetailMarkdown()
    {
        StringBuilder sb = new();
        sb.Append("# ");
        sb.AppendLine(_extension.Title);
        sb.AppendLine();

        if (!string.IsNullOrEmpty(_extension.Author))
        {
            sb.Append("**Author:** ");
            sb.AppendLine(_extension.Author);
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(_extension.Version))
        {
            sb.Append("**Version:** ");
            sb.AppendLine(_extension.Version);
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(_extension.Description))
        {
            sb.AppendLine(_extension.Description);
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(_extension.License) || _extension.Categories.Count > 0 || _extension.Contributors.Count > 0)
        {
            sb.AppendLine("## Details");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(_extension.License))
            {
                sb.Append("- **License:** ");
                sb.AppendLine(_extension.License);
            }

            if (_extension.Categories.Count > 0)
            {
                sb.Append("- **Categories:** ");
                for (var i = 0; i < _extension.Categories.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }

                    sb.Append(_extension.Categories[i]);
                }

                sb.AppendLine();
            }

            if (_extension.Contributors.Count > 0)
            {
                sb.Append("- **Contributors:** ");
                for (var j = 0; j < _extension.Contributors.Count; j++)
                {
                    if (j > 0)
                    {
                        sb.Append(", ");
                    }

                    sb.Append(_extension.Contributors[j]);
                }

                sb.AppendLine();
            }

            sb.AppendLine();
        }

        if (_extension.Commands.Count > 0)
        {
            sb.AppendLine("## Commands");
            sb.AppendLine();

            for (var k = 0; k < _extension.Commands.Count; k++)
            {
                RaycastCommand cmd = _extension.Commands[k];
                sb.Append("- **");
                sb.Append(string.IsNullOrEmpty(cmd.Title) ? cmd.Name : cmd.Title);
                sb.Append("**");
                if (!string.IsNullOrEmpty(cmd.Description))
                {
                    sb.Append(" — ");
                    sb.Append(cmd.Description);
                }

                if (!string.IsNullOrEmpty(cmd.Mode))
                {
                    sb.Append(" _(");
                    sb.Append(cmd.Mode);
                    sb.Append(")_");
                }

                sb.AppendLine();
            }

            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.Append("[View source on GitHub](https://github.com/raycast/extensions/tree/main/extensions/");
        sb.Append(_extension.DirectoryName);
        sb.AppendLine(")");

        return sb.ToString();
    }
}
