// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
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
        private bool _isUpdatingTextExpansionToggle;
        private bool _mappingServiceFailed;
        private EditingItem? _editingItem;
        private string _mappingState = "Empty";
        private bool _isServiceRunning = true;
        private bool _isUpdatingToggle;

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

        public ObservableCollection<TextExpansionMapping> TextExpansions { get; } = new();

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
                TextExpansion,
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
                _mappingServiceFailed = true;
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
            IsServiceRunning = !_mappingServiceFailed && ServiceStatusHelper.IsKeyboardManagerServiceRunning();
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
            UnifiedMappingControl.SetCondition(remapping.Condition);
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
            UnifiedMappingControl.SetCondition(disabledMapping.Condition);
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

        private async void TextExpansionsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not TextExpansionMapping textExpansion)
            {
                return;
            }

            _isEditMode = true;
            _editingItem = new EditingItem
            {
                Type = EditingItem.ItemType.TextExpansion,
                Item = textExpansion,
                OriginalTriggerKeys = textExpansion.ActivationKeyNames.ToList(),
            };

            UnifiedMappingControl.Reset();
            UnifiedMappingControl.SetTriggerType(UnifiedMappingControl.TriggerType.TextExpansion);
            UnifiedMappingControl.SetTextExpansionSourceText(textExpansion.SourceText);
            UnifiedMappingControl.SetTriggerKeys(textExpansion.ActivationKeyNames.ToList());
            UnifiedMappingControl.SetTextExpansionReplacementText(textExpansion.ReplacementText);
            RemappingDialog.Title = ResourceHelper.GetString("RemappingDialog_TitleEditTextExpansion");
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
            UnifiedMappingControl.SetTriggerTypeSelectionEnabled(!_isEditMode);
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

                bool saved = SaveMappingTransaction(triggerKeys);

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
                UnifiedMappingControl.ActionType.TextExpansion => ValidateTextExpansion(triggerKeys),
                UnifiedMappingControl.ActionType.OpenUrl => ValidationHelper.ValidateUrlMapping(
                    triggerKeys, UnifiedMappingControl.GetUrl(), isAppSpecific, appName, _mappingService!, _isEditMode),
                UnifiedMappingControl.ActionType.OpenApp => ValidationHelper.ValidateAppMapping(
                    triggerKeys, UnifiedMappingControl.GetProgramPath(), isAppSpecific, appName, _mappingService!, _isEditMode),
                UnifiedMappingControl.ActionType.Disable => ValidationHelper.ValidateDisableMapping(
                    triggerKeys, isAppSpecific, appName, _mappingService!, _isEditMode, editingRemapping),
                _ => ValidationErrorType.NoError,
            };
        }

        private ValidationErrorType ValidateTextExpansion(List<string> activationKeyNames)
        {
            if (!TextExpansionValidation.IsValidSourceText(UnifiedMappingControl.GetTextExpansionSourceText()))
            {
                return ValidationErrorType.InvalidTextExpansionSourceText;
            }

            List<int> activationKeys = ParseKeyNames(activationKeyNames);
            if (!TextExpansionValidation.IsValidActivationKeys(activationKeys))
            {
                return ValidationErrorType.ModifierOnly;
            }

            if (activationKeys.Count > 1 && KeyboardManagerInterop.IsShortcutIllegal(FormatKeyCodes(activationKeys)))
            {
                return ValidationErrorType.IllegalShortcut;
            }

            if (!TextExpansionValidation.IsValidReplacementText(UnifiedMappingControl.GetTextExpansionReplacementText()))
            {
                return ValidationErrorType.InvalidTextExpansionReplacementText;
            }

            return ValidationErrorType.NoError;
        }

        private bool SaveMappingTransaction(List<string> triggerKeys)
        {
            if (_mappingService == null)
            {
                return false;
            }

            if (UnifiedMappingControl.CurrentActionType == UnifiedMappingControl.ActionType.TextExpansion)
            {
                return SaveTextExpansionMapping(triggerKeys);
            }

            using FileStream? transactionLock = SettingsManager.TryAcquireMappingTransactionLock();
            if (transactionLock == null || !SettingsManager.TryReloadSettings())
            {
                return false;
            }

            KeyboardMappingService? originalService = null;
            KeyboardMappingService? candidateService = null;
            bool candidateSaveAttempted = false;
            try
            {
                originalService = new KeyboardMappingService();
                candidateService = new KeyboardMappingService();
                if (!_mappingService.HasSameMappings(originalService) ||
                    !originalService.HasSameMappings(candidateService))
                {
                    return false;
                }

                string? replacingId = null;
                bool exactMatch = false;

                if (_isEditMode)
                {
                    if (_editingItem?.Item is not IToggleableShortcut existingMapping ||
                        string.IsNullOrEmpty(existingMapping.Id) ||
                        !SettingsManager.EditorSettings.ShortcutSettingsDictionary.TryGetValue(existingMapping.Id, out ShortcutSettings? existingSettings))
                    {
                        return false;
                    }

                    replacingId = existingMapping.Id;
                    exactMatch = existingSettings.Shortcut.ExactMatch;
                    if (existingSettings.IsActive && !DeleteMapping(candidateService, existingSettings.Shortcut))
                    {
                        return false;
                    }
                }

                ShortcutKeyMapping? replacementMapping = CreateShortcutKeyMapping(candidateService, triggerKeys, exactMatch);
                if (replacementMapping == null ||
                    HasDuplicateEditorMapping(replacementMapping, replacingId) ||
                    !AddMapping(candidateService, replacementMapping))
                {
                    return false;
                }

                candidateSaveAttempted = true;
                if (!candidateService.SaveSettingsAndVerify())
                {
                    RestoreOriginalMappingSettings(originalService);
                    return false;
                }

                if (!SettingsManager.TryCommitShortcutKeyMapping(replacementMapping, replacingId))
                {
                    RestoreOriginalMappingSettings(originalService);
                    return false;
                }

                KeyboardMappingService previousService = _mappingService;
                _mappingService = candidateService;
                candidateService = null;
                previousService.Dispose();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error saving mapping transaction: " + ex.Message);
                if (candidateSaveAttempted && originalService != null)
                {
                    RestoreOriginalMappingSettings(originalService);
                }

                return false;
            }
            finally
            {
                candidateService?.Dispose();
                originalService?.Dispose();
            }
        }

        private bool SaveTextExpansionMapping(List<string> activationKeyNames)
        {
            List<int> activationKeys = ParseKeyNames(activationKeyNames);
            bool updateExisting = _isEditMode && _editingItem?.Item is TextExpansionMapping;
            TextExpansionMapping mapping;

            if (updateExisting)
            {
                var existing = (TextExpansionMapping)_editingItem!.Item;
                mapping = new TextExpansionMapping
                {
                    Id = existing.Id,
                    SourceText = UnifiedMappingControl.GetTextExpansionSourceText(),
                    ActivationKeys = activationKeys,
                    ActivationKeyNames = activationKeyNames.ToList(),
                    ReplacementText = UnifiedMappingControl.GetTextExpansionReplacementText(),
                    IsEnabled = existing.IsEnabled,
                };
            }
            else
            {
                mapping = new TextExpansionMapping
                {
                    Id = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture),
                    SourceText = UnifiedMappingControl.GetTextExpansionSourceText(),
                    ActivationKeys = activationKeys,
                    ActivationKeyNames = activationKeyNames.ToList(),
                    ReplacementText = UnifiedMappingControl.GetTextExpansionReplacementText(),
                    IsEnabled = true,
                };
            }

            return ExecuteTextExpansionTransaction(candidate =>
                updateExisting
                    ? candidate.UpdateTextExpansionMapping(mapping)
                    : candidate.AddTextExpansionMapping(mapping));
        }

        private bool ExecuteTextExpansionTransaction(Func<KeyboardMappingService, bool> updateCandidate)
        {
            using FileStream? transactionLock = SettingsManager.TryAcquireMappingTransactionLock();
            return transactionLock != null && ExecuteMappingTransaction(updateCandidate, static () => true);
        }

        private ShortcutKeyMapping? CreateShortcutKeyMapping(KeyboardMappingService mappingService, List<string> triggerKeys, bool exactMatch)
        {
            string? originalKeys = GetKeyCodes(mappingService, triggerKeys);
            if (string.IsNullOrEmpty(originalKeys))
            {
                return null;
            }

            var mapping = new ShortcutKeyMapping
            {
                OriginalKeys = originalKeys,
                ExactMatch = exactMatch,
                TargetApp = UnifiedMappingControl.GetIsAppSpecific() ? UnifiedMappingControl.GetAppName() : string.Empty,
            };

            switch (UnifiedMappingControl.CurrentActionType)
            {
                case UnifiedMappingControl.ActionType.KeyOrShortcut:
                    mapping.OperationType = ShortcutOperationType.RemapShortcut;
                    mapping.TargetKeys = GetKeyCodes(mappingService, UnifiedMappingControl.GetActionKeys()) ?? string.Empty;

                    // The dual-key condition (Always / Alone-tap) only applies to a single-key trigger;
                    // the control returns Always when the toggle is hidden for multi-key shortcuts.
                    mapping.Condition = UnifiedMappingControl.GetCondition();
                    break;

                case UnifiedMappingControl.ActionType.Text:
                    mapping.OperationType = ShortcutOperationType.RemapText;
                    mapping.TargetText = UnifiedMappingControl.GetTextContent();
                    break;

                case UnifiedMappingControl.ActionType.OpenUrl:
                    mapping.OperationType = ShortcutOperationType.OpenUri;
                    mapping.UriToOpen = UnifiedMappingControl.GetUrl();
                    break;

                case UnifiedMappingControl.ActionType.OpenApp:
                    mapping.OperationType = ShortcutOperationType.RunProgram;
                    mapping.ProgramPath = UnifiedMappingControl.GetProgramPath();
                    mapping.ProgramArgs = UnifiedMappingControl.GetProgramArgs();
                    mapping.StartInDirectory = UnifiedMappingControl.GetStartInDirectory();
                    mapping.IfRunningAction = UnifiedMappingControl.GetIfRunningAction();
                    mapping.Visibility = UnifiedMappingControl.GetVisibility();
                    mapping.Elevation = UnifiedMappingControl.GetElevationLevel();
                    break;

                case UnifiedMappingControl.ActionType.Disable:
                    mapping.OperationType = ShortcutOperationType.RemapShortcut;
                    mapping.TargetKeys = VkDisabledString;

                    // A single-key disable can also be "alone" (tapping alone does nothing while the key
                    // still works in combination); preserve the condition so it routes to the alone table.
                    mapping.Condition = UnifiedMappingControl.GetCondition();
                    break;

                case UnifiedMappingControl.ActionType.MouseClick:
                    throw new NotImplementedException("Mouse click remapping is not yet supported.");

                default:
                    return null;
            }

            return string.IsNullOrEmpty(mapping.TargetKeys) &&
                     mapping.OperationType is not ShortcutOperationType.RunProgram and not ShortcutOperationType.OpenUri and not ShortcutOperationType.RemapText
                ? null
                : mapping;
        }

        private static string? GetKeyCodes(KeyboardMappingService mappingService, IEnumerable<string> keyNames)
        {
            var keyCodes = keyNames.Select(mappingService.GetKeyCodeFromName).ToList();
            return keyCodes.Count == 0 || keyCodes.Any(keyCode => keyCode == 0)
                ? null
                : string.Join(";", keyCodes.Select(keyCode => keyCode.ToString(CultureInfo.InvariantCulture)));
        }

        private static bool AddMapping(KeyboardMappingService mappingService, ShortcutKeyMapping mapping)
        {
            string[] originalKeys = mapping.OriginalKeys.Split(';', StringSplitOptions.RemoveEmptyEntries);
            if (originalKeys.Length == 0)
            {
                return false;
            }

            if (mapping.OperationType == ShortcutOperationType.RemapText && originalKeys.Length == 1)
            {
                return int.TryParse(originalKeys[0], out int originalKey) &&
                       mappingService.AddSingleKeyToTextMapping(originalKey, mapping.TargetText);
            }

            if (mapping.OperationType == ShortcutOperationType.RemapShortcut && originalKeys.Length == 1)
            {
                if (!int.TryParse(originalKeys[0], out int originalKey))
                {
                    return false;
                }

                // Route "alone" (dual-key tap) single-key remaps to the separate alone table so the
                // key still passes through as a modifier when held in combination.
                bool isAlone = mapping.Condition == SingleKeyRemapCondition.Alone;
                if (mapping.TargetKeys.Contains(';'))
                {
                    return isAlone
                        ? mappingService.AddSingleKeyAloneMapping(originalKey, mapping.TargetKeys)
                        : mappingService.AddSingleKeyMapping(originalKey, mapping.TargetKeys);
                }

                if (!int.TryParse(mapping.TargetKeys, out int targetKey))
                {
                    return false;
                }

                return isAlone
                    ? mappingService.AddSingleKeyAloneMapping(originalKey, targetKey)
                    : mappingService.AddSingleKeyMapping(originalKey, targetKey);
            }

            return mappingService.AddShortcutMapping(mapping);
        }

        private static bool DeleteMapping(KeyboardMappingService mappingService, ShortcutKeyMapping mapping)
        {
            string[] originalKeys = mapping.OriginalKeys.Split(';', StringSplitOptions.RemoveEmptyEntries);
            if (originalKeys.Length == 0)
            {
                return false;
            }

            if (mapping.OperationType == ShortcutOperationType.RemapText && originalKeys.Length == 1)
            {
                return int.TryParse(originalKeys[0], out int originalKey) && mappingService.DeleteSingleKeyToTextMapping(originalKey);
            }

            if (mapping.OperationType == ShortcutOperationType.RemapShortcut && originalKeys.Length == 1)
            {
                if (!int.TryParse(originalKeys[0], out int originalKey))
                {
                    return false;
                }

                // Delete from whichever table the remap lives in, matching how it was added.
                return mapping.Condition == SingleKeyRemapCondition.Alone
                    ? mappingService.DeleteSingleKeyAloneMapping(originalKey)
                    : mappingService.DeleteSingleKeyMapping(originalKey);
            }

            return mappingService.DeleteShortcutMapping(mapping.OriginalKeys, mapping.TargetApp);
        }

        private static bool HasDuplicateEditorMapping(ShortcutKeyMapping replacementMapping, string? replacingId) =>
            SettingsManager.EditorSettings.ShortcutSettingsDictionary.Any(entry =>
                entry.Value.IsActive &&
                !entry.Key.Equals(replacingId, StringComparison.OrdinalIgnoreCase) &&
                KeyboardManagerInterop.AreShortcutsEqual(entry.Value.Shortcut.OriginalKeys, replacementMapping.OriginalKeys) &&

                // An Always and an Alone remap of the same key are distinct (separate engine tables),
                // so only treat it as a duplicate when the condition matches too.
                entry.Value.Shortcut.Condition == replacementMapping.Condition &&
                (string.IsNullOrEmpty(entry.Value.Shortcut.TargetApp) ||
                 string.IsNullOrEmpty(replacementMapping.TargetApp) ||
                 entry.Value.Shortcut.TargetApp.Equals(replacementMapping.TargetApp, StringComparison.OrdinalIgnoreCase)));

        private static void RestoreOriginalMappingSettings(KeyboardMappingService originalService)
        {
            if (!originalService.SaveSettingsAndVerify())
            {
                Logger.LogError("Failed to restore the original mapping settings after a transaction failure.");
            }
        }

        private bool DeleteMultiKeyMapping(List<string> originalKeys, string targetApp = "")
        {
            string originalKeysString = string.Join(";", originalKeys.Select(k => _mappingService!.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
            return _mappingService!.DeleteShortcutMapping(originalKeysString, targetApp);
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
                    case TextExpansionMapping textExpansion:
                        HandleTextExpansionDelete(textExpansion);
                        if (_mappingService != null)
                        {
                            LoadAllMappings();
                        }

                        break;

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

        private void HandleTextExpansionDelete(TextExpansionMapping textExpansion)
        {
            if (!ExecuteTextExpansionTransaction(candidate => candidate.DeleteTextExpansionMapping(textExpansion.Id)))
            {
                Logger.LogWarning($"Failed to delete text expansion {textExpansion.Id}");
            }
        }

        private void HandleRemappingDelete(Remapping remapping)
        {
            if (DeleteMappingTransaction(remapping.Id))
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
            if (!DeleteMappingTransaction(shortcut.Id))
            {
                Logger.LogWarning($"Failed to delete mapping: {string.Join("+", shortcut.Shortcut)}");
            }
        }

        #endregion

        #region Toggle Switch Handlers

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingToggle ||
                sender is not ToggleSwitch toggleSwitch ||
                _mappingService == null)
            {
                return;
            }

            if (toggleSwitch.DataContext is TextExpansionMapping textExpansion)
            {
                ToggleTextExpansion(toggleSwitch, textExpansion);
                return;
            }

            if (toggleSwitch.DataContext is not IToggleableShortcut shortcut)
            {
                return;
            }

            try
            {
                bool desiredState = toggleSwitch.IsOn;
                if (!SetMappingActiveStateTransaction(shortcut.Id, desiredState))
                {
                    RestoreToggleState(toggleSwitch, shortcut.IsActive);
                    Logger.LogWarning($"Failed to set mapping active state to {desiredState}.");
                    return;
                }

                shortcut.IsActive = desiredState;
            }
            catch (Exception ex)
            {
                RestoreToggleState(toggleSwitch, shortcut.IsActive);
                Logger.LogError("Error toggling shortcut active state: " + ex.Message);
            }
        }

        private void ToggleTextExpansion(ToggleSwitch toggleSwitch, TextExpansionMapping textExpansion)
        {
            if (_isUpdatingTextExpansionToggle || toggleSwitch.IsOn == textExpansion.IsEnabled)
            {
                return;
            }

            bool requestedState = toggleSwitch.IsOn;
            bool saved = ExecuteTextExpansionTransaction(candidate => candidate.SetTextExpansionEnabled(textExpansion.Id, requestedState));
            if (saved)
            {
                textExpansion.IsEnabled = requestedState;
                return;
            }

            try
            {
                _isUpdatingTextExpansionToggle = true;
                toggleSwitch.IsOn = textExpansion.IsEnabled;
            }
            finally
            {
                _isUpdatingTextExpansionToggle = false;
            }

            Logger.LogWarning($"Failed to update text expansion {textExpansion.Id}");
        }

        private void RestoreToggleState(ToggleSwitch toggleSwitch, bool isActive)
        {
            _isUpdatingToggle = true;
            try
            {
                toggleSwitch.IsOn = isActive;
            }
            finally
            {
                _isUpdatingToggle = false;
            }
        }

        private bool DeleteMappingTransaction(string mappingId)
        {
            using FileStream? transactionLock = SettingsManager.TryAcquireMappingTransactionLock();
            if (transactionLock == null || !SettingsManager.TryReloadSettings())
            {
                return false;
            }

            if (!SettingsManager.EditorSettings.ShortcutSettingsDictionary.TryGetValue(mappingId, out ShortcutSettings? settings))
            {
                return false;
            }

            return !settings.IsActive
                ? SettingsManager.TryRemoveShortcutKeyMapping(mappingId)
                : ExecuteMappingTransaction(
                    candidate => DeleteMapping(candidate, settings.Shortcut),
                    () => SettingsManager.TryRemoveShortcutKeyMapping(mappingId));
        }

        private bool SetMappingActiveStateTransaction(string mappingId, bool isActive)
        {
            using FileStream? transactionLock = SettingsManager.TryAcquireMappingTransactionLock();
            if (transactionLock == null || !SettingsManager.TryReloadSettings())
            {
                return false;
            }

            if (!SettingsManager.EditorSettings.ShortcutSettingsDictionary.TryGetValue(mappingId, out ShortcutSettings? settings))
            {
                return false;
            }

            if (settings.IsActive == isActive)
            {
                return true;
            }

            return ExecuteMappingTransaction(
                candidate => isActive ? AddMapping(candidate, settings.Shortcut) : DeleteMapping(candidate, settings.Shortcut),
                () => SettingsManager.TrySetShortcutKeyMappingActiveState(mappingId, isActive));
        }

        private bool ExecuteMappingTransaction(
            Func<KeyboardMappingService, bool> updateCandidate,
            Func<bool> commitMetadata)
        {
            if (_mappingService == null)
            {
                return false;
            }

            KeyboardMappingService? originalService = null;
            KeyboardMappingService? candidateService = null;
            bool candidateSaveAttempted = false;
            try
            {
                originalService = new KeyboardMappingService();
                candidateService = new KeyboardMappingService();
                if (!_mappingService.HasSameMappings(originalService) ||
                    !originalService.HasSameMappings(candidateService) ||
                    !updateCandidate(candidateService))
                {
                    return false;
                }

                candidateSaveAttempted = true;
                if (!candidateService.SaveSettingsAndVerify())
                {
                    RestoreOriginalMappingSettings(originalService);
                    return false;
                }

                if (!commitMetadata())
                {
                    RestoreOriginalMappingSettings(originalService);
                    return false;
                }

                KeyboardMappingService previousService = _mappingService;
                _mappingService = candidateService;
                candidateService = null!;
                previousService.Dispose();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error applying mapping transaction: " + ex.Message);
                if (candidateSaveAttempted && originalService is not null)
                {
                    RestoreOriginalMappingSettings(originalService);
                }

                return false;
            }
            finally
            {
                candidateService?.Dispose();
                originalService?.Dispose();
            }
        }

        #endregion

        #region Load Methods

        private void LoadAllMappings()
        {
            LoadRemappings();
            LoadTextMappings();
            LoadTextExpansions();
            LoadProgramShortcuts();
            LoadUrlShortcuts();
            UpdateHasAnyMappings();
        }

        private void UpdateHasAnyMappings()
        {
            bool hasAny = RemappingList.Count > 0 || DisabledList.Count > 0 || TextMappings.Count > 0 || TextExpansions.Count > 0 || ProgramShortcuts.Count > 0 || UrlShortcuts.Count > 0;
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
                if (!SettingsManager.EditorSettings.ShortcutSettingsDictionary.TryGetValue(id, out ShortcutSettings? shortcutSettings) ||
                    !SettingsManager.IsMappingInActiveProfile(shortcutSettings))
                {
                    continue;
                }

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

                    // Round-trip the dual-key condition so the list badge and edit dialog reflect Alone.
                    Condition = mapping.Condition,
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
                if (!SettingsManager.EditorSettings.ShortcutSettingsDictionary.TryGetValue(id, out ShortcutSettings? shortcutSettings) ||
                    !SettingsManager.IsMappingInActiveProfile(shortcutSettings))
                {
                    continue;
                }

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

        private void LoadTextExpansions()
        {
            TextExpansions.Clear();
            foreach (TextExpansionMapping mapping in _mappingService!.GetTextExpansionMappings())
            {
                TextExpansions.Add(mapping);
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
                if (!SettingsManager.EditorSettings.ShortcutSettingsDictionary.TryGetValue(id, out ShortcutSettings? shortcutSettings) ||
                    !SettingsManager.IsMappingInActiveProfile(shortcutSettings))
                {
                    continue;
                }

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
                if (!SettingsManager.EditorSettings.ShortcutSettingsDictionary.TryGetValue(id, out ShortcutSettings? shortcutSettings) ||
                    !SettingsManager.IsMappingInActiveProfile(shortcutSettings))
                {
                    continue;
                }

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

        private List<int> ParseKeyNames(IEnumerable<string> keyNames)
        {
            return keyNames
                .Select(keyName => _mappingService?.GetKeyCodeFromName(keyName) ?? 0)
                .ToList();
        }

        private static string FormatKeyCodes(IEnumerable<int> keyCodes)
        {
            return string.Join(";", keyCodes.Select(keyCode => keyCode.ToString(CultureInfo.InvariantCulture)));
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
