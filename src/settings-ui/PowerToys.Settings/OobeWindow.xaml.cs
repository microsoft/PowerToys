// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Interop;
using interop;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.OOBE.Views;
using Microsoft.Toolkit.Wpf.UI.XamlHost;
using Windows.ApplicationModel.Resources;

namespace PowerToys.Settings
{
    /// <summary>
    /// Interaction logic for OobeWindow.xaml
    /// </summary>
    public partial class OobeWindow : Window
    {
        private static Window inst;
        private OobeShellPage shellPage;
        private int initialModule;

        public static bool IsOpened
        {
            get
            {
                return inst != null;
            }
        }

        public OobeWindow(int initialModule)
        {
            InitializeComponent();
            Utils.FitToScreen(this);
            this.initialModule = initialModule;

            ResourceLoader loader = ResourceLoader.GetForViewIndependentUse();
            Title = loader.GetString("OobeWindow_Title");
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (shellPage != null)
            {
                shellPage.OnClosing();
            }

            inst = null;
            MainWindow.CloseHiddenWindow();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (inst != null)
            {
                inst.Close();
            }

            inst = this;
        }

        private void WindowsXamlHost_ChildChanged(object sender, EventArgs e)
        {
            if (sender == null)
            {
                return;
            }

            WindowsXamlHost windowsXamlHost = sender as WindowsXamlHost;
            shellPage = windowsXamlHost.GetUwpInternalObject() as OobeShellPage;

            if (shellPage != null)
            {
                shellPage.NavigateToModule(initialModule);
            }

            OobeShellPage.SetRunSharedEventCallback(() =>
            {
                return Constants.PowerLauncherSharedEvent();
            });

            OobeShellPage.SetColorPickerSharedEventCallback(() =>
            {
                return Constants.ShowColorPickerSharedEvent();
            });

            OobeShellPage.SetOpenMainWindowCallback((Type type) =>
            {
                ((App)Application.Current).OpenSettingsWindow(type);
            });
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.SetPopupStyle(hwnd);
        }
    }
}
