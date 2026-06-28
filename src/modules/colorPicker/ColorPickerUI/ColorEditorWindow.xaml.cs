// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ColorPicker.Helpers;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinUIEx;

namespace ColorPicker
{
    /// <summary>
    /// The color editor window. Approach A: lean on the native WinUI title bar + a fixed-size
    /// OverlappedPresenter instead of the WPF WindowChrome / DWM / custom-title-bar machinery.
    /// </summary>
    public sealed partial class ColorEditorWindow : Window
    {
        private readonly AppStateHandler _appStateHandler;

        public ColorEditorWindow(AppStateHandler appStateHandler)
        {
            InitializeComponent();

            _appStateHandler = appStateHandler;

            var title = ResourceLoaderInstance.GetString("CP_Title");
            Title = title;
            AppWindow.Title = title;

            // The native title bar / taskbar otherwise shows the generic WinUI placeholder icon;
            // point it at the ColorPicker app icon (copied next to the exe by the csproj).
            AppWindow.SetIcon("Assets/ColorPicker/icon.ico");

            // Without a backdrop the window's uncovered areas (the header above the content card)
            // render black, which reads as a bold black bar at the header/content divider. Mica
            // gives the whole window the standard WinUI material so the header is no longer black.
            SystemBackdrop = new MicaBackdrop();

            this.SetWindowSize(440, 380);

            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsAlwaysOnTop = true;
            }

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
