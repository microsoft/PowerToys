// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.WindowWalker.Components;

namespace Microsoft.CmdPal.Ext.WindowWalker.Pages;

/// <summary>
/// Offers to switch to an already open window of the app the user is searching for on the main page.
/// </summary>
internal sealed partial class FallbackWindowWalkerItem : FallbackCommandItem
{
    private const string _id = "com.microsoft.cmdpal.builtin.windowwalker.fallback";

    // UpdateQuery runs on every keystroke. Enumerating windows costs a round trip per window,
    // so reuse one snapshot while the user is typing.
    private static readonly TimeSpan SnapshotLifetime = TimeSpan.FromSeconds(1);

    private readonly NoOpCommand _emptyCommand = new();
    private readonly Lock _updateLock = new();
    private List<Window> _windows = [];
    private long _snapshotTimestamp;
    private WindowWalkerListItem? _currentItem;

    public FallbackWindowWalkerItem()
        : base(Resources.windowwalker_fallback_title, _id)
    {
        Command = _emptyCommand;
        Title = string.Empty;
        Subtitle = string.Empty;
        Icon = Icons.WindowWalkerIcon;
    }

    public override void UpdateQuery(string query)
    {
        lock (_updateLock)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < WindowMatcher.MinimumQueryLength)
            {
                Clear();
                return;
            }

            var windows = GetWindows();
            var index = WindowMatcher.FindBestMatch(windows, query, static w => w.Title, static w => w.Process.Name);
            if (index < 0)
            {
                Clear();
                return;
            }

            ShowWindow(windows[index]);
        }
    }

    private List<Window> GetWindows()
    {
        if (_snapshotTimestamp == 0 || Stopwatch.GetElapsedTime(_snapshotTimestamp) >= SnapshotLifetime)
        {
            WindowWalkerCommandsProvider.VirtualDesktopHelperInstance.UpdateDesktopList();
            OpenWindows.Instance.UpdateOpenWindowsList(CancellationToken.None);
            _windows = OpenWindows.Instance.Windows;
            _snapshotTimestamp = Stopwatch.GetTimestamp();
        }

        return _windows;
    }

    private void ShowWindow(Window window)
    {
        var item = _currentItem;
        if (item?.Window?.Hwnd == window.Hwnd)
        {
            ResultHelper.UpdateResult(item, window);
        }
        else
        {
            item = ResultHelper.CreateResult(window);
            _currentItem = item;
            Icon = Icons.GenericAppIcon;
            _ = Task.Run(() => LoadIcon(item));
        }

        Command = item.Command;
        Title = item.Title;
        Subtitle = item.Subtitle;
        MoreCommands = item.MoreCommands;
    }

    private void LoadIcon(WindowWalkerListItem item)
    {
        if (item.NeedsIconLoad)
        {
            item.LoadIcon();
        }

        // The query may have moved on to another window while the icon was loading.
        lock (_updateLock)
        {
            if (ReferenceEquals(_currentItem, item))
            {
                Icon = item.Command?.Icon;
            }
        }
    }

    private void Clear()
    {
        _currentItem = null;
        Command = _emptyCommand;
        Title = string.Empty;
        Subtitle = string.Empty;
        MoreCommands = [];
        Icon = Icons.WindowWalkerIcon;
    }
}
