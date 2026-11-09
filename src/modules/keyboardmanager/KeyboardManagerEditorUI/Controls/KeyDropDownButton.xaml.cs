// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using KeyboardManagerEditorUI.Interop;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace KeyboardManagerEditorUI.Controls
{
    public sealed partial class KeyDropDownButton : UserControl
    {
        private static List<KeyNameEntry>? _cachedKeyList;
        private static List<KeyNameEntry>? _cachedShortcutKeyList;

        public static readonly DependencyProperty KeyNameProperty =
            DependencyProperty.Register(
                nameof(KeyName),
                typeof(string),
                typeof(KeyDropDownButton),
                new PropertyMetadata(string.Empty, OnKeyNameChanged));

        public static readonly DependencyProperty IsShortcutProperty =
            DependencyProperty.Register(
                nameof(IsShortcut),
                typeof(bool),
                typeof(KeyDropDownButton),
                new PropertyMetadata(true));

        public static readonly DependencyProperty UseAccentStyleProperty =
            DependencyProperty.Register(
                nameof(UseAccentStyle),
                typeof(bool),
                typeof(KeyDropDownButton),
                new PropertyMetadata(false));

        public string KeyName
        {
            get => (string)GetValue(KeyNameProperty);
            set => SetValue(KeyNameProperty, value);
        }

        public bool IsShortcut
        {
            get => (bool)GetValue(IsShortcutProperty);
            set => SetValue(IsShortcutProperty, value);
        }

        public bool UseAccentStyle
        {
            get => (bool)GetValue(UseAccentStyleProperty);
            set => SetValue(UseAccentStyleProperty, value);
        }

        public event EventHandler<KeyChangedEventArgs>? KeyChanged;

        public KeyDropDownButton()
        {
            this.InitializeComponent();
            UpdateKeyVisualState();

            // handledEventsToo: the AutoSuggestBox may already have marked Escape as handled.
            KeySearchBox.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(KeySearchBox_KeyDown), handledEventsToo: true);

            this.Loaded += (_, _) =>
            {
                if (UseAccentStyle)
                {
                    KeyButton.Style = (Style)Application.Current.Resources["AccentKeyVisualDropDownButtonStyle"];
                }
            };
        }

        private static void OnKeyNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is KeyDropDownButton dropDownButton)
            {
                dropDownButton.UpdateKeyVisualState();
            }
        }

        private void UpdateKeyVisualState()
        {
            bool hasKeyName = !string.IsNullOrEmpty(KeyName);
            KeyNamePresenter.Visibility = hasKeyName ? Visibility.Visible : Visibility.Collapsed;
            SelectKeyPlaceholderText.Visibility = hasKeyName ? Visibility.Collapsed : Visibility.Visible;
        }

        private void KeyListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is KeyNameEntry entry)
            {
                SelectEntry(entry);
            }
        }

        private void SelectEntry(KeyNameEntry entry)
        {
            string oldKeyName = KeyName;
            KeyListFlyout.Hide();
            KeyChanged?.Invoke(this, new KeyChangedEventArgs(oldKeyName, entry.DisplayName, entry.KeyCode));
        }

        private void KeyListFlyout_Closed(object sender, object e)
        {
            // Clear selection when flyout closes
            KeyListView.SelectedItem = null;
        }

        private void KeyListFlyout_Opening(object sender, object e)
        {
            KeySearchBox.Text = string.Empty;
            RefreshKeyList();
        }

        private void KeyListFlyout_Opened(object sender, object e)
        {
            KeySearchBox.Focus(FocusState.Programmatic);
        }

        private void KeySearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            RefreshKeyList();
        }

        private void KeySearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            // TextChanged is deferred, so ItemsSource may still reflect an older query; filter the submitted text directly.
            var filtered = FilterKeyList(args.QueryText);
            if (filtered.Count > 0)
            {
                SelectEntry(filtered[0]);
            }
        }

        private void KeySearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // AutoSuggestBox consumes Escape for its own suggestion popup, which this picker doesn't use.
            // Key events consumed by an IME arrive as VirtualKey.ProcessKey, so they never reach this branch.
            if (e.Key == VirtualKey.Escape && !KeySearchBox.IsSuggestionListOpen)
            {
                e.Handled = true;
                KeyListFlyout.Hide();
                KeyButton.Focus(FocusState.Keyboard);
            }
        }

        private List<KeyNameEntry> GetKeyList()
        {
            bool isShortcut = IsShortcut;
            ref var cached = ref (isShortcut ? ref _cachedShortcutKeyList : ref _cachedKeyList);

            if (cached == null)
            {
                try
                {
                    using (var service = new KeyboardMappingService())
                    {
                        var list = service.GetKeyboardKeysList(isShortcut);

                        // Filter out the synthetic "None" entry (keycode 0) that the native layer
                        // injects for shortcut lists; selecting it would store an invalid key code.
                        cached = list.Where(e => e.KeyCode != 0).ToList();
                    }
                }
                catch
                {
                    cached = new List<KeyNameEntry>();
                }
            }

            return cached;
        }

        private List<KeyNameEntry> FilterKeyList(string? query)
        {
            query = query?.Trim() ?? string.Empty;
            return query.Length == 0
                ? GetKeyList()
                : GetKeyList().Where(k => k.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        internal void RefreshKeyList()
        {
            var list = FilterKeyList(KeySearchBox.Text);
            KeyListView.ItemsSource = list;
            NoResultsText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // Scroll to current key if possible
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].DisplayName, KeyName, StringComparison.Ordinal))
                {
                    KeyListView.SelectedIndex = i;
                    KeyListView.ScrollIntoView(list[i]);
                    break;
                }
            }
        }
    }
}
