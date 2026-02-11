// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using KeyboardManagerEditorUI.Helpers;
using KeyboardManagerEditorUI.Interop;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Storage.Pickers;

namespace KeyboardManagerEditorUI.Pages
{
    /// <summary>
    /// The Mouse remapping page that allows users to configure mouse button remappings.
    /// </summary>
    public sealed partial class Mouse : Page
    {
        private const string SettingsEventName = "PowerToys_KeyboardManager_Event_Settings";

        private int _capturedKeyCode;
        private string _capturedKeyName = string.Empty;
        private bool _isEditMode;
        private MouseMapping? _editingMouseMapping;
        private KeyToMouseMapping? _editingKeyToMouseMapping;

        /// <summary>
        /// Gets the list of mouse-to-key mappings.
        /// </summary>
        public ObservableCollection<MouseMapping> MouseToKeyMappings { get; } = new ObservableCollection<MouseMapping>();

        /// <summary>
        /// Gets the list of key-to-mouse mappings.
        /// </summary>
        public ObservableCollection<KeyToMouseMapping> KeyToMouseMappings { get; } = new ObservableCollection<KeyToMouseMapping>();

        public Mouse()
        {
            this.InitializeComponent();
            LoadMappings();
        }

        private void LoadMappings()
        {
            try
            {
                // Load settings using the interop layer
                IntPtr config = KeyboardManagerInterop.CreateMappingConfiguration();
                try
                {
                    if (!KeyboardManagerInterop.LoadMappingSettings(config))
                    {
                        Logger.LogWarning("Failed to load mapping settings, starting with empty mappings");
                        return;
                    }

                    // Load mouse button remappings via interop
                    int mouseButtonCount = KeyboardManagerInterop.GetMouseButtonRemapCount(config);
                    for (int i = 0; i < mouseButtonCount; i++)
                    {
                        MouseButtonMapping interopMapping = default;
                        if (KeyboardManagerInterop.GetMouseButtonRemap(config, i, ref interopMapping))
                        {
                            var mapping = ConvertFromInteropMouseMapping(interopMapping);
                            MouseToKeyMappings.Add(mapping);
                            FreeMouseButtonMappingStrings(interopMapping);
                        }
                    }

                    // Load key-to-mouse remappings via interop
                    int keyToMouseCount = KeyboardManagerInterop.GetKeyToMouseRemapCount(config);
                    for (int i = 0; i < keyToMouseCount; i++)
                    {
                        KeyToMouseMappingInterop interopMapping = default;
                        if (KeyboardManagerInterop.GetKeyToMouseRemap(config, i, ref interopMapping))
                        {
                            var mapping = ConvertFromInteropKeyToMouseMapping(interopMapping);
                            KeyToMouseMappings.Add(mapping);
                            FreeKeyToMouseMappingStrings(interopMapping);
                        }
                    }
                }
                finally
                {
                    KeyboardManagerInterop.DestroyMappingConfiguration(config);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load mouse mappings: {ex.Message}");
            }
        }

        private static MouseMapping ConvertFromInteropMouseMapping(MouseButtonMapping interopMapping)
        {
            var mapping = new MouseMapping
            {
                OriginalButton = GetMouseButtonName(interopMapping.OriginalButton),
                TargetApp = KeyboardManagerInterop.GetStringAndFree(interopMapping.TargetApp) is string app ? app : string.Empty,
            };

            // Reset TargetApp pointer since we freed it
            string targetKeys = interopMapping.TargetKeys != IntPtr.Zero
                ? Marshal.PtrToStringUni(interopMapping.TargetKeys) ?? string.Empty
                : string.Empty;

            switch (interopMapping.TargetType)
            {
                case 0: // Key
                    mapping.TargetType = "Key";
                    if (int.TryParse(targetKeys, out int keyCode))
                    {
                        mapping.TargetKeyCode = keyCode;
                        mapping.TargetKeyName = GetKeyName(keyCode);
                    }

                    break;
                case 1: // Shortcut
                    mapping.TargetType = "Shortcut";
                    mapping.TargetShortcutKeys = targetKeys;
                    break;
                case 2: // Text
                    mapping.TargetType = "Text";
                    mapping.TargetText = interopMapping.TargetText != IntPtr.Zero
                        ? Marshal.PtrToStringUni(interopMapping.TargetText) ?? string.Empty
                        : string.Empty;
                    break;
                case 3: // RunProgram
                    mapping.TargetType = "RunProgram";
                    mapping.ProgramPath = interopMapping.ProgramPath != IntPtr.Zero
                        ? Marshal.PtrToStringUni(interopMapping.ProgramPath) ?? string.Empty
                        : string.Empty;
                    mapping.ProgramArgs = interopMapping.ProgramArgs != IntPtr.Zero
                        ? Marshal.PtrToStringUni(interopMapping.ProgramArgs) ?? string.Empty
                        : string.Empty;
                    break;
                case 4: // OpenUri
                    mapping.TargetType = "OpenUri";
                    mapping.UriToOpen = interopMapping.UriToOpen != IntPtr.Zero
                        ? Marshal.PtrToStringUni(interopMapping.UriToOpen) ?? string.Empty
                        : string.Empty;
                    break;
            }

            return mapping;
        }

        private static KeyToMouseMapping ConvertFromInteropKeyToMouseMapping(KeyToMouseMappingInterop interopMapping)
        {
            return new KeyToMouseMapping
            {
                OriginalKeyCode = interopMapping.OriginalKey,
                OriginalKeyName = GetKeyName(interopMapping.OriginalKey),
                TargetMouseButton = GetMouseButtonName(interopMapping.TargetMouseButton),
                TargetApp = KeyboardManagerInterop.GetStringAndFree(interopMapping.TargetApp) is string app ? app : string.Empty,
            };
        }

        private static void FreeMouseButtonMappingStrings(MouseButtonMapping mapping)
        {
            // TargetApp is already freed by GetStringAndFree in ConvertFromInteropMouseMapping
            if (mapping.TargetKeys != IntPtr.Zero)
            {
                KeyboardManagerInterop.FreeString(mapping.TargetKeys);
            }

            if (mapping.TargetText != IntPtr.Zero)
            {
                KeyboardManagerInterop.FreeString(mapping.TargetText);
            }

            if (mapping.ProgramPath != IntPtr.Zero)
            {
                KeyboardManagerInterop.FreeString(mapping.ProgramPath);
            }

            if (mapping.ProgramArgs != IntPtr.Zero)
            {
                KeyboardManagerInterop.FreeString(mapping.ProgramArgs);
            }

            if (mapping.UriToOpen != IntPtr.Zero)
            {
                KeyboardManagerInterop.FreeString(mapping.UriToOpen);
            }
        }

        private static void FreeAllMouseButtonMappingStrings(MouseButtonMapping mapping)
        {
            // Free ALL strings including TargetApp
            if (mapping.TargetApp != IntPtr.Zero)
            {
                KeyboardManagerInterop.FreeString(mapping.TargetApp);
            }

            if (mapping.TargetKeys != IntPtr.Zero)
            {
                KeyboardManagerInterop.FreeString(mapping.TargetKeys);
            }

            if (mapping.TargetText != IntPtr.Zero)
            {
                KeyboardManagerInterop.FreeString(mapping.TargetText);
            }

            if (mapping.ProgramPath != IntPtr.Zero)
            {
                KeyboardManagerInterop.FreeString(mapping.ProgramPath);
            }

            if (mapping.ProgramArgs != IntPtr.Zero)
            {
                KeyboardManagerInterop.FreeString(mapping.ProgramArgs);
            }

            if (mapping.UriToOpen != IntPtr.Zero)
            {
                KeyboardManagerInterop.FreeString(mapping.UriToOpen);
            }
        }

        private static void FreeKeyToMouseMappingStrings(KeyToMouseMappingInterop mapping)
        {
            // TargetApp is already freed by GetStringAndFree in ConvertFromInteropKeyToMouseMapping
        }

        private void SaveMappings()
        {
            try
            {
                // Create a new configuration and load existing settings
                IntPtr config = KeyboardManagerInterop.CreateMappingConfiguration();
                try
                {
                    // Load existing settings to preserve keyboard mappings
                    KeyboardManagerInterop.LoadMappingSettings(config);

                    // Clear existing mouse mappings
                    ClearMouseMappingsFromConfig(config);

                    // Add all mouse button remappings
                    foreach (var mapping in MouseToKeyMappings)
                    {
                        AddMouseMappingToConfig(config, mapping);
                    }

                    // Add all key-to-mouse remappings
                    foreach (var mapping in KeyToMouseMappings)
                    {
                        AddKeyToMouseMappingToConfig(config, mapping);
                    }

                    // Save the configuration
                    if (KeyboardManagerInterop.SaveMappingSettings(config))
                    {
                        Logger.LogInfo("Mouse mappings saved successfully via interop");

                        // Signal the settings event to notify the engine to reload
                        SignalSettingsChanged();
                    }
                    else
                    {
                        Logger.LogError("Failed to save mouse mappings via interop");
                    }
                }
                finally
                {
                    KeyboardManagerInterop.DestroyMappingConfiguration(config);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save mouse mappings: {ex.Message}");
            }
        }

        private static void ClearMouseMappingsFromConfig(IntPtr config)
        {
            // Delete all existing mouse button remaps by iterating in reverse
            int mouseCount = KeyboardManagerInterop.GetMouseButtonRemapCount(config);
            for (int i = mouseCount - 1; i >= 0; i--)
            {
                MouseButtonMapping interopMapping = default;
                if (KeyboardManagerInterop.GetMouseButtonRemap(config, i, ref interopMapping))
                {
                    string targetApp = interopMapping.TargetApp != IntPtr.Zero
                        ? Marshal.PtrToStringUni(interopMapping.TargetApp) ?? string.Empty
                        : string.Empty;

                    KeyboardManagerInterop.DeleteMouseButtonRemap(config, interopMapping.OriginalButton, targetApp);

                    // Free all allocated strings including TargetApp
                    FreeAllMouseButtonMappingStrings(interopMapping);
                }
            }

            // Delete all existing key-to-mouse remaps by iterating in reverse
            int keyToMouseCount = KeyboardManagerInterop.GetKeyToMouseRemapCount(config);
            for (int i = keyToMouseCount - 1; i >= 0; i--)
            {
                KeyToMouseMappingInterop interopMapping = default;
                if (KeyboardManagerInterop.GetKeyToMouseRemap(config, i, ref interopMapping))
                {
                    string targetApp = interopMapping.TargetApp != IntPtr.Zero
                        ? Marshal.PtrToStringUni(interopMapping.TargetApp) ?? string.Empty
                        : string.Empty;

                    KeyboardManagerInterop.DeleteKeyToMouseRemap(config, interopMapping.OriginalKey, targetApp);

                    // Free TargetApp string
                    if (interopMapping.TargetApp != IntPtr.Zero)
                    {
                        KeyboardManagerInterop.FreeString(interopMapping.TargetApp);
                    }
                }
            }
        }

        private static void AddMouseMappingToConfig(IntPtr config, MouseMapping mapping)
        {
            int originalButton = GetMouseButtonIndex(mapping.OriginalButton);
            string targetApp = mapping.TargetApp ?? string.Empty;

            int targetType;
            string targetKeys;
            string targetText = string.Empty;
            string programPath = string.Empty;
            string programArgs = string.Empty;
            string uriToOpen = string.Empty;

            switch (mapping.TargetType)
            {
                case "Key":
                    targetType = 0;
                    targetKeys = mapping.TargetKeyCode.ToString(CultureInfo.InvariantCulture);
                    break;
                case "Shortcut":
                    targetType = 1;
                    targetKeys = mapping.TargetShortcutKeys ?? string.Empty;
                    break;
                case "Text":
                    targetType = 2;
                    targetKeys = string.Empty;
                    targetText = mapping.TargetText ?? string.Empty;
                    break;
                case "RunProgram":
                    targetType = 3;
                    targetKeys = string.Empty;
                    programPath = mapping.ProgramPath ?? string.Empty;
                    programArgs = mapping.ProgramArgs ?? string.Empty;
                    break;
                case "OpenUri":
                    targetType = 4;
                    targetKeys = string.Empty;
                    uriToOpen = mapping.UriToOpen ?? string.Empty;
                    break;
                default:
                    return;
            }

            KeyboardManagerInterop.AddMouseButtonRemap(
                config,
                originalButton,
                targetKeys,
                targetApp,
                targetType,
                targetText,
                programPath,
                programArgs,
                uriToOpen);
        }

        private static void AddKeyToMouseMappingToConfig(IntPtr config, KeyToMouseMapping mapping)
        {
            int targetMouseButton = GetMouseButtonIndex(mapping.TargetMouseButton);
            string targetApp = mapping.TargetApp ?? string.Empty;

            KeyboardManagerInterop.AddKeyToMouseRemap(
                config,
                mapping.OriginalKeyCode,
                targetMouseButton,
                targetApp);
        }

        private static void SignalSettingsChanged()
        {
            // Use OpenEvent since the engine already created this event
            const uint EVENT_MODIFY_STATE = 0x0002;
            IntPtr hEvent = OpenEvent(EVENT_MODIFY_STATE, false, SettingsEventName);
            if (hEvent != IntPtr.Zero)
            {
                SetEvent(hEvent);
                CloseHandle(hEvent);
                Logger.LogInfo($"Signaled {SettingsEventName} event");
            }
            else
            {
                Logger.LogError($"Failed to open {SettingsEventName} event. Engine may not be running.");
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenEvent(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetEvent(IntPtr hEvent);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private static string GetKeyName(int keyCode)
        {
            // Use GetKeyNameText for virtual key to name conversion
            uint scanCode = MapVirtualKey((uint)keyCode, 0);
            var sb = new System.Text.StringBuilder(256);
            int result = GetKeyNameText((int)(scanCode << 16), sb, sb.Capacity);
            return result > 0 ? sb.ToString() : $"0x{keyCode:X2}";
        }

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetKeyNameText(int lParam, System.Text.StringBuilder lpString, int nSize);

        // Mouse to Key handlers
        private async void NewMouseToKeyBtn_Click(object sender, RoutedEventArgs e)
        {
            _isEditMode = false;
            _editingMouseMapping = null;
            ResetMouseToKeyDialog();

            // Clear any previous error
            MouseToKeyErrorText.Visibility = Visibility.Collapsed;

            MouseToKeyDialog.PrimaryButtonClick += MouseToKeyDialog_PrimaryButtonClick;
            await MouseToKeyDialog.ShowAsync();
            MouseToKeyDialog.PrimaryButtonClick -= MouseToKeyDialog_PrimaryButtonClick;
        }

        private async void MouseToKeyListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is MouseMapping mapping)
            {
                _isEditMode = true;
                _editingMouseMapping = mapping;

                // Populate dialog with existing values
                MouseButtonComboBox.SelectedIndex = GetMouseButtonIndex(mapping.OriginalButton);
                TargetTypeComboBox.SelectedIndex = GetTargetTypeIndex(mapping.TargetType);
                UpdateTargetInputVisibility();

                // Populate app-specific checkbox
                MouseToKeyAllAppsCheckBox.IsChecked = mapping.IsAllApps;
                MouseToKeyTargetAppTextBox.Text = mapping.TargetApp;
                MouseToKeyTargetAppTextBox.Visibility = mapping.IsAllApps ? Visibility.Collapsed : Visibility.Visible;

                switch (mapping.TargetType)
                {
                    case "Key":
                        _capturedKeyCode = mapping.TargetKeyCode;
                        _capturedKeyName = mapping.TargetKeyName;
                        KeyInputTextBox.Text = mapping.TargetKeyName;
                        break;
                    case "Shortcut":
                        ShortcutInputTextBox.Text = mapping.TargetShortcutKeys;
                        break;
                    case "Text":
                        TextInputTextBox.Text = mapping.TargetText;
                        break;
                    case "RunProgram":
                        ProgramPathTextBox.Text = mapping.ProgramPath;
                        ProgramArgsTextBox.Text = mapping.ProgramArgs;
                        break;
                    case "OpenUri":
                        UrlInputTextBox.Text = mapping.UriToOpen;
                        break;
                }

                // Clear any previous error
                MouseToKeyErrorText.Visibility = Visibility.Collapsed;

                MouseToKeyDialog.PrimaryButtonClick += MouseToKeyDialog_PrimaryButtonClick;
                await MouseToKeyDialog.ShowAsync();
                MouseToKeyDialog.PrimaryButtonClick -= MouseToKeyDialog_PrimaryButtonClick;
            }
        }

        private void MouseToKeyDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Validate input
            var (isValid, errorMessage) = ValidateMouseToKeyMapping();
            if (!isValid)
            {
                args.Cancel = true;
                MouseToKeyErrorText.Text = errorMessage;
                MouseToKeyErrorText.Visibility = Visibility.Visible;
                return;
            }

            // Clear any previous error
            MouseToKeyErrorText.Visibility = Visibility.Collapsed;

            var mapping = _isEditMode && _editingMouseMapping != null
                ? _editingMouseMapping
                : new MouseMapping();

            mapping.OriginalButton = GetMouseButtonName(MouseButtonComboBox.SelectedIndex);
            mapping.TargetType = GetTargetTypeName(TargetTypeComboBox.SelectedIndex);

            // Set app-specific target
            mapping.TargetApp = MouseToKeyAllAppsCheckBox.IsChecked == true
                ? string.Empty
                : MouseToKeyTargetAppTextBox.Text.Trim();

            switch (mapping.TargetType)
            {
                case "Key":
                    mapping.TargetKeyCode = _capturedKeyCode;
                    mapping.TargetKeyName = _capturedKeyName;
                    break;
                case "Shortcut":
                    mapping.TargetShortcutKeys = ShortcutInputTextBox.Text;
                    break;
                case "Text":
                    mapping.TargetText = TextInputTextBox.Text;
                    break;
                case "RunProgram":
                    mapping.ProgramPath = ProgramPathTextBox.Text;
                    mapping.ProgramArgs = ProgramArgsTextBox.Text;
                    break;
                case "OpenUri":
                    mapping.UriToOpen = UrlInputTextBox.Text;
                    break;
            }

            if (!_isEditMode)
            {
                MouseToKeyMappings.Add(mapping);
            }
            else
            {
                // Force UI refresh by removing and re-adding the item
                int index = MouseToKeyMappings.IndexOf(mapping);
                if (index >= 0)
                {
                    MouseToKeyMappings.RemoveAt(index);
                    MouseToKeyMappings.Insert(index, mapping);
                }
            }

            SaveMappings();
        }

        private void DeleteMouseToKeyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MouseMapping mapping)
            {
                MouseToKeyMappings.Remove(mapping);
                SaveMappings();
            }
        }

