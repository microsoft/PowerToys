// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

using ColorPicker.Helpers;
using ColorPicker.Settings;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinUIEx;

namespace ColorPicker
{
    /// <summary>
    /// The color editor window. Uses a fixed-size <see cref="WinUIEx.WindowEx"/> with the native
    /// WinUI <see cref="Microsoft.UI.Xaml.Controls.TitleBar"/> and a Mica backdrop instead of the
    /// WPF WindowChrome / DWM / custom-title-bar
    /// machinery. Size, the non-resizable / non-maximizable flags, and the Mica backdrop are
    /// declared in XAML; only runtime-only concerns (native title, icon, centering, always-on-top
    /// from the user setting, closing, activation) are set here.
    /// </summary>
    public sealed partial class ColorEditorWindow : WindowEx
    {
        private readonly AppStateHandler _appStateHandler;
        private readonly IUserSettings _userSettings;
        private readonly string _windowTitle;

        public ColorEditorWindow(AppStateHandler appStateHandler, IUserSettings userSettings)
        {
            InitializeComponent();

            _appStateHandler = appStateHandler;
            _userSettings = userSettings;

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            // Keep the visible in-app title concise, but preserve the more descriptive native/UIA
            // window name used by the WPF editor. Assistive technology and window automation need
            // to distinguish the editor from the picker overlay.
            var windowTitle = ResourceLoaderInstance.GetString("cp_editor");
            _windowTitle = string.IsNullOrEmpty(windowTitle) ? "Color Picker editor" : windowTitle;
            Title = _windowTitle;
            AppTitleBar.Loaded += AppTitleBar_Loaded;

            // The native title bar / taskbar otherwise shows the generic WinUI placeholder icon;
            // point it at the ColorPicker app icon (copied next to the exe by the csproj).
            this.SetIcon("Assets/ColorPicker/icon.ico");

            // Port of the WPF WindowStartupLocation="CenterScreen": WinUI windows open at the OS
            // default (cascade near the top-left), so center the fixed-size editor on the display.
            this.CenterOnScreen();

            IsAlwaysOnTop = _userSettings.EditorAlwaysOnTop.Value;
            _userSettings.EditorAlwaysOnTop.PropertyChanged += EditorAlwaysOnTop_PropertyChanged;

            AppWindow.Closing += AppWindow_Closing;
            Activated += ColorEditorWindow_Activated;
        }

        public bool IsActiveWindow { get; private set; }

        public void ShowAndActivate()
        {
            // Hiding a minimized window keeps it minimized, so an editor that was minimized when the
            // previous session ended would otherwise reopen as just a taskbar button.
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            this.Show();

            if (!IsAlwaysOnTop)
            {
                // The picker overlay never takes the foreground, so activation can be refused and leave
                // a normal window behind the app the user is working in. Briefly making the editor
                // top-most raises it above that app without keeping it on top.
                IsAlwaysOnTop = true;
                IsAlwaysOnTop = false;
            }

            Activate();
        }

        private void EditorAlwaysOnTop_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Every settings reload raises this, so only touch the window when the value changed.
            if (IsAlwaysOnTop != _userSettings.EditorAlwaysOnTop.Value)
            {
                IsAlwaysOnTop = _userSettings.EditorAlwaysOnTop.Value;
            }
        }

        private void ColorEditorWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            // Microsoft.UI.Xaml.Controls.TitleBar synchronizes its visible title back to the
            // owning window during layout. Queue the distinct AppWindow/UIA name for the next
            // dispatcher turn so it runs after that synchronization.
            DispatcherQueue.TryEnqueue(SetWindowTitle);
            IsActiveWindow = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        private void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(SetWindowTitle);
        }

        private void SetWindowTitle()
        {
            Title = _windowTitle;
        }

        // Match the WPF behavior: closing the editor ends the user session (which hides it) rather
        // than destroying the window, so it can be reopened on the next activation.
        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            args.Cancel = true;
            _appStateHandler.EndUserSession();
        }
    }
}
