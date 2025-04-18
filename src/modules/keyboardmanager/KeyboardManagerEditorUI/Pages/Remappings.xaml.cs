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
using System.Threading.Tasks;
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
    /// <summary>
    /// The Remapping page that allow users to configure a single key or shortcut to a new key or shortcut
    /// </summary>
    public sealed partial class Remappings : Page, IDisposable
    {
        private KeyboardMappingService? _mappingService;

        private bool _disposed;

        // The list of single key mappings
        public ObservableCollection<KeyMapping> SingleKeyMappings { get; } = new ObservableCollection<KeyMapping>();

        // The list of shortcut key mappings
        public ObservableCollection<ShortcutKeyMapping> ShortcutKeyMappings { get; } = new ObservableCollection<ShortcutKeyMapping>();

        // The full list of remappings
        public ObservableCollection<Remapping> RemappingList { get; set; }

        public Remappings()
        {
            this.InitializeComponent();

            RemappingList = new ObservableCollection<Remapping>();
            _mappingService = new KeyboardMappingService();

            // Load all existing remappings
            LoadMappings();

            this.Unloaded += Remappings_Unloaded;
        }

        private void Remappings_Unloaded(object sender, RoutedEventArgs e)
        {
            // Make sure we unregister the handler when the page is unloaded
            UnregisterWindowActivationHandler();
            RemappingControl.CleanupKeyboardHook();
        }

        private void LoadMappings()
        {
            if (_mappingService == null)
            {
                return;
            }

            SingleKeyMappings.Clear();
            ShortcutKeyMappings.Clear();
            RemappingList.Clear();

            // Load all single key mappings
            foreach (var mapping in _mappingService.GetSingleKeyMappings())
            {
                SingleKeyMappings.Add(mapping);

                string[] targetKeyCodes = mapping.TargetKey.Split(';');
                var targetKeyNames = new List<string>();

                foreach (var keyCode in targetKeyCodes)
                {
                    if (int.TryParse(keyCode, out int code))
                    {
                        targetKeyNames.Add(_mappingService.GetKeyDisplayName(code));
                    }
                }

                RemappingList.Add(new Remapping
                {
                    OriginalKeys = new List<string> { _mappingService.GetKeyDisplayName(mapping.OriginalKey) },
                    RemappedKeys = targetKeyNames,
                    IsAllApps = true,
                });
            }

            // Load all shortcut key mappings
            foreach (var mapping in _mappingService.GetShortcutMappingsByType(ShortcutOperationType.RemapShortcut))
            {
                ShortcutKeyMappings.Add(mapping);

                string[] originalKeyCodes = mapping.OriginalKeys.Split(';');
                string[] targetKeyCodes = mapping.TargetKeys.Split(';');

                var originalKeyNames = new List<string>();
                var targetKeyNames = new List<string>();

                foreach (var keyCode in originalKeyCodes)
                {
                    if (int.TryParse(keyCode, out int code))
                    {
                        originalKeyNames.Add(_mappingService.GetKeyDisplayName(code));
                    }
                }

                foreach (var keyCode in targetKeyCodes)
                {
                    if (int.TryParse(keyCode, out int code))
                    {
                        targetKeyNames.Add(_mappingService.GetKeyDisplayName(code));
                    }
                }

                RemappingList.Add(new Remapping
                {
                    OriginalKeys = originalKeyNames,
                    RemappedKeys = targetKeyNames,
                    IsAllApps = string.IsNullOrEmpty(mapping.TargetApp),
                    AppName = string.IsNullOrEmpty(mapping.TargetApp) ? "All Apps" : mapping.TargetApp,
                });
            }
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

        private void RegisterWindowActivationHandler()
        {
            // Get the current window that contains this page
            var app = Application.Current as App;
            if (app?.GetWindow() is Window window)
            {
                // Register for window activation events
                window.Activated += Dialog_WindowActivated;
            }
        }

        private void UnregisterWindowActivationHandler()
        {
            var app = Application.Current as App;
            if (app?.GetWindow() is Window window)
            {
                // Unregister to prevent memory leaks
                window.Activated -= Dialog_WindowActivated;
            }
        }

        private void Dialog_WindowActivated(object sender, WindowActivatedEventArgs args)
        {
            // When window is deactivated (user switched to another app)
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                // Make sure to cleanup the keyboard hook when the window loses focus
                RemappingControl.CleanupKeyboardHook();

                RemappingControl.ResetToggleButtons();
                RemappingControl.UpdateAllAppsCheckBoxState();
            }
        }

        private async void NewRemappingBtn_Click(object sender, RoutedEventArgs e)
        {
            RemappingControl.SetOriginalKeys(new List<string>());
            RemappingControl.SetRemappedKeys(new List<string>());
            RemappingControl.SetApp(false, string.Empty);
            RemappingControl.SetUpToggleButtonInitialStatus();

            RegisterWindowActivationHandler();

            // Show the dialog to add a new remapping
            KeyDialog.PrimaryButtonClick += KeyDialog_PrimaryButtonClick;
            await KeyDialog.ShowAsync();
            KeyDialog.PrimaryButtonClick -= KeyDialog_PrimaryButtonClick;

            UnregisterWindowActivationHandler();

            RemappingControl.CleanupKeyboardHook();
        }

        private void KeyDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            List<string> originalKeys = RemappingControl.GetOriginalKeys();
            List<string> remappedKeys = RemappingControl.GetRemappedKeys();
            bool isAppSpecific = RemappingControl.GetIsAppSpecific();
            string appName = RemappingControl.GetAppName();

            // Check if original keys are empty
            if (originalKeys == null || originalKeys.Count == 0)
            {
                EmptyOriginalKeysTeachingTip.Target = RemappingControl;
                EmptyOriginalKeysTeachingTip.Tag = args;
                EmptyOriginalKeysTeachingTip.IsOpen = true;
                args.Cancel = true;
                return;
            }

            // Check if remapped keys are empty
            if (remappedKeys == null || remappedKeys.Count == 0)
            {
                EmptyRemappedKeysTeachingTip.Target = RemappingControl;
                EmptyRemappedKeysTeachingTip.Tag = args;
                EmptyRemappedKeysTeachingTip.IsOpen = true;
                args.Cancel = true;
                return;
            }

            // Check if shortcut contains only modifier keys
            if ((originalKeys.Count > 1 && ContainsOnlyModifierKeys(originalKeys)) ||
                (remappedKeys.Count > 1 && ContainsOnlyModifierKeys(remappedKeys)))
            {
                ModifierOnlyTeachingTip.Target = RemappingControl;
                ModifierOnlyTeachingTip.Tag = args;
                ModifierOnlyTeachingTip.IsOpen = true;
                args.Cancel = true;
                return;
            }

            // Check if app specific is checked but no app name is provided
            if (isAppSpecific && string.IsNullOrWhiteSpace(appName))
            {
                EmptyAppNameTeachingTip.Target = RemappingControl;
                EmptyAppNameTeachingTip.Tag = args;
                EmptyAppNameTeachingTip.IsOpen = true;
                args.Cancel = true;
                return;
            }

            // Check if this is a shortcut (multiple keys) and if it's an illegal combination
            if (originalKeys.Count > 1)
            {
                string shortcutKeysString = string.Join(";", originalKeys.Select(k => GetKeyCode(k).ToString(CultureInfo.InvariantCulture)));

                if (KeyboardManagerInterop.IsShortcutIllegal(shortcutKeysString))
                {
                    IllegalShortcutTeachingTip.Target = RemappingControl;
                    IllegalShortcutTeachingTip.Tag = args;

                    // Show the teaching tip
                    IllegalShortcutTeachingTip.IsOpen = true;

                    // Cancel the dialog closing for now since it will be handled by teaching tip actions
                    args.Cancel = true;
                    return;
                }
            }

            // Check for duplicate mappings
            if (IsDuplicateMapping(originalKeys, isAppSpecific, appName))
            {
                DuplicateRemappingTeachingTip.Target = RemappingControl;
                DuplicateRemappingTeachingTip.Tag = args;

                // Show the teaching tip
                DuplicateRemappingTeachingTip.IsOpen = true;

                args.Cancel = true;
                return;
            }

            // Check for self-mapping
            if (IsSelfMapping(originalKeys, remappedKeys))
            {
                SelfMappingTeachingTip.Target = RemappingControl;
                SelfMappingTeachingTip.Tag = args;
                SelfMappingTeachingTip.IsOpen = true;
                args.Cancel = true;
                return;
            }

            // Check for orphaned keys
            if (originalKeys.Count == 1 && _mappingService != null)
            {
                int originalKeyCode = GetKeyCode(originalKeys[0]);

                if (IsKeyOrphaned(originalKeyCode, _mappingService))
                {
                    string keyName = _mappingService.GetKeyDisplayName(originalKeyCode);

                    OrphanedKeysTeachingTip.Target = RemappingControl;
                    OrphanedKeysTeachingTip.Subtitle = $"The key {keyName} will become orphaned (inaccessible) after remapping. Please confirm if you want to proceed.";
                    OrphanedKeysTeachingTip.Tag = args;
                    OrphanedKeysTeachingTip.IsOpen = true;

                    args.Cancel = true;
                    return;
                }
            }

            bool saved = SaveCurrentMapping();
            if (saved)
            {
                LoadMappings();
            }
        }

        private void IllegalShortcutTeachingTip_CloseButtonClick(TeachingTip sender, object args)
        {
            sender.IsOpen = false;
        }

        private void DuplicateRemappingTeachingTip_CloseButtonClick(TeachingTip sender, object args)
        {
            sender.IsOpen = false;
        }

        private bool IsDuplicateMapping(List<string> originalKeys, bool isAppSpecific, string appName)
        {
            if (_mappingService == null || originalKeys == null || originalKeys.Count == 0)
            {
                return false;
            }

            // For single key remapping
            if (originalKeys.Count == 1)
            {
                int originalKeyCode = GetKeyCode(originalKeys[0]);
                if (originalKeyCode == 0)
                {
                    return false;
                }

                // Check if the key is already remapped
                foreach (var mapping in _mappingService.GetSingleKeyMappings())
                {
                    if (mapping.OriginalKey == originalKeyCode)
                    {
                        return true;
                    }
                }
            }

            // For shortcut remapping
            else
            {
                string originalKeysString = string.Join(";", originalKeys.Select(k => GetKeyCode(k).ToString(CultureInfo.InvariantCulture)));

                // Check if the shortcut is already remapped in the same app context
                foreach (var mapping in _mappingService.GetShortcutMappingsByType(ShortcutOperationType.RemapShortcut))
                {
                    // Same shortcut in the same app context
                    if (KeyboardManagerInterop.AreShortcutsEqual(originalKeysString, mapping.OriginalKeys))
                    {
                        // If both are global (all apps)
                        if (!isAppSpecific && string.IsNullOrEmpty(mapping.TargetApp))
                        {
                            return true;
                        }

                        // If both are for the same specific app
                        else if (isAppSpecific && !string.IsNullOrEmpty(mapping.TargetApp) && string.Equals(mapping.TargetApp, appName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool IsSelfMapping(List<string> originalKeys, List<string> remappedKeys)
        {
            // If either list is empty, it's not a self-mapping
            if (originalKeys == null || remappedKeys == null ||
                originalKeys.Count == 0 || remappedKeys.Count == 0)
            {
                return false;
            }

            string originalKeysString = string.Join(";", originalKeys.Select(k => GetKeyCode(k).ToString(CultureInfo.InvariantCulture)));
            string remappedKeysString = string.Join(";", remappedKeys.Select(k => GetKeyCode(k).ToString(CultureInfo.InvariantCulture)));

            return KeyboardManagerInterop.AreShortcutsEqual(originalKeysString, remappedKeysString);
        }

        private bool ContainsOnlyModifierKeys(List<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return false;
            }

            foreach (string key in keys)
            {
                int keyCode = GetKeyCode(key);
                var keyType = (KeyType)KeyboardManagerInterop.GetKeyType(keyCode);

                // If any key is an action key, return false
                if (keyType == KeyType.Action)
                {
                    return false;
                }
            }

            // All keys are modifier keys
            return true;
        }

        private bool IsKeyOrphaned(int originalKey, KeyboardMappingService mappingService)
        {
            // Check all single key mappings
            foreach (var mapping in mappingService.GetSingleKeyMappings())
            {
                if (!mapping.IsShortcut && int.TryParse(mapping.TargetKey, out int targetKey) && targetKey == originalKey)
                {
                    return false;
                }
            }

            // Check all shortcut mappings
            foreach (var mapping in mappingService.GetShortcutMappings())
            {
                string[] targetKeys = mapping.TargetKeys.Split(';');
                if (targetKeys.Length == 1 && int.TryParse(targetKeys[0], out int shortcutTargetKey) && shortcutTargetKey == originalKey)
                {
                    return false;
                }
            }

            // No mapping found for the original key
            return true;
        }

        private void SelfMappingTeachingTip_CloseButtonClick(TeachingTip sender, object args)
        {
            sender.IsOpen = false;
        }

        private void EmptyOriginalKeysTeachingTip_CloseButtonClick(TeachingTip sender, object args)
        {
            sender.IsOpen = false;
        }

        private void EmptyRemappedKeysTeachingTip_CloseButtonClick(TeachingTip sender, object args)
        {
            sender.IsOpen = false;
        }

        private void EmptyAppNameTeachingTip_CloseButtonClick(TeachingTip sender, object args)
        {
            sender.IsOpen = false;
        }

        private void ModifierOnlyTeachingTip_CloseButtonClick(TeachingTip sender, object args)
        {
            sender.IsOpen = false;
        }

        private void OrphanedKeysTeachingTip_ActionButtonClick(TeachingTip sender, object args)
        {
            // User pressed continue anyway button
            sender.IsOpen = false;

            bool saved = SaveCurrentMapping();
            if (saved)
            {
                KeyDialog.Hide();
                LoadMappings();
            }
        }

        private void OrphanedKeysTeachingTip_CloseButtonClick(TeachingTip sender, object args)
        {
            // User canceled - just close the teaching tip
            sender.IsOpen = false;
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Remapping selectedRemapping && selectedRemapping.IsEnabled)
            {
                RemappingControl.SetOriginalKeys(selectedRemapping.OriginalKeys);
                RemappingControl.SetRemappedKeys(selectedRemapping.RemappedKeys);
                RemappingControl.SetApp(!selectedRemapping.IsAllApps, selectedRemapping.AppName);
                RemappingControl.SetUpToggleButtonInitialStatus();

                RegisterWindowActivationHandler();

                KeyDialog.PrimaryButtonClick += KeyDialog_PrimaryButtonClick;
                await KeyDialog.ShowAsync();
                KeyDialog.PrimaryButtonClick -= KeyDialog_PrimaryButtonClick;

                UnregisterWindowActivationHandler();

                RemappingControl.CleanupKeyboardHook();
            }
        }

        public static int GetKeyCode(string keyName)
        {
            return KeyboardManagerInterop.GetKeyCodeFromName(keyName);
        }

        private bool SaveCurrentMapping()
        {
            if (_mappingService == null)
            {
                return false;
            }

            try
            {
                List<string> originalKeys = RemappingControl.GetOriginalKeys();
                List<string> remappedKeys = RemappingControl.GetRemappedKeys();
                bool isAppSpecific = RemappingControl.GetIsAppSpecific();
                string appName = RemappingControl.GetAppName();

                // mock data
                // originalKeys = ["A", "Ctrl"];
                // remappedKeys = ["B"];
                if (originalKeys == null || originalKeys.Count == 0 || remappedKeys == null || remappedKeys.Count == 0)
                {
                    return false;
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
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error saving shortcut mapping: " + ex.Message);
                return false;
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Remapping remapping)
            {
                DeleteRemapping(remapping);
            }
        }

        private void DeleteRemapping(Remapping remapping)
        {
            if (_mappingService == null)
            {
                return;
            }

            try
            {
                // Determine the type of remapping to delete
                if (remapping.OriginalKeys.Count == 1)
                {
                    // Single key mapping
                    int originalKey = GetKeyCode(remapping.OriginalKeys[0]);
                    if (originalKey != 0)
                    {
                        if (_mappingService.DeleteSingleKeyMapping(originalKey))
                        {
                            // Save settings after successful deletion
                            _mappingService.SaveSettings();

                            // Remove from UI
                            RemappingList.Remove(remapping);
                        }
                    }
                }
                else if (remapping.OriginalKeys.Count > 1)
                {
                    // Shortcut key mapping
                    string originalKeysString = string.Join(";", remapping.OriginalKeys.Select(k => GetKeyCode(k).ToString(CultureInfo.InvariantCulture)));

                    bool deleteResult;
                    if (!remapping.IsAllApps && !string.IsNullOrEmpty(remapping.AppName))
                    {
                        // App-specific shortcut key mapping
                        deleteResult = _mappingService.DeleteShortcutMapping(originalKeysString, remapping.AppName);
                    }
                    else
                    {
                        // Global shortcut key mapping
                        deleteResult = _mappingService.DeleteShortcutMapping(originalKeysString);
                    }

                    if (deleteResult)
                    {
                        _mappingService.SaveSettings();

                        RemappingList.Remove(remapping);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error deleting remapping: {ex.Message}");
            }
        }
    }
}
