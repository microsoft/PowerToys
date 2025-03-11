// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using KeyboardManagerEditorUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace KeyboardManagerEditorUI.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Text : Page
    {
        public ObservableCollection<URLShortcut> Shortcuts { get; set; }

        public Text()
        {
            this.InitializeComponent();

            Shortcuts = new ObservableCollection<URLShortcut>();
            Shortcuts.Add(new URLShortcut() { Shortcut = new List<string>() { "Alt (Left)", "H" }, URL = "Hello" });
            Shortcuts.Add(new URLShortcut() { Shortcut = new List<string>() { "Win (Left)", "P", }, URL = "PowerToys" });
        }

        private async void NewShortcutBtn_Click(object sender, RoutedEventArgs e)
        {
            await KeyDialog.ShowAsync();
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            await KeyDialog.ShowAsync();
        }
    }
}
