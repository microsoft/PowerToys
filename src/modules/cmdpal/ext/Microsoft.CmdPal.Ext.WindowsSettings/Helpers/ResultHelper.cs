// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

using Microsoft.CmdPal.Ext.WindowsSettings.Commands;
using Microsoft.CmdPal.Ext.WindowsSettings.Helpers;
using Microsoft.CmdPal.Ext.WindowsSettings.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsSettings;

/// <summary>
/// Helper class to easier work with List Items
/// </summary>
internal static class ResultHelper
{
    internal static List<ListItem> GetResultList(
        in IEnumerable<Classes.WindowsSetting> list,
        string query)
    {
        var resultList = new List<ListItem>(list.Count());

        foreach (var entry in list)
        {
            var result = new ListItem(new OpenSettingsCommand(entry))
            {
                Icon = IconHelpers.FromRelativePath("Assets\\WindowsSettings.svg"),
                Subtitle = entry.JoinedFullSettingsPath,
                Title = entry.Name,
                MoreCommands = ContextMenuHelper.GetContextMenu(entry).ToArray(),
            };

            // TODO GH #126 investigate tooltips
            // AddOptionalToolTip(entry, result);

            // There is a case with MMC snap-ins where we don't have .msc files fort them. Then we need to show the note for this results in subtitle too.
            // These results have mmc.exe as command and their note property is filled.
            if (entry.Command == "mmc.exe" && !string.IsNullOrEmpty(entry.Note))
            {
                result.Subtitle += $"\u0020\u0020\u002D\u0020\u0020{Resources.Note}: {entry.Note}"; // "\u0020\u0020\u002D\u0020\u0020" = "<space><space><minus><space><space>"
            }

            // To not show duplicate entries we check the existing results on the list before adding the new entry. Example: Device Manager entry for Control Panel and Device Manager entry for MMC.
            if (!resultList.Any(x => x.Title == result.Title))
            {
                resultList.Add(result);
            }
        }

        // TODO GH #127 --> Investigate scoring

        // SetScores(resultList, query);
        return resultList;
    }

    /// <summary>
    /// Checks if a setting <see cref="WindowsSetting"/> matches the search string <see cref="Query.Search"/> to filter settings by settings path.
    /// This method is called from the <see cref="Predicate{T}"/> method in <see cref="Main.Query(Query)"/> if the search string <see cref="Query.Search"/> contains the character ">".
    /// </summary>
    /// <param name="found">The WindowsSetting's result that should be checked.</param>
    /// <param name="queryString">The searchString entered by the user <see cref="Query.Search"/>s.</param>
    internal static bool FilterBySettingsPath(in Classes.WindowsSetting found, in string queryString)
    {
        if (!queryString.Contains('>'))
        {
            return false;
        }

        // Init vars
        var queryElements = queryString.Split('>');

        List<string> settingsPath = new List<string>();
        settingsPath.Add(found.Type);
        if (!(found.Areas is null))
        {
            settingsPath.AddRange(found.Areas);
        }

        // Compare query and settings path
        for (var i = 0; i < queryElements.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(queryElements[i]))
            {
                // The queryElement is an WhiteSpace. Nothing to compare.
                break;
            }

            if (i < settingsPath.Count)
            {
                if (!settingsPath[i].StartsWith(queryElements[i], StringComparison.CurrentCultureIgnoreCase))
                {
                    return false;
                }
            }
            else
            {
                // The user has entered more query parts than existing elements in settings path.
                return false;
            }
        }

        // Return "true" if <found> matches <queryString>.
        return true;
    }
}
