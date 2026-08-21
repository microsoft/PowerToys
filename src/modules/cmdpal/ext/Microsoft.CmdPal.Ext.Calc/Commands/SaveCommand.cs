// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Calc.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public sealed partial class SaveCommand : InvokableCommand
{
    private readonly string _result;

    public event TypedEventHandler<object, object> SaveRequested;

    public SaveCommand(string result)
    {
        Name = Resources.calculator_save_command_name;
        Icon = Icons.SaveIcon;
        _result = result;
    }

    public override ICommandResult Invoke()
    {
        SaveRequested?.Invoke(this, this);
        ClipboardHelper.SetText(_result);
        return CommandResult.KeepOpen();
    }
}
