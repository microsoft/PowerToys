// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using KeyboardManagerEditorUI.Helpers;
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
        /// The list of mouse-to-key mappings.
        /// </summary>
        public ObservableCollection<MouseMapping> MouseToKeyMappings { get; } = new ObservableCollection<MouseMapping>();

        /// <summary>
        /// The list of key-to-mouse mappings.
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
                // Load from the configuration file
                string configPath = GetConfigFilePath();
                if (!File.Exists(configPath))
                {
                    return;
                }

                string json = File.ReadAllText(configPath);
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                // Load mouse button remappings
                if (root.TryGetProperty("remapMouseButtons", out JsonElement mouseButtons))
                {
                    // Try new format first (global array), fall back to old format (inProcess)
                    JsonElement globalArray;
                    if (mouseButtons.TryGetProperty("global", out globalArray) ||
                        mouseButtons.TryGetProperty("inProcess", out globalArray))
                    {
                        foreach (JsonElement item in globalArray.EnumerateArray())
                        {
                            var mapping = ParseMouseMapping(item, string.Empty);
                            if (mapping != null)
                            {
                                MouseToKeyMappings.Add(mapping);
                            }
                        }
                    }

                    // Load app-specific mouse button remaps
                    if (mouseButtons.TryGetProperty("appSpecific", out JsonElement appSpecificArray))
                    {
                        foreach (JsonElement item in appSpecificArray.EnumerateArray())
                        {
                            string targetApp = string.Empty;
                            if (item.TryGetProperty("targetApp", out JsonElement targetAppElem))
                            {
                                targetApp = targetAppElem.GetString() ?? string.Empty;
                            }

                            var mapping = ParseMouseMapping(item, targetApp);
                            if (mapping != null)
                            {
                                MouseToKeyMappings.Add(mapping);
                            }
                        }
                    }
                }

                // Load key-to-mouse remappings
                if (root.TryGetProperty("remapKeysToMouse", out JsonElement keysToMouse))
                {
                    // Try new format first (global array), fall back to old format (inProcess)
                    JsonElement globalArray;
                    if (keysToMouse.TryGetProperty("global", out globalArray) ||
                        keysToMouse.TryGetProperty("inProcess", out globalArray))
                    {
                        foreach (JsonElement item in globalArray.EnumerateArray())
                        {
                            var mapping = ParseKeyToMouseMapping(item, string.Empty);
                            if (mapping != null)
                            {
                                KeyToMouseMappings.Add(mapping);
                            }
                        }
                    }

                    // Load app-specific key to mouse remaps
                    if (keysToMouse.TryGetProperty("appSpecific", out JsonElement appSpecificArray))
                    {
                        foreach (JsonElement item in appSpecificArray.EnumerateArray())
                        {
                            string targetApp = string.Empty;
                            if (item.TryGetProperty("targetApp", out JsonElement targetAppElem))
                            {
                                targetApp = targetAppElem.GetString() ?? string.Empty;
                            }

                            var mapping = ParseKeyToMouseMapping(item, targetApp);
                            if (mapping != null)
                            {
                                KeyToMouseMappings.Add(mapping);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load mouse mappings: {ex.Message}");
            }
        }

        private static MouseMapping? ParseMouseMapping(JsonElement item, string targetApp)
        {
            var mapping = new MouseMapping { TargetApp = targetApp };

            if (item.TryGetProperty("originalButton", out JsonElement origBtn))
            {
                mapping.OriginalButton = origBtn.GetString() ?? string.Empty;
            }

            if (item.TryGetProperty("newRemapKeys", out JsonElement newKeys))
            {
                string keysStr = newKeys.GetString() ?? string.Empty;
                if (keysStr.Contains(';'))
                {
                    mapping.TargetType = "Shortcut";
                    mapping.TargetShortcutKeys = keysStr;
                }
                else if (int.TryParse(keysStr, out int keyCode))
                {
                    mapping.TargetType = "Key";
                    mapping.TargetKeyCode = keyCode;
                    mapping.TargetKeyName = GetKeyName(keyCode);
                }
            }
            else if (item.TryGetProperty("unicodeText", out JsonElement newStr))
            {
                mapping.TargetType = "Text";
                mapping.TargetText = newStr.GetString() ?? string.Empty;
            }
            else if (item.TryGetProperty("runProgramFilePath", out JsonElement progPath))
            {
                mapping.TargetType = "RunProgram";
                mapping.ProgramPath = progPath.GetString() ?? string.Empty;
                if (item.TryGetProperty("runProgramArgs", out JsonElement progArgs))
                {
                    mapping.ProgramArgs = progArgs.GetString() ?? string.Empty;
                }
            }
            else if (item.TryGetProperty("openUri", out JsonElement uri))
            {
                mapping.TargetType = "OpenUri";
                mapping.UriToOpen = uri.GetString() ?? string.Empty;
            }

            return mapping;
        }

        private static KeyToMouseMapping? ParseKeyToMouseMapping(JsonElement item, string targetApp)
        {
            var mapping = new KeyToMouseMapping { TargetApp = targetApp };

            if (item.TryGetProperty("originalKeys", out JsonElement origKeys))
            {
                string keysStr = origKeys.GetString() ?? string.Empty;
                if (int.TryParse(keysStr, out int keyCode))
                {
                    mapping.OriginalKeyCode = keyCode;
                    mapping.OriginalKeyName = GetKeyName(keyCode);
                }
            }

            if (item.TryGetProperty("targetMouseButton", out JsonElement targetBtn))
            {
                mapping.TargetMouseButton = targetBtn.GetString() ?? string.Empty;
            }

            return mapping;
        }

        private void SaveMappings()
        {
            try
            {
                string configPath = GetConfigFilePath();
                string json = "{}";

                if (File.Exists(configPath))
                {
                    json = File.ReadAllText(configPath);
                }

                using JsonDocument doc = JsonDocument.Parse(json);
                var options = new JsonWriterOptions { Indented = true };

                using var stream = new MemoryStream();
                using var writer = new Utf8JsonWriter(stream, options);

                writer.WriteStartObject();

                // Copy existing properties except the ones we're updating
                foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Name != "remapMouseButtons" && prop.Name != "remapKeysToMouse")
                    {
                        prop.WriteTo(writer);
                    }
                }

                // Write mouse button remappings with global/appSpecific structure
                writer.WritePropertyName("remapMouseButtons");
                writer.WriteStartObject();

                // Write global mouse remaps
                writer.WritePropertyName("global");
                writer.WriteStartArray();
                foreach (var mapping in MouseToKeyMappings.Where(m => m.IsAllApps))
                {
                    WriteMouseMappingJson(writer, mapping, includeTargetApp: false);
                }

                writer.WriteEndArray();

                // Write app-specific mouse remaps
                writer.WritePropertyName("appSpecific");
                writer.WriteStartArray();
                foreach (var mapping in MouseToKeyMappings.Where(m => !m.IsAllApps))
                {
                    WriteMouseMappingJson(writer, mapping, includeTargetApp: true);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();

                // Write key-to-mouse remappings with global/appSpecific structure
                writer.WritePropertyName("remapKeysToMouse");
                writer.WriteStartObject();

                // Write global key-to-mouse remaps
                writer.WritePropertyName("global");
                writer.WriteStartArray();
                foreach (var mapping in KeyToMouseMappings.Where(m => m.IsAllApps))
                {
                    WriteKeyToMouseMappingJson(writer, mapping, includeTargetApp: false);
                }

                writer.WriteEndArray();

                // Write app-specific key-to-mouse remaps
                writer.WritePropertyName("appSpecific");
                writer.WriteStartArray();
                foreach (var mapping in KeyToMouseMappings.Where(m => !m.IsAllApps))
                {
                    WriteKeyToMouseMappingJson(writer, mapping, includeTargetApp: true);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();

                writer.WriteEndObject();
                writer.Flush();

                string newJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
                File.WriteAllText(configPath, newJson);

                Logger.LogInfo("Mouse mappings saved successfully");

                // Signal the settings event to notify the engine to reload
                SignalSettingsChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save mouse mappings: {ex.Message}");
            }
        }

        private static void WriteMouseMappingJson(Utf8JsonWriter writer, MouseMapping mapping, bool includeTargetApp)
        {
            writer.WriteStartObject();
            writer.WriteString("originalButton", mapping.OriginalButton);

            if (includeTargetApp)
            {
                writer.WriteString("targetApp", mapping.TargetApp);
            }

            switch (mapping.TargetType)
            {
                case "Key":
                    writer.WriteString("newRemapKeys", mapping.TargetKeyCode.ToString(CultureInfo.InvariantCulture));
                    break;
                case "Shortcut":
                    writer.WriteString("newRemapKeys", mapping.TargetShortcutKeys);
                    break;
                case "Text":
                    writer.WriteString("unicodeText", mapping.TargetText);
                    break;
                case "RunProgram":
                    writer.WriteString("runProgramFilePath", mapping.ProgramPath);
                    if (!string.IsNullOrEmpty(mapping.ProgramArgs))
                    {
                        writer.WriteString("runProgramArgs", mapping.ProgramArgs);
                    }

                    break;
                case "OpenUri":
                    writer.WriteString("openUri", mapping.UriToOpen);
                    break;
            }

            writer.WriteEndObject();
        }

        private static void WriteKeyToMouseMappingJson(Utf8JsonWriter writer, KeyToMouseMapping mapping, bool includeTargetApp)
        {
            writer.WriteStartObject();
            writer.WriteString("originalKeys", mapping.OriginalKeyCode.ToString(CultureInfo.InvariantCulture));
            writer.WriteString("targetMouseButton", mapping.TargetMouseButton);

            if (includeTargetApp)
            {
                writer.WriteString("targetApp", mapping.TargetApp);
            }

            writer.WriteEndObject();
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

        private static string GetConfigFilePath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Microsoft", "PowerToys", "Keyboard Manager", "default.json");
        }

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

                MouseToKeyDialog.PrimaryButtonClick += MouseToKeyDialog_PrimaryButtonClick;
                await MouseToKeyDialog.ShowAsync();
                MouseToKeyDialog.PrimaryButtonClick -= MouseToKeyDialog_PrimaryButtonClick;
            }
        }

        private async void MouseToKeyDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Validate input
            var (isValid, errorMessage) = ValidateMouseToKeyMapping();
            if (!isValid)
            {
                args.Cancel = true;
                var errorDialog = new ContentDialog
                {
                    Title = "Validation Error",
                    Content = errorMessage,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot,
                };
                await errorDialog.ShowAsync();
                return;
            }

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

                KeyToMouseDialog.PrimaryButtonClick += KeyToMouseDialog_PrimaryButtonClick;
                await KeyToMouseDialog.ShowAsync();
                KeyToMouseDialog.PrimaryButtonClick -= KeyToMouseDialog_PrimaryButtonClick;
            }
        }

        private async void KeyToMouseDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Validate input
            var (isValid, errorMessage) = ValidateKeyToMouseMapping();
            if (!isValid)
            {
                args.Cancel = true;
                var errorDialog = new ContentDialog
                {
                    Title = "Validation Error",
                    Content = errorMessage,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot,
                };
                await errorDialog.ShowAsync();
                return;
            }

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

            // Check for duplicate mouse button mapping (only if adding new, not editing same button)
            if (!_isEditMode || (_editingMouseMapping != null && _editingMouseMapping.OriginalButton != mouseButton))
            {
                bool isDuplicate = MouseToKeyMappings.Any(m => m.OriginalButton == mouseButton && m != _editingMouseMapping);
                if (isDuplicate)
                {
                    return (false, $"A remapping for {mouseButton} mouse button already exists.");
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

            // Check for duplicate key mapping
            if (!_isEditMode || (_editingKeyToMouseMapping != null && _editingKeyToMouseMapping.OriginalKeyCode != _capturedKeyCode))
            {
                bool isDuplicate = KeyToMouseMappings.Any(m => m.OriginalKeyCode == _capturedKeyCode && m != _editingKeyToMouseMapping);
                if (isDuplicate)
                {
                    return (false, $"A remapping for key code {_capturedKeyCode} already exists.");
                }
            }

            return (true, string.Empty);
        }
    }
}
