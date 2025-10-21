// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.ViewModels;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Windows.System;

namespace AdvancedPaste.Pages
{
    public sealed partial class MainPage : Page
    {
        private readonly ObservableCollection<ClipboardItem> clipboardHistory;
        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        private (VirtualKey Key, DateTime Timestamp) _lastKeyEvent = (VirtualKey.None, DateTime.MinValue);

        public OptionsViewModel ViewModel { get; private set; }

        public MainPage()
        {
            this.InitializeComponent();

            ViewModel = App.GetService<OptionsViewModel>();

            clipboardHistory = new ObservableCollection<ClipboardItem>();

            LoadClipboardHistoryEvent(null, null);
            Clipboard.HistoryChanged += LoadClipboardHistoryEvent;
        }

        private void LoadClipboardHistoryEvent(object sender, object e)
        {
            Task.Run(() =>
            {
                LoadClipboardHistoryAsync();
            });
        }

        public async void LoadClipboardHistoryAsync()
        {
            try
            {
                Logger.LogTrace();

                List<ClipboardItem> items = new();

                if (Clipboard.IsHistoryEnabled())
                {
                    var historyItems = await Clipboard.GetHistoryItemsAsync();
                    if (historyItems.Status == ClipboardHistoryItemsResultStatus.Success)
                    {
                        foreach (var item in historyItems.Items)
                        {
                            if (item.Content.Contains(StandardDataFormats.Text))
                            {
                                string text = await item.Content.GetTextAsync();
                                items.Add(new ClipboardItem
                                {
                                    Content = text,
                                    Format = ClipboardFormat.Text,
                                    Timestamp = item.Timestamp,
                                    Item = item,
                                });
                            }
                            else if (item.Content.Contains(StandardDataFormats.Bitmap))
                            {
                                items.Add(new ClipboardItem
                                {
                                    Format = ClipboardFormat.Image,
                                    Timestamp = item.Timestamp,
                                    Item = item,
                                });
                            }
                        }
                    }
                }

                _dispatcherQueue.TryEnqueue(async () =>
                {
                    // Clear to avoid leaks due to Garbage Collection not clearing the bitmap from memory. Fix for https://github.com/microsoft/PowerToys/issues/33423
                    clipboardHistory.Where(x => x.Image is not null)
                                    .ToList()
                                    .ForEach(x => x.Image.ClearValue(BitmapImage.UriSourceProperty));

                    clipboardHistory.Clear();

                    foreach (var item in items)
                    {
                        if (item.Item.Content.Contains(StandardDataFormats.Bitmap))
                        {
                            IRandomAccessStreamReference imageReceived = null;
                            imageReceived = await item.Item.Content.GetBitmapAsync();
                            if (imageReceived != null)
                            {
                                using (var imageStream = await imageReceived.OpenReadAsync())
                                {
                                    var bitmapImage = new BitmapImage();
                                    bitmapImage.SetSource(imageStream);
                                    item.Image = bitmapImage;
                                }
                            }
                        }

                        clipboardHistory.Add(item);
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Loading clipboard history failed", ex);
            }
        }

        private static MainWindow GetMainWindow() => (App.Current as App)?.GetMainWindow();

        private void ClipboardHistoryItemDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.LogTrace();

            if (sender is MenuFlyoutItem btn)
            {
                ClipboardItem item = btn.CommandParameter as ClipboardItem;
                Clipboard.DeleteItemFromHistory(item.Item);
                clipboardHistory.Remove(item);

                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteClipboardItemDeletedEvent());
            }
        }

        private async void PasteFormat_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is PasteFormat format)
            {
                await ViewModel.ExecutePasteFormatAsync(format, PasteActionSource.ContextMenu);
            }
        }

        private async void KeyboardAccelerator_Invoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
        {
            if (GetMainWindow()?.Visible is false)
            {
                return;
            }

            Logger.LogTrace();

            var thisKeyEvent = (sender.Key, Timestamp: DateTime.Now);
            if (thisKeyEvent.Key == _lastKeyEvent.Key && (thisKeyEvent.Timestamp - _lastKeyEvent.Timestamp) < TimeSpan.FromMilliseconds(200))
            {
                // Sometimes, multiple keyboard accelerator events are raised for a single Ctrl + VirtualKey press.
                return;
            }

            _lastKeyEvent = thisKeyEvent;

            switch (sender.Key)
            {
                case VirtualKey.Escape:
                    GetMainWindow()?.Close();
                    break;

                case VirtualKey.Number1:
                case VirtualKey.Number2:
                case VirtualKey.Number3:
                case VirtualKey.Number4:
                case VirtualKey.Number5:
                case VirtualKey.Number6:
                case VirtualKey.Number7:
                case VirtualKey.Number8:
                case VirtualKey.Number9:
                    await ViewModel.ExecutePasteFormatAsync(sender.Key);
                    break;

                default:
                    break;
            }
        }

        private void Page_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                GetMainWindow()?.Close();
            }
        }

        private async void ClipboardHistory_ItemInvoked(ItemsView sender, ItemsViewItemInvokedEventArgs args)
        {
            if (args.InvokedItem is ClipboardItem item)
            {
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteClipboardItemClicked());
                if (!string.IsNullOrEmpty(item.Content))
                {
                    ClipboardHelper.SetTextContent(item.Content);
                }
                else if (item.Image is not null)
                {
                    RandomAccessStreamReference image = await item.Item.Content.GetBitmapAsync();
                    ClipboardHelper.SetImageContent(image);
                }
            }
        }
    }
}
