// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using KeyboardManagerEditorUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;
using static KeyboardManagerEditorUI.Interop.ShortcutKeyMapping;

#pragma warning disable SA1124 // Do not use regions

namespace KeyboardManagerEditorUI.Controls
{
    /// <summary>
    /// Unified control that consolidates all mapping input types:
    /// - Key/Shortcut remapping (InputControl)
    /// - Text output (TextPageInputControl)
    /// - URL opening (UrlPageInputControl)
    /// - App launching (AppPageInputControl)
    /// </summary>
    public sealed partial class UnifiedMappingControl : UserControl, IDisposable, IKeyboardHookTarget
    {
        #region Fields

        private readonly ObservableCollection<string> _triggerKeys = new();
        private readonly ObservableCollection<string> _actionKeys = new();

        private bool _disposed;
        private bool _internalUpdate;

        private KeyInputMode _currentInputMode = KeyInputMode.OriginalKeys;

        #endregion

        #region Enums

        /// <summary>
        /// Defines the type of trigger for the mapping.
        /// </summary>
        public enum TriggerType
        {
            KeyOrShortcut,
            Mouse,
        }

        /// <summary>
        /// Defines the type of action to perform.
        /// </summary>
        public enum ActionType
        {
            KeyOrShortcut,
            Text,
            OpenUrl,
            OpenApp,
            MouseClick,
        }

        /// <summary>
        /// Defines the mouse button options.
        /// </summary>
        public enum MouseButton
        {
            LeftMouse,
            RightMouse,
            ScrollUp,
            ScrollDown,
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current trigger type.
        /// </summary>
        public TriggerType CurrentTriggerType
        {
            get
            {
                if (TriggerTypeComboBox?.SelectedItem is ComboBoxItem item)
                {
                    return item.Tag?.ToString() switch
                    {
                        "Mouse" => TriggerType.Mouse,
                        _ => TriggerType.KeyOrShortcut,
                    };
                }

                return TriggerType.KeyOrShortcut;
            }
        }

        /// <summary>
        /// Gets the current action type.
        /// </summary>
        public ActionType CurrentActionType
        {
            get
            {
                if (ActionTypeComboBox?.SelectedItem is ComboBoxItem item)
                {
                    return item.Tag?.ToString() switch
                    {
                        "Text" => ActionType.Text,
                        "OpenUrl" => ActionType.OpenUrl,
                        "OpenApp" => ActionType.OpenApp,
                        "MouseClick" => ActionType.MouseClick,
                        _ => ActionType.KeyOrShortcut,
                    };
                }

                return ActionType.KeyOrShortcut;
            }
        }

        #endregion

        #region Constructor

        public UnifiedMappingControl()
        {
            this.InitializeComponent();

            TriggerKeys.ItemsSource = _triggerKeys;
            ActionKeys.ItemsSource = _actionKeys;

            this.Unloaded += UnifiedMappingControl_Unloaded;
        }

        #endregion

        #region Lifecycle Events

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Set up event handlers for app-specific checkbox
            AppSpecificCheckBox.Checked += AppSpecificCheckBox_Changed;
            AppSpecificCheckBox.Unchecked += AppSpecificCheckBox_Changed;

            // Activate keyboard hook for the trigger input
            if (TriggerKeyToggleBtn.IsChecked == true)
            {
                _currentInputMode = KeyInputMode.OriginalKeys;
                KeyboardHookHelper.Instance.ActivateHook(this);
            }
        }

        private void UnifiedMappingControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Reset();
            CleanupKeyboardHook();
        }

        #endregion

        #region Trigger Type Handling

