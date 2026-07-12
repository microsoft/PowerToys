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
                }
            }
        }

        public bool IsNotSelectionMode => !_isSelectionMode;

        public ListViewSelectionMode ListSelectionMode => _isSelectionMode ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None;

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
                ShortcutSettings shortcutSettings = SettingsManager.EditorSettings.ShortcutSettingsDictionary[id];
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
                ShortcutSettings shortcutSettings = SettingsManager.EditorSettings.ShortcutSettingsDictionary[id];
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
                ShortcutSettings shortcutSettings = SettingsManager.EditorSettings.ShortcutSettingsDictionary[id];
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
                ShortcutSettings shortcutSettings = SettingsManager.EditorSettings.ShortcutSettingsDictionary[id];
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

            if (!string.IsNullOrEmpty(_searchText) &&
                row.SearchableText.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) < 0)
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

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (_suppressFilterEvents)
            {
                return;
            }

            _searchText = sender.Text?.Trim() ?? string.Empty;
            ApplyFilter();
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

            SearchBox.Text = string.Empty;
            WinFilterToggle.IsChecked = false;
            CtrlFilterToggle.IsChecked = false;
            AltFilterToggle.IsChecked = false;
            ShiftFilterToggle.IsChecked = false;
            AppFilterCombo.SelectedIndex = 0;

            _searchText = string.Empty;
            _filterWin = false;
            _filterCtrl = false;
            _filterAlt = false;
            _filterShift = false;
            _appFilter = null;

            _suppressFilterEvents = false;

            ApplyFilter();
        }

        private void SelectionModeToggle_Click(object sender, RoutedEventArgs e)
        {
            IsSelectionMode = SelectionModeToggle.IsChecked == true;

            if (!IsSelectionMode)
            {
                ClearAllSelections();
            }

            UpdateSelectedCount();
        }

        private void ClearAllSelections()
        {
            RemappingsListView.SelectedItems.Clear();
            DisabledListView.SelectedItems.Clear();
            TextListView.SelectedItems.Clear();
            ProgramsListView.SelectedItems.Clear();
            UrlsListView.SelectedItems.Clear();
        }

        private void MappingList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            SelectedCount =
                RemappingsListView.SelectedItems.Count +
                DisabledListView.SelectedItems.Count +
                TextListView.SelectedItems.Count +
                ProgramsListView.SelectedItems.Count +
                UrlsListView.SelectedItems.Count;
        }

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
            SelectionModeToggle.IsChecked = false;
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
