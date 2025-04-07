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
            SaveCurrentMapping();
            LoadMappings();
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

        private void SaveCurrentMapping()
        {
            if (_mappingService == null)
            {
                return;
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
