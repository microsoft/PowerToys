// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerToys.Commands;

internal sealed partial class CopyColorCommand : InvokableCommand
{
    private readonly Color _color;

    public CopyColorCommand(Color color)
    {
        _color = color;
        Name = $"Copy color {color}";
        Icon = new IconInfo("\uE790"); // Color icon (more appropriate than copy icon)
    }

    public override ICommandResult Invoke()
    {
        ClipboardHelper.SetText($"#{_color.R:X2}{_color.G:X2}{_color.B:X2}");
        return CommandResult.ShowToast($"Copied '#{_color.R:X2}{_color.G:X2}{_color.B:X2}' to clipboard");
    }
}
