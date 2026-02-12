// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.WindowWalker.Commands;
using Microsoft.CmdPal.Ext.WindowWalker.Helpers;
using Microsoft.CmdPal.Ext.WindowWalker.Pages;
using Microsoft.CmdPal.Ext.WindowWalker.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowWalker.Components;

/// <summary>
/// Helper class to work with results
/// </summary>
internal static class ResultHelper
{
    /// <summary>
    /// Returns a list of all results for the query.
    /// </summary>
    /// <param name="scoredWindows">List with all search controller matches</param>
    /// <returns>List of results</returns>
    internal static WindowWalkerListItem[] GetResultList(ICollection<Scored<Window>>? scoredWindows)
    {
        if (scoredWindows is null || scoredWindows.Count == 0)
        {
            return [];
        }

        var list = scoredWindows as IList<Scored<Window>> ?? new List<Scored<Window>>(scoredWindows);

        var addExplorerInfo = false;
        for (var i = 0; i < list.Count; i++)
        {
            var window = list[i].Item;
            if (window?.Process is null)
            {
                continue;
            }

            if (string.Equals(window.Process.Name, "explorer.exe", StringComparison.OrdinalIgnoreCase) && window.Process.IsShellProcess)
            {
                addExplorerInfo = true;
                break;
            }
        }

        var projected = new WindowWalkerListItem[list.Count];
        if (list.Count >= 32)
        {
            Parallel.For(0, list.Count, i =>
            {
                projected[i] = CreateResultFromSearchResult(list[i]);
            });
        }
        else
        {
            for (var i = 0; i < list.Count; i++)
            {
                projected[i] = CreateResultFromSearchResult(list[i]);
            }
        }

        if (addExplorerInfo && !SettingsManager.Instance.HideExplorerSettingInfo)
        {
            var withInfo = new WindowWalkerListItem[projected.Length + 1];
            withInfo[0] = GetExplorerInfoResult();
            Array.Copy(projected, 0, withInfo, 1, projected.Length);
            return withInfo;
        }

        return projected;
    }

    /// <summary>
    /// Creates a Result object from a given SearchResult.
    /// </summary>
    /// <param name="searchResult">The SearchResult object to convert.</param>
    /// <returns>A Result object populated with data from the SearchResult.</returns>
    private static WindowWalkerListItem CreateResultFromSearchResult(Scored<Window> searchResult)
    {
        var item = new WindowWalkerListItem(searchResult.Item)
        {
            Title = searchResult.Item.Title,
            Subtitle = GetSubtitle(searchResult.Item),
            Tags = GetTags(searchResult.Item),
        };
        item.MoreCommands = ContextMenuHelper.GetContextMenuResults(item).ToArray();
        return item;
    }

    /// <summary>
    /// Returns the subtitle for a result
    /// </summary>
    /// <param name="window">The window properties of the result</param>
    /// <returns>String with the subtitle</returns>
    private static string GetSubtitle(Window window)
    {
        if (window is null or null)
        {
            return string.Empty;
        }

        var subtitleText = Resources.windowwalker_Running + ": " + window.Process.Name;

        return subtitleText;
    }

    private static Tag[] GetTags(Window window)
    {
        var tags = new List<Tag>();
        if (!window.Process.IsResponding)
        {
            tags.Add(new Tag
            {
                Text = Resources.windowwalker_NotResponding,
                Foreground = ColorHelpers.FromRgb(220, 20, 60),
            });
        }

        if (SettingsManager.Instance.SubtitleShowPid)
        {
            tags.Add(new Tag
            {
                Text = $"{Resources.windowwalker_ProcessId}: {window.Process.ProcessID}",
            });
        }

        if (SettingsManager.Instance.SubtitleShowDesktopName && WindowWalkerCommandsProvider.VirtualDesktopHelperInstance.GetDesktopCount() > 1)
        {
            tags.Add(new Tag
            {
                Text = $"{Resources.windowwalker_Desktop}: {window.Desktop.Name}",
            });
        }

        return tags.ToArray();
    }

    private static WindowWalkerListItem GetExplorerInfoResult()
    {
        return new WindowWalkerListItem(null)
        {
            Title = Resources.windowwalker_ExplorerInfoTitle,
            Icon = Icons.Info,
            Subtitle = Resources.windowwalker_ExplorerInfoSubTitle,
            Command = new ExplorerInfoResultCommand(),
        };
    }
}
