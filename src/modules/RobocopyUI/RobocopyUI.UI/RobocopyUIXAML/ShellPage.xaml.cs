// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Xaml;
using RobocopyUI.Helpers;
using WinUIEx;

namespace RobocopyUI
{
    public sealed partial class ShellPage : Window
    {
        public ShellPage()
        {
            InitializeComponent();
            this.Activated += ShellPage_Activated;
            ContentFrame.Navigate(typeof(HomePage));
        }

        private void ShellPage_Activated(object sender, WindowActivatedEventArgs args)
        {
            this.CenterOnScreen(1200, 900);

            // Extend the canvas to include the title bar so the app can support theming
            ExtendsContentIntoTitleBar = true;
            TitleBarHelper.SetPreferredTheme(this);
            SetTitleBar(titleBar);

            AppWindow.SetIcon("Assets\\RobocopyUI\\RobocopyUI.ico");
            var title = ResourceLoaderInstance.ResourceLoader.GetString("ShellPageWindow/Title");
            this.Title = title;
            titleBar.Title = title;
            AppWindow.Title = title;

            this.Activated -= ShellPage_Activated;
        }
    }
}
