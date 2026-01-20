// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.WindowWalker.Components;
using Microsoft.CmdPal.Ext.WindowWalker.Helpers;
using Microsoft.CmdPal.Ext.WindowWalker.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowWalker.Pages;

internal sealed partial class WindowWalkerListPage : DynamicListPage, IDisposable
{
    private System.Threading.CancellationTokenSource _cancellationTokenSource = new();

    private bool _disposed;

    public WindowWalkerListPage()
    {
        Icon = Icons.WindowWalkerIcon;
        Name = Resources.windowwalker_name;
        Id = "com.microsoft.cmdpal.windowwalker";
        PlaceholderText = Resources.windowwalker_PlaceholderText;

        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Icon = Icon,
            Title = Resources.window_walker_top_level_command_title,
            Subtitle = Resources.windowwalker_NoResultsMessage,
        };
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        RaiseItemsChanged(0);
    }

    private WindowWalkerListItem[] Query(string query)
    {
        ArgumentNullException.ThrowIfNull(query);

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new System.Threading.CancellationTokenSource();

        WindowWalkerCommandsProvider.VirtualDesktopHelperInstance.UpdateDesktopList();
        OpenWindows.Instance.UpdateOpenWindowsList(_cancellationTokenSource.Token);

        var windows = OpenWindows.Instance.Windows;

        if (string.IsNullOrWhiteSpace(query))
        {
            if (!SettingsManager.Instance.InMruOrder)
            {
                windows.Sort(static (a, b) => string.Compare(a?.Title, b?.Title, StringComparison.OrdinalIgnoreCase));
            }

            var results = new Scored<Window>[windows.Count];
            for (var i = 0; i < windows.Count; i++)
            {
                results[i] = new Scored<Window> { Item = windows[i], Score = 100 };
            }

            return ResultHelper.GetResultList(results);
        }

        var scored = ListHelpers.FilterListWithScores(windows, query, ScoreFunction).ToArray();
        return ResultHelper.GetResultList(scored);
    }

    private static int ScoreFunction(string q, Window window)
    {
        var titleScore = FuzzyStringMatcher.ScoreFuzzy(q, window.Title);
        var processNameScore = FuzzyStringMatcher.ScoreFuzzy(q, window.Process?.Name ?? string.Empty);
        return Math.Max(titleScore, processNameScore);
    }

    public override IListItem[] GetItems() => Query(SearchText);

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _cancellationTokenSource?.Dispose();
                _disposed = true;
            }
        }
    }
}
