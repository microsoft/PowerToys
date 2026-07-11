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

    private readonly ISettingsInterface _settings;
    private readonly System.Timers.Timer _timer;
    private readonly Action? _onUpdated;
    private readonly Func<DateTime> _clock;
    private readonly object _updateLock = new();
    private readonly OpenUrlCommand _notificationCenterCommand;
    private readonly NoOpCommand _noOpCommand;
    private bool _timeWithSeconds;
    private bool _disposed;
    private (bool Seconds, int DateMode, string CustomFormat, bool NotificationCenter, int FirstWeek, int FirstDay) _appliedSettings;

    private CopyTextCommand _copyTimeCommand;
    private CopyTextCommand _copyDateCommand;
    private CopyTextCommand _copyWeekNumberCommand;
    private bool? _weekNumberShown;
    private bool? _notificationCenterOnClick;

    internal CopyTextCommand CopyTimeCommand => _copyTimeCommand;

    internal CopyTextCommand CopyDateCommand => _copyDateCommand;

    internal CopyTextCommand CopyWeekNumberCommand => _copyWeekNumberCommand;

    internal NowDockBand(ISettingsInterface settings, Action? onUpdated = null, Func<DateTime>? clock = null)
    {
        _settings = settings;
        _appliedSettings = ReadSettings();
        _timeWithSeconds = _appliedSettings.Seconds;
        _onUpdated = onUpdated;
        _clock = clock ?? (() => DateTime.Now);

        // Open Notification Center on click (can be turned off in the settings)
        _notificationCenterCommand = new OpenUrlCommand("ms-actioncenter:")
        {
            Id = "com.microsoft.cmdpal.timedate.dockBand",
            Name = Resources.timedate_show_notification_center_command_name,
            Result = CommandResult.Dismiss(),
        };
        _noOpCommand = new NoOpCommand() { Id = "com.microsoft.cmdpal.timedate.dockBand", Name = Resources.Microsoft_plugin_timedate_dock_band_title };
        _copyTimeCommand = new CopyTextCommand(string.Empty) { Name = Resources.timedate_copy_time_command_name };
        _copyDateCommand = new CopyTextCommand(string.Empty) { Name = Resources.timedate_copy_date_command_name };
        _copyWeekNumberCommand = new CopyTextCommand(string.Empty) { Name = Resources.timedate_copy_week_number_command_name };

        UpdateText();

        _timer = new System.Timers.Timer() { AutoReset = true };
        ConfigureTimer();
    }

    // Re-reads the settings and, if any of them changed, refreshes the displayed text.
    // If the "show seconds" preference changed, the timer cadence is reconfigured as
    // well. Safe to call at any time (e.g. from a settings-changed handler) so the dock
    // clock stays in sync without requiring the app to restart.
    internal void UpdateSettings()
    {
        var settings = ReadSettings();
        if (_appliedSettings == settings)
        {
            return;
        }

        var secondsChanged = _appliedSettings.Seconds != settings.Seconds;
        _appliedSettings = settings;
        _timeWithSeconds = settings.Seconds;
        if (secondsChanged)
        {
            ConfigureTimer();
        }

        UpdateText();
    }

    private (bool Seconds, int DateMode, string CustomFormat, bool NotificationCenter, int FirstWeek, int FirstDay) ReadSettings() =>
        (_settings.DockClockWithSecond,
         _settings.ClockBandDateMode,
         _settings.CustomDateFormatInClockBand,
         _settings.ClockBandOpensNotificationCenter,
         _settings.FirstWeekOfYear,
         _settings.FirstDayOfWeek);

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
        // Runs on the timer thread and on the settings changed event; serialize the
        // updates so observers never see a half-applied state.
        lock (_updateLock)
        {
            if (_disposed)
            {
                return;
            }

            UpdateTextCore();
        }

        _onUpdated?.Invoke(); // Must remain last — ViewModel reads Title/Subtitle via GetItems() on callback
    }

    private void UpdateTextCore()
    {
        var now = _clock();
        var timeString = now.ToString(
            TimeAndDateHelper.GetStringFormat(FormatStringType.Time, _timeWithSeconds, false),
            CultureInfo.CurrentCulture);
        var dateString = now.ToString(
            TimeAndDateHelper.GetStringFormat(FormatStringType.Date, false, false),
            CultureInfo.CurrentCulture);

        var dateMode = _settings.ClockBandDateMode;
        var subtitleString = TimeAndDateHelper.GetClockBandDateLine(now, _settings);

        // The week number is part of the band (subtitle and copy command) in the
        // week number and ISO week date modes. The copy command matches what is
        // shown: the configured week number in mode 1, the ISO week number in mode 2.
        var showWeekNumber = dateMode is 1 or 2;
        if (showWeekNumber)
        {
            var weekNumber = dateMode == 2 ? ISOWeek.GetWeekOfYear(now) : TimeAndDateHelper.GetWeekOfYear(now, _settings);
            _copyWeekNumberCommand.Text = weekNumber.ToString(CultureInfo.CurrentCulture);
        }

        Title = timeString;
        Subtitle = subtitleString;

        _copyTimeCommand.Text = timeString;
        _copyDateCommand.Text = dateString;

        var notificationCenterOnClick = _settings.ClockBandOpensNotificationCenter;
        if (_notificationCenterOnClick != notificationCenterOnClick)
        {
            _notificationCenterOnClick = notificationCenterOnClick;
            Command = notificationCenterOnClick ? _notificationCenterCommand : _noOpCommand;
        }

        // Only rebuild the context menu when the setting changed.
        if (_weekNumberShown != showWeekNumber)
        {
            _weekNumberShown = showWeekNumber;
            MoreCommands = showWeekNumber
                ? [
                    new CommandContextItem(_copyTimeCommand),
                    new CommandContextItem(_copyDateCommand),
                    new CommandContextItem(_copyWeekNumberCommand)
                ]
                : [
                    new CommandContextItem(_copyTimeCommand),
                    new CommandContextItem(_copyDateCommand)
                ];
        }
    }

    public void Dispose()
    {
        lock (_updateLock)
        {
            _disposed = true;
        }

        _timer.Stop();
        _timer.Dispose();
    }
}
