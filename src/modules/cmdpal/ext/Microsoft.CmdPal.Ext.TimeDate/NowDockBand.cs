// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CmdPal.Ext.TimeDate.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate;

internal sealed partial class NowDockBand : ListItem, IDisposable
{
    private readonly Func<DateTime> _clock;
    private readonly ClockUpdateService _clockUpdateService;
    private readonly bool _ownsClockUpdateService;
    private bool _timeWithSeconds;
    private ISettingsInterface? _settings;
    private ICommand? _allClocksPage;
    private string? _titleFormat;
    private string? _subtitleFormat;

    private CopyTextCommand _copyTimeCommand;
    private CopyTextCommand _copyDateCommand;

    internal CopyTextCommand CopyTimeCommand => _copyTimeCommand;

    internal CopyTextCommand CopyDateCommand => _copyDateCommand;

    internal NowDockBand(bool timeWithSeconds = false, Func<DateTime>? clock = null, ClockUpdateService? clockUpdateService = null)
    {
        _timeWithSeconds = timeWithSeconds;
        _clock = clock ?? (() => DateTime.Now);
        _clockUpdateService = clockUpdateService ?? new ClockUpdateService();
        _ownsClockUpdateService = clockUpdateService is null;

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

        _clockUpdateService.Tick += ClockUpdateService_Tick;
        _clockUpdateService.SetRequiresSecondUpdates(this, _timeWithSeconds);
    }

    internal NowDockBand(IDockClockSettings settings, ICommand allClocksPage, Func<DateTime>? clock = null, ClockUpdateService? clockUpdateService = null)
        : this(false, clock, clockUpdateService)
    {
        _allClocksPage = allClocksPage;
        UpdateSettings(settings);
        MoreCommands = [.. MoreCommands, new CommandContextItem(new EditDefaultDockClockPage(settings))];
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
        _clockUpdateService.SetRequiresSecondUpdates(this, _timeWithSeconds);
        UpdateText();
    }

    internal void UpdateSettings(IDockClockSettings settings)
    {
        _settings = settings;
        _titleFormat = settings.DockClockTitleFormat;
        _subtitleFormat = settings.DockClockSubtitleFormat;
        Command = settings.DockClockClickAction == "allClocks" && _allClocksPage is not null
            ? _allClocksPage
            : new OpenUrlCommand("ms-actioncenter:")
            {
                Id = "com.microsoft.cmdpal.timedate.dockBand",
                Name = Resources.timedate_show_notification_center_command_name,
                Result = CommandResult.Dismiss(),
            };
        _clockUpdateService.SetRequiresSecondUpdates(this, CustomClockDisplay.RequiresSecondUpdates(_titleFormat, _subtitleFormat));
        UpdateText();
    }

    private void ClockUpdateService_Tick(object? sender, EventArgs e)
    {
        var now = _clock();
        var timeString = GetTitle(now);
        var dateString = GetSubtitle(now);
        if (timeString != Title || dateString != Subtitle)
        {
            UpdateText();
        }
    }

    internal void UpdateText()
    {
        var now = _clock();
        var timeString = GetTitle(now);
        var dateString = GetSubtitle(now);

        Title = timeString;
        Subtitle = dateString;
        _copyTimeCommand.Text = timeString;
        _copyDateCommand.Text = dateString;
    }

    private string GetTitle(DateTime now) => _settings is null
        ? now.ToString(TimeAndDateHelper.GetStringFormat(FormatStringType.Time, _timeWithSeconds, false), CultureInfo.CurrentCulture)
        : CustomClockDisplay.Format(new DateTimeOffset(now), _titleFormat!, _settings);

    private string GetSubtitle(DateTime now) => _settings is null
        ? now.ToString(TimeAndDateHelper.GetStringFormat(FormatStringType.Date, false, false), CultureInfo.CurrentCulture)
        : CustomClockDisplay.Format(new DateTimeOffset(now), _subtitleFormat!, _settings);

    public void Dispose()
    {
        _clockUpdateService.Tick -= ClockUpdateService_Tick;
        _clockUpdateService.SetRequiresSecondUpdates(this, false);
        if (_ownsClockUpdateService)
        {
            _clockUpdateService.Dispose();
        }
    }
}
