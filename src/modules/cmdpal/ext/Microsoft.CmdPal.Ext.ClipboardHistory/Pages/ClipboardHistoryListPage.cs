// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

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
            CleanupCachedImages(clipboardHistory.Where(static item => item.ImagePath is not null).Select(static item => item.ImagePath!));
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
                if (item.Content.Contains(StandardDataFormats.Text))
                {
                    var text = await item.Content.GetTextAsync().AsTask().ConfigureAwait(false);
                    items.Add(new ClipboardItem { Settings = _settingsManager, Content = text, Item = item });
                }
                else if (item.Content.Contains(StandardDataFormats.Bitmap))
                {
                    items.Add(new ClipboardItem { Settings = _settingsManager, Item = item });
                }
            }

            foreach (var item in items)
            {
                if (item.Item.Content.Contains(StandardDataFormats.Bitmap))
                {
                    var imageReceived = await item.Item.Content.GetBitmapAsync().AsTask().ConfigureAwait(false);

                    if (imageReceived is not null)
                    {
                        item.ImageData = imageReceived;
                        item.ImagePath = GetImagePath(item.Item.Id);
                        await CacheImageAsync(imageReceived, item.ImagePath).ConfigureAwait(false);
                    }
                }
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
            CleanupCachedImages(clipboardHistory.Where(static item => item.ImagePath is not null).Select(static item => item.ImagePath!));
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

    private static string GetImagePath(string id)
    {
        var directory = GetCacheDirectory();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)));
        return Path.Combine(directory, $"{hash}.png");
    }

    private static void CleanupCachedImages(IEnumerable<string> activePaths)
    {
        try
        {
            var activeFileNames = activePaths.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var directory = GetCacheDirectory();
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (var path in Directory.EnumerateFiles(directory, "*.png"))
            {
                if (!activeFileNames.Contains(Path.GetFileName(path)))
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (IOException ex)
                    {
                        TryLogMessage($"Failed to remove cached clipboard image: {ex.Message}");
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        TryLogMessage($"Failed to remove cached clipboard image: {ex.Message}");
                    }
                }
            }
        }
        catch (IOException ex)
        {
            TryLogMessage($"Failed to enumerate cached clipboard images: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            TryLogMessage($"Failed to enumerate cached clipboard images: {ex.Message}");
        }
    }

    private static async Task CacheImageAsync(RandomAccessStreamReference imageData, string path)
    {
        try
        {
            using var stream = await imageData.OpenReadAsync().AsTask().ConfigureAwait(false);
            using var input = stream.AsStreamForRead();
            var directory = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(directory);
            using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 64 * 1024, useAsync: true);
            await input.CopyToAsync(output).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            TryLogMessage($"Failed to cache clipboard image data: {ex}");
        }
    }

    private static string GetCacheDirectory() => Path.Combine(Path.GetTempPath(), "PowerToys", "CmdPal", "ClipboardHistory");

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
        IsLoading = true;

        // https://github.com/microsoft/windows-rs/issues/317
        // The synchronous prefix must run in STA or the clipboard API hangs.
        // Continuations use the thread pool because this raw thread has no
        // synchronization context.
        try
        {
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
            IsLoading = false;
            TryLogMessage($"Failed to start clipboard history load thread: {ex}");
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
        CleanupCachedImages([]);
        GC.SuppressFinalize(this);
    }
}
