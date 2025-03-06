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
    public sealed partial class Shortcuts : Page
    {
        public ObservableCollection<Remapping> RemappedShortcuts { get; set; }

        public Shortcuts()
        {
            this.InitializeComponent();

            RemappedShortcuts = new ObservableCollection<Remapping>();
            RemappedShortcuts.Add(new Remapping() { OriginalKeys = new List<string>() { "Ctrl", "Shift", "F" }, RemappedKeys = new List<string>() { "Shift", "F" }, IsAllApps = true });
            RemappedShortcuts.Add(new Remapping() { OriginalKeys = new List<string>() { "Ctrl (Left)" }, RemappedKeys = new List<string>() { "Ctrl (Right)" }, IsAllApps = true });
            RemappedShortcuts.Add(new Remapping() { OriginalKeys = new List<string>() { "Shift", "M" }, RemappedKeys = new List<string>() { "Ctrl", "M" }, IsAllApps = true });
            RemappedShortcuts.Add(new Remapping() { OriginalKeys = new List<string>() { "Shift", "Alt", "B" }, RemappedKeys = new List<string>() { "Alt", "B" }, IsAllApps = false, AppName = "outlook.exe" });
            RemappedShortcuts.Add(new Remapping() { OriginalKeys = new List<string>() { "Numpad 1" }, RemappedKeys = new List<string>() { "Ctrl", "F" }, IsAllApps = true });
            RemappedShortcuts.Add(new Remapping() { OriginalKeys = new List<string>() { "Numpad 2" }, RemappedKeys = new List<string>() { "Alt", "F" }, IsAllApps = true, AppName = "outlook.exe" });
        }

        private async void NewShortcutBtn_Click(object sender, RoutedEventArgs e)
        {
            await KeyDialog.ShowAsync();
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Remapping selectedShortcut)
            {
                // ShortcutControl.SetOriginalKeys(selectedShortcut.OriginalKeys);
                ShortcutControl.SetRemappedKeys(selectedShortcut.RemappedKeys);
                ShortcutControl.SetApp(!selectedShortcut.IsAllApps, selectedShortcut.AppName);
                await KeyDialog.ShowAsync();
            }
        }
    }
}
