// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate;

internal sealed partial class NowDockBand : ListItem, IDisposable
{
    private readonly System.Timers.Timer _timer;
    private readonly Action? _onUpdated;
    private readonly Func<DateTime> _clock;

    internal CopyTextCommand _copyTimeCommand;
    internal CopyTextCommand _copyDateCommand;

    internal NowDockBand(Action? onUpdated = null, Func<DateTime>? clock = null)
    {
        _onUpdated = onUpdated;
        _clock = clock ?? (() => DateTime.Now);

        Command = new NoOpCommand() { Id = "com.microsoft.cmdpal.timedate.dockBand" };
        _copyTimeCommand = new CopyTextCommand(string.Empty) { Name = Resources.timedate_copy_time_command_name };
        _copyDateCommand = new CopyTextCommand(string.Empty) { Name = Resources.timedate_copy_date_command_name };
        MoreCommands =
        [
            new CommandContextItem(_copyTimeCommand),
            new CommandContextItem(_copyDateCommand),
        ];

        UpdateText();

        _timer = new System.Timers.Timer(1000) { AutoReset = true };
        _timer.Elapsed += (_, _) => UpdateText();
        _timer.Start();
    }

    internal void UpdateText()
    {
        var now = _clock();
        var timeString = now.ToString(
            TimeAndDateHelper.GetStringFormat(FormatStringType.Time, true, false),
            CultureInfo.CurrentCulture);
        var dateString = now.ToString(
            TimeAndDateHelper.GetStringFormat(FormatStringType.Date, false, false),
            CultureInfo.CurrentCulture);

        Title = timeString;
        Subtitle = dateString;
        _copyTimeCommand.Text = timeString;
        _copyDateCommand.Text = dateString;

        _onUpdated?.Invoke(); // Must remain last — ViewModel reads Title/Subtitle via GetItems() on callback
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
