// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
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

        private string _filterText;

        private ObservableCollection<PasteFormat> pasteFormats = new();

        private bool _pasteAsPlainEnabled;
        private bool _pasteAsMarkdownEnabled;
        private bool _pasteAsJsonEnabled;
        private bool _pasteAudioToTextEnabled;
        private bool _pasteAsFileEnabled;

        public OptionsViewModel ViewModel { get; private set; }

        public MainPage()
        {
            this.InitializeComponent();

            ViewModel = App.GetService<OptionsViewModel>();

            clipboardHistory = new ObservableCollection<ClipboardItem>();

            LoadClipboardHistoryAsync();
            Clipboard.HistoryChanged += LoadClipboardHistoryEvent;
            ViewModel.FormatsChanged += FormatsChangedHandler;
            ViewModel.WindowShown += WindowShownHandler;
            this.EnablePasteOptions();
        }

        private bool WindowShownHandler()
        {
            EnablePasteOptions();
            return true;
        }

        private void LoadClipboardHistoryEvent(object sender, object e)
        {
            Task.Run(() =>
            {
                LoadClipboardHistoryAsync();
            });
        }

        private void GenerateFormatList()
        {
            List<PasteFormat> pasteFormatFullList =
            [
                new PasteFormat { Icon = new FontIcon() { Glyph = "\uE8AC" }, Name = ResourceLoaderInstance.ResourceLoader.GetString("PasteAsPlainText"), Format = PasteFormats.PlainText, Enabled = _pasteAsPlainEnabled },
                new PasteFormat { Icon = new FontIcon() { Glyph = "\ue8a5" }, Name = ResourceLoaderInstance.ResourceLoader.GetString("PasteAsMarkdown"), Format = PasteFormats.Markdown, Enabled = _pasteAsMarkdownEnabled },
                new PasteFormat { Icon = new FontIcon() { Glyph = "\uE943" }, Name = ResourceLoaderInstance.ResourceLoader.GetString("PasteAsJson"), Format = PasteFormats.Json, Enabled = _pasteAsJsonEnabled },
                new PasteFormat { Icon = new FontIcon() { Glyph = "\uE943" }, Name = ResourceLoaderInstance.ResourceLoader.GetString("PasteAudioToText"), Format = PasteFormats.AudioToText, Enabled = _pasteAudioToTextEnabled },
                new PasteFormat { Icon = new FontIcon() { Glyph = "\uE943" }, Name = ResourceLoaderInstance.ResourceLoader.GetString("PasteAsFile"), Format = PasteFormats.File, Enabled = _pasteAsFileEnabled },
            ];

            ObservableCollection<PasteFormat> toAddFormats;

            if (_filterText != null)
            {
                toAddFormats = new ObservableCollection<PasteFormat>(pasteFormatFullList.Where(pasteFormat => pasteFormat.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)).OrderByDescending(pasteFormat => pasteFormat.Enabled));
            }
            else
            {
                toAddFormats = new ObservableCollection<PasteFormat>(pasteFormatFullList.OrderByDescending(pasteFormat => pasteFormat.Enabled));
            }

            pasteFormats.Clear();

            foreach (var format in toAddFormats)
            {
                pasteFormats.Add(format);
            }
        }

        private void EnablePasteOptions()
        {
            Logger.LogInfo("Enabling paste options");

            _pasteAsPlainEnabled = false;
            _pasteAsMarkdownEnabled = false;
            _pasteAsJsonEnabled = false;
            _pasteAudioToTextEnabled = false;
            _pasteAsFileEnabled = false;

            if (ViewModel.ClipboardHasText)
            {
                _pasteAsJsonEnabled = true;
                _pasteAsPlainEnabled = true;
                _pasteAsFileEnabled = true;
            }

            if (ViewModel.ClipboardHasHtml)
            {
                _pasteAsMarkdownEnabled = true;
                _pasteAsFileEnabled = true;
            }

            if (ViewModel.ClipboardHasImage)
            {
                _pasteAsFileEnabled = true;
            }

            if (ViewModel.ClipboardHasAudio)
            {
                _pasteAudioToTextEnabled = true;
            }

            GenerateFormatList();
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
                                items.Add(new ClipboardItem { Content = text, Item = item });
                            }
                            else if (item.Content.Contains(StandardDataFormats.Bitmap))
                            {
                                items.Add(new ClipboardItem { Item = item });
                            }
                        }
                    }
                }

                _dispatcherQueue.TryEnqueue(async () =>
                {
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

        private void PasteAsPlain()
        {
            ViewModel.ToPlainText();
        }

        private void PasteAsMarkdown()
        {
            ViewModel.ToMarkdown();
        }

        private void PasteAsJson()
        {
            ViewModel.ToJson();
        }

        private void AudioToText()
        {
            ViewModel.AudioToText();
        }

        private void PasteAsFile()
        {
            ViewModel.ToFile();
        }

        private void PasteOptionsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is PasteFormat format)
            {
                switch (format.Format)
                {
                    case PasteFormats.PlainText:
                        {
                            PasteAsPlain();
                            PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteFormatClickedEvent(PasteFormats.PlainText));
                            break;
                        }

                    case PasteFormats.Markdown:
                        {
                            PasteAsMarkdown();
                            PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteFormatClickedEvent(PasteFormats.Markdown));
                            break;
                        }

                    case PasteFormats.Json:
                        {
                            PasteAsJson();
                            PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteFormatClickedEvent(PasteFormats.Json));
                            break;
                        }

                    case PasteFormats.AudioToText:
                        {
                            AudioToText();
                            PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteFormatClickedEvent(PasteFormats.AudioToText));
                            return;
                        }

                    case PasteFormats.File:
                        {
                            PasteAsFile();
                            PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteFormatClickedEvent(PasteFormats.File));
                            break;
                        }
                }
            }
        }

        private void KeyboardAccelerator_Invoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
        {
            Logger.LogTrace();

            switch (sender.Key)
            {
                case VirtualKey.Escape:
                    {
                        (App.Current as App).GetMainWindow().Close();
                        break;
                    }

                case VirtualKey.Number1:
                    {
                        PasteAsPlain();
                        PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteInAppKeyboardShortcutEvent(PasteFormats.PlainText));
                        break;
                    }

                case VirtualKey.Number2:
                    {
                        PasteAsMarkdown();
                        PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteInAppKeyboardShortcutEvent(PasteFormats.Markdown));
                        break;
                    }

                case VirtualKey.Number3:
                    {
                        PasteAsJson();
                        PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteInAppKeyboardShortcutEvent(PasteFormats.Json));
                        break;
                    }

                default:
                    break;
            }
        }

        private void Page_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                (App.Current as App).GetMainWindow().Close();
            }
        }

        private async void ClipboardHistory_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as ClipboardItem;
            if (item is not null)
            {
                if (!string.IsNullOrEmpty(item.Content))
                {
                    ClipboardHelper.SetClipboardTextContent(item.Content);
                }
                else if (item.Image is not null)
                {
                    RandomAccessStreamReference image = null;
                    image = await item.Item.Content.GetBitmapAsync();
                    ClipboardHelper.SetClipboardImageContent(image);
                }
            }
        }

        private void PasteFormatListContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var listViewItem = args.ItemContainer;

            if (listViewItem != null)
            {
                var model = (PasteFormat)args.Item;

                listViewItem.IsEnabled = model.Enabled;
            }
        }

        private bool FormatsChangedHandler(string input)
        {
            _filterText = input;
            GenerateFormatList();
            return true;
        }
    }
}
