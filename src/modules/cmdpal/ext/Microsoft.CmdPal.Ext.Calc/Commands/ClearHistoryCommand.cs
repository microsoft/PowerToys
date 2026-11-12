// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Calc.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

internal sealed partial class ClearHistoryCommand : InvokableCommand
{
    private readonly ISettingsInterface _settings;

    public ClearHistoryCommand(ISettingsInterface settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
        Name = Resources.calculator_history_delete_all;
        Icon = Icons.DeleteIcon;
    }

    public override CommandResult Invoke()
    {
        _settings.ClearHistory();
        return CommandResult.KeepOpen();
    }
}
