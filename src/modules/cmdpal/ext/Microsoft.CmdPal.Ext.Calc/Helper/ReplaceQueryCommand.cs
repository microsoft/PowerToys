// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public sealed partial class ReplaceQueryCommand : InvokableCommand
{
    public event TypedEventHandler<object, object> ReplaceRequested;

    public ReplaceQueryCommand()
    {
        Name = "Replace query";
        Icon = new IconInfo("\uE70F"); // Edit icon
    }

    public override ICommandResult Invoke()
    {
        ReplaceRequested?.Invoke(this, null);
        return CommandResult.KeepOpen();
    }
}
