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
            UnifiedMappingControl.SetAppSpecific(!urlShortcut.IsAllApps, urlShortcut.AppName);
            RemappingDialog.Title = ResourceHelper.GetString("RemappingDialog_TitleEdit");
            await ShowRemappingDialog();
        }

        private async System.Threading.Tasks.Task ShowRemappingDialog()
        {
            RemappingDialog.PrimaryButtonClick += RemappingDialog_PrimaryButtonClick;
            UnifiedMappingControl.ValidationStateChanged += UnifiedMappingControl_ValidationStateChanged;
            RemappingDialog.IsPrimaryButtonEnabled = UnifiedMappingControl.IsInputComplete();

            await RemappingDialog.ShowAsync();

            RemappingDialog.PrimaryButtonClick -= RemappingDialog_PrimaryButtonClick;
            UnifiedMappingControl.ValidationStateChanged -= UnifiedMappingControl_ValidationStateChanged;
            _isEditMode = false;
            _editingItem = null;
            KeyboardHookHelper.Instance.CleanupHook();
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

                if (_isEditMode && _editingItem != null)
                {
                    DeleteExistingMapping();
                }

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
                    LoadAllMappings();
                }
                else
                {
                    UnifiedMappingControl.ShowValidationError(ResourceHelper.GetString("Error_SaveFailed_Title"), ResourceHelper.GetString("Error_SaveFailed_Message"));
                    args.Cancel = true;
                }
            }
            catch (NotImplementedException ex)
            {
                UnifiedMappingControl.ShowValidationError(ResourceHelper.GetString("Error_NotImplemented_Title"), ex.Message);
                args.Cancel = true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error saving mapping: " + ex.Message);
                UnifiedMappingControl.ShowValidationError(ResourceHelper.GetString("Error_Generic_Title"), ResourceHelper.GetString("Error_Generic_Message") + ex.Message);
                args.Cancel = true;
            }
        }

        private ValidationErrorType ValidateMapping(UnifiedMappingControl.ActionType actionType, List<string> triggerKeys)
        {
            bool isAppSpecific = UnifiedMappingControl.GetIsAppSpecific();
            string appName = UnifiedMappingControl.GetAppName();
            Remapping? editingRemapping = _isEditMode && _editingItem?.Item is Remapping r ? r : null;

            return actionType switch
            {
                UnifiedMappingControl.ActionType.KeyOrShortcut => ValidationHelper.ValidateKeyMapping(
                    triggerKeys, UnifiedMappingControl.GetActionKeys(), isAppSpecific, appName, _mappingService!, _isEditMode, editingRemapping),
                UnifiedMappingControl.ActionType.Text => ValidationHelper.ValidateTextMapping(
                    triggerKeys, UnifiedMappingControl.GetTextContent(), isAppSpecific, appName, _mappingService!, _isEditMode),
                UnifiedMappingControl.ActionType.OpenUrl => ValidationHelper.ValidateUrlMapping(
                    triggerKeys, UnifiedMappingControl.GetUrl(), isAppSpecific, appName, _mappingService!, _isEditMode),
                UnifiedMappingControl.ActionType.OpenApp => ValidationHelper.ValidateAppMapping(
                    triggerKeys, UnifiedMappingControl.GetProgramPath(), isAppSpecific, appName, _mappingService!, _isEditMode),
                UnifiedMappingControl.ActionType.Disable => ValidationHelper.ValidateDisableMapping(
                    triggerKeys, isAppSpecific, appName, _mappingService!, _isEditMode, editingRemapping),
                _ => ValidationErrorType.NoError,
            };
        }

        private void DeleteExistingMapping()
        {
            if (_editingItem == null || _mappingService == null)
            {
                return;
            }

            try
            {
                switch (_editingItem.Type)
                {
                    case EditingItem.ItemType.Remapping when _editingItem.Item is Remapping remapping:
                        RemappingHelper.DeleteRemapping(_mappingService, remapping);
                        break;

                    default:
                        if (_editingItem.Item is IToggleableShortcut shortcut)
                        {
                            DeleteShortcutMapping(_editingItem.OriginalTriggerKeys, _editingItem.AppName ?? string.Empty);
                            if (!string.IsNullOrEmpty(shortcut.Id))
                            {
                                SettingsManager.RemoveShortcutKeyMappingFromSettings(shortcut.Id);
                            }
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error deleting existing mapping: " + ex.Message);
            }
        }

        private void DeleteShortcutMapping(List<string> originalKeys, string targetApp = "")
        {
            bool deleted = originalKeys.Count == 1
                ? DeleteSingleKeyToTextMapping(originalKeys[0])
                : DeleteMultiKeyMapping(originalKeys, targetApp);

            if (deleted)
            {
                _mappingService!.SaveSettings();
            }
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
                UnifiedMappingControl.GetAppName());
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
                TargetApp = isAppSpecific ? appName : string.Empty,
            };

            if (triggerKeys.Count == 1)
            {
                int originalKey = _mappingService!.GetKeyCodeFromName(triggerKeys[0]);
                if (originalKey == 0)
                {
                    return false;
                }

                shortcutKeyMapping.OriginalKeys = originalKey.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _mappingService.AddSingleKeyMapping(originalKey, VkDisabled);
            }
            else
            {
                _mappingService!.AddShortcutMapping(
                    originalKeysString,
                    VkDisabledString,
                    isAppSpecific ? appName : string.Empty);
            }

            SettingsManager.AddShortcutKeyMappingToSettings(shortcutKeyMapping);
            return _mappingService.SaveSettings();
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
                ? SaveSingleKeyToTextMapping(triggerKeys[0], textContent, isAppSpecific, appName)
                : SaveShortcutToTextMapping(triggerKeys, textContent, isAppSpecific, appName);
        }

        private bool SaveSingleKeyToTextMapping(string keyName, string textContent, bool isAppSpecific, string appName)
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
                TargetApp = isAppSpecific ? appName : string.Empty,
            };

            bool saved = _mappingService.AddSingleKeyToTextMapping(originalKey, textContent);
            if (saved)
            {
                _mappingService.SaveSettings();
                SettingsManager.AddShortcutKeyMappingToSettings(shortcutKeyMapping);
            }

            return saved;
        }

        private bool SaveShortcutToTextMapping(List<string> triggerKeys, string textContent, bool isAppSpecific, string appName)
        {
            string originalKeysString = string.Join(";", triggerKeys.Select(k => _mappingService!.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));

            var shortcutKeyMapping = new ShortcutKeyMapping
            {
                OperationType = ShortcutOperationType.RemapText,
                OriginalKeys = originalKeysString,
                TargetKeys = textContent,
                TargetText = textContent,
                TargetApp = isAppSpecific ? appName : string.Empty,
            };

            bool saved = isAppSpecific && !string.IsNullOrEmpty(appName)
                ? _mappingService!.AddShortcutMapping(originalKeysString, textContent, appName, ShortcutOperationType.RemapText)
                : _mappingService!.AddShortcutMapping(originalKeysString, textContent, operationType: ShortcutOperationType.RemapText);

            if (saved)
            {
                _mappingService.SaveSettings();
                SettingsManager.AddShortcutKeyMappingToSettings(shortcutKeyMapping);
            }

            return saved;
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
            };

            bool saved = _mappingService!.AddShortcutMapping(shortcutKeyMapping);
            if (saved)
            {
                _mappingService.SaveSettings();
                SettingsManager.AddShortcutKeyMappingToSettings(shortcutKeyMapping);
            }

            return saved;
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
            };

            bool saved = _mappingService!.AddShortcutMapping(shortcutKeyMapping);
            if (saved)
            {
                _mappingService.SaveSettings();
                SettingsManager.AddShortcutKeyMappingToSettings(shortcutKeyMapping);
            }

            return saved;
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
                switch (menuFlyoutItem.Tag)
                {
                    case Remapping remapping:
                        HandleRemappingDelete(remapping);
                        UpdateHasAnyMappings();
                        break;

                    case IToggleableShortcut shortcut:
                        HandleShortcutDelete(shortcut);
                        LoadAllMappings();
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error deleting mapping: " + ex.Message);
            }
        }

        private void HandleRemappingDelete(Remapping remapping)
        {
            if (!remapping.IsActive)
            {
                SettingsManager.RemoveShortcutKeyMappingFromSettings(remapping.Id);
                LoadRemappings();
            }
            else if (RemappingHelper.DeleteRemapping(_mappingService!, remapping))
            {
                LoadRemappings();
            }
            else
            {
                Logger.LogWarning($"Failed to delete remapping: {string.Join("+", remapping.Shortcut)}");
            }
        }

        private void HandleShortcutDelete(IToggleableShortcut shortcut)
        {
            bool deleted = shortcut.Shortcut.Count == 1
                ? DeleteSingleKeyToTextMapping(shortcut.Shortcut[0]) // Remapping has its own handler, single key will always be text mapping
                : DeleteMultiKeyShortcut(shortcut);

            if (deleted)
            {
                _mappingService!.SaveSettings();
            }

            SettingsManager.RemoveShortcutKeyMappingFromSettings(shortcut.Id);
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
            if (sender is not ToggleSwitch toggleSwitch || toggleSwitch.DataContext is not IToggleableShortcut shortcut || _mappingService == null)
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
        }

        private void EnableShortcut(IToggleableShortcut shortcut)
        {
            if (shortcut is Remapping remapping)
            {
                if (remapping.RemappedKeys == null || remapping.RemappedKeys.Count == 0)
                {
                    // Disabled mapping — re-enable by adding back the VK_DISABLED target
                    EnableDisabledMapping(remapping);
                }
                else
                {
                    RemappingHelper.SaveMapping(_mappingService!, remapping.Shortcut, remapping.RemappedKeys, !remapping.IsAllApps, remapping.AppName, false);
                }

                shortcut.IsActive = true;
                SettingsManager.ToggleShortcutKeyMappingActiveState(shortcut.Id);
                return;
            }

            ShortcutKeyMapping shortcutKeyMapping = SettingsManager.EditorSettings.ShortcutSettingsDictionary[shortcut.Id].Shortcut;
            bool saved = shortcut.Shortcut.Count == 1
                ? _mappingService!.AddSingleKeyToTextMapping(_mappingService.GetKeyCodeFromName(shortcut.Shortcut[0]), shortcutKeyMapping.TargetText)
                : shortcutKeyMapping.OperationType == ShortcutOperationType.RemapText
                    ? _mappingService!.AddShortcutMapping(shortcutKeyMapping.OriginalKeys, shortcutKeyMapping.TargetText, operationType: ShortcutOperationType.RemapText)
                    : _mappingService!.AddShortcutMapping(shortcutKeyMapping);

            if (saved)
            {
                shortcut.IsActive = true;
                SettingsManager.ToggleShortcutKeyMappingActiveState(shortcut.Id);
                _mappingService.SaveSettings();
            }
        }

        private void DisableShortcut(IToggleableShortcut shortcut)
        {
            if (shortcut is Remapping remapping)
            {
                shortcut.IsActive = false;
                RemappingHelper.DeleteRemapping(_mappingService!, remapping, false);
                SettingsManager.ToggleShortcutKeyMappingActiveState(shortcut.Id);
                return;
            }

            bool deleted = shortcut.Shortcut.Count == 1
                ? DeleteSingleKeyToTextMapping(shortcut.Shortcut[0])
                : DeleteMultiKeyMapping(shortcut.Shortcut, shortcut.AppName);

            if (deleted)
            {
                shortcut.IsActive = false;
                SettingsManager.ToggleShortcutKeyMappingActiveState(shortcut.Id);
                _mappingService!.SaveSettings();
            }
        }

        private void EnableDisabledMapping(Remapping remapping)
        {
            string originalKeysString = string.Join(
                ";",
                remapping.Shortcut.Select(k => _mappingService!.GetKeyCodeFromName(k).ToString(System.Globalization.CultureInfo.InvariantCulture)));

            if (remapping.Shortcut.Count == 1)
            {
                int originalKey = _mappingService!.GetKeyCodeFromName(remapping.Shortcut[0]);
                if (originalKey != 0)
                {
                    _mappingService.AddSingleKeyMapping(originalKey, VkDisabled);
                }
            }
            else
            {
                _mappingService!.AddShortcutMapping(
                    originalKeysString,
                    VkDisabledString,
                    !remapping.IsAllApps ? remapping.AppName : string.Empty);
            }

            _mappingService!.SaveSettings();
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
