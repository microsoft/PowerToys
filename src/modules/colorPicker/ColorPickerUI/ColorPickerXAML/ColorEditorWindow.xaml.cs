// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ColorPicker.Helpers;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinUIEx;

namespace ColorPicker
{
    /// <summary>
    /// The color editor window. Uses a fixed-size <see cref="WinUIEx.WindowEx"/> with the native
    /// WinUI <see cref="Microsoft.UI.Xaml.Controls.TitleBar"/> and a Mica backdrop instead of the
    /// WPF WindowChrome / DWM / custom-title-bar
    /// machinery. Size, the non-resizable / non-maximizable / non-minimizable flags, always-on-top,
    /// and the Mica backdrop are declared in XAML; only runtime-only concerns (native title, icon,
    /// centering, closing, activation) are set here.
    /// </summary>
    public sealed partial class ColorEditorWindow : WindowEx
    {
        private readonly AppStateHandler _appStateHandler;

        public ColorEditorWindow(AppStateHandler appStateHandler)
        {
            InitializeComponent();

            _appStateHandler = appStateHandler;

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            Title = AppTitleBar.Title;

            // The native title bar / taskbar otherwise shows the generic WinUI placeholder icon;
            // point it at the ColorPicker app icon (copied next to the exe by the csproj).
            this.SetIcon("Assets/ColorPicker/icon.ico");

            // Port of the WPF WindowStartupLocation="CenterScreen": WinUI windows open at the OS
            // default (cascade near the top-left), so center the fixed-size editor on the display.
            this.CenterOnScreen();

            AppWindow.Closing += AppWindow_Closing;
            Activated += ColorEditorWindow_Activated;
        }

        public bool IsActiveWindow { get; private set; }

        private void ColorEditorWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            IsActiveWindow = args.WindowActivationState != WindowActivationState.Deactivated;
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
