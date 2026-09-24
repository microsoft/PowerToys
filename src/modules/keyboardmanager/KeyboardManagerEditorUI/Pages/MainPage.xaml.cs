// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using KeyboardManagerEditorUI.Controls;
using KeyboardManagerEditorUI.Helpers;
using KeyboardManagerEditorUI.Interop;
using KeyboardManagerEditorUI.Settings;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static KeyboardManagerEditorUI.Interop.ShortcutKeyMapping;

namespace KeyboardManagerEditorUI.Pages
{
    /// <summary>
    /// A consolidated page that displays all mappings from Remappings, Text, Programs, and URLs pages.
    /// </summary>
#pragma warning disable SA1124 // Do not use regions
    public sealed partial class MainPage : Page, IDisposable, INotifyPropertyChanged
    {
        /// <summary>VK_DISABLED sentinel: target key code that tells the engine to suppress the key.</summary>
        private const int VkDisabled = 0x100;

        /// <summary>String form of <see cref="VkDisabled"/> used in shortcut key mapping serialization.</summary>
        private const string VkDisabledString = "256";

        private DispatcherTimer? _serviceCheckTimer;
        private KeyboardMappingService? _mappingService;
        private bool _disposed;
        private bool _isEditMode;
        private EditingItem? _editingItem;
        private string? _pendingOrphanedKeyName;
        private string _mappingState = "Empty";
        private bool _isServiceRunning = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string MappingState
        {
            get => _mappingState;
            private set
            {
                if (_mappingState != value)
                {
                    _mappingState = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MappingState)));
                }
            }
        }

        public bool IsServiceRunning
        {
            get => _isServiceRunning;
            private set
            {
                if (_isServiceRunning != value)
                {
                    _isServiceRunning = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsServiceRunning)));
                    UpdateServiceBannerVisibility();
                }
            }
        }

        public ObservableCollection<Remapping> RemappingList { get; } = new();

        public ObservableCollection<Remapping> DisabledList { get; } = new();

        public ObservableCollection<TextMapping> TextMappings { get; } = new();

        public ObservableCollection<ProgramShortcut> ProgramShortcuts { get; } = new();

        public ObservableCollection<URLShortcut> UrlShortcuts { get; } = new();

        [DllImport("PowerToys.KeyboardManagerEditorLibraryWrapper.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern void GetKeyDisplayName(int keyCode, [Out] StringBuilder keyName, int maxLength);

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

        public MainPage()
        {
            this.InitializeComponent();
            try
            {
                _mappingService = new KeyboardMappingService();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize mapping service: " + ex.Message);
                IsServiceRunning = false;
                return;
            }

            if (_mappingService != null)
            {
                LoadAllMappings();
            }
            else
            {
                MappingState = "Error";
            }

            // A failed configuration read leaves the editor showing an empty list that looks like
            // "you have no remaps". Saving is blocked in that state (KeyboardMappingService), so
            // say why rather than letting the user hit an unexplained save failure.
            if (_mappingService is { ConfigurationLoaded: false })
            {
                ConfigLoadFailedBanner.Title = ResourceHelper.GetString("Error_ConfigLoadFailed_Title");
                ConfigLoadFailedBanner.Message = ResourceHelper.GetString("Error_ConfigLoadFailed_Message");
                ConfigLoadFailedBanner.IsOpen = true;
            }

            Unloaded += All_Unloaded;

            CheckServiceStatus();

            // Set up periodic checks every 3 seconds
            _serviceCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3),
            };
            _serviceCheckTimer.Tick += (s, e) => CheckServiceStatus();
            _serviceCheckTimer.Start();
        }

        private void All_Unloaded(object sender, RoutedEventArgs e) => Dispose();

        private void CheckServiceStatus()
        {
            IsServiceRunning = ServiceStatusHelper.IsKeyboardManagerServiceRunning();
        }

        private void UpdateServiceBannerVisibility()
        {
            ServiceDownBanner.Visibility = IsServiceRunning ? Visibility.Collapsed : Visibility.Visible;
        }

        #region Dialog Show Methods

        private async void NewRemappingBtn_Click(object sender, RoutedEventArgs e)
        {
            _isEditMode = false;
            _editingItem = null;
            UnifiedMappingControl.Reset();
            RemappingDialog.Title = ResourceHelper.GetString("RemappingDialog/Title");
            await ShowRemappingDialog();
        }

        private async void RemappingsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not Remapping remapping)
            {
                return;
            }

            _isEditMode = true;
            _editingItem = new EditingItem
            {
                Type = EditingItem.ItemType.Remapping,
                Item = remapping,
                OriginalTriggerKeys = remapping.Shortcut.ToList(),
                AppName = remapping.AppName,
                IsAllApps = remapping.IsAllApps,
            };

            UnifiedMappingControl.Reset();
            UnifiedMappingControl.SetTriggerKeys(remapping.Shortcut.ToList());
            UnifiedMappingControl.SetActionType(UnifiedMappingControl.ActionType.KeyOrShortcut);
            UnifiedMappingControl.SetActionKeys(remapping.RemappedKeys.ToList());
            UnifiedMappingControl.SetExactMatch(remapping.ExactMatch);
            UnifiedMappingControl.SetAppSpecific(!remapping.IsAllApps, remapping.AppName);
            RemappingDialog.Title = ResourceHelper.GetString("RemappingDialog_TitleEdit");
            await ShowRemappingDialog();
        }

        private async void DisabledList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not Remapping disabledMapping)
            {
                return;
            }

            _isEditMode = true;
            _editingItem = new EditingItem
            {
                Type = EditingItem.ItemType.Remapping,
                Item = disabledMapping,
                OriginalTriggerKeys = disabledMapping.Shortcut.ToList(),
                AppName = disabledMapping.AppName,
                IsAllApps = disabledMapping.IsAllApps,
            };

            UnifiedMappingControl.Reset();
            UnifiedMappingControl.SetTriggerKeys(disabledMapping.Shortcut.ToList());
            UnifiedMappingControl.SetActionType(UnifiedMappingControl.ActionType.Disable);
            UnifiedMappingControl.SetExactMatch(disabledMapping.ExactMatch);
            UnifiedMappingControl.SetAppSpecific(!disabledMapping.IsAllApps, disabledMapping.AppName);
            RemappingDialog.Title = ResourceHelper.GetString("RemappingDialog_TitleEdit");
            await ShowRemappingDialog();
        }

        private async void TextMappingsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not TextMapping textMapping)
            {
                return;
            }

            _isEditMode = true;
            _editingItem = new EditingItem
            {
                Type = EditingItem.ItemType.TextMapping,
                Item = textMapping,
                OriginalTriggerKeys = textMapping.Shortcut.ToList(),
                AppName = textMapping.AppName,
                IsAllApps = textMapping.IsAllApps,
            };

            UnifiedMappingControl.Reset();
            UnifiedMappingControl.SetTriggerKeys(textMapping.Shortcut.ToList());
            UnifiedMappingControl.SetActionType(UnifiedMappingControl.ActionType.Text);
            UnifiedMappingControl.SetTextContent(textMapping.Text);
            UnifiedMappingControl.SetExactMatch(textMapping.ExactMatch);
            UnifiedMappingControl.SetAppSpecific(!textMapping.IsAllApps, textMapping.AppName);
            RemappingDialog.Title = ResourceHelper.GetString("RemappingDialog_TitleEdit");
            await ShowRemappingDialog();
        }

        private async void ProgramShortcutsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not ProgramShortcut programShortcut)
            {
                return;
            }

            _isEditMode = true;
            _editingItem = new EditingItem
            {
                Type = EditingItem.ItemType.ProgramShortcut,
                Item = programShortcut,
                OriginalTriggerKeys = programShortcut.Shortcut.ToList(),
                AppName = programShortcut.AppName,
                IsAllApps = programShortcut.IsAllApps,
            };

            UnifiedMappingControl.Reset();
            UnifiedMappingControl.SetTriggerKeys(programShortcut.Shortcut.ToList());
            UnifiedMappingControl.SetActionType(UnifiedMappingControl.ActionType.OpenApp);
            UnifiedMappingControl.SetProgramPath(programShortcut.AppToRun);
            UnifiedMappingControl.SetProgramArgs(programShortcut.Args);

            if (!string.IsNullOrEmpty(programShortcut.Id) &&
                SettingsManager.EditorSettings.ShortcutSettingsDictionary.TryGetValue(programShortcut.Id, out var settings))
            {
                var mapping = settings.Shortcut;
                UnifiedMappingControl.SetStartInDirectory(mapping.StartInDirectory);
                UnifiedMappingControl.SetElevationLevel(mapping.Elevation);
                UnifiedMappingControl.SetVisibility(mapping.Visibility);
                UnifiedMappingControl.SetIfRunningAction(mapping.IfRunningAction);
            }

            UnifiedMappingControl.SetExactMatch(programShortcut.ExactMatch);
            UnifiedMappingControl.SetAppSpecific(!programShortcut.IsAllApps, programShortcut.AppName);
            RemappingDialog.Title = ResourceHelper.GetString("RemappingDialog_TitleEdit");
            await ShowRemappingDialog();
        }

        private async void UrlShortcutsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not URLShortcut urlShortcut)
            {
                return;
            }

            _isEditMode = true;
            _editingItem = new EditingItem
            {
                Type = EditingItem.ItemType.UrlShortcut,
                Item = urlShortcut,
                OriginalTriggerKeys = urlShortcut.Shortcut.ToList(),
                AppName = urlShortcut.AppName,
                IsAllApps = urlShortcut.IsAllApps,
            };

            UnifiedMappingControl.Reset();
            UnifiedMappingControl.SetTriggerKeys(urlShortcut.Shortcut.ToList());
            UnifiedMappingControl.SetActionType(UnifiedMappingControl.ActionType.OpenUrl);
            UnifiedMappingControl.SetUrl(urlShortcut.URL);
            UnifiedMappingControl.SetExactMatch(urlShortcut.ExactMatch);
            UnifiedMappingControl.SetAppSpecific(!urlShortcut.IsAllApps, urlShortcut.AppName);
            RemappingDialog.Title = ResourceHelper.GetString("RemappingDialog_TitleEdit");
            await ShowRemappingDialog();
        }

        private async System.Threading.Tasks.Task ShowRemappingDialog()
        {
            _pendingOrphanedKeyName = null;
            RemappingDialog.PrimaryButtonClick += RemappingDialog_PrimaryButtonClick;
            UnifiedMappingControl.ValidationStateChanged += UnifiedMappingControl_ValidationStateChanged;
            RemappingDialog.IsPrimaryButtonEnabled = UnifiedMappingControl.IsInputComplete();

            ContentDialogResult result = await RemappingDialog.ShowAsync();

            RemappingDialog.PrimaryButtonClick -= RemappingDialog_PrimaryButtonClick;
            UnifiedMappingControl.ValidationStateChanged -= UnifiedMappingControl_ValidationStateChanged;
            _isEditMode = false;
            _editingItem = null;
            KeyboardHookHelper.Instance.CleanupHook();

            if (result == ContentDialogResult.Primary && _pendingOrphanedKeyName != null)
            {
                ShowOrphanedKeyBanner(_pendingOrphanedKeyName);
            }

            _pendingOrphanedKeyName = null;
        }

        private void ShowOrphanedKeyBanner(string keyName)
        {
            OrphanedKeyBanner.Title = ResourceHelper.GetString("OrphanedKeyInfo_Title");
            OrphanedKeyBanner.Message = ResourceHelper.GetString("Warning_OrphanedKeys").Replace("{0}", keyName, System.StringComparison.Ordinal);
            OrphanedKeyBanner.IsOpen = true;
        }

        private void UnifiedMappingControl_ValidationStateChanged(object? sender, EventArgs e)
        {
            if (!UnifiedMappingControl.IsInputComplete())
            {
                RemappingDialog.IsPrimaryButtonEnabled = false;
                return;
            }

            if (_mappingService != null)
            {
                List<string> triggerKeys = UnifiedMappingControl.GetTriggerKeys();
                if (triggerKeys?.Count > 0)
                {
                    ValidationErrorType error = ValidateMapping(UnifiedMappingControl.CurrentActionType, triggerKeys);
                    if (error != ValidationErrorType.NoError)
                    {
                        UnifiedMappingControl.ShowValidationErrorFromType(error);
                        RemappingDialog.IsPrimaryButtonEnabled = false;
                        return;
                    }
                }
            }

            UnifiedMappingControl.HideValidationMessage();
            RemappingDialog.IsPrimaryButtonEnabled = true;
        }

        #endregion

        #region Save Logic

        private void RemappingDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            UnifiedMappingControl.HideValidationMessage();

            if (_mappingService == null)
            {
                Logger.LogError("Mapping service is null, cannot save mapping");
                UnifiedMappingControl.ShowValidationError(ResourceHelper.GetString("Error_MappingServiceUnavailable_Title"), ResourceHelper.GetString("Error_MappingServiceUnavailable_Message"));
                args.Cancel = true;
                return;
            }

            bool restoreExistingOnFailure = false;
            try
            {
                List<string> triggerKeys = UnifiedMappingControl.GetTriggerKeys();

                if (triggerKeys == null || triggerKeys.Count == 0)
                {
                    UnifiedMappingControl.ShowValidationErrorFromType(ValidationErrorType.EmptyOriginalKeys);
                    args.Cancel = true;
                    return;
                }

                ValidationErrorType validationError = ValidateMapping(UnifiedMappingControl.CurrentActionType, triggerKeys);
                if (validationError != ValidationErrorType.NoError)
                {
                    UnifiedMappingControl.ShowValidationErrorFromType(validationError);
                    args.Cancel = true;
                    return;
                }

                if (!_mappingService.ConfigurationLoaded || (_isEditMode && _editingItem != null && !DeleteExistingMapping()))
                {
                    UnifiedMappingControl.ShowValidationError(ResourceHelper.GetString("Error_SaveFailed_Title"), ResourceHelper.GetString("Error_SaveFailed_Message"));
                    args.Cancel = true;
                    return;
                }

                restoreExistingOnFailure = _isEditMode && _editingItem != null;
                bool saved = UnifiedMappingControl.CurrentActionType switch
                {
                    UnifiedMappingControl.ActionType.KeyOrShortcut => SaveKeyOrShortcutMapping(triggerKeys),
                    UnifiedMappingControl.ActionType.Text => SaveTextMapping(triggerKeys),
                    UnifiedMappingControl.ActionType.OpenUrl => SaveUrlMapping(triggerKeys),
                    UnifiedMappingControl.ActionType.OpenApp => SaveProgramMapping(triggerKeys),
                    UnifiedMappingControl.ActionType.Disable => SaveDisableMapping(triggerKeys),
                    UnifiedMappingControl.ActionType.MouseClick => throw new NotImplementedException("Mouse click remapping is not yet supported."),
                    _ => false,
                };

                if (saved)
                {
                    restoreExistingOnFailure = false;
                    if (_isEditMode && _editingItem?.Item is IToggleableShortcut editedShortcut)
                    {
                        SettingsManager.RemoveShortcutKeyMappingFromSettings(editedShortcut.Id);
                    }

                    // Evaluate the final engine configuration: an edited row may have been the
                    // only remaining way to produce the key that the replacement remaps away.
                    _pendingOrphanedKeyName = ShouldWarnOrphanedKeys(UnifiedMappingControl.CurrentActionType, triggerKeys, out string orphanedKeyName)
                        ? orphanedKeyName
                        : null;
                    LoadAllMappings();
                }
                else
                {
                    RestoreExistingMapping();
                    restoreExistingOnFailure = false;
                    _pendingOrphanedKeyName = null;
                    UnifiedMappingControl.ShowValidationError(ResourceHelper.GetString("Error_SaveFailed_Title"), ResourceHelper.GetString("Error_SaveFailed_Message"));
                    args.Cancel = true;
                }
            }
            catch (NotImplementedException ex)
            {
                if (restoreExistingOnFailure)
                {
                    RestoreExistingMapping();
                }

                UnifiedMappingControl.ShowValidationError(ResourceHelper.GetString("Error_NotImplemented_Title"), ex.Message);
                args.Cancel = true;
            }
            catch (Exception ex)
            {
                if (restoreExistingOnFailure)
                {
                    RestoreExistingMapping();
                }

                Logger.LogError("Error saving mapping: " + ex.Message);
                UnifiedMappingControl.ShowValidationError(ResourceHelper.GetString("Error_Generic_Title"), ResourceHelper.GetString("Error_Generic_Message") + ex.Message);
                args.Cancel = true;
            }
        }

        private ValidationErrorType ValidateMapping(UnifiedMappingControl.ActionType actionType, List<string> triggerKeys)
        {
            bool isAppSpecific = UnifiedMappingControl.GetIsAppSpecific();
            string appName = UnifiedMappingControl.GetAppName();
            string? editingMappingId = _isEditMode && _editingItem?.Item is IToggleableShortcut shortcut ? shortcut.Id : null;

            return actionType switch
            {
                UnifiedMappingControl.ActionType.KeyOrShortcut => ValidationHelper.ValidateKeyMapping(
                    triggerKeys, UnifiedMappingControl.GetActionKeys(), isAppSpecific, appName, _mappingService!, editingMappingId),
                UnifiedMappingControl.ActionType.Text => ValidationHelper.ValidateTextMapping(
                    triggerKeys, UnifiedMappingControl.GetTextContent(), isAppSpecific, appName, _mappingService!, editingMappingId),
                UnifiedMappingControl.ActionType.OpenUrl => ValidationHelper.ValidateUrlMapping(
                    triggerKeys, UnifiedMappingControl.GetUrl(), isAppSpecific, appName, _mappingService!, editingMappingId),
                UnifiedMappingControl.ActionType.OpenApp => ValidationHelper.ValidateAppMapping(
                    triggerKeys, UnifiedMappingControl.GetProgramPath(), isAppSpecific, appName, _mappingService!, editingMappingId),
                UnifiedMappingControl.ActionType.Disable => ValidationHelper.ValidateDisableMapping(
                    triggerKeys, isAppSpecific, appName, _mappingService!, editingMappingId),
                _ => ValidationErrorType.NoError,
            };
        }

        private bool ShouldWarnOrphanedKeys(UnifiedMappingControl.ActionType actionType, List<string> triggerKeys, out string orphanedKeyName)
        {
            orphanedKeyName = string.Empty;

            // Orphaned-key detection mirrors the classic Remap Keys editor: it applies only to
            // single-key remaps, where remapping a key away can leave that physical key unreachable.
            if (_mappingService == null || triggerKeys.Count != 1 || actionType == UnifiedMappingControl.ActionType.MouseClick)
            {
                return false;
            }

            int originalKey = _mappingService.GetKeyCodeFromName(triggerKeys[0]);
            if (originalKey == 0)
            {
                return false;
            }

            if (ValidationHelper.IsKeyOrphaned(originalKey, _mappingService))
            {
                orphanedKeyName = triggerKeys[0];
                return true;
            }

            return false;
        }

        private bool DeleteExistingMapping()
        {
            if (_editingItem == null || _mappingService == null)
            {
                return false;
            }

            try
            {
                if (_editingItem.Item is IToggleableShortcut { IsActive: false })
                {
                    return true;
                }

                switch (_editingItem.Type)
                {
                    case EditingItem.ItemType.Remapping when _editingItem.Item is Remapping remapping:
                        return RemappingHelper.DeleteRemapping(_mappingService, remapping, deleteFromSettings: false, saveSettings: false);

                    default:
                        if (_editingItem.Item is IToggleableShortcut)
                        {
                            return DeleteShortcutMapping(_editingItem.OriginalTriggerKeys, _editingItem.AppName ?? string.Empty);
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error deleting existing mapping: " + ex.Message);
            }

            return false;
        }

        private void RestoreExistingMapping()
        {
            if (!_isEditMode || _editingItem?.Item is not IToggleableShortcut { IsActive: true } shortcut || _mappingService == null)
            {
                return;
            }

            try
            {
                ShortcutKeyMapping mapping = SettingsManager.EditorSettings.ShortcutSettingsDictionary[shortcut.Id].Shortcut;
                bool restored = RemappingHelper.RestoreMapping(_mappingService, mapping);

                if (!restored || !_mappingService.SaveSettings())
                {
                    Logger.LogError("Failed to restore the original mapping after an unsuccessful edit");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error restoring the original mapping after an unsuccessful edit: " + ex.Message);
            }
        }

        private bool DeleteShortcutMapping(List<string> originalKeys, string targetApp = "")
        {
            bool deleted = originalKeys.Count == 1
                ? DeleteSingleKeyToTextMapping(originalKeys[0])
                : DeleteMultiKeyMapping(originalKeys, targetApp);

            return deleted;
        }

        private bool DeleteMultiKeyMapping(List<string> originalKeys, string targetApp = "")
        {
            string originalKeysString = string.Join(";", originalKeys.Select(k => _mappingService!.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
            return _mappingService!.DeleteShortcutMapping(originalKeysString, targetApp);
        }

        private bool SaveKeyOrShortcutMapping(List<string> triggerKeys)
        {
            List<string> actionKeys = UnifiedMappingControl.GetActionKeys();
            if (actionKeys == null || actionKeys.Count == 0)
            {
                return false;
            }

            return RemappingHelper.SaveMapping(
                _mappingService!,
                triggerKeys,
                actionKeys,
                UnifiedMappingControl.GetIsAppSpecific(),
                UnifiedMappingControl.GetAppName(),
                exactMatch: UnifiedMappingControl.GetExactMatch());
        }

        private bool SaveDisableMapping(List<string> triggerKeys)
        {
            bool isAppSpecific = UnifiedMappingControl.GetIsAppSpecific();
            string appName = UnifiedMappingControl.GetAppName();

            string originalKeysString = string.Join(
                ";",
                triggerKeys.Select(k => _mappingService!.GetKeyCodeFromName(k).ToString(System.Globalization.CultureInfo.InvariantCulture)));

            var shortcutKeyMapping = new ShortcutKeyMapping
            {
                OperationType = ShortcutOperationType.RemapShortcut,
                OriginalKeys = originalKeysString,
                TargetKeys = VkDisabledString,
                TargetApp = triggerKeys.Count > 1 && isAppSpecific ? appName : string.Empty,
                ExactMatch = UnifiedMappingControl.GetExactMatch(),
            };

            bool added;
            if (triggerKeys.Count == 1)
            {
                int originalKey = _mappingService!.GetKeyCodeFromName(triggerKeys[0]);
                if (originalKey == 0)
                {
                    return false;
                }

                shortcutKeyMapping.OriginalKeys = originalKey.ToString(System.Globalization.CultureInfo.InvariantCulture);
                added = _mappingService.AddSingleKeyMapping(originalKey, VkDisabled);
            }
            else
            {
                added = _mappingService!.AddShortcutMapping(
                    originalKeysString,
                    VkDisabledString,
                    isAppSpecific ? appName : string.Empty,
                    exactMatch: shortcutKeyMapping.ExactMatch);
            }

            return RemappingHelper.CompleteSave(_mappingService, shortcutKeyMapping, added);
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

            return triggerKeys.Count == 1
                ? SaveSingleKeyToTextMapping(triggerKeys[0], textContent)
                : SaveShortcutToTextMapping(triggerKeys, textContent, isAppSpecific, appName);
        }

        private bool SaveSingleKeyToTextMapping(string keyName, string textContent)
        {
            int originalKey = _mappingService!.GetKeyCodeFromName(keyName);
            if (originalKey == 0)
            {
                return false;
            }

            var shortcutKeyMapping = new ShortcutKeyMapping
            {
                OperationType = ShortcutOperationType.RemapText,
                OriginalKeys = originalKey.ToString(CultureInfo.InvariantCulture),
                TargetKeys = textContent,
                TargetText = textContent,
            };

            bool saved = _mappingService.AddSingleKeyToTextMapping(originalKey, textContent);
            return RemappingHelper.CompleteSave(_mappingService, shortcutKeyMapping, saved);
        }

        private bool SaveShortcutToTextMapping(List<string> triggerKeys, string textContent, bool isAppSpecific, string appName)
        {
            string originalKeysString = string.Join(";", triggerKeys.Select(k => _mappingService!.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
            bool exactMatch = UnifiedMappingControl.GetExactMatch();

            var shortcutKeyMapping = new ShortcutKeyMapping
            {
                OperationType = ShortcutOperationType.RemapText,
                OriginalKeys = originalKeysString,
                TargetKeys = textContent,
                TargetText = textContent,
                TargetApp = isAppSpecific ? appName : string.Empty,
                ExactMatch = exactMatch,
            };

            bool saved = isAppSpecific && !string.IsNullOrEmpty(appName)
                ? _mappingService!.AddShortcutMapping(originalKeysString, textContent, appName, ShortcutOperationType.RemapText, exactMatch)
                : _mappingService!.AddShortcutMapping(originalKeysString, textContent, operationType: ShortcutOperationType.RemapText, exactMatch: exactMatch);

            return RemappingHelper.CompleteSave(_mappingService, shortcutKeyMapping, saved);
        }

        private bool SaveUrlMapping(List<string> triggerKeys)
        {
            string url = UnifiedMappingControl.GetUrl();
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            string originalKeysString = string.Join(";", triggerKeys.Select(k => _mappingService!.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));

            var shortcutKeyMapping = new ShortcutKeyMapping
            {
                OperationType = ShortcutOperationType.OpenUri,
                OriginalKeys = originalKeysString,
                TargetKeys = originalKeysString,
                UriToOpen = url,
                TargetApp = UnifiedMappingControl.GetIsAppSpecific() ? UnifiedMappingControl.GetAppName() : string.Empty,
                ExactMatch = UnifiedMappingControl.GetExactMatch(),
            };

            bool saved = _mappingService!.AddShortcutMapping(shortcutKeyMapping);
            return RemappingHelper.CompleteSave(_mappingService, shortcutKeyMapping, saved);
        }

        private bool SaveProgramMapping(List<string> triggerKeys)
        {
            string programPath = UnifiedMappingControl.GetProgramPath();
            if (string.IsNullOrEmpty(programPath))
            {
                return false;
            }

            string originalKeysString = string.Join(";", triggerKeys.Select(k => _mappingService!.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));

            var shortcutKeyMapping = new ShortcutKeyMapping
            {
                OperationType = ShortcutOperationType.RunProgram,
                OriginalKeys = originalKeysString,
                TargetKeys = originalKeysString,
                ProgramPath = programPath,
                ProgramArgs = UnifiedMappingControl.GetProgramArgs(),
                StartInDirectory = UnifiedMappingControl.GetStartInDirectory(),
                IfRunningAction = UnifiedMappingControl.GetIfRunningAction(),
                Visibility = UnifiedMappingControl.GetVisibility(),
                Elevation = UnifiedMappingControl.GetElevationLevel(),
                TargetApp = UnifiedMappingControl.GetIsAppSpecific() ? UnifiedMappingControl.GetAppName() : string.Empty,
                ExactMatch = UnifiedMappingControl.GetExactMatch(),
            };

            bool saved = _mappingService!.AddShortcutMapping(shortcutKeyMapping);
            return RemappingHelper.CompleteSave(_mappingService, shortcutKeyMapping, saved);
        }

        #endregion

        #region Delete Handlers

        private async void DeleteMapping_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem menuFlyoutItem || _mappingService == null)
            {
                return;
            }

            if (await DeleteConfirmationDialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                bool deleted = false;
                switch (menuFlyoutItem.Tag)
                {
                    case Remapping remapping:
                        deleted = HandleRemappingDelete(remapping);
                        UpdateHasAnyMappings();
                        break;

                    case IToggleableShortcut shortcut:
                        deleted = HandleShortcutDelete(shortcut);
                        LoadAllMappings();
                        break;
                }

                // The row that had focus is gone and the flyout closed with it, so without this a
                // screen-reader user gets no confirmation that anything happened.
                if (deleted)
                {
                    AnnounceToScreenReader(ResourceHelper.GetString("Announcement_MappingDeleted"));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error deleting mapping: " + ex.Message);
            }
        }

        /// <summary>
        /// Raises a live-region notification on the page so assistive technology reports an action
        /// whose only visible effect is that something disappeared.
        /// </summary>
        private void AnnounceToScreenReader(string message)
        {
            try
            {
                var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(this);
                peer ??= Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(this);

                peer?.RaiseNotificationEvent(
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationKind.ItemRemoved,
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationProcessing.MostRecent,
                    message,
                    "KeyboardManagerEditorMappingChanged");
            }
            catch (Exception ex)
            {
                // Announcements are best-effort; never let one break the delete.
                Logger.LogWarning("Could not raise an accessibility notification: " + ex.Message);
            }
        }

        private bool HandleRemappingDelete(Remapping remapping)
        {
            if (!remapping.IsActive)
            {
                SettingsManager.RemoveShortcutKeyMappingFromSettings(remapping.Id);
                LoadRemappings();
                return true;
            }
            else if (RemappingHelper.DeleteRemapping(_mappingService!, remapping))
            {
                LoadRemappings();
                return true;
            }
            else
            {
                Logger.LogWarning($"Failed to delete remapping: {string.Join("+", remapping.Shortcut)}");
                return false;
            }
        }

        private bool HandleShortcutDelete(IToggleableShortcut shortcut)
        {
            if (!shortcut.IsActive)
            {
                SettingsManager.RemoveShortcutKeyMappingFromSettings(shortcut.Id);
                return true;
            }

            bool deleted = shortcut.Shortcut.Count == 1
                ? DeleteSingleKeyToTextMapping(shortcut.Shortcut[0]) // Remapping has its own handler, single key will always be text mapping
                : DeleteMultiKeyShortcut(shortcut);

            if (RemappingHelper.CompleteDelete(_mappingService!, shortcut.Id, deleted))
            {
                SettingsManager.RemoveShortcutKeyMappingFromSettings(shortcut.Id);
                return true;
            }

            return false;
        }

        private bool DeleteMultiKeyShortcut(IToggleableShortcut shortcut)
        {
            string originalKeys = string.Join(";", shortcut.Shortcut.Select(k => _mappingService!.GetKeyCodeFromName(k)));
            return _mappingService!.DeleteShortcutMapping(originalKeys, shortcut.AppName);
        }

        #endregion

        #region Toggle Switch Handlers

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleSwitch toggleSwitch || toggleSwitch.DataContext is not IToggleableShortcut shortcut || _mappingService == null || toggleSwitch.IsOn == shortcut.IsActive)
            {
                return;
            }

            try
            {
                if (toggleSwitch.IsOn)
                {
                    EnableShortcut(shortcut);
                }
                else
                {
                    DisableShortcut(shortcut);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error toggling shortcut active state: " + ex.Message);
            }
            finally
            {
                // Failed native updates must leave both the row and its switch in their old state.
                toggleSwitch.IsOn = shortcut.IsActive;
            }
        }

        private void EnableShortcut(IToggleableShortcut shortcut)
        {
            if (shortcut is Remapping remapping)
            {
                bool remappingSaved;
                if (remapping.RemappedKeys == null || remapping.RemappedKeys.Count == 0)
                {
                    // Disabled mapping — re-enable by adding back the VK_DISABLED target
                    remappingSaved = EnableDisabledMapping(remapping);
                }
                else
                {
                    remappingSaved = RemappingHelper.SaveMapping(_mappingService!, remapping.Shortcut, remapping.RemappedKeys, !remapping.IsAllApps, remapping.AppName, exactMatch: remapping.ExactMatch, saveToSettings: false);
                }

                if (remappingSaved)
                {
                    shortcut.IsActive = true;
                    SettingsManager.ToggleShortcutKeyMappingActiveState(shortcut.Id);
                }

                return;
            }

            ShortcutKeyMapping shortcutKeyMapping = SettingsManager.EditorSettings.ShortcutSettingsDictionary[shortcut.Id].Shortcut;
            bool saved = shortcut.Shortcut.Count == 1
                ? _mappingService!.AddSingleKeyToTextMapping(_mappingService.GetKeyCodeFromName(shortcut.Shortcut[0]), shortcutKeyMapping.TargetText)
                : shortcutKeyMapping.OperationType == ShortcutOperationType.RemapText
                    ? _mappingService!.AddShortcutMapping(shortcutKeyMapping.OriginalKeys, shortcutKeyMapping.TargetText, shortcutKeyMapping.TargetApp, operationType: ShortcutOperationType.RemapText, exactMatch: shortcutKeyMapping.ExactMatch)
                    : _mappingService!.AddShortcutMapping(shortcutKeyMapping);

            if (RemappingHelper.CompleteSave(_mappingService!, shortcutKeyMapping, saved, saveToSettings: false))
            {
                shortcut.IsActive = true;
                SettingsManager.ToggleShortcutKeyMappingActiveState(shortcut.Id);
            }
        }

        private void DisableShortcut(IToggleableShortcut shortcut)
        {
            if (shortcut is Remapping remapping)
            {
                if (RemappingHelper.DeleteRemapping(_mappingService!, remapping, deleteFromSettings: false))
                {
                    shortcut.IsActive = false;
                    SettingsManager.ToggleShortcutKeyMappingActiveState(shortcut.Id);
                }

                return;
            }

            bool deleted = shortcut.Shortcut.Count == 1
                ? DeleteSingleKeyToTextMapping(shortcut.Shortcut[0])
                : DeleteMultiKeyMapping(shortcut.Shortcut, shortcut.AppName);

            if (RemappingHelper.CompleteDelete(_mappingService!, shortcut.Id, deleted))
            {
                shortcut.IsActive = false;
                SettingsManager.ToggleShortcutKeyMappingActiveState(shortcut.Id);
            }
        }

        private bool EnableDisabledMapping(Remapping remapping)
        {
            string originalKeysString = string.Join(
                ";",
                remapping.Shortcut.Select(k => _mappingService!.GetKeyCodeFromName(k).ToString(System.Globalization.CultureInfo.InvariantCulture)));

            bool added;
            if (remapping.Shortcut.Count == 1)
            {
                int originalKey = _mappingService!.GetKeyCodeFromName(remapping.Shortcut[0]);
                added = originalKey != 0 && _mappingService.AddSingleKeyMapping(originalKey, VkDisabled);
            }
            else
            {
                added = _mappingService!.AddShortcutMapping(
                    originalKeysString,
                    VkDisabledString,
                    !remapping.IsAllApps ? remapping.AppName : string.Empty,
                    exactMatch: remapping.ExactMatch);
            }

            ShortcutKeyMapping mapping = SettingsManager.EditorSettings.ShortcutSettingsDictionary[remapping.Id].Shortcut;
            return RemappingHelper.CompleteSave(_mappingService!, mapping, added, saveToSettings: false);
        }

        private bool DeleteSingleKeyToTextMapping(string keyName)
        {
            int originalKey = _mappingService!.GetKeyCodeFromName(keyName);
            return originalKey != 0 && _mappingService.DeleteSingleKeyToTextMapping(originalKey);
        }

        #endregion

        #region Load Methods

        private void LoadAllMappings()
        {
            LoadRemappings();
            LoadTextMappings();
            LoadProgramShortcuts();
            LoadUrlShortcuts();
            UpdateHasAnyMappings();
        }

        private void UpdateHasAnyMappings()
        {
            bool hasAny = RemappingList.Count > 0 || DisabledList.Count > 0 || TextMappings.Count > 0 || ProgramShortcuts.Count > 0 || UrlShortcuts.Count > 0;
            MappingState = hasAny ? "HasMappings" : "Empty";
        }

        private void LoadRemappings()
        {
            SettingsManager.EditorSettings.ShortcutsByOperationType.TryGetValue(ShortcutOperationType.RemapShortcut, out var remapShortcutIds);

            if (remapShortcutIds == null)
            {
                return;
            }

            RemappingList.Clear();
            DisabledList.Clear();

            foreach (var id in remapShortcutIds)
            {
                ShortcutSettings shortcutSettings = SettingsManager.EditorSettings.ShortcutSettingsDictionary[id];
                ShortcutKeyMapping mapping = shortcutSettings.Shortcut;
                var originalKeyNames = ParseKeyCodes(mapping.OriginalKeys);

                bool isDisabled = mapping.TargetKeys == VkDisabledString;

                var remapping = new Remapping
                {
                    Shortcut = originalKeyNames,
                    RemappedKeys = isDisabled ? new List<string>() : ParseKeyCodes(mapping.TargetKeys),
                    IsAllApps = string.IsNullOrEmpty(mapping.TargetApp),
                    AppName = mapping.TargetApp ?? string.Empty,
                    Id = shortcutSettings.Id,
                    IsActive = shortcutSettings.IsActive,
                    ExactMatch = mapping.ExactMatch,
                };

                if (isDisabled)
                {
                    DisabledList.Add(remapping);
                }
                else
                {
                    RemappingList.Add(remapping);
                }
            }
        }

        private void LoadTextMappings()
        {
            SettingsManager.EditorSettings.ShortcutsByOperationType.TryGetValue(ShortcutOperationType.RemapText, out var remapShortcutIds);

            if (remapShortcutIds == null)
            {
                return;
            }

            TextMappings.Clear();

            foreach (var id in remapShortcutIds)
            {
                ShortcutSettings shortcutSettings = SettingsManager.EditorSettings.ShortcutSettingsDictionary[id];
                ShortcutKeyMapping mapping = shortcutSettings.Shortcut;
                var originalKeyNames = ParseKeyCodes(mapping.OriginalKeys);

                TextMappings.Add(new TextMapping
                {
                    Shortcut = originalKeyNames,
                    Text = mapping.TargetText,
                    IsAllApps = string.IsNullOrEmpty(mapping.TargetApp),
                    AppName = mapping.TargetApp ?? string.Empty,
                    Id = shortcutSettings.Id,
                    IsActive = shortcutSettings.IsActive,
                    ExactMatch = mapping.ExactMatch,
                });
            }
        }

        private void LoadProgramShortcuts()
        {
            SettingsManager.EditorSettings.ShortcutsByOperationType.TryGetValue(ShortcutOperationType.RunProgram, out var remapShortcutIds);

            if (remapShortcutIds == null)
            {
                return;
            }

            ProgramShortcuts.Clear();

            foreach (var id in remapShortcutIds)
            {
                ShortcutSettings shortcutSettings = SettingsManager.EditorSettings.ShortcutSettingsDictionary[id];
                ShortcutKeyMapping mapping = shortcutSettings.Shortcut;
                var originalKeyNames = ParseKeyCodes(mapping.OriginalKeys);

                ProgramShortcuts.Add(new ProgramShortcut
                {
                    Shortcut = originalKeyNames,
                    AppToRun = mapping.ProgramPath,
                    Args = mapping.ProgramArgs,
                    IsActive = shortcutSettings.IsActive,
                    Id = shortcutSettings.Id,
                    IsAllApps = string.IsNullOrEmpty(mapping.TargetApp),
                    AppName = mapping.TargetApp ?? string.Empty,
                    StartInDirectory = mapping.StartInDirectory,
                    Elevation = mapping.Elevation.ToString(),
                    IfRunningAction = mapping.IfRunningAction.ToString(),
                    Visibility = mapping.Visibility.ToString(),
                    ExactMatch = mapping.ExactMatch,
                });
            }
        }

        private void LoadUrlShortcuts()
        {
            SettingsManager.EditorSettings.ShortcutsByOperationType.TryGetValue(ShortcutOperationType.OpenUri, out var remapShortcutIds);

            if (remapShortcutIds == null)
            {
                return;
            }

            UrlShortcuts.Clear();

            foreach (var id in remapShortcutIds)
            {
                ShortcutSettings shortcutSettings = SettingsManager.EditorSettings.ShortcutSettingsDictionary[id];
                ShortcutKeyMapping mapping = shortcutSettings.Shortcut;
                var originalKeyNames = ParseKeyCodes(mapping.OriginalKeys);

                UrlShortcuts.Add(new URLShortcut
                {
                    Shortcut = originalKeyNames,
                    URL = mapping.UriToOpen,
                    Id = shortcutSettings.Id,
                    IsActive = shortcutSettings.IsActive,
                    IsAllApps = string.IsNullOrEmpty(mapping.TargetApp),
                    AppName = mapping.TargetApp ?? string.Empty,
                    ExactMatch = mapping.ExactMatch,
                });
            }
        }

        private List<string> ParseKeyCodes(string keyCodesString)
        {
            return keyCodesString.Split(';')
                .Where(keyCode => int.TryParse(keyCode, out _))
                .Select(keyCode =>
                {
                    int code = int.Parse(keyCode, CultureInfo.InvariantCulture);
                    return _mappingService?.GetKeyDisplayName(code) ?? $"VK {code}";
                })
                .ToList();
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
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _serviceCheckTimer?.Stop();
                _serviceCheckTimer = null;
                _mappingService?.Dispose();
                _mappingService = null;
            }

            _disposed = true;
        }

        #endregion
    }
}
#pragma warning restore SA1124 // Do not use regions
