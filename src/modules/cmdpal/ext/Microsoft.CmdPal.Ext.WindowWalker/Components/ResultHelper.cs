// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CmdPal.Ext.WindowWalker.Commands;
using Microsoft.CmdPal.Ext.WindowWalker.Helpers;
using Microsoft.CmdPal.Ext.WindowWalker.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.WindowWalker.Components;

/// <summary>
/// Helper class to work with results
/// </summary>
internal static class ResultHelper
{
    /// <summary>
    /// Returns a list of all results for the query.
    /// </summary>
    /// <param name="searchControllerResults">List with all search controller matches</param>
    /// <returns>List of results</returns>
    internal static List<WindowWalkerListItem> GetResultList(List<SearchResult> searchControllerResults, bool isKeywordSearch)
    {
        if (searchControllerResults is null || searchControllerResults.Count == 0)
        {
            return [];
        }

        var resultsList = new List<WindowWalkerListItem>(searchControllerResults.Count);
        var addExplorerInfo = searchControllerResults.Any(x =>
            string.Equals(x.Result.Process.Name, "explorer.exe", StringComparison.OrdinalIgnoreCase) &&
            x.Result.Process.IsShellProcess);

        // Process each SearchResult to convert it into a Result.
        // Using parallel processing if the operation is CPU-bound and the list is large.
        resultsList = searchControllerResults
            .AsParallel()
            .Select(x => CreateResultFromSearchResult(x))
            .ToList();

        if (addExplorerInfo && !SettingsManager.Instance.HideExplorerSettingInfo)
        {
            resultsList.Insert(0, GetExplorerInfoResult());
        }

        return resultsList;
    }

    /// <summary>
    /// Creates a Result object from a given SearchResult.
    /// </summary>
    /// <param name="searchResult">The SearchResult object to convert.</param>
    /// <returns>A Result object populated with data from the SearchResult.</returns>
    private static WindowWalkerListItem CreateResultFromSearchResult(SearchResult searchResult)
    {
        var item = new WindowWalkerListItem(searchResult.Result)
        {
            Title = searchResult.Result.Title,
            Subtitle = GetSubtitle(searchResult.Result),
            Tags = GetTags(searchResult.Result),
            Icon = ConvertWindowIconToIconInfo(searchResult.Result.GetWindowIcon()),
        };
        item.MoreCommands = ContextMenuHelper.GetContextMenuResults(item).ToArray();

        return item;
    }

    /// <summary>
    /// Converts a System.Drawing.Icon to Microsoft.CommandPalette.Extensions.IIconInfo
    /// </summary>
    /// <param name="icon">The System.Drawing.Icon to convert</param>
    /// <returns>An IIconInfo representation of the icon</returns>
    private static IIconInfo? ConvertWindowIconToIconInfo(System.Drawing.Icon? icon)
    {
        if (icon == null)
        {
            return null;
        }

        // 아이콘에서 비트맵을 추출하고 이를 IIconInfo로 변환
        try
        {
            using (var bitmap = icon.ToBitmap())
            using (var memoryStream = new System.IO.MemoryStream())
            {
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                IRandomAccessStream randomAccessStream = memoryStream.AsRandomAccessStream();

                return IconInfo.FromStream(randomAccessStream);
            }
        }
        catch (Exception)
        {
            // 변환 중 오류 발생 시 기본 아이콘 반환
            return null;
        }
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
