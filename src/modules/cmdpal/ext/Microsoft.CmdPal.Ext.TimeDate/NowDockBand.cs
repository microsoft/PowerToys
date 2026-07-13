// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CmdPal.Ext.TimeDate.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate;

internal sealed partial class NowDockBand : ListItem, IDisposable
{
    private readonly Func<DateTime> _clock;
    private readonly ClockUpdateService _clockUpdateService;
    private readonly ICommand _allClocksPage;
    private IDockClockSettings _settings;
    private CompiledClockFormat _titleFormat;
    private CompiledClockFormat _subtitleFormat;

    private CopyTextCommand _copyTimeCommand;
    private CopyTextCommand _copyDateCommand;

    internal CopyTextCommand CopyTimeCommand => _copyTimeCommand;

    internal CopyTextCommand CopyDateCommand => _copyDateCommand;

    internal NowDockBand(IDockClockSettings settings, ICommand allClocksPage, ClockUpdateService clockUpdateService, Func<DateTime>? clock = null)
    {
        _settings = settings;
        _allClocksPage = allClocksPage;
        _titleFormat = CustomClockDisplay.CompileFormat(settings.DockClockTitleFormat);
        _subtitleFormat = CustomClockDisplay.CompileFormat(settings.DockClockSubtitleFormat);
        _clock = clock ?? (() => DateTime.Now);
        _clockUpdateService = clockUpdateService;

        UpdateCommand(settings);
        _copyTimeCommand = new CopyTextCommand(string.Empty) { Name = Resources.timedate_copy_time_command_name };
        _copyDateCommand = new CopyTextCommand(string.Empty) { Name = Resources.timedate_copy_date_command_name };
        MoreCommands =
        [
            new CommandContextItem(_copyTimeCommand),
            new CommandContextItem(_copyDateCommand),
        ];

        UpdateText();
        MoreCommands = [.. MoreCommands, new CommandContextItem(new EditDefaultDockClockPage(settings))];
        _clockUpdateService.Subscribe(this, ClockUpdateService_Tick, RequiresSecondUpdates);
    }

    internal void UpdateSettings(IDockClockSettings settings)
    {
        _settings = settings;
        _titleFormat = CustomClockDisplay.CompileFormat(settings.DockClockTitleFormat);
        _subtitleFormat = CustomClockDisplay.CompileFormat(settings.DockClockSubtitleFormat);
        UpdateCommand(settings);
        _clockUpdateService.SetRequiresSecondUpdates(this, RequiresSecondUpdates);
        UpdateText();
    }

    private bool RequiresSecondUpdates => _titleFormat.RequiresSecondUpdates || _subtitleFormat.RequiresSecondUpdates;

    private void UpdateCommand(IDockClockSettings settings)
    {
        Command = settings.DockClockClickAction == "allClocks"
            ? _allClocksPage
            : new OpenUrlCommand("ms-actioncenter:")
            {
                Id = "com.microsoft.cmdpal.timedate.dockBand",
                Name = Resources.timedate_show_notification_center_command_name,
                Result = CommandResult.Dismiss(),
            };
    }

    private void ClockUpdateService_Tick(object? sender, EventArgs e)
    {
        UpdateText();
    }

    internal void UpdateText()
    {
        var now = _clock();
        var timeString = GetTitle(now);
        var dateString = GetSubtitle(now);
        if (timeString == Title && dateString == Subtitle)
        {
            return;
        }

        Title = timeString;
        Subtitle = dateString;
        _copyTimeCommand.Text = timeString;
        _copyDateCommand.Text = dateString;
    }

    private string GetTitle(DateTime now) => CustomClockDisplay.Format(new DateTimeOffset(now), _titleFormat, _settings);

    private string GetSubtitle(DateTime now) => CustomClockDisplay.Format(new DateTimeOffset(now), _subtitleFormat, _settings);

    public void Dispose()
    {
        _clockUpdateService.Unsubscribe(this);
    }
}
