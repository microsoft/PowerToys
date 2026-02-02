// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.Common.Messages;
using Microsoft.CmdPal.Ext.Calc.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public sealed partial class CalculatorPasteCommand : InvokableCommand
{
    public event TypedEventHandler<object, object> ReplaceRequested;

    private readonly ISettingsInterface _settings;
    private readonly bool _canStoreHistory;
    private string _query;
    private string _text;

    public CalculatorPasteCommand(string result, string query, ISettingsInterface settings, bool canStoreHistory = true)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
        _canStoreHistory = canStoreHistory;
        _query = query ?? string.Empty;
        _text = result;
        Name = Resources.calculator_paste_command_name;
        Icon = Icons.PasteIcon;
    }

    private static void HideWindow()
    {
        // TODO GH #524: This isn't great - this requires us to have Secret Sauce in
        // the clipboard extension to be able to manipulate the HWND.
        // We probably need to put some window manipulation into the API, but
        // what form that takes is not clear yet.
        WeakReferenceMessenger.Default.Send<HideWindowMessage>();
    }

    public void Update(string text, string query)
    {
        _text = text;
        _query = query ?? string.Empty;
    }

    public override ICommandResult Invoke()
    {
        ClipboardHelper.SetText(_text);
        if (_canStoreHistory)
        {
            _settings.AddHistoryItem(new HistoryItem(_query, _text, DateTime.UtcNow));
        }

        HideWindow();

        // Give the window some time to hide, and allow the other app to gain focus.
        // Since we don't currently have a way to wait until the other window is ready
        // to receive input, we just wing it with a short delay.
        Thread.Sleep(200);

        PasteHelper.SendPasteKeyCombination();

        ReplaceRequested?.Invoke(this, null);

        return CommandResult.ShowToast(new ToastArgs()
        {
            Message = Resources.calculator_paste_toast_text,
            Result = CommandResult.KeepOpen(),
        });
    }
}
