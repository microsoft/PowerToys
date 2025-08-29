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

    public EmojiListItem(string emoji)
        : base(new CopyTextCommand(emoji) { Icon = new IconInfo(emoji) })
    {
        _emoji = emoji;
        Title = emoji;
    }
}
