// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CmdPal.Ext.TimeDate.Pages;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate;

internal sealed partial class CustomClockDockBand : ListItem, IDisposable
{
    private readonly CustomClock _clockDefinition;
    private readonly ISettingsInterface _settings;
    private readonly ClockUpdateService _clockUpdateService;
    private readonly Func<DateTime> _utcNow;
    private readonly CompiledClockFormat _titleFormat;
    private readonly CompiledClockFormat _subtitleFormat;
    private readonly CompiledClockFormat? _copyFormat;
    private readonly TimeZoneInfo? _explicitTimeZone;
    private readonly CopyTextCommand _copyTitleCommand;
    private readonly CopyTextCommand _copySubtitleCommand;
    private readonly CopyCurrentClockFormatCommand? _copyCustomFormatCommand;

    internal Guid ClockId => _clockDefinition.Id;

    internal CopyTextCommand CopyTitleCommand => _copyTitleCommand;

    internal CopyTextCommand CopySubtitleCommand => _copySubtitleCommand;

    internal CopyCurrentClockFormatCommand? CopyCustomFormatCommand => _copyCustomFormatCommand;

    internal CustomClockDockBand(CustomClock clockDefinition, CustomClockManager clockManager, ISettingsInterface settings, ClockUpdateService clockUpdateService, Func<DateTime>? utcNow = null)
    {
        _clockDefinition = clockDefinition;
        _settings = settings;
        _clockUpdateService = clockUpdateService;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _titleFormat = CustomClockDisplay.CompileFormat(clockDefinition.TitleFormat);
        _subtitleFormat = CustomClockDisplay.CompileFormat(clockDefinition.SubtitleFormat);
        _copyFormat = string.IsNullOrEmpty(clockDefinition.CopyFormat) ? null : CustomClockDisplay.CompileFormat(clockDefinition.CopyFormat);
        _explicitTimeZone = CustomClockDisplay.ResolveExplicitTimeZone(clockDefinition);
        _copyTitleCommand = new CopyTextCommand(string.Empty)
        {
            Name = CustomClockFormatOptions.GetCopyCommandName(settings, clockDefinition.TitleFormat),
        };
        _copySubtitleCommand = new CopyTextCommand(string.Empty)
        {
            Name = CustomClockFormatOptions.GetCopyCommandName(settings, clockDefinition.SubtitleFormat),
        };
        _copyCustomFormatCommand = _copyFormat is null
            ? null
            : new CopyCurrentClockFormatCommand(CustomClockFormatOptions.GetCopyCommandName(settings, clockDefinition.CopyFormat), GetCustomCopyText);
        _clockUpdateService.Subscribe(this, ClockUpdateService_Tick, _titleFormat.RequiresSecondUpdates || _subtitleFormat.RequiresSecondUpdates);
        MoreCommands =
        [
            new CommandContextItem(_copyTitleCommand),
            new CommandContextItem(_copySubtitleCommand),
            .. _copyCustomFormatCommand is null ? [] : new CommandContextItem[] { new(_copyCustomFormatCommand) },
            new CommandContextItem(new EditCustomClockPage(clockManager, settings, clockDefinition, customizeDock: true)),
        ];
        UpdateText();
    }

    private void ClockUpdateService_Tick(object? sender, EventArgs e) => UpdateText();

    internal void UpdateText()
    {
        var now = CustomClockDisplay.GetCurrentTime(_explicitTimeZone ?? TimeZoneInfo.Local, new DateTimeOffset(DateTime.SpecifyKind(_utcNow(), DateTimeKind.Utc)));
        var title = CustomClockDisplay.Format(now, _titleFormat, _settings);
        var subtitle = CustomClockDisplay.Format(now, _subtitleFormat, _settings);
        if (title == Title && subtitle == Subtitle)
        {
            return;
        }

        Title = title;
        Subtitle = subtitle;
        _copyTitleCommand.Text = title;
        _copySubtitleCommand.Text = subtitle;
    }

    private string GetCustomCopyText()
    {
        var format = _copyFormat;
        if (format is null)
        {
            return string.Empty;
        }

        var now = CustomClockDisplay.GetCurrentTime(_explicitTimeZone ?? TimeZoneInfo.Local, new DateTimeOffset(DateTime.SpecifyKind(_utcNow(), DateTimeKind.Utc)));
        return CustomClockDisplay.Format(now, format, _settings);
    }

    public void Dispose()
    {
        _clockUpdateService.Unsubscribe(this);
    }
}
