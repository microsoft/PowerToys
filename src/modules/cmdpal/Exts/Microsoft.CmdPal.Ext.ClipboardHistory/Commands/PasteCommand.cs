// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

using Windows.ApplicationModel.DataTransfer;
using Windows.Networking.NetworkOperators;
using Windows.System;
using WinRT.Interop;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Commands;

internal sealed partial class PasteCommand : InvokableCommand
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private readonly ClipboardItem _clipboardItem;
    private readonly ClipboardFormat _clipboardFormat;

    private const int HIDE = 0;
    private const int SHOW = 5;

    internal PasteCommand(ClipboardItem clipboardItem, ClipboardFormat clipboardFormat)
    {
        _clipboardItem = clipboardItem;
        _clipboardFormat = clipboardFormat;
        Name = "Paste";
        Icon = new("\xE8C8"); // Copy icon
    }

    private void HideWindow()
    {
        var hostHwnd = ExtensionHost.Host.HostingHwnd;

        ShowWindow(new IntPtr((long)hostHwnd), HIDE);
    }

    private void ShowWindow()
    {
        var hostHwnd = ExtensionHost.Host.HostingHwnd;

        ShowWindow(new IntPtr((long)hostHwnd), SHOW);
    }

    public override CommandResult Invoke()
    {
        ClipboardHelper.SetClipboardContent(_clipboardItem, _clipboardFormat);
        HideWindow();
        ClipboardHelper.SendPasteKeyCombination();
        Clipboard.DeleteItemFromHistory(_clipboardItem.Item);
        CommandResult.ShowToast("Pasting.");
        return CommandResult.Dismiss();
    }
}
