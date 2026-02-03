// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using KeyboardManagerEditorUI.Helpers;
using KeyboardManagerEditorUI.Interop;
using KeyboardManagerEditorUI.Settings;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static KeyboardManagerEditorUI.Interop.ShortcutKeyMapping;

namespace KeyboardManagerEditorUI.Pages
{
    public sealed partial class Programs : Page, IDisposable
    {
        private KeyboardMappingService? _mappingService;

        // Flag to indicate if the user is editing an existing mapping
        private bool _isEditMode;
        private ProgramShortcut? _editingMapping;

        private bool _disposed;

        // The list of text mappings
        public ObservableCollection<ProgramShortcut> Shortcuts { get; set; } = new ObservableCollection<ProgramShortcut> { };

        public Programs()
        {
            this.InitializeComponent();

            try
            {
                _mappingService = new KeyboardMappingService();
                LoadProgramShortcuts();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize KeyboardMappingService: " + ex.Message);
            }

            this.Unloaded += Text_Unloaded;
        }

        private void Text_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        private void LoadProgramShortcuts()
        {
            if (_mappingService == null)
            {
                return;
            }

            Shortcuts.Clear();

            foreach (var shortcutSettings in SettingsManager.GetShortcutSettingsByOperationType(ShortcutOperationType.RunProgram))
            {
                ShortcutKeyMapping mapping = shortcutSettings.Shortcut;
                string[] originalKeyCodes = mapping.OriginalKeys.Split(';');
                var originalKeyNames = new List<string>();
                foreach (var keyCode in originalKeyCodes)
                {
                    if (int.TryParse(keyCode, out int code))
                    {
                        originalKeyNames.Add(_mappingService.GetKeyDisplayName(code));
                    }
                }

                Shortcuts.Add(new ProgramShortcut
                {
                    Shortcut = originalKeyNames,
                    AppToRun = mapping.ProgramPath,
                    Args = mapping.ProgramArgs,
                    IsActive = shortcutSettings.IsActive,
                    Id = shortcutSettings.Id,
                });
            }
        }

        private async void NewShortcutBtn_Click(object sender, RoutedEventArgs e)
        {
            _isEditMode = false;
            _editingMapping = null;

            AppShortcutControl.ClearKeys();
            AppShortcutControl.SetProgramPathContent(string.Empty);
            AppShortcutControl.SetProgramArgsContent(string.Empty);

            await KeyDialog.ShowAsync();
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ProgramShortcut selectedMapping)
            {
                _isEditMode = true;
                _editingMapping = selectedMapping;

                AppShortcutControl.SetShortcutKeys(selectedMapping.Shortcut);
                AppShortcutControl.SetProgramPathContent(selectedMapping.AppToRun);
                AppShortcutControl.SetProgramArgsContent(selectedMapping.Args);

                await KeyDialog.ShowAsync();
            }
        }

        private void KeyDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (_mappingService == null)
            {
                return;
            }

            List<string> keys = AppShortcutControl.GetShortcutKeys();
            string programPath = AppShortcutControl.GetProgramPathContent();
            string programArgs = AppShortcutControl.GetProgramArgsContent();
            ElevationLevel elevationLevel = AppShortcutControl.GetElevationLevel();
            StartWindowType startWindowType = AppShortcutControl.GetVisibility();
            ProgramAlreadyRunningAction programAlreadyRunningAction = AppShortcutControl.GetIfRunningAction();

            // Validate inputs
            ValidationErrorType errorType = ValidationHelper.ValidateProgramOrUrlMapping(keys, false, string.Empty, _mappingService);

            if (errorType != ValidationErrorType.NoError)
            {
                ShowValidationError(errorType, args);
                return;
            }

            bool saved = false;

