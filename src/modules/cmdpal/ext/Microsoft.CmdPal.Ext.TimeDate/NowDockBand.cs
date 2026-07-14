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
    private readonly CopyTextCommand _copyTitleCommand;
    private readonly CopyTextCommand _copySubtitleCommand;
    private IDockClockSettings _settings;
    private CompiledClockFormat _titleFormat;
    private CompiledClockFormat _subtitleFormat;
    private CompiledClockFormat? _copyFormat;
    private CopyCurrentClockFormatCommand? _copyCustomFormatCommand;

    internal CopyTextCommand CopyTitleCommand => _copyTitleCommand;

    internal CopyTextCommand CopySubtitleCommand => _copySubtitleCommand;

    internal CopyCurrentClockFormatCommand? CopyCustomFormatCommand => _copyCustomFormatCommand;

    internal NowDockBand(IDockClockSettings settings, ICommand allClocksPage, ClockUpdateService clockUpdateService, Func<DateTime>? clock = null)
    {
        _settings = settings;
        _allClocksPage = allClocksPage;
        _titleFormat = CustomClockDisplay.CompileFormat(settings.DockClockTitleFormat);
        _subtitleFormat = CustomClockDisplay.CompileFormat(settings.DockClockSubtitleFormat);
        _copyFormat = CompileOptionalFormat(settings.DockClockCopyFormat);
        _clock = clock ?? (() => DateTime.Now);
        _clockUpdateService = clockUpdateService;
        _copyTitleCommand = new CopyTextCommand(string.Empty);
        _copySubtitleCommand = new CopyTextCommand(string.Empty);

        UpdateCommand(settings);
        UpdateCopyCommandNames(settings);
        UpdateMoreCommands(settings);
        UpdateText();
        _clockUpdateService.Subscribe(this, ClockUpdateService_Tick, RequiresSecondUpdates);
    }

    internal void UpdateSettings(IDockClockSettings settings)
    {
        _settings = settings;
        _titleFormat = CustomClockDisplay.CompileFormat(settings.DockClockTitleFormat);
        _subtitleFormat = CustomClockDisplay.CompileFormat(settings.DockClockSubtitleFormat);
        _copyFormat = CompileOptionalFormat(settings.DockClockCopyFormat);
        UpdateCommand(settings);
        UpdateCopyCommandNames(settings);
        UpdateMoreCommands(settings);
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
                Id = CustomClockIds.LocalDockBand,
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
        _copyTitleCommand.Text = timeString;
        _copySubtitleCommand.Text = dateString;
    }

    private string GetTitle(DateTime now) => CustomClockDisplay.Format(new DateTimeOffset(now), _titleFormat, _settings);

    private string GetSubtitle(DateTime now) => CustomClockDisplay.Format(new DateTimeOffset(now), _subtitleFormat, _settings);

    private string GetCustomCopyText()
    {
        var format = _copyFormat;
        return format is null ? string.Empty : CustomClockDisplay.Format(new DateTimeOffset(_clock()), format, _settings);
    }

    private void UpdateMoreCommands(IDockClockSettings settings)
    {
        _copyCustomFormatCommand = _copyFormat is null
            ? null
            : new CopyCurrentClockFormatCommand(CustomClockFormatOptions.GetCopyCommandName(settings, settings.DockClockCopyFormat), GetCustomCopyText);
        MoreCommands =
        [
            new CommandContextItem(_copyTitleCommand),
            new CommandContextItem(_copySubtitleCommand),
            .. _copyCustomFormatCommand is null ? [] : new CommandContextItem[] { new(_copyCustomFormatCommand) },
            new CommandContextItem(new EditDefaultDockClockPage(settings)),
        ];
    }

    private void UpdateCopyCommandNames(IDockClockSettings settings)
    {
        _copyTitleCommand.Name = CustomClockFormatOptions.GetCopyCommandName(settings, settings.DockClockTitleFormat);
        _copySubtitleCommand.Name = CustomClockFormatOptions.GetCopyCommandName(settings, settings.DockClockSubtitleFormat);
    }

    private static CompiledClockFormat? CompileOptionalFormat(string format) => string.IsNullOrEmpty(format) ? null : CustomClockDisplay.CompileFormat(format);

    public void Dispose()
    {
        _clockUpdateService.Unsubscribe(this);
    }
}
