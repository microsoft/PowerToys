// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.WinUI;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.Library.Telemetry.Events;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.SettingsXAML.Controls.Dashboard;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.System;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public enum ShortcutControlSource
    {
        SettingsPage,
        ConflictWindow,
    }

    public sealed partial class ShortcutControl : UserControl, IDisposable
    {
        private readonly UIntPtr ignoreKeyEventFlag = (UIntPtr)0x5555;
        private System.Collections.Generic.HashSet<VirtualKey> _modifierKeysOnEntering = new System.Collections.Generic.HashSet<VirtualKey>();
        private bool _enabled;
        private HotkeySettings hotkeySettings;
        private HotkeySettings internalSettings;
        private HotkeySettings lastValidSettings;
        private HotkeySettingsControlHook hook;
        private bool _isActive;
        private bool disposedValue;

        public string Header { get; set; }

        public string Keys { get; set; }

        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register("Enabled", typeof(bool), typeof(ShortcutControl), null);
        public static readonly DependencyProperty HotkeySettingsProperty = DependencyProperty.Register("HotkeySettings", typeof(HotkeySettings), typeof(ShortcutControl), null);
        public static readonly DependencyProperty AllowDisableProperty = DependencyProperty.Register("AllowDisable", typeof(bool), typeof(ShortcutControl), new PropertyMetadata(false, OnAllowDisableChanged));
        public static readonly DependencyProperty HasConflictProperty = DependencyProperty.Register("HasConflict", typeof(bool), typeof(ShortcutControl), new PropertyMetadata(false, OnHasConflictChanged));
        public static readonly DependencyProperty TooltipProperty = DependencyProperty.Register("Tooltip", typeof(string), typeof(ShortcutControl), new PropertyMetadata(null, OnTooltipChanged));
        public static readonly DependencyProperty KeyVisualShouldShowConflictProperty = DependencyProperty.Register("KeyVisualShouldShowConflict", typeof(bool), typeof(ShortcutControl), new PropertyMetadata(false));
        public static readonly DependencyProperty IgnoreConflictProperty = DependencyProperty.Register("IgnoreConflict", typeof(bool), typeof(ShortcutControl), new PropertyMetadata(false));

        // Dependency property to track the source/context of the ShortcutControl
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(ShortcutControlSource), typeof(ShortcutControl), new PropertyMetadata(ShortcutControlSource.SettingsPage));

        private static ResourceLoader resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;

        private static void OnAllowDisableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var me = d as ShortcutControl;
            if (me == null)
            {
                return;
            }

            var description = me.c?.FindDescendant<TextBlock>();
            if (description == null)
            {
                return;
            }

            var newValue = (bool)(e?.NewValue ?? false);

            var text = newValue ? resourceLoader.GetString("Activation_Shortcut_With_Disable_Description") : resourceLoader.GetString("Activation_Shortcut_Description");
            description.Text = text;
        }

        private static void OnHasConflictChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as ShortcutControl;
            if (control == null)
            {
                return;
            }

            control.UpdateKeyVisualStyles();

            // Check if conflict was resolved (had conflict before, no conflict now)
            var oldValue = (bool)(e.OldValue ?? false);
            var newValue = (bool)(e.NewValue ?? false);

            // General conflict resolution telemetry (for all sources)
            if (oldValue && !newValue)
            {
                // Determine the actual source based on the control's context
                var actualSource = DetermineControlSource(control);

                // Conflict was resolved - send general telemetry
                PowerToysTelemetry.Log.WriteEvent(new ShortcutConflictResolvedEvent()
                {
                    Source = actualSource.ToString(),
                });
            }
        }

        private static ShortcutControlSource DetermineControlSource(ShortcutControl control)
        {
            // Walk up the visual tree to find the parent window/container
            DependencyObject parent = control;
            while (parent != null)
            {
                parent = VisualTreeHelper.GetParent(parent);

                // Check if we're in a ShortcutConflictWindow
                if (parent != null && parent.GetType().Name == "ShortcutConflictWindow")
                {
                    return ShortcutControlSource.ConflictWindow;
                }

                if (parent != null && (parent.GetType().Name == "MainWindow" || parent.GetType().Name == "ShellPage"))
                {
                    return ShortcutControlSource.SettingsPage;
                }
            }

            // Fallback to the explicitly set value or default
            return ShortcutControlSource.ConflictWindow;
        }

        private static void OnTooltipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as ShortcutControl;
            if (control == null)
            {
                return;
            }

            control.UpdateTooltip();
        }

        private ShortcutDialogContentControl c = new ShortcutDialogContentControl();
        private ContentDialog shortcutDialog;

        public bool AllowDisable
        {
            get => (bool)GetValue(AllowDisableProperty);
            set => SetValue(AllowDisableProperty, value);
        }

        public bool HasConflict
        {
            get => (bool)GetValue(HasConflictProperty);
            set => SetValue(HasConflictProperty, value);
        }

        public string Tooltip
        {
            get => (string)GetValue(TooltipProperty);
            set => SetValue(TooltipProperty, value);
        }

        public bool KeyVisualShouldShowConflict
        {
            get => (bool)GetValue(KeyVisualShouldShowConflictProperty);
            set => SetValue(KeyVisualShouldShowConflictProperty, value);
        }

        public bool IgnoreConflict
        {
            get => (bool)GetValue(IgnoreConflictProperty);
            set => SetValue(IgnoreConflictProperty, value);
        }

        public ShortcutControlSource Source
        {
            get => (ShortcutControlSource)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public bool Enabled
        {
            get
            {
                return _enabled;
            }

            set
            {
                SetValue(IsActiveProperty, value);
                _enabled = value;

                if (value)
                {
                    EditButton.IsEnabled = true;
                }
                else
                {
                    EditButton.IsEnabled = false;
                }
            }
        }

        public HotkeySettings HotkeySettings
        {
            get
            {
                return hotkeySettings;
            }

            set
            {
                if (hotkeySettings != value)
                {
                    // Unsubscribe from old settings
                    if (hotkeySettings != null)
                    {
                        hotkeySettings.PropertyChanged -= OnHotkeySettingsPropertyChanged;
                    }

                    hotkeySettings = value;
                    SetValue(HotkeySettingsProperty, value);

                    // Subscribe to new settings
                    if (hotkeySettings != null)
                    {
                        hotkeySettings.PropertyChanged += OnHotkeySettingsPropertyChanged;

                        // Update UI based on conflict properties
                        UpdateConflictStatusFromHotkeySettings();
                    }

                    SetKeys();
                    c.Keys = HotkeySettings?.GetKeysList();
                }
            }
        }

        private void OnHotkeySettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HotkeySettings.HasConflict) ||
                e.PropertyName == nameof(HotkeySettings.ConflictDescription))
            {
                UpdateConflictStatusFromHotkeySettings();
            }
        }

        private void UpdateConflictStatusFromHotkeySettings()
        {
            if (hotkeySettings != null)
            {
                // Update the ShortcutControl's conflict properties from HotkeySettings
                HasConflict = hotkeySettings.HasConflict;
                Tooltip = hotkeySettings.HasConflict ? hotkeySettings.ConflictDescription : null;
                IgnoreConflict = HotkeyConflictIgnoreHelper.IsIgnoringConflicts(hotkeySettings);
                KeyVisualShouldShowConflict = !IgnoreConflict && HasConflict;
            }
            else
            {
                HasConflict = false;
                Tooltip = null;
            }
        }

        public ShortcutControl()
        {
            InitializeComponent();
            internalSettings = new HotkeySettings();

            this.Unloaded += ShortcutControl_Unloaded;
            this.Loaded += ShortcutControl_Loaded;

            c.ResetClick += C_ResetClick;
            c.ClearClick += C_ClearClick;
            c.LearnMoreClick += C_LearnMoreClick;

            // We create the Dialog in C# because doing it in XAML is giving WinUI/XAML Island bugs when using dark theme.
            shortcutDialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = resourceLoader.GetString("Activation_Shortcut_Title"),
                Content = c,
                PrimaryButtonText = resourceLoader.GetString("Activation_Shortcut_Save"),
                CloseButtonText = resourceLoader.GetString("Activation_Shortcut_Cancel"),
                DefaultButton = ContentDialogButton.Primary,
            };
            shortcutDialog.RightTapped += ShortcutDialog_Disable;

            AutomationProperties.SetName(EditButton, resourceLoader.GetString("Activation_Shortcut_Title"));

            OnAllowDisableChanged(this, null);
        }

        private void C_LearnMoreClick(object sender, RoutedEventArgs e)
        {
            // Close the current shortcut dialog
            shortcutDialog.Hide();

            // Create and show the ShortcutConflictWindow
            var conflictWindow = new ShortcutConflictWindow();
            conflictWindow.Activate();
        }

        private void UpdateKeyVisualStyles()
        {
            if (PreviewKeysControl?.ItemsSource != null)
            {
                // Force refresh of the ItemsControl to update KeyVisual styles
                var items = PreviewKeysControl.ItemsSource;
                PreviewKeysControl.ItemsSource = null;
                PreviewKeysControl.ItemsSource = items;
            }
        }

        private void UpdateTooltip()
        {
            if (!string.IsNullOrEmpty(Tooltip))
            {
                ToolTipService.SetToolTip(EditButton, Tooltip);
            }
            else
            {
                ToolTipService.SetToolTip(EditButton, null);
            }
        }

        private void ShortcutControl_Unloaded(object sender, RoutedEventArgs e)
        {
            shortcutDialog.PrimaryButtonClick -= ShortcutDialog_PrimaryButtonClick;
            shortcutDialog.Opened -= ShortcutDialog_Opened;
            shortcutDialog.Closing -= ShortcutDialog_Closing;

            c.LearnMoreClick -= C_LearnMoreClick;

            if (App.GetSettingsWindow() != null)
            {
                App.GetSettingsWindow().Activated -= ShortcutDialog_SettingsWindow_Activated;
            }

            // Unsubscribe from HotkeySettings property changes
            if (hotkeySettings != null)
            {
                hotkeySettings.PropertyChanged -= OnHotkeySettingsPropertyChanged;
            }

            // Dispose the HotkeySettingsControlHook object to terminate the hook threads when the textbox is unloaded
            hook?.Dispose();

            hook = null;
        }

        private void ShortcutControl_Loaded(object sender, RoutedEventArgs e)
        {
            // These all belong here; because of virtualization in e.g. a ListView, the control can go through several Loaded / Unloaded cycles.
            hook?.Dispose();

            hook = new HotkeySettingsControlHook(Hotkey_KeyDown, Hotkey_KeyUp, Hotkey_IsActive, FilterAccessibleKeyboardEvents);

            shortcutDialog.PrimaryButtonClick += ShortcutDialog_PrimaryButtonClick;
            shortcutDialog.Opened += ShortcutDialog_Opened;
            shortcutDialog.Closing += ShortcutDialog_Closing;

            if (App.GetSettingsWindow() != null)
            {
                App.GetSettingsWindow().Activated += ShortcutDialog_SettingsWindow_Activated;
            }

            // Initialize tooltip when loaded
            UpdateTooltip();
        }

        private void KeyEventHandler(int key, bool matchValue, int matchValueCode)
        {
            VirtualKey virtualKey = (VirtualKey)key;
            switch (virtualKey)
            {
                case VirtualKey.LeftWindows:
                case VirtualKey.RightWindows:
                    if (!matchValue && _modifierKeysOnEntering.Contains(virtualKey))
                    {
                        SendSingleKeyboardInput((short)virtualKey, (uint)NativeKeyboardHelper.KeyEventF.KeyUp);
                        _ = _modifierKeysOnEntering.Remove(virtualKey);
                    }

                    internalSettings.Win = matchValue;
                    break;
                case VirtualKey.Control:
                case VirtualKey.LeftControl:
                case VirtualKey.RightControl:
                    if (!matchValue && _modifierKeysOnEntering.Contains(VirtualKey.Control))
                    {
                        SendSingleKeyboardInput((short)virtualKey, (uint)NativeKeyboardHelper.KeyEventF.KeyUp);
                        _ = _modifierKeysOnEntering.Remove(VirtualKey.Control);
                    }

                    internalSettings.Ctrl = matchValue;
                    break;
                case VirtualKey.Menu:
                case VirtualKey.LeftMenu:
                case VirtualKey.RightMenu:
                    if (!matchValue && _modifierKeysOnEntering.Contains(VirtualKey.Menu))
                    {
                        SendSingleKeyboardInput((short)virtualKey, (uint)NativeKeyboardHelper.KeyEventF.KeyUp);
                        _ = _modifierKeysOnEntering.Remove(VirtualKey.Menu);
                    }

                    internalSettings.Alt = matchValue;
                    break;
                case VirtualKey.Shift:
                case VirtualKey.LeftShift:
                case VirtualKey.RightShift:
                    if (!matchValue && _modifierKeysOnEntering.Contains(VirtualKey.Shift))
                    {
                        SendSingleKeyboardInput((short)virtualKey, (uint)NativeKeyboardHelper.KeyEventF.KeyUp);
                        _ = _modifierKeysOnEntering.Remove(VirtualKey.Shift);
                    }

                    internalSettings.Shift = matchValue;
                    break;
                case VirtualKey.Escape:
                    internalSettings = new HotkeySettings();
                    shortcutDialog.IsPrimaryButtonEnabled = false;
                    return;
                default:
                    internalSettings.Code = matchValueCode;
                    break;
            }
        }

        // Function to send a single key event to the system which would be ignored by the hotkey control.
        private void SendSingleKeyboardInput(short keyCode, uint keyStatus)
        {
            NativeKeyboardHelper.INPUT inputShift = new NativeKeyboardHelper.INPUT
            {
                type = NativeKeyboardHelper.INPUTTYPE.INPUT_KEYBOARD,
                data = new NativeKeyboardHelper.InputUnion
                {
                    ki = new NativeKeyboardHelper.KEYBDINPUT
                    {
                        wVk = keyCode,
                        dwFlags = keyStatus,

                        // Any keyevent with the extraInfo set to this value will be ignored by the keyboard hook and sent to the system instead.
                        dwExtraInfo = ignoreKeyEventFlag,
                    },
                },
            };

            NativeKeyboardHelper.INPUT[] inputs = new NativeKeyboardHelper.INPUT[] { inputShift };

            _ = NativeMethods.SendInput(1, inputs, NativeKeyboardHelper.INPUT.Size);
        }

        private bool FilterAccessibleKeyboardEvents(int key, UIntPtr extraInfo)
        {
            // A keyboard event sent with this value in the extra Information field should be ignored by the hook so that it can be captured by the system instead.
            if (extraInfo == ignoreKeyEventFlag)
            {
                return false;
            }

            // If the current key press is tab, based on the other keys ignore the key press so as to shift focus out of the hotkey control.
            if ((VirtualKey)key == VirtualKey.Tab)
            {
                // Shift was not pressed while entering and Shift is not pressed while leaving the hotkey control, treat it as a normal tab key press.
                if (!internalSettings.Shift && !_modifierKeysOnEntering.Contains(VirtualKey.Shift) && !internalSettings.Win && !internalSettings.Alt && !internalSettings.Ctrl)
                {
                    return false;
                }

                // Shift was not pressed while entering but it was pressed while leaving the hotkey, therefore simulate a shift key press as the system does not know about shift being pressed in the hotkey.
                else if (internalSettings.Shift && !_modifierKeysOnEntering.Contains(VirtualKey.Shift) && !internalSettings.Win && !internalSettings.Alt && !internalSettings.Ctrl)
                {
                    // This is to reset the shift key press within the control as it was not used within the control but rather was used to leave the hotkey.
                    internalSettings.Shift = false;

                    SendSingleKeyboardInput((short)VirtualKey.Shift, (uint)NativeKeyboardHelper.KeyEventF.KeyDown);

                    return false;
                }

                // Shift was pressed on entering and remained pressed, therefore only ignore the tab key so that it can be passed to the system.
                // As the shift key is already assumed to be pressed by the system while it entered the hotkey control, shift would still remain pressed, hence ignoring the tab input would simulate a Shift+Tab key press.
                else if (!internalSettings.Shift && _modifierKeysOnEntering.Contains(VirtualKey.Shift) && !internalSettings.Win && !internalSettings.Alt && !internalSettings.Ctrl)
                {
                    return false;
                }
            }

            // Either the cancel or save button has keyboard focus.
            if (FocusManager.GetFocusedElement(LayoutRoot.XamlRoot).GetType() == typeof(Button))
            {
                return false;
            }

            return true;
        }

        private void Hotkey_KeyDown(int key)
        {
            KeyEventHandler(key, true, key);

            c.Keys = internalSettings.GetKeysList();
            c.ConflictMessage = string.Empty;
            c.HasConflict = false;

            if (internalSettings.GetKeysList().Count == 0)
            {
                // Empty, disable save button
                shortcutDialog.IsPrimaryButtonEnabled = false;
            }
            else if (internalSettings.GetKeysList().Count == 1)
            {
                // 1 key, disable save button
                shortcutDialog.IsPrimaryButtonEnabled = false;

                // Check if the one key is a hotkey
                if (internalSettings.Shift || internalSettings.Win || internalSettings.Alt || internalSettings.Ctrl)
                {
                    c.IsError = false;
                }
                else
                {
                    c.IsError = true;
                }
            }

            // Tab and Shift+Tab are accessible keys and should not be displayed in the hotkey control.
            if (internalSettings.Code > 0 && !internalSettings.IsAccessibleShortcut())
            {
                lastValidSettings = internalSettings with { };

                if (!ComboIsValid(lastValidSettings))
                {
                    DisableKeys();
                }
                else
                {
                    EnableKeys();

                    if (lastValidSettings.IsValid())
                    {
                        if (string.Equals(lastValidSettings.ToString(), hotkeySettings.ToString(), StringComparison.OrdinalIgnoreCase))
                        {
                            c.HasConflict = hotkeySettings.HasConflict;
                            c.ConflictMessage = hotkeySettings.ConflictDescription;
                        }
                        else
                        {
                            // Check for conflicts with the new hotkey settings
                            CheckForConflicts(lastValidSettings);
                        }
                    }
                }
            }

            c.IsWarningAltGr = internalSettings.Ctrl && internalSettings.Alt && !internalSettings.Win && (internalSettings.Code > 0);
        }

        private void CheckForConflicts(HotkeySettings settings)
        {
            void UpdateUIForConflict(bool hasConflict, HotkeyConflictResponse hotkeyConflictResponse)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (hasConflict)
                    {
                        // Build conflict message from all conflicts - only show module names
                        var conflictingModules = new HashSet<string>();

                        foreach (var conflict in hotkeyConflictResponse.AllConflicts)
                        {
                            if (!string.IsNullOrEmpty(conflict.ModuleName))
                            {
                                conflictingModules.Add(conflict.ModuleName);
                            }
                        }

                        var moduleNames = conflictingModules.ToArray();
                        if (string.Equals(moduleNames[0], "System", StringComparison.OrdinalIgnoreCase))
                        {
                            c.ConflictMessage = ResourceLoaderInstance.ResourceLoader.GetString("SysHotkeyConflictTooltipText");
                        }
                        else
                        {
                            c.ConflictMessage = ResourceLoaderInstance.ResourceLoader.GetString("InAppHotkeyConflictTooltipText");
                        }

                        c.HasConflict = true;
                    }
                    else
                    {
                        c.ConflictMessage = string.Empty;
                        c.HasConflict = false;
                    }
                });
            }

            HotkeyConflictHelper.CheckHotkeyConflict(
                settings,
                ShellPage.SendDefaultIPCMessage,
                UpdateUIForConflict);
        }

        private void EnableKeys()
        {
            shortcutDialog.IsPrimaryButtonEnabled = true;
            c.IsError = false;
        }

        private void DisableKeys()
        {
            shortcutDialog.IsPrimaryButtonEnabled = false;
            c.IsError = true;
        }

        private void Hotkey_KeyUp(int key)
        {
            KeyEventHandler(key, false, 0);
        }

        private bool Hotkey_IsActive()
        {
            return _isActive;
        }

        private void ShortcutDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            if (!ComboIsValid(hotkeySettings))
            {
                DisableKeys();
            }
            else
            {
                EnableKeys();
            }

            // Reset the status on entering the hotkey each time.
            _modifierKeysOnEntering.Clear();

            // To keep track of the modifier keys, whether it was pressed on entering.
            if ((NativeMethods.GetAsyncKeyState((int)VirtualKey.Shift) & 0x8000) != 0)
            {
                _modifierKeysOnEntering.Add(VirtualKey.Shift);
            }

            if ((NativeMethods.GetAsyncKeyState((int)VirtualKey.Control) & 0x8000) != 0)
            {
                _modifierKeysOnEntering.Add(VirtualKey.Control);
            }

            if ((NativeMethods.GetAsyncKeyState((int)VirtualKey.Menu) & 0x8000) != 0)
            {
                _modifierKeysOnEntering.Add(VirtualKey.Menu);
            }

            if ((NativeMethods.GetAsyncKeyState((int)VirtualKey.LeftWindows) & 0x8000) != 0)
            {
                _modifierKeysOnEntering.Add(VirtualKey.LeftWindows);
            }

            if ((NativeMethods.GetAsyncKeyState((int)VirtualKey.RightWindows) & 0x8000) != 0)
            {
                _modifierKeysOnEntering.Add(VirtualKey.RightWindows);
            }

            _isActive = true;
        }

        private async void OpenDialogButton_Click(object sender, RoutedEventArgs e)
        {
            c.Keys = null;
            c.Keys = HotkeySettings.GetKeysList();

            c.IgnoreConflict = IgnoreConflict;
            c.HasConflict = hotkeySettings.HasConflict;
            c.ConflictMessage = hotkeySettings.ConflictDescription;

            // 92 means the Win key. The logic is: warning should be visible if the shortcut contains Alt AND contains Ctrl AND NOT contains Win.
            // Additional key must be present, as this is a valid, previously used shortcut shown at dialog open. Check for presence of non-modifier-key is not necessary therefore
            c.IsWarningAltGr = c.Keys.Contains("Ctrl") && c.Keys.Contains("Alt") && !c.Keys.Contains(92);

            shortcutDialog.XamlRoot = this.XamlRoot;
            shortcutDialog.RequestedTheme = this.ActualTheme;
            await shortcutDialog.ShowAsync();
        }

        private void C_ResetClick(object sender, RoutedEventArgs e)
        {
            hotkeySettings = null;

            SetValue(HotkeySettingsProperty, hotkeySettings);
            SetKeys();

            lastValidSettings = hotkeySettings;
            shortcutDialog.Hide();

            // Send RequestAllConflicts IPC to update the UI after changed hotkey settings.
            GlobalHotkeyConflictManager.Instance?.RequestAllConflicts();
        }

        private void C_ClearClick(object sender, RoutedEventArgs e)
        {
            hotkeySettings = new HotkeySettings();

            SetValue(HotkeySettingsProperty, hotkeySettings);
            SetKeys();

            lastValidSettings = hotkeySettings;
            shortcutDialog.Hide();

            // Send RequestAllConflicts IPC to update the UI after changed hotkey settings.
            GlobalHotkeyConflictManager.Instance?.RequestAllConflicts();
        }

        private void ShortcutDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (ComboIsValid(lastValidSettings))
            {
                if (c.HasConflict)
                {
                    lastValidSettings = lastValidSettings with { HasConflict = true };
                }
                else
                {
                    lastValidSettings = lastValidSettings with { HasConflict = false };
                }

                HotkeySettings = lastValidSettings;
            }

            SetKeys();

            // Send RequestAllConflicts IPC to update the UI after changed hotkey settings.
            GlobalHotkeyConflictManager.Instance?.RequestAllConflicts();

            shortcutDialog.Hide();
        }

        private void ShortcutDialog_Disable(object sender, RightTappedRoutedEventArgs e)
        {
            if (!AllowDisable)
            {
                return;
            }

            var empty = new HotkeySettings();
            HotkeySettings = empty;
            SetKeys();
            shortcutDialog.Hide();
        }

        private static bool ComboIsValid(HotkeySettings settings)
        {
            if (settings != null && (settings.IsValid() || settings.IsEmpty()))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private void ShortcutDialog_SettingsWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            args.Handled = true;
            if (args.WindowActivationState != WindowActivationState.Deactivated && (hook == null || hook.GetDisposedState() == true))
            {
                // If the PT settings window gets focused/activated again, we enable the keyboard hook to catch the keyboard input.
                hook = new HotkeySettingsControlHook(Hotkey_KeyDown, Hotkey_KeyUp, Hotkey_IsActive, FilterAccessibleKeyboardEvents);
            }
            else if (args.WindowActivationState == WindowActivationState.Deactivated && hook != null && hook.GetDisposedState() == false)
            {
                // If the PT settings window lost focus/activation, we disable the keyboard hook to allow keyboard input on other windows.
                hook.Dispose();
                hook = null;
            }
        }

        private void ShortcutDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            _isActive = false;
            lastValidSettings = hotkeySettings;
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (hook != null)
                    {
                        hook.Dispose();
                    }

                    hook = null;
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void SetKeys()
        {
            var keys = HotkeySettings?.GetKeysList();

            if (keys != null && keys.Count > 0)
            {
                VisualStateManager.GoToState(this, "Configured", true);
                PreviewKeysControl.ItemsSource = keys;
                AutomationProperties.SetHelpText(EditButton, HotkeySettings.ToString());
            }
            else
            {
                VisualStateManager.GoToState(this, "Normal", true);
                AutomationProperties.SetHelpText(EditButton, resourceLoader.GetString("ConfigureShortcut"));
            }
        }
    }
}