        // Key to Mouse handlers
        private async void NewKeyToMouseBtn_Click(object sender, RoutedEventArgs e)
        {
            _isEditMode = false;
            _editingKeyToMouseMapping = null;
            ResetKeyToMouseDialog();

            // Clear any previous error
            KeyToMouseErrorText.Visibility = Visibility.Collapsed;

            KeyToMouseDialog.PrimaryButtonClick += KeyToMouseDialog_PrimaryButtonClick;
            await KeyToMouseDialog.ShowAsync();
            KeyToMouseDialog.PrimaryButtonClick -= KeyToMouseDialog_PrimaryButtonClick;
        }

        private async void KeyToMouseListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is KeyToMouseMapping mapping)
            {
                _isEditMode = true;
                _editingKeyToMouseMapping = mapping;

                _capturedKeyCode = mapping.OriginalKeyCode;
                _capturedKeyName = mapping.OriginalKeyName;
                KeyToMouseKeyTextBox.Text = mapping.OriginalKeyName;
                TargetMouseButtonComboBox.SelectedIndex = GetMouseButtonIndex(mapping.TargetMouseButton);

                // Populate app-specific checkbox
                KeyToMouseAllAppsCheckBox.IsChecked = mapping.IsAllApps;
                KeyToMouseTargetAppTextBox.Text = mapping.TargetApp;
                KeyToMouseTargetAppTextBox.Visibility = mapping.IsAllApps ? Visibility.Collapsed : Visibility.Visible;

                // Clear any previous error
                KeyToMouseErrorText.Visibility = Visibility.Collapsed;

                KeyToMouseDialog.PrimaryButtonClick += KeyToMouseDialog_PrimaryButtonClick;
                await KeyToMouseDialog.ShowAsync();
                KeyToMouseDialog.PrimaryButtonClick -= KeyToMouseDialog_PrimaryButtonClick;
            }
        }

        private void KeyToMouseDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Validate input
            var (isValid, errorMessage) = ValidateKeyToMouseMapping();
            if (!isValid)
            {
                args.Cancel = true;
                KeyToMouseErrorText.Text = errorMessage;
                KeyToMouseErrorText.Visibility = Visibility.Visible;
                return;
            }

            // Clear any previous error
            KeyToMouseErrorText.Visibility = Visibility.Collapsed;

            var mapping = _isEditMode && _editingKeyToMouseMapping != null
                ? _editingKeyToMouseMapping
                : new KeyToMouseMapping();

            mapping.OriginalKeyCode = _capturedKeyCode;
            mapping.OriginalKeyName = _capturedKeyName;
            mapping.TargetMouseButton = GetMouseButtonName(TargetMouseButtonComboBox.SelectedIndex);

            // Set app-specific target
            mapping.TargetApp = KeyToMouseAllAppsCheckBox.IsChecked == true
                ? string.Empty
                : KeyToMouseTargetAppTextBox.Text.Trim();

            if (!_isEditMode)
            {
                KeyToMouseMappings.Add(mapping);
            }
            else
            {
                // Force UI refresh by removing and re-adding the item
                int index = KeyToMouseMappings.IndexOf(mapping);
                if (index >= 0)
                {
                    KeyToMouseMappings.RemoveAt(index);
                    KeyToMouseMappings.Insert(index, mapping);
                }
            }

            SaveMappings();
        }

        private void DeleteKeyToMouseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is KeyToMouseMapping mapping)
            {
                KeyToMouseMappings.Remove(mapping);
                SaveMappings();
            }
        }

        // Key capture handlers
        private void KeyInputTextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            e.Handled = true;
            _capturedKeyCode = (int)e.Key;
            _capturedKeyName = GetKeyName(_capturedKeyCode);
            KeyInputTextBox.Text = _capturedKeyName;
        }

        private void KeyToMouseKeyTextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            e.Handled = true;
            _capturedKeyCode = (int)e.Key;
            _capturedKeyName = GetKeyName(_capturedKeyCode);
            KeyToMouseKeyTextBox.Text = _capturedKeyName;
        }

        // Target type selection handler
        private void TargetTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTargetInputVisibility();
        }

        private void UpdateTargetInputVisibility()
        {
            if (KeyInputPanel == null)
            {
                return;
            }

            KeyInputPanel.Visibility = Visibility.Collapsed;
            ShortcutInputPanel.Visibility = Visibility.Collapsed;
            TextInputPanel.Visibility = Visibility.Collapsed;
            ProgramInputPanel.Visibility = Visibility.Collapsed;
            UrlInputPanel.Visibility = Visibility.Collapsed;

            switch (TargetTypeComboBox.SelectedIndex)
            {
                case 0:
                    KeyInputPanel.Visibility = Visibility.Visible;
                    break;
                case 1:
                    ShortcutInputPanel.Visibility = Visibility.Visible;
                    break;
                case 2:
                    TextInputPanel.Visibility = Visibility.Visible;
                    break;
                case 3:
                    ProgramInputPanel.Visibility = Visibility.Visible;
                    break;
                case 4:
                    UrlInputPanel.Visibility = Visibility.Visible;
                    break;
            }
        }

        private async void BrowseProgramBtn_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".exe");
            picker.FileTypeFilter.Add("*");

            // Get the window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                ProgramPathTextBox.Text = file.Path;
            }
        }

        // Helper methods
        private void ResetMouseToKeyDialog()
        {
            MouseButtonComboBox.SelectedIndex = 0;
            TargetTypeComboBox.SelectedIndex = 0;
            _capturedKeyCode = 0;
            _capturedKeyName = string.Empty;
            KeyInputTextBox.Text = string.Empty;
            ShortcutInputTextBox.Text = string.Empty;
            TextInputTextBox.Text = string.Empty;
            ProgramPathTextBox.Text = string.Empty;
            ProgramArgsTextBox.Text = string.Empty;
            UrlInputTextBox.Text = string.Empty;
            MouseToKeyAllAppsCheckBox.IsChecked = true;
            MouseToKeyTargetAppTextBox.Text = string.Empty;
            MouseToKeyTargetAppTextBox.Visibility = Visibility.Collapsed;
            UpdateTargetInputVisibility();
        }

        private void ResetKeyToMouseDialog()
        {
            _capturedKeyCode = 0;
            _capturedKeyName = string.Empty;
            KeyToMouseKeyTextBox.Text = string.Empty;
            TargetMouseButtonComboBox.SelectedIndex = 0;
            KeyToMouseAllAppsCheckBox.IsChecked = true;
            KeyToMouseTargetAppTextBox.Text = string.Empty;
            KeyToMouseTargetAppTextBox.Visibility = Visibility.Collapsed;
        }

        private void MouseToKeyAllAppsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (MouseToKeyTargetAppTextBox != null)
            {
                MouseToKeyTargetAppTextBox.Visibility = MouseToKeyAllAppsCheckBox.IsChecked == true
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        private void KeyToMouseAllAppsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (KeyToMouseTargetAppTextBox != null)
            {
                KeyToMouseTargetAppTextBox.Visibility = KeyToMouseAllAppsCheckBox.IsChecked == true
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        private static int GetMouseButtonIndex(string buttonName)
        {
            return buttonName switch
            {
                "Left" => 0,
                "Right" => 1,
                "Middle" => 2,
                "X1" => 3,
                "X2" => 4,
                "ScrollUp" => 5,
                "ScrollDown" => 6,
                _ => 0,
            };
        }

        private static string GetMouseButtonName(int index)
        {
            return index switch
            {
                0 => "Left",
                1 => "Right",
                2 => "Middle",
                3 => "X1",
                4 => "X2",
                5 => "ScrollUp",
                6 => "ScrollDown",
                _ => "Left",
            };
        }

        private static int GetTargetTypeIndex(string typeName)
        {
            return typeName switch
            {
                "Key" => 0,
                "Shortcut" => 1,
                "Text" => 2,
                "RunProgram" => 3,
                "OpenUri" => 4,
                _ => 0,
            };
        }

        private static string GetTargetTypeName(int index)
        {
            return index switch
            {
                0 => "Key",
                1 => "Shortcut",
                2 => "Text",
                3 => "RunProgram",
                4 => "OpenUri",
                _ => "Key",
            };
        }

        // Validation methods
        private (bool IsValid, string ErrorMessage) ValidateMouseToKeyMapping()
        {
            string targetType = GetTargetTypeName(TargetTypeComboBox.SelectedIndex);
            string mouseButton = GetMouseButtonName(MouseButtonComboBox.SelectedIndex);

            // Get the target app from the dialog
            string targetApp = MouseToKeyAllAppsCheckBox.IsChecked == true
                ? string.Empty
                : MouseToKeyTargetAppTextBox.Text.Trim().ToLowerInvariant();

            // Check for duplicate mouse button mapping for the same app scope
            // A button can have one global remap AND separate app-specific remaps
            // But not two global remaps or two remaps for the same app
            if (!_isEditMode ||
                (_editingMouseMapping != null &&
                 (_editingMouseMapping.OriginalButton != mouseButton ||
                  !string.Equals(_editingMouseMapping.TargetApp ?? string.Empty, targetApp, StringComparison.OrdinalIgnoreCase))))
            {
                bool isDuplicate = MouseToKeyMappings.Any(m =>
                    m.OriginalButton == mouseButton &&
                    string.Equals(m.TargetApp ?? string.Empty, targetApp, StringComparison.OrdinalIgnoreCase) &&
                    m != _editingMouseMapping);

                if (isDuplicate)
                {
                    if (string.IsNullOrEmpty(targetApp))
                    {
                        return (false, $"A global remapping for {mouseButton} mouse button already exists.");
                    }
                    else
                    {
                        return (false, $"A remapping for {mouseButton} mouse button already exists for application '{targetApp}'.");
                    }
                }
            }

            switch (targetType)
            {
                case "Key":
                    if (_capturedKeyCode == 0)
                    {
                        return (false, "Please capture a key by clicking the key input box and pressing a key.");
                    }

                    break;

                case "Shortcut":
                    if (string.IsNullOrWhiteSpace(ShortcutInputTextBox.Text))
                    {
                        return (false, "Please enter shortcut key codes (e.g., 162;67 for Ctrl+C).");
                    }

                    // Validate format: should be semicolon-separated numbers
                    string[] parts = ShortcutInputTextBox.Text.Split(';');
                    foreach (string part in parts)
                    {
                        if (!int.TryParse(part.Trim(), out _))
                        {
                            return (false, $"Invalid shortcut format. '{part}' is not a valid key code.");
                        }
                    }

                    break;

                case "Text":
                    if (string.IsNullOrEmpty(TextInputTextBox.Text))
                    {
                        return (false, "Please enter text to type when the mouse button is pressed.");
                    }

                    break;

                case "RunProgram":
                    if (string.IsNullOrWhiteSpace(ProgramPathTextBox.Text))
                    {
                        return (false, "Please enter a program path.");
                    }

                    // Warn but don't block if file doesn't exist (could be in PATH)
                    break;

                case "OpenUri":
                    if (string.IsNullOrWhiteSpace(UrlInputTextBox.Text))
                    {
                        return (false, "Please enter a URL to open.");
                    }

                    // Validate URL format
                    string url = UrlInputTextBox.Text.Trim();
                    if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                        !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                        !url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                    {
                        return (false, "URL should start with http://, https://, or file://");
                    }

                    if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                    {
                        return (false, "Invalid URL format.");
                    }

                    break;
            }

            return (true, string.Empty);
        }

        private (bool IsValid, string ErrorMessage) ValidateKeyToMouseMapping()
        {
            if (_capturedKeyCode == 0)
            {
                return (false, "Please capture a key by clicking the key input box and pressing a key.");
            }

            // Get the target app from the dialog
            string targetApp = KeyToMouseAllAppsCheckBox.IsChecked == true
                ? string.Empty
                : KeyToMouseTargetAppTextBox.Text.Trim().ToLowerInvariant();

            // Check for duplicate key mapping for the same app scope
            // A key can have one global remap AND separate app-specific remaps
            // But not two global remaps or two remaps for the same app
            if (!_isEditMode ||
                (_editingKeyToMouseMapping != null &&
                 (_editingKeyToMouseMapping.OriginalKeyCode != _capturedKeyCode ||
                  !string.Equals(_editingKeyToMouseMapping.TargetApp ?? string.Empty, targetApp, StringComparison.OrdinalIgnoreCase))))
            {
                bool isDuplicate = KeyToMouseMappings.Any(m =>
                    m.OriginalKeyCode == _capturedKeyCode &&
                    string.Equals(m.TargetApp ?? string.Empty, targetApp, StringComparison.OrdinalIgnoreCase) &&
                    m != _editingKeyToMouseMapping);

                if (isDuplicate)
                {
                    if (string.IsNullOrEmpty(targetApp))
                    {
                        return (false, $"A global remapping for key '{_capturedKeyName}' already exists.");
                    }
                    else
                    {
                        return (false, $"A remapping for key '{_capturedKeyName}' already exists for application '{targetApp}'.");
                    }
                }
            }

            return (true, string.Empty);
        }
    }
}
