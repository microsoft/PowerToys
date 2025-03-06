// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using KeyboardManagerEditorUI.Helpers;
using KeyboardManagerEditorUI.Interop;
using ManagedCommon;
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

                string[] targetKeyCode = mapping.TargetKey.Split(';');
                var targetKeyNames = new List<string>();

                foreach (var keyCode in targetKeyCode)
                {
                    if (int.TryParse(keyCode, out int code))
                    {
                        targetKeyNames.Add(GetKeyDisplayName(code));
                    }
                }

                RemappedShortcuts.Add(new Remapping
                {
                    OriginalKeys = new List<string> { GetKeyDisplayName(mapping.OriginalKey) },
                    RemappedKeys = targetKeyNames,
                    IsAllApps = true,
                });
            }

            foreach (var mapping in _mappingService.GetShortcutMappingsByType(ShortcutOperationType.RemapShortcut))
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

        private async void NewShortcutBtn_Click(object sender, RoutedEventArgs e)
        {
            ShortcutControl.SetOriginalKeys(new List<string>());
            ShortcutControl.SetRemappedKeys(new List<string>());
            ShortcutControl.SetApp(false, string.Empty);

            KeyDialog.PrimaryButtonClick += KeyDialog_PrimaryButtonClick;
            await KeyDialog.ShowAsync();
            KeyDialog.PrimaryButtonClick -= KeyDialog_PrimaryButtonClick;
        }

        private void KeyDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            SaveCurrentMapping();
            LoadMappings();
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

        public static int GetKeyCode(string keyName)
        {
            return KeyboardManagerInterop.GetKeyCodeFromName(keyName);
        }

        private void SaveCurrentMapping()
        {
            if (_mappingService == null)
            {
                return;
            }

            try
            {
                List<string> originalKeys = ShortcutControl.GetOriginalKeys();
                List<string> remappedKeys = ShortcutControl.GetRemappedKeys();
                bool isAppSpecific = ShortcutControl.GetIsAppSpecific();
                string appName = ShortcutControl.GetAppName();

                // mock data
                // originalKeys = ["A"];
                // remappedKeys = ["B"];
                if (originalKeys == null || originalKeys.Count == 0 || remappedKeys == null || remappedKeys.Count == 0)
                {
                    return;
                }

                if (originalKeys.Count == 1)
                {
                    int originalKey = GetKeyCode(originalKeys[0]);
                    if (originalKey != 0)
                    {
                        if (remappedKeys.Count == 1)
                        {
                            int targetKey = GetKeyCode(remappedKeys[0]);
                            if (targetKey != 0)
                            {
                                _mappingService.AddSingleKeyMapping(originalKey, targetKey);
                            }
                        }
                        else
                        {
                            string targetKeys = string.Join(";", remappedKeys.Select(k => GetKeyCode(k).ToString(CultureInfo.InvariantCulture)));
                            _mappingService.AddSingleKeyMapping(originalKey, targetKeys);
                        }
                    }
                }
                else
                {
                    string originalKeysString = string.Join(";", originalKeys.Select(k => GetKeyCode(k).ToString(CultureInfo.InvariantCulture)));
                    string targetKeysString = string.Join(";", remappedKeys.Select(k => GetKeyCode(k).ToString(CultureInfo.InvariantCulture)));

                    if (isAppSpecific && !string.IsNullOrEmpty(appName))
                    {
                        _mappingService.AddShortcutMapping(originalKeysString, targetKeysString, appName);
                    }
                    else
                    {
                        _mappingService.AddShortcutMapping(originalKeysString, targetKeysString);
                    }
                }

                _mappingService.SaveSettings();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error saving shortcut mapping: " + ex.Message);
            }
        }
    }
}
