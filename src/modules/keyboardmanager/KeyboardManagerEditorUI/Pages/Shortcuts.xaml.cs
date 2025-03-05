// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using KeyboardManagerEditorUI.Helpers;
using KeyboardManagerEditorUI.Interop;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace KeyboardManagerEditorUI.Pages
{
    public sealed partial class Shortcuts : Page, IDisposable
    {
        private KeyboardMappingService? _mappingService;

        private bool _disposed;

        public ObservableCollection<KeyMapping> SingleKeyMappings { get; } = new ObservableCollection<KeyMapping>();

        public ObservableCollection<ShortcutKeyMapping> ShortcutMappings { get; } = new ObservableCollection<ShortcutKeyMapping>();

        public ObservableCollection<Remapping> RemappedShortcuts { get; set; }

        [DllImport("PowerToys.KeyboardManagerEditorLibraryWrapper.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern void GetKeyDisplayName(int keyCode, [Out] StringBuilder keyName, int maxLength);

        public Shortcuts()
        {
            this.InitializeComponent();

            RemappedShortcuts = new ObservableCollection<Remapping>();
            _mappingService = new KeyboardMappingService();
            LoadMappings();

            /*
            RemappedShortcuts = new ObservableCollection<Remapping>();
            RemappedShortcuts.Add(new Remapping() { OriginalKeys = new List<string>() { "Ctrl", "Shift", "F" }, RemappedKeys = new List<string>() { "Shift", "F" }, IsAllApps = true });
            RemappedShortcuts.Add(new Remapping() { OriginalKeys = new List<string>() { "Ctrl (Left)" }, RemappedKeys = new List<string>() { "Ctrl (Right)" }, IsAllApps = true });
            RemappedShortcuts.Add(new Remapping() { OriginalKeys = new List<string>() { "Shift", "M" }, RemappedKeys = new List<string>() { "Ctrl", "M" }, IsAllApps = true });
            RemappedShortcuts.Add(new Remapping() { OriginalKeys = new List<string>() { "Shift", "Alt", "B" }, RemappedKeys = new List<string>() { "Alt", "B" }, IsAllApps = false, AppName = "outlook.exe" });
            RemappedShortcuts.Add(new Remapping() { OriginalKeys = new List<string>() { "Numpad 1" }, RemappedKeys = new List<string>() { "Ctrl", "F" }, IsAllApps = true });
            RemappedShortcuts.Add(new Remapping() { OriginalKeys = new List<string>() { "Numpad 2" }, RemappedKeys = new List<string>() { "Alt", "F" }, IsAllApps = true, AppName = "outlook.exe" });
            */
        }

        private void LoadMappings()
        {
            if (_mappingService == null)
            {
                return;
            }

            SingleKeyMappings.Clear();
            ShortcutMappings.Clear();
            RemappedShortcuts.Clear();

            foreach (var mapping in _mappingService.GetSingleKeyMappings())
            {
                SingleKeyMappings.Add(mapping);

                RemappedShortcuts.Add(new Remapping
                {
                    OriginalKeys = new List<string> { GetKeyDisplayName(mapping.OriginalKey) },
                    RemappedKeys = new List<string> { GetKeyDisplayName(mapping.TargetKey) },
                    IsAllApps = true,
                });
            }

            foreach (var mapping in _mappingService.GetShortcutMappings())
            {
                ShortcutMappings.Add(mapping);

                string[] originalKeyCodes = mapping.OriginalKeys.Split(';');
                string[] targetKeyCodes = mapping.TargetKeys.Split(';');

                var originalKeyNames = new List<string>();
                var targetKeyNames = new List<string>();

                foreach (var keyCode in originalKeyCodes)
                {
                    if (int.TryParse(keyCode, out int code))
                    {
                        originalKeyNames.Add(GetKeyDisplayName(code));
                    }
                }

                foreach (var keyCode in targetKeyCodes)
                {
                    if (int.TryParse(keyCode, out int code))
                    {
                        targetKeyNames.Add(GetKeyDisplayName(code));
                    }
                }

                RemappedShortcuts.Add(new Remapping
                {
                    OriginalKeys = originalKeyNames,
                    RemappedKeys = targetKeyNames,
                    IsAllApps = string.IsNullOrEmpty(mapping.TargetApp),
                    AppName = mapping.TargetApp ?? string.Empty,
                });
            }
        }

        public static string GetKeyDisplayName(int keyCode)
        {
            var keyName = new StringBuilder(64);
            GetKeyDisplayName(keyCode, keyName, keyName.Capacity);
            return keyName.ToString();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _mappingService?.Dispose();
                    _mappingService = null;
                }

                _disposed = true;
            }
        }

        private async void AddSingleKeyMapping_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "??????",
                PrimaryButtonText = "??",
                CloseButtonText = "??",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
            };

            var panel = new StackPanel { Spacing = 10 };

            var originalKeyBox = new TextBox { PlaceholderText = "??? (??: 65 ? 'A')" };
            panel.Children.Add(new TextBlock { Text = "???:" });
            panel.Children.Add(originalKeyBox);

            var targetKeyBox = new TextBox { PlaceholderText = "??? (??: 66 ? 'B')" };
            panel.Children.Add(new TextBlock { Text = "???:" });
            panel.Children.Add(targetKeyBox);

            dialog.Content = panel;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                if (int.TryParse(originalKeyBox.Text, out int originalKey) &&
                    int.TryParse(targetKeyBox.Text, out int targetKey))
                {
                    if (_mappingService != null && _mappingService.AddSingleKeyMapping(originalKey, targetKey))
                    {
                        _mappingService.SaveSettings();
                        LoadMappings();
                    }
                }
            }
        }

        private async void AddShortcutMapping_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "???????",
                PrimaryButtonText = "??",
                CloseButtonText = "??",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
            };

            var panel = new StackPanel { Spacing = 10 };

            var originalKeysBox = new TextBox { PlaceholderText = "????? (??: 162;65 ? 'Ctrl+A')" };
            panel.Children.Add(new TextBlock { Text = "?????:" });
            panel.Children.Add(originalKeysBox);

            var targetKeysBox = new TextBox { PlaceholderText = "????? (??: 162;66 ? 'Ctrl+B')" };
            panel.Children.Add(new TextBlock { Text = "?????:" });
            panel.Children.Add(targetKeysBox);

            var targetAppBox = new TextBox { PlaceholderText = "???? (?????)" };
            panel.Children.Add(new TextBlock { Text = "????:" });
            panel.Children.Add(targetAppBox);

            dialog.Content = panel;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                if (!string.IsNullOrEmpty(originalKeysBox.Text) && !string.IsNullOrEmpty(targetKeysBox.Text))
                {
                    if (_mappingService != null && _mappingService.AddShortcutMapping(originalKeysBox.Text, targetKeysBox.Text, targetAppBox.Text))
                    {
                        _mappingService.SaveSettings();
                        LoadMappings();
                    }
                }
            }
        }

        private async void NewShortcutBtn_Click(object sender, RoutedEventArgs e)
        {
            await KeyDialog.ShowAsync();
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Remapping selectedShortcut)
            {
                ShortcutControl.SetOriginalKeys(selectedShortcut.OriginalKeys);
                ShortcutControl.SetRemappedKeys(selectedShortcut.RemappedKeys);
                ShortcutControl.SetApp(!selectedShortcut.IsAllApps, selectedShortcut.AppName);
                await KeyDialog.ShowAsync();
            }
        }
    }
}
