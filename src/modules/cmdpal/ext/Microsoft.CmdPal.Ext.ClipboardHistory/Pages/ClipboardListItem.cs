// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CmdPal.Common.Commands;
using Microsoft.CmdPal.Ext.ClipboardHistory.Commands;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Models;

internal sealed partial class ClipboardListItem : ListItem
{
    private readonly SettingsManager _settingsManager;
    private readonly ClipboardItem _item;

    private readonly CommandContextItem _deleteContextMenuItem;
    private readonly CommandContextItem? _pasteCommand;
    private readonly CommandContextItem? _copyCommand;
    private readonly CommandContextItem? _openUrlCommand;
    private readonly Lazy<Details> _lazyDetails;

    public override IDetails? Details
    {
        get => _lazyDetails.Value;
        set
        {
        }
    }

    public ClipboardListItem(ClipboardItem item, SettingsManager settingsManager)
    {
        _item = item;
        _settingsManager = settingsManager;
        _settingsManager.Settings.SettingsChanged += SettingsOnSettingsChanged;

        _lazyDetails = new(() => CreateDetails());

        var deleteConfirmationCommand = new ConfirmableCommand
        {
            Command = new DeleteItemCommand(_item),
            ConfirmationTitle = Properties.Resources.delete_confirmation_title!,
            ConfirmationMessage = Properties.Resources.delete_confirmation_message!,
            IsConfirmationRequired = () => _settingsManager.DeleteFromHistoryRequiresConfirmation,
        };
        _deleteContextMenuItem = new CommandContextItem(deleteConfirmationCommand)
        {
            IsCritical = true,
            RequestedShortcut = KeyChords.DeleteEntry,
        };

        if (item.IsImage)
        {
            Title = "Image";

            _pasteCommand = new CommandContextItem(new PasteCommand(_item, ClipboardFormat.Image, _settingsManager));
            _copyCommand = new CommandContextItem(new CopyCommand(_item, ClipboardFormat.Image));
        }
        else if (item.IsText)
        {
            var splitContent = _item.Content?.Split("\n") ?? [];
            var head = splitContent.Take(3);
            var preview2 = string.Join(
                "\n",
                StripLeadingWhitespace(head));

            Title = preview2;

            _pasteCommand = new CommandContextItem(new PasteCommand(_item, ClipboardFormat.Text, _settingsManager));
            _copyCommand = new CommandContextItem(new CopyCommand(_item, ClipboardFormat.Text));

            // Check if the text content is a valid URL and add OpenUrl command
            if (UrlHelper.IsValidUrl(_item.Content ?? string.Empty))
            {
                var normalizedUrl = UrlHelper.NormalizeUrl(_item.Content ?? string.Empty);
                _openUrlCommand = new CommandContextItem(new OpenUrlCommand(normalizedUrl))
                {
                    RequestedShortcut = KeyChords.OpenUrl,
                };
            }
            else
            {
                _openUrlCommand = null;
            }
        }
        else
        {
            _pasteCommand = null;
            _copyCommand = null;
            _openUrlCommand = null;
        }

        RefreshCommands();
    }

    private void SettingsOnSettingsChanged(object sender, Settings args)
    {
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        if (_item is { IsText: false, IsImage: false })
        {
            MoreCommands = [_deleteContextMenuItem];
            Icon = _settingsManager.PrimaryAction == PrimaryAction.Paste ? Icons.Clipboard : Icons.Copy;
        }

        switch (_settingsManager.PrimaryAction)
        {
            case PrimaryAction.Paste:
                Command = _pasteCommand?.Command;
                MoreCommands = BuildMoreCommands(_copyCommand);

                if (_item.IsText)
                {
                    Icon = Icons.ClipboardLetter;
                }
                else if (_item.IsImage)
                {
                    Icon = Icons.ClipboardImage;
                }
                else
                {
                    Icon = Icons.ClipboardImage;
                }

                break;
            case PrimaryAction.Default:
            case PrimaryAction.Copy:
            default:
                Command = _copyCommand?.Command;
                MoreCommands = BuildMoreCommands(_pasteCommand);

                if (_item.IsText)
                {
                    Icon = Icons.DocumentCopy;
                }
                else if (_item.IsImage)
                {
                    Icon = Icons.ImageCopy;
                }
                else
                {
                    Icon = Icons.Copy;
                }

                break;
        }
    }

    private IContextItem[] BuildMoreCommands(CommandContextItem? firstCommand)
    {
        var commands = new List<IContextItem>();

        if (firstCommand != null)
        {
            commands.Add(firstCommand);
        }

        if (_openUrlCommand != null)
        {
            commands.Add(_openUrlCommand);
        }

        commands.Add(new Separator());
        commands.Add(_deleteContextMenuItem);

        return commands.ToArray();
    }

    private Details CreateDetails()
    {
        IDetailsElement[] metadata =
        [
            new DetailsElement
            {
                Key = "Copied on",
                Data = new DetailsLink(_item.Timestamp.DateTime.ToString(DateTimeFormatInfo.CurrentInfo)),
            }
        ];

        if (_item.IsImage)
        {
            var iconData = new IconData(_item.ImageData);
            var heroImage = new IconInfo(iconData);
            return new Details
            {
                Title = _item.GetDataType(),
                HeroImage = heroImage,
                Metadata = metadata,
            };
        }

        if (_item.IsText)
        {
            return new Details
            {
                Title = _item.GetDataType(),
                Body = $"```text\n{_item.Content}\n```",
                Metadata = metadata,
            };
        }

        return new Details { Title = _item.GetDataType() };
    }

    private static List<string> StripLeadingWhitespace(IEnumerable<string> lines)
    {
        // Determine the minimum leading whitespace
        var minLeadingWhitespace = lines
            .Min(static line => line.TakeWhile(char.IsWhiteSpace).Count());

        // Remove the minimum leading whitespace from each line
        var shiftedLines = lines.Select(line =>
            line.Length >= minLeadingWhitespace
                ? line[minLeadingWhitespace..]
                : line).ToList();

        return shiftedLines;
    }
}
