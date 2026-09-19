// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CommandPalette.Extensions;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

internal sealed partial class ClipboardHistorySource : IClipboardHistorySource
{
    private readonly IClipboardHistorySettings _settings;
    private readonly Lock _gate = new();
    private bool _subscribed;
    private bool _disposed;

    public event EventHandler? HistoryChanged;

    public event EventHandler? HistoryEnabledChanged;

    public ClipboardHistorySource(IClipboardHistorySettings settings)
    {
        _settings = settings;
    }

    public async Task<IReadOnlyList<ClipboardHistoryEntry>> ReadAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_subscribed)
            {
                Clipboard.HistoryChanged += OnHistoryChanged;
                try
                {
                    Clipboard.HistoryEnabledChanged += OnHistoryEnabledChanged;
                }
                catch
                {
                    Clipboard.HistoryChanged -= OnHistoryChanged;
                    throw;
                }

                _subscribed = true;
            }
        }

        if (!Clipboard.IsHistoryEnabled())
        {
            return [];
        }

        var history = await Clipboard.GetHistoryItemsAsync().AsTask(cancellationToken);
        if (history.Status != ClipboardHistoryItemsResultStatus.Success)
        {
            throw new InvalidOperationException($"Clipboard history returned {history.Status}.");
        }

        List<ClipboardHistoryEntry> entries = [];
        foreach (var item in history.Items)
        {
            if (item.Content.Contains(StandardDataFormats.Text) || item.Content.Contains(StandardDataFormats.Bitmap))
            {
                entries.Add(new ClipboardHistoryEntry(item.Id, token => LoadItemAsync(item, token)));
            }
        }

        return entries;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            if (_subscribed)
            {
                Clipboard.HistoryChanged -= OnHistoryChanged;
                Clipboard.HistoryEnabledChanged -= OnHistoryEnabledChanged;
                _subscribed = false;
            }
        }
    }

    private async Task<IListItem> LoadItemAsync(ClipboardHistoryItem item, CancellationToken cancellationToken)
    {
        var text = item.Content.Contains(StandardDataFormats.Text)
            ? await item.Content.GetTextAsync().AsTask(cancellationToken)
            : null;
        var image = item.Content.Contains(StandardDataFormats.Bitmap)
            ? await item.Content.GetBitmapAsync().AsTask(cancellationToken)
            : null;

        return new ClipboardListItem(new ClipboardItem { Settings = _settings, Content = text, ImageData = image, Item = item }, _settings);
    }

    private void OnHistoryChanged(object? sender, ClipboardHistoryChangedEventArgs args) => HistoryChanged?.Invoke(this, EventArgs.Empty);

    private void OnHistoryEnabledChanged(object? sender, object args) => HistoryEnabledChanged?.Invoke(this, EventArgs.Empty);
}
