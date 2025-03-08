// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using FileActionsMenu.Ui.Helpers;
using Application = Microsoft.UI.Xaml.Application;

namespace FileActionsMenu.Ui
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            if (PowerToys.GPOWrapperProjection.GPOWrapper.GetConfiguredFileActionsMenuEnabledValue() == PowerToys.GPOWrapperProjection.GpoRuleConfigured.Disabled)
            {
                Environment.Exit(0);
                return;
            }

            string[] items = ExplorerHelper.GetSelectedItems();
            if (items.Length == 0)
            {
                Environment.Exit(0);
                return;
            }

            _ = new MainWindow(items);
        }
    }
}
