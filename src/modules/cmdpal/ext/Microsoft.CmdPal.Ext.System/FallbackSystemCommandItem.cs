// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.System.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.System;

internal sealed partial class FallbackSystemCommandItem : FallbackCommandItem
{
    private const string _id = "com.microsoft.cmdpal.builtin.system.fallback";

    private readonly ISettingsInterface _settings;

    // Whether Windows Update was waiting for a restart the last time the command list
    // was built. Unlike the other inputs this can change during a session, so we track
    // it and rebuild the list when it flips instead of capturing it once.
    private bool _isUpdatePending;

    private List<IListItem> systemCommands;

    public FallbackSystemCommandItem(ISettingsInterface settings)
        : base(new NoOpCommand(), Resources.Microsoft_plugin_ext_fallback_display_title, _id)
    {
        Title = string.Empty;
        Subtitle = string.Empty;
        Icon = Icons.LockIcon;

        _settings = settings;
        _isUpdatePending = settings.IsUpdatePending();
        systemCommands = BuildSystemCommands(_isUpdatePending);
    }

    private List<IListItem> BuildSystemCommands(bool isUpdatePending)
    {
        var isBootedInUefiMode = _settings.GetSystemFirmwareType() == FirmwareType.Uefi;
        var hideEmptyRB = _settings.HideEmptyRecycleBin();
        var confirmSystemCommands = _settings.ShowDialogToConfirmCommand();
        var showSuccessOnEmptyRB = _settings.ShowSuccessMessageAfterEmptyingRecycleBin();

        return Commands.GetSystemCommands(isBootedInUefiMode, isUpdatePending, hideEmptyRB, confirmSystemCommands, showSuccessOnEmptyRB);
    }

    public override void UpdateQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Command = null;
            Title = string.Empty;
            Subtitle = string.Empty;
            return;
        }

        // The update-pending state can change while CmdPal is running (an update gets
        // staged, or a restart clears it). Re-check it and rebuild the list only when it
        // changes so the "Update and restart"/"shut down" items don't go stale. The
        // underlying WUAPI query is cached, so this stays cheap per keystroke.
        var isUpdatePending = _settings.IsUpdatePending();
        if (isUpdatePending != _isUpdatePending)
        {
            _isUpdatePending = isUpdatePending;
            systemCommands = BuildSystemCommands(isUpdatePending);
        }

        IListItem? result = null;
        var resultScore = 0;

        // find the max score for the query
        foreach (var command in systemCommands)
        {
            var title = command.Title;
            var subTitle = command.Subtitle;
            var titleScore = FuzzyStringMatcher.ScoreFuzzy(query, title);
            var subTitleScore = FuzzyStringMatcher.ScoreFuzzy(query, subTitle);

            var maxScore = Math.Max(titleScore, subTitleScore);
            if (maxScore > resultScore)
            {
                resultScore = maxScore;
                result = command;
            }
        }

        if (result is null)
        {
            Command = null;
            Title = string.Empty;
            Subtitle = string.Empty;

            return;
        }

        Title = result.Title;
        Subtitle = result.Subtitle;
        Icon = result.Icon;
        Command = result.Command;
    }
}
