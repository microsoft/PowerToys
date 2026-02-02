// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using KeyboardManagerEditorUI.Controls;
using KeyboardManagerEditorUI.Helpers;
using KeyboardManagerEditorUI.Interop;
using KeyboardManagerEditorUI.Settings;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static KeyboardManagerEditorUI.Interop.ShortcutKeyMapping;

namespace KeyboardManagerEditorUI.Pages
{
    /// <summary>
    /// A consolidated page that displays all mappings from Remappings, Text, Programs, and URLs pages.
    /// </summary>
#pragma warning disable SA1124 // Do not use regions
    public sealed partial class All : Page, IDisposable
    {
        private KeyboardMappingService? _mappingService;
        private bool _disposed;

        // Edit mode tracking
        private bool _isEditMode;
        private EditingItem? _editingItem;

        public ObservableCollection<Remapping> RemappingList { get; } = new ObservableCollection<Remapping>();

        public ObservableCollection<TextMapping> TextMappings { get; } = new ObservableCollection<TextMapping>();

        public ObservableCollection<ProgramShortcut> ProgramShortcuts { get; } = new ObservableCollection<ProgramShortcut>();

        public ObservableCollection<URLShortcut> UrlShortcuts { get; } = new ObservableCollection<URLShortcut>();

        [DllImport("PowerToys.KeyboardManagerEditorLibraryWrapper.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern void GetKeyDisplayName(int keyCode, [Out] StringBuilder keyName, int maxLength);

        /// <summary>
        /// Tracks what item is being edited and its type.
        /// </summary>
        private sealed class EditingItem
        {
            public enum ItemType
            {
                Remapping,
                TextMapping,
                ProgramShortcut,
                UrlShortcut,
            }

            public ItemType Type { get; set; }

            public object Item { get; set; } = null!;

            public List<string> OriginalTriggerKeys { get; set; } = new();

            public string? AppName { get; set; }

            public bool IsAllApps { get; set; } = true;
        }

        public All()
        {
            this.InitializeComponent();

            try
            {
                _mappingService = new KeyboardMappingService();
                LoadAllMappings();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize KeyboardMappingService in All page: " + ex.Message);
            }

            this.Unloaded += All_Unloaded;
        }

        private void All_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        #region Dialog Show Methods

        private async void NewRemappingBtn_Click(object sender, RoutedEventArgs e)
        {
            _isEditMode = false;
            _editingItem = null;

            // Reset the control before showing
            UnifiedMappingControl.Reset();
            RemappingDialog.Title = "Add new remapping";

            await ShowRemappingDialog();
        }

        private async void RemappingsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Remapping remapping)
            {
                _isEditMode = true;
                _editingItem = new EditingItem
                {
                    Type = EditingItem.ItemType.Remapping,
                    Item = remapping,
                    OriginalTriggerKeys = remapping.OriginalKeys.ToList(),
                    AppName = remapping.AppName,
                    IsAllApps = remapping.IsAllApps,
                };

                UnifiedMappingControl.Reset();
                UnifiedMappingControl.SetTriggerKeys(remapping.OriginalKeys.ToList());
                UnifiedMappingControl.SetActionType(UnifiedMappingControl.ActionType.KeyOrShortcut);
                UnifiedMappingControl.SetActionKeys(remapping.RemappedKeys.ToList());
                UnifiedMappingControl.SetAppSpecific(!remapping.IsAllApps, remapping.AppName);

                RemappingDialog.Title = "Edit remapping";
                await ShowRemappingDialog();
            }
        }

