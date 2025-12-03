// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Ext.WindowWalker.Components;
using Microsoft.CmdPal.Ext.WindowWalker.Messages;
using Microsoft.CmdPal.Ext.WindowWalker.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowWalker.Pages;

internal sealed partial class WindowWalkerListPage : DynamicListPage, IDisposable, IRecipient<RefreshWindowsMessage>
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

        // Register to receive refresh messages
        WeakReferenceMessenger.Default.Register(this);
    }

    /// <summary>
    /// Handle the RefreshWindowsMessage to refresh the window list
    /// after a window is closed or a process is killed.
    /// </summary>
    public void Receive(RefreshWindowsMessage message)
    {
        // Small delay to allow Windows to actually close the window
        System.Threading.Tasks.Task.Delay(100).ContinueWith(_ =>
        {
            RaiseItemsChanged(0);
        });
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) =>
        RaiseItemsChanged(0);

    public List<WindowWalkerListItem> Query(string query)
    {
        ArgumentNullException.ThrowIfNull(query);

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new System.Threading.CancellationTokenSource();

        WindowWalkerCommandsProvider.VirtualDesktopHelperInstance.UpdateDesktopList();
        OpenWindows.Instance.UpdateOpenWindowsList(_cancellationTokenSource.Token);
        SearchController.Instance.UpdateSearchText(query);
        var searchControllerResults = SearchController.Instance.SearchMatches;

        return ResultHelper.GetResultList(searchControllerResults, !string.IsNullOrEmpty(query));
    }

    public override IListItem[] GetItems() => Query(SearchText).ToArray();

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
                WeakReferenceMessenger.Default.Unregister<RefreshWindowsMessage>(this);
                _cancellationTokenSource?.Dispose();
                _disposed = true;
            }
        }
    }
}
