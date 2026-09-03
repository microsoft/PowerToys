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

        // Bound collections hold the CURRENTLY VISIBLE (filtered) rows. The full set lives in the
        // backing lists below; ApplyFilter() rebuilds the bound collections from them.
        public ObservableCollection<Remapping> RemappingList { get; } = new();

        public ObservableCollection<Remapping> DisabledList { get; } = new();

        public ObservableCollection<TextMapping> TextMappings { get; } = new();

        public ObservableCollection<ProgramShortcut> ProgramShortcuts { get; } = new();

        public ObservableCollection<URLShortcut> UrlShortcuts { get; } = new();

        // Backing (unfiltered) source lists. The bound collections above are views onto these.
        private readonly List<Remapping> _allRemappings = new();
        private readonly List<Remapping> _allDisabled = new();
        private readonly List<TextMapping> _allTextMappings = new();
        private readonly List<ProgramShortcut> _allProgramShortcuts = new();
        private readonly List<URLShortcut> _allUrlShortcuts = new();

        // Virtual-key codes for each modifier family (both generic and left/right specific).
        private static readonly int[] _ctrlVkCodes = { 0x11, 0xA2, 0xA3 };
        private static readonly int[] _altVkCodes = { 0x12, 0xA4, 0xA5 };
        private static readonly int[] _shiftVkCodes = { 0x10, 0xA0, 0xA1 };
        private static readonly int[] _winVkCodes = { 0x5B, 0x5C };

        // Sentinel stored in _appFilter for the "Global only" option (item index 1 in the combo).
        // Uses a control character so it can never collide with a real app name.
        private const string GlobalOnlyToken = "global-only";

        // Ephemeral (never persisted) filter state.
        private string _searchText = string.Empty;
        private string _normalizedSearchText = string.Empty;
        private bool _filterWin;
        private bool _filterCtrl;
        private bool _filterAlt;
        private bool _filterShift;
        private string? _appFilter; // null = all apps, GlobalOnlyToken = global only, else a specific app name.
        private bool _suppressFilterEvents;

        // Cached composite formats for the (localized) bulk-delete strings, parsed lazily on first use.
        private CompositeFormat? _deleteSelectedFormat;
        private CompositeFormat? _bulkDeleteConfirmationFormat;

        // Options shown in the app-filter combo box: [All apps], [Global only], then each distinct app name.
        public ObservableCollection<string> AppFilterOptions { get; } = new();

        private bool _hasAnyData;
        private bool _isSelectionMode;
        private int _selectedCount;

        public string SearchText
        {
            get => _searchText;
            set
            {
                string newValue = value ?? string.Empty;
                if (_searchText != newValue)
                {
                    _searchText = newValue;
                    _normalizedSearchText = newValue.Trim();
                    RaisePropertyChanged(nameof(SearchText));

                    if (!_suppressFilterEvents)
                    {
                        ApplyFilter();
                    }
                }
            }
        }

        // True when there is at least one remapping loaded (regardless of the active filter).
        public bool HasAnyData
        {
            get => _hasAnyData;
            private set
            {
                if (_hasAnyData != value)
                {
                    _hasAnyData = value;
                    RaisePropertyChanged(nameof(HasAnyData));
                }
            }
        }

        // True while the user is multi-selecting rows for bulk deletion.
        public bool IsSelectionMode
        {
            get => _isSelectionMode;
            private set
            {
                if (_isSelectionMode != value)
                {
                    _isSelectionMode = value;
                    RaisePropertyChanged(nameof(IsSelectionMode));
                    RaisePropertyChanged(nameof(IsNotSelectionMode));
                    RaisePropertyChanged(nameof(ListSelectionMode));
                    RaisePropertyChanged(nameof(SelectModeLabel));
                    RaisePropertyChanged(nameof(SelectModeTooltip));
                }
            }
        }

        public bool IsNotSelectionMode => !_isSelectionMode;

        public ListViewSelectionMode ListSelectionMode => _isSelectionMode ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None;

        // Label/tooltip for the selection-mode toggle. While in selection mode the toggle itself is
        // the way out ("Cancel"), so the affordance to leave is always visible.
        public string SelectModeLabel => ResourceHelper.GetString(_isSelectionMode ? "SelectModeToggle_Cancel" : "SelectModeToggle_Select");

        public string SelectModeTooltip => ResourceHelper.GetString(_isSelectionMode ? "SelectModeToggle_CancelTooltip" : "SelectModeToggle_SelectTooltip");

        // Number of rows selected across all sections while in selection mode.
        public int SelectedCount
        {
            get => _selectedCount;
            private set
            {
                if (_selectedCount != value)
                {
                    _selectedCount = value;
                    RaisePropertyChanged(nameof(SelectedCount));
                    RaisePropertyChanged(nameof(HasSelection));
                    RaisePropertyChanged(nameof(DeleteSelectedLabel));
                }
            }
        }

        public bool HasSelection => _selectedCount > 0;

        public string DeleteSelectedLabel
        {
            get
            {
                _deleteSelectedFormat ??= CompositeFormat.Parse(ResourceHelper.GetString("BulkDelete_SelectedFormat"));
                return string.Format(CultureInfo.CurrentCulture, _deleteSelectedFormat, _selectedCount);
            }
        }

        private void RaisePropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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

            // Identify the row being edited (any mapping type) so validation can exclude it by identity
            // instead of a count tolerance — its Id is its key in ShortcutSettingsDictionary.
            string? editingId = _isEditMode ? (_editingItem?.Item as IToggleableShortcut)?.Id : null;

            return actionType switch
            {
                UnifiedMappingControl.ActionType.KeyOrShortcut => ValidationHelper.ValidateKeyMapping(
                    triggerKeys, UnifiedMappingControl.GetActionKeys(), isAppSpecific, appName, _mappingService!, _isEditMode, editingId),
                UnifiedMappingControl.ActionType.Text => ValidationHelper.ValidateTextMapping(
                    triggerKeys, UnifiedMappingControl.GetTextContent(), isAppSpecific, appName, _mappingService!, _isEditMode, editingId),
                UnifiedMappingControl.ActionType.OpenUrl => ValidationHelper.ValidateUrlMapping(
                    triggerKeys, UnifiedMappingControl.GetUrl(), isAppSpecific, appName, _mappingService!, _isEditMode, editingId),
                UnifiedMappingControl.ActionType.OpenApp => ValidationHelper.ValidateAppMapping(
                    triggerKeys, UnifiedMappingControl.GetProgramPath(), isAppSpecific, appName, _mappingService!, _isEditMode, editingId),
                UnifiedMappingControl.ActionType.Disable => ValidationHelper.ValidateDisableMapping(
                    triggerKeys, isAppSpecific, appName, _mappingService!, _isEditMode, editingId),
                _ => ValidationErrorType.NoError,
            };
        }

        private bool SaveMappingTransaction(List<string> triggerKeys)
        {
            if (_mappingService == null)
            {
                return false;
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
                    case Remapping remapping:
                        HandleRemappingDelete(remapping);
                        RefreshAppFilterOptions();
                        ApplyFilter();
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
                toggleSwitch.DataContext is not IToggleableShortcut shortcut ||
                _mappingService == null)
            {
                return;
            }

            // Only act on a genuine user toggle, where the control's new state diverges from the model.
            // ListView container recycling (heavy during filtering, bulk-delete reloads and scrolling of a
            // long list) re-applies the OneTime IsOn binding to the recycled-in item, which also raises
            // Toggled. Without this guard those spurious events call Enable/DisableShortcut, whose
            // non-idempotent ToggleShortcutKeyMappingActiveState flips the wrong entry's persisted active
            // state — making unrelated mappings silently turn OFF.
            if (toggleSwitch.IsOn == shortcut.IsActive)
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
            LoadProgramShortcuts();
            LoadUrlShortcuts();
            RefreshAppFilterOptions();
            ApplyFilter();
        }

        private void UpdateHasAnyMappings()
        {
            bool hasData = _allRemappings.Count > 0 || _allDisabled.Count > 0 || _allTextMappings.Count > 0 || _allProgramShortcuts.Count > 0 || _allUrlShortcuts.Count > 0;
            bool hasVisible = RemappingList.Count > 0 || DisabledList.Count > 0 || TextMappings.Count > 0 || ProgramShortcuts.Count > 0 || UrlShortcuts.Count > 0;

            HasAnyData = hasData;
            MappingState = !hasData ? "Empty" : (hasVisible ? "HasMappings" : "NoResults");
        }

        private void LoadRemappings()
        {
            SettingsManager.EditorSettings.ShortcutsByOperationType.TryGetValue(ShortcutOperationType.RemapShortcut, out var remapShortcutIds);

            _allRemappings.Clear();
            _allDisabled.Clear();

            if (remapShortcutIds == null)
            {
                return;
            }

            foreach (var id in remapShortcutIds)
            {
                if (!SettingsManager.EditorSettings.ShortcutSettingsDictionary.TryGetValue(id, out ShortcutSettings? shortcutSettings) ||
                    !SettingsManager.IsMappingInActiveProfile(shortcutSettings))
                {
                    continue;
                }

                ShortcutKeyMapping mapping = shortcutSettings.Shortcut;
                var originalKeyNames = ParseKeyCodes(mapping.OriginalKeys);
                var remappedKeyNames = ParseKeyCodes(mapping.TargetKeys);

                bool isDisabled = mapping.TargetKeys == VkDisabledString;

                var remapping = new Remapping
                {
                    Shortcut = originalKeyNames,
                    RemappedKeys = isDisabled ? new List<string>() : remappedKeyNames,
                    IsAllApps = string.IsNullOrEmpty(mapping.TargetApp),
                    AppName = mapping.TargetApp ?? string.Empty,
                    Id = shortcutSettings.Id,
                    IsActive = shortcutSettings.IsActive,

                    // Round-trip the dual-key condition so the list badge and edit dialog reflect Alone.
                    Condition = mapping.Condition,
                    TriggerKeyCodes = ParseVkCodes(mapping.OriginalKeys),
                    SearchableText = BuildSearchableText(originalKeyNames.Concat(isDisabled ? Enumerable.Empty<string>() : remappedKeyNames).Append(mapping.TargetApp ?? string.Empty)),
                };

                if (isDisabled)
                {
                    _allDisabled.Add(remapping);
                }
                else
                {
                    _allRemappings.Add(remapping);
                }
            }
        }

        private void LoadTextMappings()
        {
            SettingsManager.EditorSettings.ShortcutsByOperationType.TryGetValue(ShortcutOperationType.RemapText, out var remapShortcutIds);

            _allTextMappings.Clear();

            if (remapShortcutIds == null)
            {
                return;
            }

            foreach (var id in remapShortcutIds)
            {
                if (!SettingsManager.EditorSettings.ShortcutSettingsDictionary.TryGetValue(id, out ShortcutSettings? shortcutSettings) ||
                    !SettingsManager.IsMappingInActiveProfile(shortcutSettings))
                {
                    continue;
                }

                ShortcutKeyMapping mapping = shortcutSettings.Shortcut;
                var originalKeyNames = ParseKeyCodes(mapping.OriginalKeys);

                _allTextMappings.Add(new TextMapping
                {
                    Shortcut = originalKeyNames,
                    Text = mapping.TargetText,
                    IsAllApps = string.IsNullOrEmpty(mapping.TargetApp),
                    AppName = mapping.TargetApp ?? string.Empty,
                    Id = shortcutSettings.Id,
                    IsActive = shortcutSettings.IsActive,
                    TriggerKeyCodes = ParseVkCodes(mapping.OriginalKeys),
                    SearchableText = BuildSearchableText(originalKeyNames.Append(mapping.TargetText).Append(mapping.TargetApp ?? string.Empty)),
                });
            }
        }

        private void LoadProgramShortcuts()
        {
            SettingsManager.EditorSettings.ShortcutsByOperationType.TryGetValue(ShortcutOperationType.RunProgram, out var remapShortcutIds);

            _allProgramShortcuts.Clear();

            if (remapShortcutIds == null)
            {
                return;
            }

            foreach (var id in remapShortcutIds)
            {
                if (!SettingsManager.EditorSettings.ShortcutSettingsDictionary.TryGetValue(id, out ShortcutSettings? shortcutSettings) ||
                    !SettingsManager.IsMappingInActiveProfile(shortcutSettings))
                {
                    continue;
                }

                ShortcutKeyMapping mapping = shortcutSettings.Shortcut;
                var originalKeyNames = ParseKeyCodes(mapping.OriginalKeys);

                _allProgramShortcuts.Add(new ProgramShortcut
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
                    TriggerKeyCodes = ParseVkCodes(mapping.OriginalKeys),
                    SearchableText = BuildSearchableText(originalKeyNames.Append(mapping.ProgramPath).Append(mapping.ProgramArgs).Append(mapping.TargetApp ?? string.Empty)),
                });
            }
        }

        private void LoadUrlShortcuts()
        {
            SettingsManager.EditorSettings.ShortcutsByOperationType.TryGetValue(ShortcutOperationType.OpenUri, out var remapShortcutIds);

            _allUrlShortcuts.Clear();

            if (remapShortcutIds == null)
            {
                return;
            }

            foreach (var id in remapShortcutIds)
            {
                if (!SettingsManager.EditorSettings.ShortcutSettingsDictionary.TryGetValue(id, out ShortcutSettings? shortcutSettings) ||
                    !SettingsManager.IsMappingInActiveProfile(shortcutSettings))
                {
                    continue;
                }

                ShortcutKeyMapping mapping = shortcutSettings.Shortcut;
                var originalKeyNames = ParseKeyCodes(mapping.OriginalKeys);

                _allUrlShortcuts.Add(new URLShortcut
                {
                    Shortcut = originalKeyNames,
                    URL = mapping.UriToOpen,
                    Id = shortcutSettings.Id,
                    IsActive = shortcutSettings.IsActive,
                    IsAllApps = string.IsNullOrEmpty(mapping.TargetApp),
                    AppName = mapping.TargetApp ?? string.Empty,
                    TriggerKeyCodes = ParseVkCodes(mapping.OriginalKeys),
                    SearchableText = BuildSearchableText(originalKeyNames.Append(mapping.UriToOpen).Append(mapping.TargetApp ?? string.Empty)),
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

        // Parse the raw ";"-separated VK code string into integers (no display-name conversion),
        // so modifier filtering can classify keys by code and stay locale-independent.
        private static List<int> ParseVkCodes(string keyCodesString)
        {
            var codes = new List<int>();
            foreach (var part in keyCodesString.Split(';'))
            {
                if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
                {
                    codes.Add(code);
                }
            }

            return codes;
        }

        // Combine a row's human-readable parts into a single lowercased string for text search.
        private static string BuildSearchableText(IEnumerable<string> parts)
        {
            var sb = new StringBuilder();
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(part);
            }

            return sb.ToString().ToLowerInvariant();
        }

        #endregion

        #region Filter and Selection

        // Rebuilds the five bound (visible) collections from their backing lists using the active filters.
        private void ApplyFilter()
        {
            RebuildView(_allRemappings, RemappingList);
            RebuildView(_allDisabled, DisabledList);
            RebuildView(_allTextMappings, TextMappings);
            RebuildView(_allProgramShortcuts, ProgramShortcuts);
            RebuildView(_allUrlShortcuts, UrlShortcuts);
            UpdateHasAnyMappings();
        }

        private void RebuildView<T>(List<T> source, ObservableCollection<T> view)
            where T : IToggleableShortcut
        {
            view.Clear();
            foreach (var item in source)
            {
                if (RowMatches(item))
                {
                    view.Add(item);
                }
            }
        }

        // Returns true when a row passes the active filters. Filter categories combine with AND;
        // the modifier toggles combine with OR (selecting Win + Ctrl shows the Win OR Ctrl layers).
        private bool RowMatches(IToggleableShortcut row)
        {
            if (_filterWin || _filterCtrl || _filterAlt || _filterShift)
            {
                bool modifierMatch =
                    (_filterWin && ContainsAny(row.TriggerKeyCodes, _winVkCodes)) ||
                    (_filterCtrl && ContainsAny(row.TriggerKeyCodes, _ctrlVkCodes)) ||
                    (_filterAlt && ContainsAny(row.TriggerKeyCodes, _altVkCodes)) ||
                    (_filterShift && ContainsAny(row.TriggerKeyCodes, _shiftVkCodes));

                if (!modifierMatch)
                {
                    return false;
                }
            }

            if (_appFilter == GlobalOnlyToken)
            {
                if (!row.IsAllApps)
                {
                    return false;
                }
            }
            else if (_appFilter != null)
            {
                if (row.IsAllApps || !string.Equals(row.AppName, _appFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(_normalizedSearchText) &&
                row.SearchableText.IndexOf(_normalizedSearchText, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return true;
        }

        private static bool ContainsAny(IReadOnlyList<int> codes, int[] vkSet)
        {
            for (int i = 0; i < codes.Count; i++)
            {
                if (Array.IndexOf(vkSet, codes[i]) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        // Recomputes the app-filter combo's items from the backing lists, preserving the current selection.
        private void RefreshAppFilterOptions()
        {
            var apps = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            AddApps(_allRemappings, apps);
            AddApps(_allDisabled, apps);
            AddApps(_allTextMappings, apps);
            AddApps(_allProgramShortcuts, apps);
            AddApps(_allUrlShortcuts, apps);

            string? previous = _appFilter;

            _suppressFilterEvents = true;

            AppFilterOptions.Clear();
            AppFilterOptions.Add(ResourceHelper.GetString("FilterApp_AllApps"));
            AppFilterOptions.Add(ResourceHelper.GetString("FilterApp_GlobalOnly"));
            foreach (var app in apps)
            {
                AppFilterOptions.Add(app);
            }

            int index = 0;
            if (previous == GlobalOnlyToken)
            {
                index = 1;
            }
            else if (previous != null)
            {
                int found = AppFilterOptions.IndexOf(previous);
                index = found >= 0 ? found : 0;
            }

            AppFilterCombo.SelectedIndex = index;
            _appFilter = index == 0 ? null : (index == 1 ? GlobalOnlyToken : AppFilterOptions[index]);

            _suppressFilterEvents = false;
        }

        private static void AddApps<T>(List<T> source, SortedSet<string> apps)
            where T : IToggleableShortcut
        {
            foreach (var item in source)
            {
                if (!item.IsAllApps && !string.IsNullOrEmpty(item.AppName))
                {
                    apps.Add(item.AppName);
                }
            }
        }

        private void ModifierFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressFilterEvents)
            {
                return;
            }

            _filterWin = WinFilterToggle.IsChecked == true;
            _filterCtrl = CtrlFilterToggle.IsChecked == true;
            _filterAlt = AltFilterToggle.IsChecked == true;
            _filterShift = ShiftFilterToggle.IsChecked == true;
            ApplyFilter();
        }

        private void AppFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressFilterEvents)
            {
                return;
            }

            int index = AppFilterCombo.SelectedIndex;
            _appFilter = index <= 0 ? null : (index == 1 ? GlobalOnlyToken : AppFilterOptions[index]);
            ApplyFilter();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            _suppressFilterEvents = true;

            SearchText = string.Empty;
            WinFilterToggle.IsChecked = false;
            CtrlFilterToggle.IsChecked = false;
            AltFilterToggle.IsChecked = false;
            ShiftFilterToggle.IsChecked = false;
            AppFilterCombo.SelectedIndex = 0;

            _filterWin = false;
            _filterCtrl = false;
            _filterAlt = false;
            _filterShift = false;
            _appFilter = null;

            _suppressFilterEvents = false;

            ApplyFilter();
        }

        private void SelectionModeButton_Click(object sender, RoutedEventArgs e)
        {
            bool entering = !IsSelectionMode;

            // When leaving selection mode, clear the selection while the lists are still in Multiple
            // mode. ListView.SelectedItems is only valid for Multiple, so it must be touched before
            // ListSelectionMode flips to None below (otherwise it throws and takes the panel down).
            if (!entering)
            {
                ClearAllSelections();
            }

            IsSelectionMode = entering;

            UpdateSelectedCount();
        }

        private void ClearAllSelections()
        {
            ClearSelectionIfMultiple(RemappingsListView);
            ClearSelectionIfMultiple(DisabledListView);
            ClearSelectionIfMultiple(TextListView);
            ClearSelectionIfMultiple(ProgramsListView);
            ClearSelectionIfMultiple(UrlsListView);
        }

        // ListView.SelectedItems is only valid while SelectionMode is Multiple; touching it in any
        // other mode throws. Guard so selection housekeeping is safe whatever the current mode is.
        private static void ClearSelectionIfMultiple(ListViewBase list)
        {
            if (list.SelectionMode == ListViewSelectionMode.Multiple)
            {
                list.SelectedItems.Clear();
            }
        }

        private void MappingList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            SelectedCount =
                SelectedCountIfMultiple(RemappingsListView) +
                SelectedCountIfMultiple(DisabledListView) +
                SelectedCountIfMultiple(TextListView) +
                SelectedCountIfMultiple(ProgramsListView) +
                SelectedCountIfMultiple(UrlsListView);
        }

        // Mirrors ClearSelectionIfMultiple: SelectedItems.Count also throws outside Multiple mode.
        private static int SelectedCountIfMultiple(ListViewBase list)
            => list.SelectionMode == ListViewSelectionMode.Multiple ? list.SelectedItems.Count : 0;

        private async void DeleteSelectedBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_mappingService == null || SelectedCount == 0)
            {
                return;
            }

            _bulkDeleteConfirmationFormat ??= CompositeFormat.Parse(ResourceHelper.GetString("BulkDeleteConfirmation_Format"));
            BulkDeleteConfirmationText.Text = string.Format(CultureInfo.CurrentCulture, _bulkDeleteConfirmationFormat, SelectedCount);

            if (await BulkDeleteConfirmationDialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                // Snapshot the selection first; deletion mutates the collections and settings underneath.
                var remappings = RemappingsListView.SelectedItems.OfType<Remapping>().ToList();
                var disabled = DisabledListView.SelectedItems.OfType<Remapping>().ToList();
                var texts = TextListView.SelectedItems.OfType<TextMapping>().ToList();
                var programs = ProgramsListView.SelectedItems.OfType<ProgramShortcut>().ToList();
                var urls = UrlsListView.SelectedItems.OfType<URLShortcut>().ToList();

                foreach (var item in remappings)
                {
                    HandleRemappingDelete(item);
                }

                foreach (var item in disabled)
                {
                    HandleRemappingDelete(item);
                }

                foreach (var item in texts)
                {
                    HandleShortcutDelete(item);
                }

                foreach (var item in programs)
                {
                    HandleShortcutDelete(item);
                }

                foreach (var item in urls)
                {
                    HandleShortcutDelete(item);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during bulk delete: " + ex.Message);
            }

            IsSelectionMode = false;
            LoadAllMappings();
            UpdateSelectedCount();
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