            try
            {
                // Delete existing mapping if in edit mode
                if (_isEditMode && _editingMapping != null)
                {
                    if (_editingMapping.Shortcut.Count == 1)
                    {
                        int originalKey = _mappingService.GetKeyCodeFromName(_editingMapping.Shortcut[0]);
                        if (originalKey != 0)
                        {
                            _mappingService.DeleteSingleKeyMapping(originalKey);
                        }
                    }
                    else
                    {
                        string originalKeys = string.Join(";", _editingMapping.Shortcut.Select(k => _mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
                        _mappingService.DeleteShortcutMapping(originalKeys);
                    }

                    SettingsManager.RemoveShortcutKeyMappingFromSettings(_editingMapping.Id);
                }

                // Shortcut to text mapping
                string originalKeysString = string.Join(";", keys.Select(k => _mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));

                ShortcutKeyMapping shortcutKeyMapping = new ShortcutKeyMapping()
                {
                    OperationType = ShortcutOperationType.RunProgram,
                    OriginalKeys = originalKeysString,
                    TargetKeys = originalKeysString,
                    ProgramPath = programPath,
                    ProgramArgs = programArgs,
                    IfRunningAction = programAlreadyRunningAction,
                    Visibility = startWindowType,
                    Elevation = elevationLevel,
                };

                saved = _mappingService.AddShortcutMapping(shortcutKeyMapping);

                if (saved)
                {
                    _mappingService.SaveSettings();
                    SettingsManager.AddShortcutKeyMappingToSettings(shortcutKeyMapping);
                    LoadProgramShortcuts(); // Refresh the list
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error saving text mapping: " + ex.Message);
                args.Cancel = true;
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mappingService == null || !(sender is Button button) || !(button.DataContext is ProgramShortcut shortcut))
            {
                return;
            }

            try
            {
                bool deleted = false;
                if (shortcut.Shortcut.Count == 1)
                {
                    // Single key mapping
                    int originalKey = _mappingService.GetKeyCodeFromName(shortcut.Shortcut[0]);
                    if (originalKey != 0)
                    {
                        deleted = _mappingService.DeleteSingleKeyToTextMapping(originalKey);
                    }
                }
                else
                {
                    // Shortcut mapping
                    string originalKeys = string.Join(";", shortcut.Shortcut.Select(k => _mappingService.GetKeyCodeFromName(k)));
                    deleted = _mappingService.DeleteShortcutMapping(originalKeys);
                }

                if (deleted)
                {
                    _mappingService.SaveSettings();
                }

                SettingsManager.RemoveShortcutKeyMappingFromSettings(shortcut.Id);
                LoadProgramShortcuts();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error deleting text mapping: " + ex.Message);
            }
        }

        private void ShowValidationError(ValidationErrorType errorType, ContentDialogButtonClickEventArgs args)
        {
            if (ValidationHelper.ValidationMessages.TryGetValue(errorType, out (string Title, string Message) error))
            {
                ValidationTip.Title = error.Title;
                ValidationTip.Subtitle = error.Message;
                ValidationTip.IsOpen = true;
                args.Cancel = true;
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
                    _mappingService?.Dispose();
                    _mappingService = null;
                }

                _disposed = true;
            }
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggleSwitch && toggleSwitch.DataContext is ProgramShortcut shortcut && _mappingService != null)
            {
                try
                {
                    if (toggleSwitch.IsOn)
                    {
                        bool saved = false;
                        ShortcutKeyMapping shortcutKeyMapping = SettingsManager.EditorSettings.ShortcutSettingsDictionary[shortcut.Id].Shortcut;

                        saved = _mappingService.AddShorcutMapping(shortcutKeyMapping);

                        if (saved)
                        {
                            shortcut.IsActive = true;
                            _mappingService.SaveSettings();
                            SettingsManager.ToggleShortcutKeyMappingActiveState(shortcut.Id);
                        }
                        else
                        {
                            toggleSwitch.IsOn = false;
                        }
                    }
                    else
                    {
                        bool deleted = false;
                        if (shortcut.Shortcut.Count == 1)
                        {
                            // Single key mapping
                            int originalKey = _mappingService.GetKeyCodeFromName(shortcut.Shortcut[0]);
                            if (originalKey != 0)
                            {
                                deleted = _mappingService.DeleteSingleKeyToTextMapping(originalKey);
                            }
                        }
                        else
                        {
                            // Shortcut mapping
                            string originalKeys = string.Join(";", shortcut.Shortcut.Select(k => _mappingService.GetKeyCodeFromName(k)));
                            deleted = _mappingService.DeleteShortcutMapping(originalKeys);
                        }

                        if (deleted)
                        {
                            shortcut.IsActive = false;
                            SettingsManager.ToggleShortcutKeyMappingActiveState(shortcut.Id);
                            _mappingService.SaveSettings();
                        }
                        else
                        {
                            toggleSwitch.IsOn = true;
                        }

                        LoadProgramShortcuts();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error toggling shortcut active state: " + ex.Message);
                }
            }
        }
    }
}