        private async void TextMappingsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TextMapping textMapping)
            {
                _isEditMode = true;
                _editingItem = new EditingItem
                {
                    Type = EditingItem.ItemType.TextMapping,
                    Item = textMapping,
                    OriginalTriggerKeys = textMapping.Keys.ToList(),
                    AppName = textMapping.AppName,
                    IsAllApps = textMapping.IsAllApps,
                };

                UnifiedMappingControl.Reset();
                UnifiedMappingControl.SetTriggerKeys(textMapping.Keys.ToList());
                UnifiedMappingControl.SetActionType(UnifiedMappingControl.ActionType.Text);
                UnifiedMappingControl.SetTextContent(textMapping.Text);
                UnifiedMappingControl.SetAppSpecific(!textMapping.IsAllApps, textMapping.AppName);

                RemappingDialog.Title = "Edit text mapping";
                await ShowRemappingDialog();
            }
        }

        private async void ProgramShortcutsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ProgramShortcut programShortcut)
            {
                _isEditMode = true;
                _editingItem = new EditingItem
                {
                    Type = EditingItem.ItemType.ProgramShortcut,
                    Item = programShortcut,
                    OriginalTriggerKeys = programShortcut.Shortcut.ToList(),
                };

                UnifiedMappingControl.Reset();
                UnifiedMappingControl.SetTriggerKeys(programShortcut.Shortcut.ToList());
                UnifiedMappingControl.SetActionType(UnifiedMappingControl.ActionType.OpenApp);
                UnifiedMappingControl.SetProgramPath(programShortcut.AppToRun);
                UnifiedMappingControl.SetProgramArgs(programShortcut.Args);

                // Load additional settings from SettingsManager if available
                if (!string.IsNullOrEmpty(programShortcut.Id) &&
                    SettingsManager.EditorSettings.ShortcutSettingsDictionary.TryGetValue(programShortcut.Id, out var settings))
                {
                    var mapping = settings.Shortcut;
                    UnifiedMappingControl.SetStartInDirectory(mapping.StartInDirectory);
                    UnifiedMappingControl.SetElevationLevel(mapping.Elevation);
                    UnifiedMappingControl.SetVisibility(mapping.Visibility);
                    UnifiedMappingControl.SetIfRunningAction(mapping.IfRunningAction);
                }

                RemappingDialog.Title = "Edit program shortcut";
                await ShowRemappingDialog();
            }
        }

        private async void UrlShortcutsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is URLShortcut urlShortcut)
            {
                _isEditMode = true;
                _editingItem = new EditingItem
                {
                    Type = EditingItem.ItemType.UrlShortcut,
                    Item = urlShortcut,
                    OriginalTriggerKeys = urlShortcut.Shortcut.ToList(),
                };

                UnifiedMappingControl.Reset();
                UnifiedMappingControl.SetTriggerKeys(urlShortcut.Shortcut.ToList());
                UnifiedMappingControl.SetActionType(UnifiedMappingControl.ActionType.OpenUrl);
                UnifiedMappingControl.SetUrl(urlShortcut.URL);

                RemappingDialog.Title = "Edit URL shortcut";
                await ShowRemappingDialog();
            }
        }

        private async System.Threading.Tasks.Task ShowRemappingDialog()
        {
            // Hook up the primary button click handler
            RemappingDialog.PrimaryButtonClick += RemappingDialog_PrimaryButtonClick;

            // Show the dialog
            await RemappingDialog.ShowAsync();

            // Unhook the handler
            RemappingDialog.PrimaryButtonClick -= RemappingDialog_PrimaryButtonClick;

            // Reset edit mode
            _isEditMode = false;
            _editingItem = null;

            // Cleanup keyboard hook after dialog closes
            KeyboardHookHelper.Instance.CleanupHook();
        }

        #endregion

        #region Save Logic
        private void RemappingDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (_mappingService == null)
            {
                Logger.LogError("Mapping service is null, cannot save mapping");
                args.Cancel = true;
                return;
            }

            try
            {
                bool saved = false;
                var actionType = UnifiedMappingControl.CurrentActionType;
                List<string> triggerKeys = UnifiedMappingControl.GetTriggerKeys();

                if (triggerKeys == null || triggerKeys.Count == 0)
                {
                    // No trigger keys specified
                    args.Cancel = true;
                    return;
                }

                // If in edit mode, delete the existing mapping first
                if (_isEditMode && _editingItem != null)
                {
                    DeleteExistingMapping();
                }

                switch (actionType)
                {
                    case UnifiedMappingControl.ActionType.KeyOrShortcut:
                        saved = SaveKeyOrShortcutMapping(triggerKeys);
                        break;

                    case UnifiedMappingControl.ActionType.Text:
                        saved = SaveTextMapping(triggerKeys);
                        break;

                    case UnifiedMappingControl.ActionType.OpenUrl:
                        saved = SaveUrlMapping(triggerKeys);
                        break;

                    case UnifiedMappingControl.ActionType.OpenApp:
                        saved = SaveProgramMapping(triggerKeys);
                        break;

                    case UnifiedMappingControl.ActionType.MouseClick:
                        // Not implemented yet
                        args.Cancel = true;
                        return;
                }

                if (saved)
                {
                    LoadAllMappings();
                }
                else
                {
                    args.Cancel = true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error saving mapping: " + ex.Message);
                args.Cancel = true;
            }
        }

        private void DeleteExistingMapping()
        {
            if (_editingItem == null || _mappingService == null)
            {
                return;
            }

            try
            {
                var originalKeys = _editingItem.OriginalTriggerKeys;

                switch (_editingItem.Type)
                {
                    case EditingItem.ItemType.Remapping:
                        if (_editingItem.Item is Remapping remapping)
                        {
                            RemappingHelper.DeleteRemapping(_mappingService, remapping);
                        }

                        break;

                    case EditingItem.ItemType.TextMapping:
                        if (originalKeys.Count == 1)
                        {
                            int originalKey = _mappingService.GetKeyCodeFromName(originalKeys[0]);
                            if (originalKey != 0)
                            {
                                _mappingService.DeleteSingleKeyToTextMapping(originalKey);
                            }
                        }
                        else
                        {
                            string originalKeysString = string.Join(";", originalKeys.Select(k => _mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
                            _mappingService.DeleteShortcutMapping(originalKeysString, _editingItem.IsAllApps ? string.Empty : _editingItem.AppName ?? string.Empty);
                        }

                        break;

                    case EditingItem.ItemType.ProgramShortcut:
                        if (_editingItem.Item is ProgramShortcut programShortcut)
                        {
                            if (originalKeys.Count == 1)
                            {
                                int originalKey = _mappingService.GetKeyCodeFromName(originalKeys[0]);
                                if (originalKey != 0)
                                {
                                    _mappingService.DeleteSingleKeyMapping(originalKey);
                                }
                            }
                            else
                            {
                                string originalKeysString = string.Join(";", originalKeys.Select(k => _mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
                                _mappingService.DeleteShortcutMapping(originalKeysString);
                            }

                            if (!string.IsNullOrEmpty(programShortcut.Id))
                            {
                                SettingsManager.RemoveShortcutKeyMappingFromSettings(programShortcut.Id);
                            }
                        }

                        break;

                    case EditingItem.ItemType.UrlShortcut:
                        if (originalKeys.Count == 1)
                        {
                            int originalKey = _mappingService.GetKeyCodeFromName(originalKeys[0]);
                            if (originalKey != 0)
                            {
                                _mappingService.DeleteSingleKeyMapping(originalKey);
                            }
                        }
                        else
                        {
                            string originalKeysString = string.Join(";", originalKeys.Select(k => _mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
                            _mappingService.DeleteShortcutMapping(originalKeysString);
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error deleting existing mapping: " + ex.Message);
            }
        }

        private bool SaveKeyOrShortcutMapping(List<string> triggerKeys)
        {
            List<string> actionKeys = UnifiedMappingControl.GetActionKeys();
            bool isAppSpecific = UnifiedMappingControl.GetIsAppSpecific();
            string appName = UnifiedMappingControl.GetAppName();

            if (actionKeys == null || actionKeys.Count == 0)
            {
                return false;
            }

            return RemappingHelper.SaveMapping(_mappingService!, triggerKeys, actionKeys, isAppSpecific, appName);
        }

        private bool SaveTextMapping(List<string> triggerKeys)
        {
            string textContent = UnifiedMappingControl.GetTextContent();
            bool isAppSpecific = UnifiedMappingControl.GetIsAppSpecific();
            string appName = UnifiedMappingControl.GetAppName();

            if (string.IsNullOrEmpty(textContent))
            {
                return false;
            }

            if (triggerKeys.Count == 1)
            {
                // Single key to text mapping
                int originalKey = _mappingService!.GetKeyCodeFromName(triggerKeys[0]);
                if (originalKey != 0)
                {
                    bool saved = _mappingService.AddSingleKeyToTextMapping(originalKey, textContent);
                    if (saved)
                    {
                        return _mappingService.SaveSettings();
                    }
                }
            }
            else
            {
                // Shortcut to text mapping
                string originalKeysString = string.Join(";", triggerKeys.Select(k => _mappingService!.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));

                bool saved;
                if (isAppSpecific && !string.IsNullOrEmpty(appName))
                {
                    saved = _mappingService!.AddShortcutMapping(originalKeysString, textContent, appName, ShortcutOperationType.RemapText);
                }
                else
                {
                    saved = _mappingService!.AddShortcutMapping(originalKeysString, textContent, operationType: ShortcutOperationType.RemapText);
                }

                if (saved)
                {
                    return _mappingService.SaveSettings();
                }
            }

            return false;
        }

        private bool SaveUrlMapping(List<string> triggerKeys)
        {
            string url = UnifiedMappingControl.GetUrl();

            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            string originalKeysString = string.Join(";", triggerKeys.Select(k => _mappingService!.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));

            ShortcutKeyMapping shortcutKeyMapping = new ShortcutKeyMapping()
            {
                OperationType = ShortcutOperationType.OpenUri,
                OriginalKeys = originalKeysString,
                TargetKeys = originalKeysString,
                UriToOpen = url,
            };

            bool saved = _mappingService!.AddShorcutMapping(shortcutKeyMapping);

            if (saved)
            {
                return _mappingService.SaveSettings();
            }

            return false;
        }

        private bool SaveProgramMapping(List<string> triggerKeys)
        {
            string programPath = UnifiedMappingControl.GetProgramPath();
            string programArgs = UnifiedMappingControl.GetProgramArgs();
            string startInDir = UnifiedMappingControl.GetStartInDirectory();
            ElevationLevel elevationLevel = UnifiedMappingControl.GetElevationLevel();
            StartWindowType visibility = UnifiedMappingControl.GetVisibility();
            ProgramAlreadyRunningAction ifRunningAction = UnifiedMappingControl.GetIfRunningAction();

            if (string.IsNullOrEmpty(programPath))
            {
                return false;
            }

            string originalKeysString = string.Join(";", triggerKeys.Select(k => _mappingService!.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));

            ShortcutKeyMapping shortcutKeyMapping = new ShortcutKeyMapping()
            {
                OperationType = ShortcutOperationType.RunProgram,
                OriginalKeys = originalKeysString,
                TargetKeys = originalKeysString,
                ProgramPath = programPath,
                ProgramArgs = programArgs,
                StartInDirectory = startInDir,
                IfRunningAction = ifRunningAction,
                Visibility = visibility,
                Elevation = elevationLevel,
            };

            bool saved = _mappingService!.AddShorcutMapping(shortcutKeyMapping);

            if (saved)
            {
                _mappingService.SaveSettings();
                SettingsManager.AddShortcutKeyMappingToSettings(shortcutKeyMapping);
                return true;
            }

            return false;
        }

        #endregion

        #region Load Methods

        private void LoadAllMappings()
        {
            LoadRemappings();
            LoadTextMappings();
            LoadProgramShortcuts();
            LoadUrlShortcuts();
        }

        private void LoadRemappings()
        {
            if (_mappingService == null)
            {
                return;
            }

            RemappingList.Clear();

            // Load all single key mappings
            foreach (var mapping in _mappingService.GetSingleKeyMappings())
            {
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

        private void LoadTextMappings()
        {
            if (_mappingService == null)
            {
                return;
            }

            TextMappings.Clear();

            // Load key-to-text mappings
            var keyToTextMappings = _mappingService.GetKeyToTextMappings();
            foreach (var mapping in keyToTextMappings)
            {
                TextMappings.Add(new TextMapping
                {
                    Keys = new List<string> { _mappingService.GetKeyDisplayName(mapping.OriginalKey) },
                    Text = mapping.TargetText,
                    IsAllApps = true,
                    AppName = "All Apps",
                });
            }

            // Load shortcut-to-text mappings
            foreach (var mapping in _mappingService.GetShortcutMappingsByType(ShortcutOperationType.RemapText))
            {
                string[] originalKeyCodes = mapping.OriginalKeys.Split(';');
                var originalKeyNames = new List<string>();
                foreach (var keyCode in originalKeyCodes)
                {
                    if (int.TryParse(keyCode, out int code))
                    {
                        originalKeyNames.Add(_mappingService.GetKeyDisplayName(code));
                    }
                }

                TextMappings.Add(new TextMapping
                {
                    Keys = originalKeyNames,
                    Text = mapping.TargetText,
                    IsAllApps = string.IsNullOrEmpty(mapping.TargetApp),
                    AppName = string.IsNullOrEmpty(mapping.TargetApp) ? "All Apps" : mapping.TargetApp,
                });
            }
        }

        private void LoadProgramShortcuts()
        {
            if (_mappingService == null)
            {
                return;
            }

            ProgramShortcuts.Clear();

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

                ProgramShortcuts.Add(new ProgramShortcut
                {
                    Shortcut = originalKeyNames,
                    AppToRun = mapping.ProgramPath,
                    Args = mapping.ProgramArgs,
                    IsActive = shortcutSettings.IsActive,
                    Id = shortcutSettings.Id,
                });
            }
        }

        private void LoadUrlShortcuts()
        {
            if (_mappingService == null)
            {
                return;
            }

            UrlShortcuts.Clear();

            foreach (var mapping in _mappingService.GetShortcutMappingsByType(ShortcutOperationType.OpenUri))
            {
                string[] originalKeyCodes = mapping.OriginalKeys.Split(';');
                var originalKeyNames = new List<string>();
                foreach (var keyCode in originalKeyCodes)
                {
                    if (int.TryParse(keyCode, out int code))
                    {
                        originalKeyNames.Add(GetKeyDisplayNameFromCode(code));
                    }
                }

                UrlShortcuts.Add(new URLShortcut
                {
                    Shortcut = originalKeyNames,
                    URL = mapping.UriToOpen,
                });
            }
        }

        private static string GetKeyDisplayNameFromCode(int keyCode)
        {
            var keyName = new StringBuilder(64);
            GetKeyDisplayName(keyCode, keyName, keyName.Capacity);
            return keyName.ToString();
        }

        #endregion

        #region IDisposable

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

        #endregion
    }
}
#pragma warning restore SA1124 // Do not use regions
