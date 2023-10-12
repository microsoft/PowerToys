// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.ApplicationModel.Core;
using Windows.UI.Popups;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class UninstallModuleButton : UserControl
    {
        private ResourceLoader resourceLoader = ResourceLoaderInstance.ResourceLoader;

        public string ModuleName
        {
            get { return (string)GetValue(ModuleNameProperty); }
            set { SetValue(ModuleNameProperty, value); }
        }

        public static readonly Microsoft.UI.Xaml.DependencyProperty ModuleNameProperty =
            Microsoft.UI.Xaml.DependencyProperty.Register("ModuleName", typeof(string), typeof(UninstallModuleButton), null);

        public UninstallModuleButton()
        {
            this.InitializeComponent();
            Loaded += UninstallModuleButton_Loaded;
        }

        private void UninstallModuleButton_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            FlyoutButton.Content = resourceLoader.GetString("Yes");

            RestartApp.Title = resourceLoader.GetString("RestartApp_Dialog_Title");
            RestartApp.ActionButtonContent = resourceLoader.GetString("Yes");
        }

        private void FlyoutButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            List<string> filesInPowertoysDir = UMBUtilites.GetFilesNamesInDir(AppDomain.CurrentDomain.BaseDirectory);
            List<string> moduleFiles = UMBUtilites.FindStringsContainingWord(filesInPowertoysDir, this.ModuleName);

            foreach (string file in moduleFiles)
            {
                UMBUtilites.DeleteFile(file);
            }

            UMBUtilites.WriteWordToFile(this.ModuleName, "uninstalled_modules");

            RestartApp.IsOpen = true;
        }

        private void RestartApp_ActionButtonClick(TeachingTip sender, object args)
        {
            RestartApp.IsOpen = false;
        }
    }
}
