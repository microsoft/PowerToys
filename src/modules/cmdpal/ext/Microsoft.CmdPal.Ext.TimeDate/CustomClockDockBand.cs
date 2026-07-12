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
    private readonly Action _onUpdated;
    private readonly Func<DateTime> _utcNow;

    internal CustomClockDockBand(CustomClock clockDefinition, CustomClockManager clockManager, ISettingsInterface settings, ClockUpdateService clockUpdateService, Action onUpdated, Func<DateTime>? utcNow = null)
    {
        _clockDefinition = clockDefinition;
        _settings = settings;
        _clockUpdateService = clockUpdateService;
        _onUpdated = onUpdated;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _clockUpdateService.Tick += ClockUpdateService_Tick;
        _clockUpdateService.SetRequiresSecondUpdates(this, CustomClockDisplay.RequiresSecondUpdates(clockDefinition));
        MoreCommands = [new CommandContextItem(new EditCustomClockPage(clockManager, settings, clockDefinition, customizeDock: true))];
        UpdateText();
    }

    private void ClockUpdateService_Tick(object? sender, EventArgs e) => UpdateText();

    internal void UpdateText()
    {
        var now = CustomClockDisplay.GetCurrentTime(_clockDefinition, new DateTimeOffset(DateTime.SpecifyKind(_utcNow(), DateTimeKind.Utc)));
        var title = CustomClockDisplay.Format(now, _clockDefinition.TitleFormat, _settings);
        var subtitle = CustomClockDisplay.Format(now, _clockDefinition.SubtitleFormat, _settings);
        if (title == Title && subtitle == Subtitle)
        {
            return;
        }

        Title = title;
        Subtitle = subtitle;
        _onUpdated();
    }

    public void Dispose()
    {
        _clockUpdateService.Tick -= ClockUpdateService_Tick;
        _clockUpdateService.SetRequiresSecondUpdates(this, false);
    }
}
