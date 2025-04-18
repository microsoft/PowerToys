// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.System.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell;

public sealed partial class EmptyRecycleBinCommand : InvokableCommand
{
    public EmptyRecycleBinCommand(bool settingEmptyRBSuccesMsg)
    {
        _settingEmptyRBSuccesMsg = settingEmptyRBSuccesMsg;
    }

    public override CommandResult Invoke()
    {
        Task.Run(() => ResultHelper.EmptyRecycleBinTask(_settingEmptyRBSuccesMsg));

        return CommandResult.Dismiss();
    }

    private bool _settingEmptyRBSuccesMsg;
}
