// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public sealed partial class CalculatorCopyCommand : CopyTextCommand
{
    public event TypedEventHandler<object, object> ReplaceRequested;

    private readonly ISettingsInterface _settings;
    private readonly bool _canStoreHistory;
    private string _query;

    public CalculatorCopyCommand(string result, string query, ISettingsInterface settings, bool canStoreHistory = true)
        : base(result)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
        _canStoreHistory = canStoreHistory;
        _query = query ?? string.Empty;
        Name = Properties.Resources.calculator_copy_command_name;
        Result = ResultHelper.CreateCopyCommandResult(settings.CloseOnEnter);
    }

    public void Update(string text, string query)
    {
        Text = text;
        _query = query ?? string.Empty;
    }

    public override ICommandResult Invoke()
    {
        ClipboardHelper.SetText(Text);
        if (_canStoreHistory)
        {
            _settings.AddHistoryItem(new HistoryItem(_query, Text, DateTime.UtcNow));
        }

        ReplaceRequested?.Invoke(this, null);

        return Result;
    }
}
