// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Windows;

namespace FileActionsMenu.Ui
{
    public partial class App : Application
    {
        public App()
        {
            string[] items = ExplorerHelper.GetSelectedItems();
            if (items.Length == 0)
            {
                Environment.Exit(0);
                return;
            }

            _ = new MainWindow(items);

            // main.AllowsTransparency = true;
            // main.WindowStyle = WindowStyle.None;
        }
    }
}
