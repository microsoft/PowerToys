// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Calc.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

internal sealed partial class DeleteHistoryItemCommand : InvokableCommand
{
    private readonly ISettingsInterface _settings;
    private readonly Guid _historyItemId;

    public DeleteHistoryItemCommand(ISettingsInterface settings, Guid historyItemId)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
        _historyItemId = historyItemId;
        Name = Resources.calculator_history_delete;
        Icon = Icons.DeleteIcon;
    }

    public override CommandResult Invoke()
    {
        _settings.RemoveHistoryItem(_historyItemId);
        return CommandResult.KeepOpen();
    }
}
