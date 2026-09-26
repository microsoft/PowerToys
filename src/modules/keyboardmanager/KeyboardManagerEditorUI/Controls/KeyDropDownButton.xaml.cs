// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using KeyboardManagerEditorUI.Helpers;
using KeyboardManagerEditorUI.Interop;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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

        public static readonly DependencyProperty AccessibleNameProperty =
            DependencyProperty.Register(
                nameof(AccessibleName),
                typeof(string),
                typeof(KeyDropDownButton),
                new PropertyMetadata(string.Empty));

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

        /// <summary>
        /// Gets the name screen readers announce for this button.
        /// </summary>
        /// <remarks>
        /// The button's visual content is a <c>KeyCharPresenter</c>, which renders a glyph rather
        /// than text, and an unfilled slot renders nothing at all - so without this the button is
        /// announced as just "button". Auto-grow leaves an empty placeholder at the end of a
        /// shortcut, which is exactly the slot a keyboard user needs to find.
        /// </remarks>
        public string AccessibleName
        {
            get => (string)GetValue(AccessibleNameProperty);
            private set => SetValue(AccessibleNameProperty, value);
        }

        private static void OnKeyNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is KeyDropDownButton button)
            {
                button.UpdateAccessibleName();
            }
        }

        private void UpdateAccessibleName()
        {
            string keyName = KeyName;
            AccessibleName = string.IsNullOrEmpty(keyName)
                ? ResourceHelper.GetString("KeyDropDown_Empty_AccessibleName")
                : ResourceHelper.GetString("KeyDropDown_AccessibleName").Replace("{0}", keyName, StringComparison.Ordinal);
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
            UpdateAccessibleName();
            this.Loaded += (_, _) =>
            {
                if (UseAccentStyle)
                {
                    KeyButton.Style = (Style)Application.Current.Resources["AccentKeyVisualDropDownButtonStyle"];
                }
            };
        }

        private void KeyListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is KeyNameEntry entry)
            {
                string oldKeyName = KeyName;
                KeyListFlyout.Hide();
                KeyChanged?.Invoke(this, new KeyChangedEventArgs(oldKeyName, entry.DisplayName, entry.KeyCode));
            }
        }

        private void KeyListFlyout_Closed(object sender, object e)
        {
            // Clear selection when flyout closes
            KeyListView.SelectedItem = null;
        }

        private void KeyListFlyout_Opening(object sender, object e)
        {
            RefreshKeyList();
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

        internal void RefreshKeyList()
        {
            KeyListView.ItemsSource = GetKeyList();

            // Scroll to current key if possible
            var list = GetKeyList();
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
