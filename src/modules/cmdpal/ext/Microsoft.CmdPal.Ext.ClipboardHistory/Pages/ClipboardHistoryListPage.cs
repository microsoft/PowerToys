// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Win32;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Pages;

internal sealed partial class ClipboardHistoryListPage : ListPage, IDisposable, IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly IClipboardHistorySettings _settingsManager;
    private readonly IClipboardHistorySource _source;
    private readonly ClipboardHistoryCache _cache;
    private Task? _observedRefresh;
    private bool _started;
    private bool _refreshFailed;
    private bool _disposed;

    public ClipboardHistoryListPage(SettingsManager settingsManager)
        : this(settingsManager, new ClipboardHistorySource(settingsManager), new ClipboardHistoryWorker())
    {
    }

    internal ClipboardHistoryListPage(IClipboardHistorySettings settingsManager, IClipboardHistorySource source, IClipboardHistoryWorker worker)
    {
        ArgumentNullException.ThrowIfNull(settingsManager);

        _settingsManager = settingsManager;
        _source = source;
        _cache = new ClipboardHistoryCache(source, worker, OnSnapshotChanged);
        Icon = Icons.ClipboardListIcon;
        Name = Properties.Resources.clipboard_history_page_name;
        Id = "com.microsoft.cmdpal.clipboardHistory";
        ShowDetails = true;

        _source.HistoryChanged += OnHistoryChanged;
        _source.HistoryEnabledChanged += OnHistoryEnabledChanged;
        _settingsManager.Changed += OnSettingsChanged;
    }

    private void OnHistoryChanged(object? sender, EventArgs args) => Refresh();

    private void OnHistoryEnabledChanged(object? sender, EventArgs args)
    {
        _cache.Clear();
        Refresh();
    }

    private void OnSettingsChanged(object? sender, EventArgs args) => OnSnapshotChanged();

    private void OnSnapshotChanged()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            foreach (var item in _cache.Items)
            {
                if (item is ClipboardListItem clipboardItem)
                {
                    clipboardItem.RefreshCommands();
                }
            }

            RaiseItemsChanged(_cache.Items.Length);
        }
    }

    private bool IsClipboardHistoryEnabled()
    {
        var registryKey = @"HKEY_CURRENT_USER\Software\Microsoft\Clipboard\";
        try
        {
            var enableClipboardHistory = (int)(Registry.GetValue(registryKey, "EnableClipboardHistory", false) ?? 0);
            return enableClipboardHistory != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool IsClipboardHistoryDisabledByGPO()
    {
        var registryKey = @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\System\";
        try
        {
            var allowClipboardHistory = Registry.GetValue(registryKey, "AllowClipboardHistory", null);
            return allowClipboardHistory is not null ? (int)allowClipboardHistory == 0 : false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async void Refresh()
    {
        Task? refresh = null;
        try
        {
            lock (_gate)
            {
                if (!_started || _disposed)
                {
                    return;
                }

                var pending = _cache.RefreshAsync();
                if (ReferenceEquals(pending, _observedRefresh))
                {
                    return;
                }

                refresh = pending;
                _observedRefresh = refresh;
                _refreshFailed = false;
                IsLoading = true;
            }

            await refresh.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                if (!_disposed && (refresh is null || ReferenceEquals(refresh, _observedRefresh)))
                {
                    _refreshFailed = true;
                }
            }

            ExtensionHost.ShowStatus(new StatusMessage() { Message = Properties.Resources.clipboard_failed_to_load, State = MessageState.Error }, StatusContext.Page);
            ExtensionHost.LogMessage(ex.ToString());
        }
        finally
        {
            lock (_gate)
            {
                if (!_disposed && refresh is not null && ReferenceEquals(refresh, _observedRefresh))
                {
                    IsLoading = _cache.IsRefreshing;
                }
            }
        }
    }

    public override IListItem[] GetItems()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return [];
            }

            if (!_started || _refreshFailed)
            {
                _started = true;
                Refresh();
            }

            return _cache.Items;
        }
    }

    public void Dispose() => _ = DisposeWorkerAsync();

    public async ValueTask DisposeAsync()
    {
        var disposeSource = false;
        lock (_gate)
        {
            if (!_disposed)
            {
                _disposed = true;
                _source.HistoryChanged -= OnHistoryChanged;
                _source.HistoryEnabledChanged -= OnHistoryEnabledChanged;
                _settingsManager.Changed -= OnSettingsChanged;
                disposeSource = true;
            }
        }

        try
        {
            if (disposeSource)
            {
                _source.Dispose();
            }
        }
        finally
        {
            await _cache.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task DisposeWorkerAsync()
    {
        try
        {
            await DisposeAsync();
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage(ex.ToString());
        }
    }
}
