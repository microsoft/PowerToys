// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace KeyboardManagerEditorUI
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(titleBar);
            RootView.SelectedItem = RootView.MenuItems[0];
        }

        private void RootView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem selectedItem)
            {
                switch ((string)selectedItem.Tag)
                {
                    case "Remappings": NavigationFrame.Navigate(typeof(Pages.Shortcuts)); break;
                    case "Programs": NavigationFrame.Navigate(typeof(Pages.Programs)); break;
                    case "Text": NavigationFrame.Navigate(typeof(Pages.Text)); break;
                    case "URLs": NavigationFrame.Navigate(typeof(Pages.URLs)); break;
                }
            }
        }
    }
}
