// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
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
                if (root.TryGetProperty("remapMouseButtons", out JsonElement mouseButtons) &&
                    mouseButtons.TryGetProperty("inProcess", out JsonElement inProcess))
                {
                    foreach (JsonElement item in inProcess.EnumerateArray())
                    {
                        var mapping = new MouseMapping();

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

                        MouseToKeyMappings.Add(mapping);
                    }
                }

                // Load key-to-mouse remappings
                if (root.TryGetProperty("remapKeysToMouse", out JsonElement keysToMouse) &&
                    keysToMouse.TryGetProperty("inProcess", out JsonElement k2mInProcess))
                {
                    foreach (JsonElement item in k2mInProcess.EnumerateArray())
                    {
                        var mapping = new KeyToMouseMapping();

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

                        KeyToMouseMappings.Add(mapping);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load mouse mappings: {ex.Message}");
            }
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

                // Write mouse button remappings
                writer.WritePropertyName("remapMouseButtons");
                writer.WriteStartObject();
                writer.WritePropertyName("inProcess");
                writer.WriteStartArray();

                foreach (var mapping in MouseToKeyMappings)
                {
                    writer.WriteStartObject();
                    writer.WriteString("originalButton", mapping.OriginalButton);

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

                writer.WriteEndArray();
                writer.WriteEndObject();

                // Write key-to-mouse remappings
                writer.WritePropertyName("remapKeysToMouse");
                writer.WriteStartObject();
                writer.WritePropertyName("inProcess");
                writer.WriteStartArray();

                foreach (var mapping in KeyToMouseMappings)
                {
                    writer.WriteStartObject();
                    writer.WriteString("originalKeys", mapping.OriginalKeyCode.ToString(CultureInfo.InvariantCulture));
                    writer.WriteString("targetMouseButton", mapping.TargetMouseButton);
                    writer.WriteEndObject();
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

        private static void SignalSettingsChanged()
        {
            IntPtr hEvent = CreateEvent(IntPtr.Zero, false, false, SettingsEventName);
            if (hEvent != IntPtr.Zero)
            {
                SetEvent(hEvent);
                CloseHandle(hEvent);
                Logger.LogInfo($"Signaled {SettingsEventName} event");
            }
            else
            {
                Logger.LogError($"Failed to create {SettingsEventName} event");
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

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

        private void MouseToKeyDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var mapping = _isEditMode && _editingMouseMapping != null
                ? _editingMouseMapping
                : new MouseMapping();

            mapping.OriginalButton = GetMouseButtonName(MouseButtonComboBox.SelectedIndex);
            mapping.TargetType = GetTargetTypeName(TargetTypeComboBox.SelectedIndex);

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

                KeyToMouseDialog.PrimaryButtonClick += KeyToMouseDialog_PrimaryButtonClick;
                await KeyToMouseDialog.ShowAsync();
                KeyToMouseDialog.PrimaryButtonClick -= KeyToMouseDialog_PrimaryButtonClick;
            }
        }

        private void KeyToMouseDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var mapping = _isEditMode && _editingKeyToMouseMapping != null
                ? _editingKeyToMouseMapping
                : new KeyToMouseMapping();

            mapping.OriginalKeyCode = _capturedKeyCode;
            mapping.OriginalKeyName = _capturedKeyName;
            mapping.TargetMouseButton = GetMouseButtonName(TargetMouseButtonComboBox.SelectedIndex);

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
            UpdateTargetInputVisibility();
        }

        private void ResetKeyToMouseDialog()
        {
            _capturedKeyCode = 0;
            _capturedKeyName = string.Empty;
            KeyToMouseKeyTextBox.Text = string.Empty;
            TargetMouseButtonComboBox.SelectedIndex = 0;
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
    }
}
