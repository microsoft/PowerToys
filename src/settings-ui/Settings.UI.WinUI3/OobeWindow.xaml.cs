// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using interop;
using Microsoft.PowerToys.Settings.UI.WinUI3.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.WinUI3.OOBE.Views;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.Settings.UI.WinUI3
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class OobeWindow : Window
    {
        private static Window inst;
        private PowerToysModules initialModule;

        public static bool IsOpened
        {
            get
            {
                return inst != null;
            }
        }

        public OobeWindow(PowerToysModules initialModule)
        {
            this.InitializeComponent();

            /* todo(Stefan): Is needed
             * Utils.FitToScreen(this);
            */

            this.initialModule = initialModule;

            ResourceLoader loader = ResourceLoader.GetForViewIndependentUse();
            Title = loader.GetString("OobeWindow_Title");

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

        /*
         todo(Stefan): XAML ISLAND

         *        private void WindowsXamlHost_ChildChanged(object sender, EventArgs e)
                {
                    if (sender == null)
                    {
                        return;
                    }

                    WindowsXamlHost windowsXamlHost = sender as WindowsXamlHost;
                    shellPage = windowsXamlHost.GetUwpInternalObject() as OobeShellPage;

                }


         *      protected override void OnSourceInitialized(EventArgs e)
                {
                    base.OnSourceInitialized(e);
                    var hwnd = new WindowInteropHelper(this).Handle;
                    NativeMethods.SetPopupStyle(hwnd);
                }
        */
    }
}
