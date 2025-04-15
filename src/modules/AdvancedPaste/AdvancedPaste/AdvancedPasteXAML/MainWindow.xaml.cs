// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using System.Linq;
using AdvancedPaste.Converters;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Settings;
using AdvancedPaste.ViewModels;

using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinUIEx;
using WinUIEx.Messaging;

namespace AdvancedPaste
{
    public sealed partial class MainWindow : WindowEx, IDisposable
    {
        private readonly WindowMessageMonitor _msgMonitor;
        private readonly IUserSettings _userSettings;
        private readonly OptionsViewModel _optionsViewModel;

        private bool _disposedValue;

        public MainWindow()
        {
            InitializeComponent();

            _userSettings = App.GetService<IUserSettings>();
            _optionsViewModel = App.GetService<OptionsViewModel>();

            var baseHeight = MinHeight;
            var coreActionCount = PasteFormat.MetadataDict.Values.Count(metadata => metadata.IsCoreAction);

            void UpdateHeight()
            {
                double GetHeight(int maxCustomActionCount) =>
                    baseHeight +
                    new PasteFormatsToHeightConverter().GetHeight(coreActionCount + _userSettings.AdditionalActions.Count) +
                    new PasteFormatsToHeightConverter() { MaxItems = maxCustomActionCount }.GetHeight(_optionsViewModel.IsCustomAIServiceEnabled ? _userSettings.CustomActions.Count : 0);

                MinHeight = GetHeight(1);
                Height = GetHeight(5);
            }

            UpdateHeight();

            _userSettings.Changed += (_, _) => UpdateHeight();
            _optionsViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(_optionsViewModel.IsCustomAIServiceEnabled))
                {
                    UpdateHeight();
                }
            };

            AppWindow.SetIcon("Assets/AdvancedPaste/AdvancedPaste.ico");
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(titleBar);

            var loader = ResourceLoaderInstance.ResourceLoader;
            Title = loader.GetString("WindowTitle");

            Activated += OnActivated;

            _msgMonitor = new WindowMessageMonitor(this);
            _msgMonitor.WindowMessageReceived += (_, e) =>
            {
                const int WM_NCLBUTTONDBLCLK = 0x00A3;
                if (e.Message.MessageId == WM_NCLBUTTONDBLCLK)
                {
                    // Disable double click on title bar to maximize window
                    e.Result = 0;
                    e.Handled = true;
                }
            };

            WindowHelpers.BringToForeground(this.GetWindowHandle());
            WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(this.GetWindowHandle());
        }

        private void OnActivated(object sender, WindowActivatedEventArgs args)
        {
            if (_userSettings.CloseAfterLosingFocus && args.WindowActivationState == WindowActivationState.Deactivated)
            {
                Hide();
            }
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                _msgMonitor?.Dispose();
                (Application.Current as App).EtwTrace?.Dispose();

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private async void WindowEx_Closed(object sender, Microsoft.UI.Xaml.WindowEventArgs args)
        {
            await _optionsViewModel.CancelPasteActionAsync();
            Hide();
            args.Handled = true;
        }

        private void Hide()
        {
            Windows.Win32.PInvoke.ShowWindow(new Windows.Win32.Foundation.HWND(this.GetWindowHandle()), Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_HIDE);
        }

        public void SetFocus()
        {
            MainPage.CustomFormatTextBox.InputTxtBox.Focus(FocusState.Programmatic);
        }

        public void ClearInputText()
        {
            MainPage.CustomFormatTextBox.InputTxtBox.Text = string.Empty;
        }

        internal void StartLoading()
        {
            MainPage.CustomFormatTextBox.IsLoading(true);
        }

        internal void FinishLoading(bool success)
        {
            MainPage.CustomFormatTextBox.IsLoading(false);

            if (success)
            {
                VisualStateManager.GoToState(MainPage.CustomFormatTextBox, "DefaultState", true);
            }
        }
    }
}