        private void TriggerTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TriggerTypeComboBox?.SelectedItem is ComboBoxItem item)
            {
                string? tag = item.Tag?.ToString();

                // Cleanup keyboard hook when switching to mouse
                if (tag == "Mouse")
                {
                    CleanupKeyboardHook();
                    UncheckAllToggleButtons();

                    // Hide MouseClick action option - can't map mouse to mouse
                    SetMouseClickActionVisibility(false);
                }
                else
                {
                    // Show MouseClick action option for keyboard triggers
                    SetMouseClickActionVisibility(true);
                }
            }
        }

        /// <summary>
        /// Shows or hides the MouseClick action option in the ActionTypeComboBox.
        /// </summary>
        private void SetMouseClickActionVisibility(bool visible)
        {
            if (ActionTypeComboBox == null)
            {
                return;
            }

            foreach (var comboItem in ActionTypeComboBox.Items)
            {
                if (comboItem is ComboBoxItem cbi && cbi.Tag?.ToString() == "MouseClick")
                {
                    cbi.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

                    // If currently selected and hiding, switch to KeyOrShortcut
                    if (!visible && ActionTypeComboBox.SelectedItem == (object)cbi)
                    {
                        ActionTypeComboBox.SelectedIndex = 0;
                    }

                    break;
                }
            }
        }

        private void TriggerKeyToggleBtn_Checked(object sender, RoutedEventArgs e)
        {
            if (TriggerKeyToggleBtn.IsChecked == true)
            {
                _currentInputMode = KeyInputMode.OriginalKeys;

                // Uncheck action toggle if checked
                if (ActionKeyToggleBtn?.IsChecked == true)
                {
                    ActionKeyToggleBtn.IsChecked = false;
                }

                KeyboardHookHelper.Instance.ActivateHook(this);
            }
        }

        private void TriggerKeyToggleBtn_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_currentInputMode == KeyInputMode.OriginalKeys)
            {
                CleanupKeyboardHook();
            }
        }

        #endregion

        #region Action Type Handling

        private void ActionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ActionTypeComboBox?.SelectedItem is ComboBoxItem item)
            {
                string? tag = item.Tag?.ToString();

                // Cleanup keyboard hook when switching away from key/shortcut
                if (tag != "KeyOrShortcut")
                {
                    if (_currentInputMode == KeyInputMode.RemappedKeys)
                    {
                        CleanupKeyboardHook();
                    }

                    if (ActionKeyToggleBtn?.IsChecked == true)
                    {
                        ActionKeyToggleBtn.IsChecked = false;
                    }
                }
            }
        }

        private void ActionKeyToggleBtn_Checked(object sender, RoutedEventArgs e)
        {
            if (ActionKeyToggleBtn.IsChecked == true)
            {
                _currentInputMode = KeyInputMode.RemappedKeys;

                // Uncheck trigger toggle if checked
                if (TriggerKeyToggleBtn?.IsChecked == true)
                {
                    TriggerKeyToggleBtn.IsChecked = false;
                }

                KeyboardHookHelper.Instance.ActivateHook(this);
            }
        }

        private void ActionKeyToggleBtn_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_currentInputMode == KeyInputMode.RemappedKeys)
            {
                CleanupKeyboardHook();
            }
        }

        #endregion

        #region App-Specific Handling

        private void AppSpecificCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_internalUpdate)
            {
                return;
            }

            CleanupKeyboardHook();
            UncheckAllToggleButtons();

            AppNameTextBox.Visibility = AppSpecificCheckBox.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateAppSpecificCheckBoxState()
        {
            // Only enable app-specific remapping for shortcuts (multiple keys)
            bool isShortcut = _triggerKeys.Count > 1;

            try
            {
                _internalUpdate = true;

                AppSpecificCheckBox.IsEnabled = isShortcut;
                if (!isShortcut)
                {
                    AppSpecificCheckBox.IsChecked = false;
                    AppNameTextBox.Visibility = Visibility.Collapsed;
                }
            }
            finally
            {
                _internalUpdate = false;
            }
        }

        #endregion

        #region TextBox Focus Handlers

        private void AppNameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            CleanupKeyboardHook();
            UncheckAllToggleButtons();
        }

        private void TextContentBox_GotFocus(object sender, RoutedEventArgs e)
        {
            CleanupKeyboardHook();
            UncheckAllToggleButtons();
        }

        private void UrlPathInput_GotFocus(object sender, RoutedEventArgs e)
        {
            CleanupKeyboardHook();
            UncheckAllToggleButtons();
        }

        private void ProgramPathInput_GotFocus(object sender, RoutedEventArgs e)
        {
            CleanupKeyboardHook();
            UncheckAllToggleButtons();
        }

        private void ProgramArgsInput_GotFocus(object sender, RoutedEventArgs e)
        {
            CleanupKeyboardHook();
            UncheckAllToggleButtons();
        }

        private void StartInPathInput_GotFocus(object sender, RoutedEventArgs e)
        {
            CleanupKeyboardHook();
            UncheckAllToggleButtons();
        }

        #endregion

        #region File/Folder Pickers

        private async void ProgramPathSelectButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();

            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            picker.FileTypeFilter.Add(".exe");

            StorageFile file = await picker.PickSingleFileAsync();

            if (file != null)
            {
                ProgramPathInput.Text = file.Path;
            }
        }

        private async void StartInSelectButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();

            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            picker.FileTypeFilter.Add("*");

            StorageFolder folder = await picker.PickSingleFolderAsync();

            if (folder != null)
            {
                StartInPathInput.Text = folder.Path;
            }
        }

        #endregion

        #region IKeyboardHookTarget Implementation

        public void OnKeyDown(VirtualKey key, List<string> formattedKeys)
        {
            if (_currentInputMode == KeyInputMode.OriginalKeys)
            {
                _triggerKeys.Clear();
                foreach (var keyName in formattedKeys)
                {
                    _triggerKeys.Add(keyName);
                }

                UpdateAppSpecificCheckBoxState();
            }
            else if (_currentInputMode == KeyInputMode.RemappedKeys)
            {
                _actionKeys.Clear();
                foreach (var keyName in formattedKeys)
                {
                    _actionKeys.Add(keyName);
                }
            }
        }

        public void ClearKeys()
        {
            if (_currentInputMode == KeyInputMode.OriginalKeys)
            {
                _triggerKeys.Clear();
            }
            else
            {
                _actionKeys.Clear();
            }
        }

        public void OnInputLimitReached()
        {
            ShowNotificationTip("Shortcuts can only have up to 4 modifier keys");
        }

        #endregion

        #region Public API - Getters

        /// <summary>
        /// Gets the trigger keys.
        /// </summary>
        public List<string> GetTriggerKeys() => _triggerKeys.ToList();

        /// <summary>
        /// Gets the action keys (for Key/Shortcut action type).
        /// </summary>
        public List<string> GetActionKeys() => _actionKeys.ToList();

        /// <summary>
        /// Gets the selected mouse trigger button code.
        /// </summary>
        public int? GetMouseTriggerButtonCode()
        {
            if (MouseTriggerComboBox?.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Tag?.ToString(), out int buttonCode))
            {
                return buttonCode;
            }

            return null;
        }

        /// <summary>
        /// Gets the selected mouse action button code (for MouseClick action type).
        /// </summary>
        public int? GetMouseActionButtonCode()
        {
            if (MouseActionComboBox?.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Tag?.ToString(), out int buttonCode))
            {
                return buttonCode;
            }

            return null;
        }

        /// <summary>
        /// Gets the text content (for Text action type).
        /// </summary>
        public string GetTextContent() => TextContentBox?.Text ?? string.Empty;

        /// <summary>
        /// Gets the URL (for OpenUrl action type).
        /// </summary>
        public string GetUrl() => UrlPathInput?.Text ?? string.Empty;

        /// <summary>
        /// Gets the program path (for OpenApp action type).
        /// </summary>
        public string GetProgramPath() => ProgramPathInput?.Text ?? string.Empty;

        /// <summary>
        /// Gets the program arguments (for OpenApp action type).
        /// </summary>
        public string GetProgramArgs() => ProgramArgsInput?.Text ?? string.Empty;

        /// <summary>
        /// Gets the start-in directory (for OpenApp action type).
        /// </summary>
        public string GetStartInDirectory() => StartInPathInput?.Text ?? string.Empty;

        /// <summary>
        /// Gets whether the mapping is app-specific.
        /// </summary>
        public bool GetIsAppSpecific()
        {
            return AppSpecificCheckBox?.IsChecked ?? false;
        }

        /// <summary>
        /// Gets the app name for app-specific mappings.
        /// </summary>
        public string GetAppName()
        {
            return GetIsAppSpecific() ? (AppNameTextBox?.Text ?? string.Empty) : string.Empty;
        }

        /// <summary>
        /// Gets the elevation level (for OpenApp action type).
        /// </summary>
        public ElevationLevel GetElevationLevel() => (ElevationLevel)(ElevationComboBox?.SelectedIndex ?? 0);

        /// <summary>
        /// Gets the window visibility (for OpenApp action type).
        /// </summary>
        public StartWindowType GetVisibility() => (StartWindowType)(VisibilityComboBox?.SelectedIndex ?? 0);

        /// <summary>
        /// Gets the if-running action (for OpenApp action type).
        /// </summary>
        public ProgramAlreadyRunningAction GetIfRunningAction() => (ProgramAlreadyRunningAction)(IfRunningComboBox?.SelectedIndex ?? 0);

        #endregion

        #region Public API - Setters

        /// <summary>
        /// Sets the trigger keys.
        /// </summary>
        public void SetTriggerKeys(List<string> keys)
        {
            _triggerKeys.Clear();
            if (keys != null)
            {
                foreach (var key in keys)
                {
                    _triggerKeys.Add(key);
                }
            }

            UpdateAppSpecificCheckBoxState();
        }

        /// <summary>
        /// Sets the action keys.
        /// </summary>
        public void SetActionKeys(List<string> keys)
        {
            _actionKeys.Clear();
            if (keys != null)
            {
                foreach (var key in keys)
                {
                    _actionKeys.Add(key);
                }
            }
        }

        /// <summary>
        /// Sets the action type.
        /// </summary>
        public void SetActionType(ActionType actionType)
        {
            int index = actionType switch
            {
                ActionType.Text => 1,
                ActionType.OpenUrl => 2,
                ActionType.OpenApp => 3,
                ActionType.MouseClick => 4,
                _ => 0,
            };

            if (ActionTypeComboBox != null)
            {
                ActionTypeComboBox.SelectedIndex = index;
            }
        }

        /// <summary>
        /// Sets the text content (for Text action type).
        /// </summary>
        public void SetTextContent(string text)
        {
            if (TextContentBox != null)
            {
                TextContentBox.Text = text;
            }
        }

        /// <summary>
        /// Sets the URL (for OpenUrl action type).
        /// </summary>
        public void SetUrl(string url)
        {
            if (UrlPathInput != null)
            {
                UrlPathInput.Text = url;
            }
        }

        /// <summary>
        /// Sets the program path (for OpenApp action type).
        /// </summary>
        public void SetProgramPath(string path)
        {
            if (ProgramPathInput != null)
            {
                ProgramPathInput.Text = path;
            }
        }

        /// <summary>
        /// Sets the program arguments (for OpenApp action type).
        /// </summary>
        public void SetProgramArgs(string args)
        {
            if (ProgramArgsInput != null)
            {
                ProgramArgsInput.Text = args;
            }
        }

        /// <summary>
        /// Sets the start-in directory (for OpenApp action type).
        /// </summary>
        public void SetStartInDirectory(string path)
        {
            if (StartInPathInput != null)
            {
                StartInPathInput.Text = path;
            }
        }

        /// <summary>
        /// Sets the elevation level (for OpenApp action type).
        /// </summary>
        public void SetElevationLevel(ElevationLevel elevationLevel)
        {
            if (ElevationComboBox != null)
            {
                ElevationComboBox.SelectedIndex = (int)elevationLevel;
            }
        }

        /// <summary>
        /// Sets the window visibility (for OpenApp action type).
        /// </summary>
        public void SetVisibility(StartWindowType visibility)
        {
            if (VisibilityComboBox != null)
            {
                VisibilityComboBox.SelectedIndex = (int)visibility;
            }
        }

        /// <summary>
        /// Sets the if-already-running action (for OpenApp action type).
        /// </summary>
        public void SetIfRunningAction(ProgramAlreadyRunningAction ifRunningAction)
        {
            if (IfRunningComboBox != null)
            {
                IfRunningComboBox.SelectedIndex = (int)ifRunningAction;
            }
        }

        /// <summary>
        /// Sets whether the mapping is app-specific.
        /// </summary>
        public void SetAppSpecific(bool isAppSpecific, string appName)
        {
            if (AppSpecificCheckBox != null)
            {
                AppSpecificCheckBox.IsChecked = isAppSpecific;
                if (isAppSpecific && AppNameTextBox != null)
                {
                    AppNameTextBox.Text = appName;
                    AppNameTextBox.Visibility = Visibility.Visible;
                }
            }
        }

        #endregion

        #region Helper Methods

        private void UncheckAllToggleButtons()
        {
            if (TriggerKeyToggleBtn?.IsChecked == true)
            {
                TriggerKeyToggleBtn.IsChecked = false;
            }

            if (ActionKeyToggleBtn?.IsChecked == true)
            {
                ActionKeyToggleBtn.IsChecked = false;
            }
        }

        private void CleanupKeyboardHook()
        {
            KeyboardHookHelper.Instance.CleanupHook();
        }

        /// <summary>
        /// Resets all inputs to their default state.
        /// </summary>
        public void Reset()
        {
            _triggerKeys.Clear();
            _actionKeys.Clear();

            UncheckAllToggleButtons();

            _currentInputMode = KeyInputMode.OriginalKeys;

            // Hide any validation messages
            HideValidationMessage();

            // Reset combo boxes
            if (TriggerTypeComboBox != null)
            {
                TriggerTypeComboBox.SelectedIndex = 0;
            }

            if (ActionTypeComboBox != null)
            {
                ActionTypeComboBox.SelectedIndex = 0;
            }

            if (MouseTriggerComboBox != null)
            {
                MouseTriggerComboBox.SelectedIndex = -1;
            }

            // Reset text inputs
            if (TextContentBox != null)
            {
                TextContentBox.Text = string.Empty;
            }

            if (UrlPathInput != null)
            {
                UrlPathInput.Text = string.Empty;
            }

            if (ProgramPathInput != null)
            {
                ProgramPathInput.Text = string.Empty;
            }

            if (ProgramArgsInput != null)
            {
                ProgramArgsInput.Text = string.Empty;
            }

            if (StartInPathInput != null)
            {
                StartInPathInput.Text = string.Empty;
            }

            if (AppNameTextBox != null)
            {
                AppNameTextBox.Text = string.Empty;
                AppNameTextBox.Visibility = Visibility.Collapsed;
            }

            // Reset checkboxes
            if (AppSpecificCheckBox != null)
            {
                AppSpecificCheckBox.IsChecked = false;
                AppSpecificCheckBox.IsEnabled = false;
            }

            // Reset app combo boxes
            if (ElevationComboBox != null)
            {
                ElevationComboBox.SelectedIndex = 0;
            }

            if (IfRunningComboBox != null)
            {
                IfRunningComboBox.SelectedIndex = 0;
            }

            if (VisibilityComboBox != null)
            {
                VisibilityComboBox.SelectedIndex = 0;
            }

            HideValidationMessage();
        }

        /// <summary>
        /// Resets only the toggle buttons without clearing the key displays.
        /// </summary>
        public void ResetToggleButtons()
        {
            UncheckAllToggleButtons();
        }

        #endregion

        #region Notifications

        /// <summary>
        /// Shows a warning notification in the InfoBar.
        /// </summary>
        public void ShowNotificationTip(string message)
        {
            ShowValidationMessage("Warning", message, InfoBarSeverity.Warning);
        }

        /// <summary>
        /// Shows an error in the InfoBar with title and message.
        /// </summary>
        public void ShowValidationError(string title, string message)
        {
            ShowValidationMessage(title, message, InfoBarSeverity.Error);
        }

        /// <summary>
        /// Shows a validation error based on the error type.
        /// </summary>
        public void ShowValidationErrorFromType(ValidationErrorType errorType)
        {
            if (ValidationHelper.ValidationMessages.TryGetValue(errorType, out var messageInfo))
            {
                ShowValidationError(messageInfo.Title, messageInfo.Message);
            }
            else
            {
                ShowValidationError("Validation Error", "An unknown validation error occurred.");
            }
        }

        /// <summary>
        /// Shows a message in the InfoBar with the specified severity.
        /// </summary>
        private void ShowValidationMessage(string title, string message, InfoBarSeverity severity)
        {
            if (ValidationInfoBar != null)
            {
                ValidationInfoBar.Title = title;
                ValidationInfoBar.Message = message;
                ValidationInfoBar.Severity = severity;
                ValidationInfoBar.IsOpen = true;
            }
        }

        /// <summary>
        /// Hides the validation InfoBar.
        /// </summary>
        public void HideValidationMessage()
        {
            if (ValidationInfoBar != null)
            {
                ValidationInfoBar.IsOpen = false;
            }
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
                    CleanupKeyboardHook();
                    HideValidationMessage();
                }

                _disposed = true;
            }
        }

        #endregion
    }
}

#pragma warning restore SA1124 // Do not use regions
