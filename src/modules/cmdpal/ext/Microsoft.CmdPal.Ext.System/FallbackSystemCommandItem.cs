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

    public FallbackSystemCommandItem(ISettingsInterface settings)
        : base(new NoOpCommand(), Resources.Microsoft_plugin_ext_fallback_display_title, _id)
    {
        Title = string.Empty;
        Subtitle = string.Empty;
        Icon = Icons.LockIcon;

        var isBootedInUefiMode = settings.GetSystemFirmwareType() == FirmwareType.Uefi;
        var hideEmptyRB = settings.HideEmptyRecycleBin();
        var confirmSystemCommands = settings.ShowDialogToConfirmCommand();
        var showSuccessOnEmptyRB = settings.ShowSuccessMessageAfterEmptyingRecycleBin();

        systemCommands = Commands.GetSystemCommands(isBootedInUefiMode, hideEmptyRB, confirmSystemCommands, showSuccessOnEmptyRB);
    }

    private readonly List<IListItem> systemCommands;

    public override void UpdateQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Command = null;
            Title = string.Empty;
            Subtitle = string.Empty;
            return;
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
