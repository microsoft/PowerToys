// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Xaml;
using RobocopyUI.Helpers;

namespace RobocopyUI
{
    public sealed partial class ShellPage : Window
    {
        public ShellPage()
        {
            InitializeComponent();

            this.Title = ResourceLoaderInstance.ResourceLoader.GetString("ShellPageWindow/Title");

            ContentFrame.Navigate(typeof(HomePage));
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            // Extend the canvas to include the title bar so the app can support theming
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(titleBar);
            TitleBarHelper.SetPreferredTheme(this);

            AppWindow.SetIcon("Assets\\RobocopyUI\\RobocopyUI.ico");
            titleBar.Title = "Robocopy UI";
            AppWindow.Title = "Robocopy UI";
        }
    }
}
