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
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static KeyboardManagerEditorUI.Helpers.ValidationHelper;

namespace KeyboardManagerEditorUI.Pages
{
    /// <summary>
    /// The Remapping page that allow users to configure a single key or shortcut to a new key or shortcut
    /// </summary>
    public sealed partial class Remappings : Page, IDisposable
    {
        private KeyboardMappingService? _mappingService;

        // Flag to indicate if the user is editing an existing remapping
        private bool _isEditMode;
        private Remapping? _editingRemapping;

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
            KeyboardHookHelper.Instance.CleanupHook();
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
                KeyboardHookHelper.Instance.CleanupHook();

                RemappingControl.ResetToggleButtons();
                RemappingControl.UpdateAllAppsCheckBoxState();
            }
        }

        private async void NewRemappingBtn_Click(object sender, RoutedEventArgs e)
        {
            _isEditMode = false;
            _editingRemapping = null;

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

            KeyboardHookHelper.Instance.CleanupHook();
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Remapping selectedRemapping && selectedRemapping.IsEnabled)
            {
                // Set to edit mode
                _isEditMode = true;
                _editingRemapping = selectedRemapping;

                RemappingControl.SetOriginalKeys(selectedRemapping.OriginalKeys);
                RemappingControl.SetRemappedKeys(selectedRemapping.RemappedKeys);
                RemappingControl.SetApp(!selectedRemapping.IsAllApps, selectedRemapping.AppName);
                RemappingControl.SetUpToggleButtonInitialStatus();

                RegisterWindowActivationHandler();

                KeyDialog.PrimaryButtonClick += KeyDialog_PrimaryButtonClick;
                await KeyDialog.ShowAsync();
                KeyDialog.PrimaryButtonClick -= KeyDialog_PrimaryButtonClick;

                UnregisterWindowActivationHandler();

                KeyboardHookHelper.Instance.CleanupHook();

                // Reset the edit status
                _isEditMode = false;
                _editingRemapping = null;
            }
        }

        private void KeyDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            List<string> originalKeys = RemappingControl.GetOriginalKeys();
            List<string> remappedKeys = RemappingControl.GetRemappedKeys();
            bool isAppSpecific = RemappingControl.GetIsAppSpecific();
            string appName = RemappingControl.GetAppName();

            // Make sure _mappingService is not null before validating and saving
            if (_mappingService == null)
            {
                Logger.LogError("Mapping service is null, cannot validate mapping");
                return;
            }

            // Validate the remapping
            ValidationErrorType errorType = ValidationHelper.ValidateKeyMapping(
                originalKeys, remappedKeys, isAppSpecific, appName, _mappingService, _isEditMode, _editingRemapping);

            if (errorType != ValidationErrorType.NoError)
            {
                ShowValidationError(errorType, args);
                return;
            }

            // Check for orphaned keys
            if (originalKeys.Count == 1 && _mappingService != null)
            {
                int originalKeyCode = _mappingService.GetKeyCodeFromName(originalKeys[0]);

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

            // If in edit mode, delete the existing remapping before saving the new one
            if (_isEditMode && _editingRemapping != null)
            {
                if (!RemappingHelper.DeleteRemapping(_mappingService!, _editingRemapping))
                {
                    return;
                }
            }

            // If no errors, proceed to save the remapping
            bool saved = RemappingHelper.SaveMapping(_mappingService!, originalKeys, remappedKeys, isAppSpecific, appName);
            if (saved)
            {
                // Display the remapping in the list after saving
                LoadMappings();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Remapping remapping)
            {
                if (RemappingHelper.DeleteRemapping(_mappingService!, remapping))
                {
                    LoadMappings();
                }
            }
        }

        private void ValidationTeachingTip_CloseButtonClick(TeachingTip sender, object args)
        {
            sender.IsOpen = false;
        }

        private void OrphanedKeysTeachingTip_ActionButtonClick(TeachingTip sender, object args)
        {
            // User pressed continue anyway button
            sender.IsOpen = false;

            if (_isEditMode && _editingRemapping != null)
            {
                if (!RemappingHelper.DeleteRemapping(_mappingService!, _editingRemapping))
                {
                    return;
                }
            }

            bool saved = RemappingHelper.SaveMapping(
                _mappingService!, RemappingControl.GetOriginalKeys(), RemappingControl.GetRemappedKeys(), RemappingControl.GetIsAppSpecific(), RemappingControl.GetAppName());
            if (saved)
            {
                KeyDialog.Hide();
                LoadMappings();
            }
        }

        private void OrphanedKeysTeachingTip_CloseButtonClick(TeachingTip sender, object args)
        {
            // Just close the teaching tip if the user canceled
            sender.IsOpen = false;
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

        private bool ShowValidationError(ValidationErrorType errorType, ContentDialogButtonClickEventArgs args)
        {
            var (title, message) = ValidationMessages[errorType];

            ValidationTeachingTip.Title = title;
            ValidationTeachingTip.Subtitle = message;
            ValidationTeachingTip.Target = RemappingControl;
            ValidationTeachingTip.Tag = args;
            ValidationTeachingTip.IsOpen = true;
            args.Cancel = true;
            return false;
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
    }
}
