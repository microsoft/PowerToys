// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WindowWalker.Commands;
using Microsoft.CmdPal.Ext.WindowWalker.Pages;

namespace Microsoft.CmdPal.Ext.WindowWalker.Components;

/// <summary>
/// Helper class to work with results
/// </summary>
internal static class ResultHelper
{
    /// <summary>
    /// Creates a list item for a window.
    /// </summary>
    internal static WindowWalkerListItem CreateResult(Window window)
    {
        var item = new WindowWalkerListItem(window);
        UpdateResult(item, window);
        return item;
    }

    /// <summary>
    /// Updates a cached list item with the latest window data.
    /// </summary>
    internal static void UpdateResult(WindowWalkerListItem item, Window window)
    {
        item.UpdateWindow(window);
        item.Title = window.Title;
        item.Subtitle = GetSubtitle(window);
        item.Tags = GetTags(window);
        item.MoreCommands = [.. ContextMenuHelper.GetContextMenuResults(item)];
    }

    /// <summary>
    /// Returns the subtitle for a result
    /// </summary>
    /// <param name="window">The window properties of the result</param>
    /// <returns>String with the subtitle</returns>
    private static string GetSubtitle(Window window)
    {
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

        return [.. tags];
    }

    internal static WindowWalkerListItem GetExplorerInfoResult()
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
