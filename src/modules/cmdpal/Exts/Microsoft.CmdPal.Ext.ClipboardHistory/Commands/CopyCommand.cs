// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.DataTransfer;
using Windows.Networking.NetworkOperators;
using Windows.UI;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Commands;

internal sealed partial class CopyCommand : InvokableCommand
{
    private readonly ClipboardItem _clipboardItem;
    private readonly ClipboardFormat _clipboardFormat;

    internal CopyCommand(ClipboardItem clipboardItem, ClipboardFormat clipboardFormat)
    {
        _clipboardItem = clipboardItem;
        _clipboardFormat = clipboardFormat;
        Name = "Copy";
        if (clipboardFormat == ClipboardFormat.Text)
        {
            Icon = new("\xE8C8"); // Copy icon
        }
        else
        {
            Icon = new("\xE8B9"); // Picture icon
        }
    }

    public override CommandResult Invoke()
    {
        ClipboardHelper.SetClipboardContent(_clipboardItem, _clipboardFormat);
        CommandResult.ShowToast("Copied to clipboard");
        return CommandResult.Dismiss();
    }
}
