// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Win32;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Pages;

internal sealed partial class ClipboardHistoryListPage : ListPage
{
    private readonly ObservableCollection<ClipboardItem> clipboardHistory;
    private readonly string _defaultIconPath;

    public ClipboardHistoryListPage()
    {
        clipboardHistory = [];
        _defaultIconPath = string.Empty;
        Icon = new("\uF0E3"); // ClipboardList icon
        Name = "Clipboard History";
        Id = "com.microsoft.cmdpal.clipboardHistory";
        ShowDetails = true;

        Clipboard.HistoryChanged += TrackClipboardHistoryChanged_EventHandler;
    }

    private void TrackClipboardHistoryChanged_EventHandler(object? sender, ClipboardHistoryChangedEventArgs? e) => RaiseItemsChanged(0);

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
            return allowClipboardHistory != null ? (int)allowClipboardHistory == 0 : false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task LoadClipboardHistoryAsync()
    {
        try
        {
            List<ClipboardItem> items = [];

            if (!Clipboard.IsHistoryEnabled())
            {
                return;
            }

            var historyItems = await Clipboard.GetHistoryItemsAsync();
            if (historyItems.Status != ClipboardHistoryItemsResultStatus.Success)
            {
                return;
            }

            foreach (var item in historyItems.Items)
            {
                if (item.Content.Contains(StandardDataFormats.Text))
                {
                    var text = await item.Content.GetTextAsync();
                    items.Add(new ClipboardItem { Content = text, Item = item });
                }
                else if (item.Content.Contains(StandardDataFormats.Bitmap))
                {
                    items.Add(new ClipboardItem { Item = item });
                }
            }

            clipboardHistory.Clear();

            foreach (var item in items)
            {
                if (item.Item.Content.Contains(StandardDataFormats.Bitmap))
                {
                    var imageReceived = await item.Item.Content.GetBitmapAsync();

                    if (imageReceived != null)
                    {
                        item.ImageData = imageReceived;
                    }
                }

                clipboardHistory.Add(item);
            }
        }
        catch (Exception ex)
        {
            // TODO GH #108 We need to figure out some logging
            // Logger.LogError("Loading clipboard history failed", ex);
            ExtensionHost.ShowStatus(new StatusMessage() { Message = "Loading clipboard history failed", State = MessageState.Error }, StatusContext.Page);
            ExtensionHost.LogMessage(ex.ToString());
        }
    }

    private void LoadClipboardHistoryInSTA()
    {
        // https://github.com/microsoft/windows-rs/issues/317
        // Clipboard API needs to be called in STA or it
        // hangs.
        var thread = new Thread(() =>
        {
            var t = LoadClipboardHistoryAsync();
            t.ConfigureAwait(false);
            t.Wait();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    private ListItem[] GetClipboardHistoryListItems()
    {
        LoadClipboardHistoryInSTA();
        List<ListItem> listItems = [];
        for (var i = 0; i < clipboardHistory.Count; i++)
        {
            var item = clipboardHistory[i];
            if (item != null)
            {
                listItems.Add(item.ToListItem());
            }
        }

        return listItems.ToArray();
    }

    public override IListItem[] GetItems() => GetClipboardHistoryListItems();
}
