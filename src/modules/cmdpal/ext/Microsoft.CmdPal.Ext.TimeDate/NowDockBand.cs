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
    private static readonly TimeSpan PerSecondUpdateInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PerMinuteUpdateInterval = TimeSpan.FromMinutes(1);

    // Manually set the icon to blank, to override the "open link" icon on the
    // command itself
    public override IconInfo Icon => new(string.Empty);

    private readonly System.Timers.Timer _timer;
    private readonly Action? _onUpdated;
    private readonly Func<DateTime> _clock;
    private bool _timeWithSeconds;

    private CopyTextCommand _copyTimeCommand;
    private CopyTextCommand _copyDateCommand;

    internal CopyTextCommand CopyTimeCommand => _copyTimeCommand;

    internal CopyTextCommand CopyDateCommand => _copyDateCommand;

    internal NowDockBand(bool timeWithSeconds = false, Action? onUpdated = null, Func<DateTime>? clock = null)
    {
        _timeWithSeconds = timeWithSeconds;
        _onUpdated = onUpdated;
        _clock = clock ?? (() => DateTime.Now);

        Command = new OpenUrlCommand("ms-actioncenter:")
        {
            Id = "com.microsoft.cmdpal.timedate.dockBand",
            Name = Resources.timedate_show_notification_center_command_name,
            Result = CommandResult.Dismiss(),
        };
        _copyTimeCommand = new CopyTextCommand(string.Empty) { Name = Resources.timedate_copy_time_command_name };
        _copyDateCommand = new CopyTextCommand(string.Empty) { Name = Resources.timedate_copy_date_command_name };
        MoreCommands =
        [
            new CommandContextItem(_copyTimeCommand),
            new CommandContextItem(_copyDateCommand),
        ];

        UpdateText();

        _timer = new System.Timers.Timer() { AutoReset = true };
        ConfigureTimer();
    }

    // Reads the current "show seconds" preference and, if it changed, reconfigures
    // the timer cadence and refreshes the displayed text. Safe to call at any time
    // (e.g. from a settings-changed handler) so the dock clock stays in sync without
    // requiring the app to restart.
    internal void UpdateSettings(bool timeWithSeconds)
    {
        if (_timeWithSeconds == timeWithSeconds)
        {
            return;
        }

        _timeWithSeconds = timeWithSeconds;
        ConfigureTimer();
        UpdateText();
    }

    private void ConfigureTimer()
    {
        _timer.Stop();
        _timer.Elapsed -= Timer_Elapsed;
        _timer.Elapsed -= Timer_ElapsedFirstMinuteTick;

        if (_timeWithSeconds)
        {
            _timer.Interval = PerSecondUpdateInterval.TotalMilliseconds;
            _timer.Elapsed += Timer_Elapsed;
        }
        else
        {
            // Align the first tick to the next minute boundary so the clock flips
            // exactly when the system clock does, then fall back to a per-minute cadence.
            var now = _clock();
            _timer.Interval = PerMinuteUpdateInterval.TotalMilliseconds - ((now.Second * 1000) + now.Millisecond);
            _timer.Elapsed += Timer_ElapsedFirstMinuteTick;
        }

        _timer.Start();
    }

    private void Timer_ElapsedFirstMinuteTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (sender is System.Timers.Timer timer)
        {
            timer.Interval = PerMinuteUpdateInterval.TotalMilliseconds;
            timer.Elapsed -= Timer_ElapsedFirstMinuteTick;
            timer.Elapsed += Timer_Elapsed;
        }

        Timer_Elapsed(sender, e);
    }

    private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        UpdateText();
    }

    internal void UpdateText()
    {
        var now = _clock();
        var timeString = now.ToString(
            TimeAndDateHelper.GetStringFormat(FormatStringType.Time, _timeWithSeconds, false),
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
