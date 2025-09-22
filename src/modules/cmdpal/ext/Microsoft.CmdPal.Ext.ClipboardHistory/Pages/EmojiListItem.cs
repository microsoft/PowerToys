// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Win32;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Pages;

internal sealed partial class EmojiListItem : ListItem
{
    private readonly string _emoji;
    private readonly IconInfo _icon;
    public override IconInfo Icon => _icon;
    public EmojiListItem(string emoji)
        : base()
    {
        _emoji = emoji;
        _icon = new IconInfo(emoji);
        Title = emoji;

        DataPackage textDataPackage = new()
        {
            RequestedOperation = DataPackageOperation.Copy,
        };
        textDataPackage.SetText(emoji);

        ClipboardItem content = new()
        {
            Item = textDataPackage,
        };
        
        var copyCommand = new CopyTextCommand(emoji) { Icon = _icon };

        var pasteCommand = new PasteCommand(content, ClipboardFormat.Text, null)
        {
            Icon = _icon,
            Name = Properties.Resources.paste_command_name,
        };

        Command = pasteCommand;
        MoreCommands = [ new CommandContextItem(copyCommand) ];
    }
}
