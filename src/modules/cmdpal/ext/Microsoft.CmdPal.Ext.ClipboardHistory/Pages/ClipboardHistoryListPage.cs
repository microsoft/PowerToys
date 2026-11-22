// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Pages;

internal sealed partial class ClipboardHistoryListPage : ListPage, IDisposable
{
    private readonly SettingsManager _settingsManager;
    private readonly string _defaultIconPath;
    private volatile ClipboardItem[] clipboardHistory = [];
    private InterlockedBoolean hasLoadedOnce;
    private InterlockedBoolean loadInFlight;
    private InterlockedBoolean reloadRequested;
    private InterlockedBoolean disposed;

    public ClipboardHistoryListPage(SettingsManager settingsManager)
    {
        ArgumentNullException.ThrowIfNull(settingsManager);

        _settingsManager = settingsManager;
        _defaultIconPath = string.Empty;
        Icon = Icons.ClipboardListIcon;
        Name = Properties.Resources.clipboard_history_page_name;
        Id = "com.microsoft.cmdpal.clipboardHistory";
        ShowDetails = true;

        Clipboard.HistoryChanged += TrackClipboardHistoryChanged_EventHandler;
    }

    private void TrackClipboardHistoryChanged_EventHandler(object? sender, ClipboardHistoryChangedEventArgs? e)
    {
        if (disposed.Value)
        {
            return;
        }

        reloadRequested.Value = true;
        LoadClipboardHistoryInSTA();
    }

    private async Task LoadClipboardHistoryAsync()
    {
        var loadSucceeded = false;
        try
        {
            List<ClipboardItem> items = [];

            if (!Clipboard.IsHistoryEnabled())
            {
                return;
            }

            var historyItems = await Clipboard.GetHistoryItemsAsync().AsTask().ConfigureAwait(false);
            if (historyItems.Status != ClipboardHistoryItemsResultStatus.Success)
            {
                return;
            }

            foreach (var item in historyItems.Items)
            {
                if (disposed.Value)
                {
                    return;
                }

                var content = item.Content;
                var hasText = content.Contains(StandardDataFormats.Text);
                var hasBitmap = content.Contains(StandardDataFormats.Bitmap);
                if (!hasText && !hasBitmap)
                {
                    continue;
                }

                items.Add(new ClipboardItem
                {
                    Settings = _settingsManager,
                    Item = item,
                    Content = hasText ? await content.GetTextAsync().AsTask().ConfigureAwait(false) : null,
                    ImageData = hasBitmap ? await content.GetBitmapAsync().AsTask().ConfigureAwait(false) : null,
                });
            }

            if (disposed.Value)
            {
                return;
            }

            clipboardHistory = [.. items];
            loadSucceeded = true;
        }
        catch (Exception ex)
        {
            // TODO GH #108 We need to figure out some logging
            // Logger.LogError("Loading clipboard history failed", ex);
            ExtensionHost.ShowStatus(new StatusMessage() { Message = Properties.Resources.clipboard_failed_to_load, State = MessageState.Error }, StatusContext.Page);
            ExtensionHost.LogMessage(ex.ToString());
        }
        finally
        {
            loadInFlight.Value = false;

            if (!loadSucceeded)
            {
                hasLoadedOnce.Value = false;
            }

            try
            {
                IsLoading = false;
            }
            catch (Exception ex)
            {
                TryLogMessage($"Failed to clear clipboard history loading state: {ex}");
            }

            if (loadSucceeded && !disposed.Value)
            {
                try
                {
                    RaiseItemsChanged(0);
                }
                catch (Exception ex)
                {
                    TryLogMessage($"Failed to notify clipboard history update: {ex}");
                }
            }

            if (!disposed.Value && reloadRequested.Clear())
            {
                LoadClipboardHistoryInSTA();
            }
        }
    }

    private static void TryLogMessage(string message)
    {
        try
        {
            ExtensionHost.LogMessage(message);
        }
        catch (Exception)
        {
            // Logging must not take down the unobserved STA worker.
        }
    }

    private void LoadClipboardHistoryInSTA()
    {
        if (!loadInFlight.Set())
        {
            return;
        }

        reloadRequested.Clear();
        StartClipboardHistoryLoad();
    }

    private void StartClipboardHistoryLoad()
    {
        try
        {
            SetLoadingState(true);

            // https://github.com/microsoft/windows-rs/issues/317
            // The synchronous prefix must run in STA or the clipboard API hangs.
            // Continuations use the thread pool because this raw thread has no
            // synchronization context.
            var thread = new Thread(() =>
            {
                try
                {
                    LoadClipboardHistoryAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    TryLogMessage($"Clipboard history load thread failed: {ex}");
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
        catch (Exception ex)
        {
            loadInFlight.Value = false;
            hasLoadedOnce.Value = false;
            SetLoadingState(false);
            TryLogMessage($"Failed to start clipboard history load thread: {ex}");
        }
    }

    private void SetLoadingState(bool value)
    {
        try
        {
            IsLoading = value;
        }
        catch (Exception ex)
        {
            TryLogMessage($"Failed to set clipboard history loading state: {ex}");
        }
    }

    private ListItem[] GetClipboardHistoryListItems()
    {
        List<ListItem> listItems = [];
        foreach (var item in clipboardHistory)
        {
            listItems.Add(new ClipboardListItem(item, _settingsManager));
        }

        return listItems.ToArray();
    }

    public override IListItem[] GetItems()
    {
        if (!disposed.Value && hasLoadedOnce.Set())
        {
            LoadClipboardHistoryInSTA();
        }

        return GetClipboardHistoryListItems();
    }

    public void Dispose()
    {
        if (!disposed.Set())
        {
            return;
        }

        Clipboard.HistoryChanged -= TrackClipboardHistoryChanged_EventHandler;

        GC.SuppressFinalize(this);
    }
}
