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
using System.Text;
using KeyboardManagerEditorUI.Helpers;
using KeyboardManagerEditorUI.Interop;
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
    public sealed partial class URLs : Page, IDisposable
    {
        private KeyboardMappingService? _mappingService;

        private bool _disposed;

        public ObservableCollection<URLShortcut> Shortcuts { get; set; }

        [DllImport("PowerToys.KeyboardManagerEditorLibraryWrapper.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern void GetKeyDisplayName(int keyCode, [Out] StringBuilder keyName, int maxLength);

        public URLs()
        {
            this.InitializeComponent();

            Shortcuts = new ObservableCollection<URLShortcut>();

            _mappingService = new KeyboardMappingService();

            foreach (var mapping in _mappingService.GetShortcutMappingsByType(ShortcutOperationType.OpenUri))
            {
                string[] originalKeyCodes = mapping.OriginalKeys.Split(';');
                var originalKeyNames = new List<string>();
                foreach (var keyCode in originalKeyCodes)
                {
                    if (int.TryParse(keyCode, out int code))
                    {
                        originalKeyNames.Add(GetKeyDisplayName(code));
                    }
                }

                var shortcut = new URLShortcut
                {
                    Shortcut = originalKeyNames,
                    URL = mapping.UriToOpen,
                };
                Shortcuts.Add(shortcut);
            }

            /*
                Shortcuts.Add(new URLShortcut() { Shortcut = new List<string>() { "Shift", "Win", "M" }, URL = "https://www.microsoft.com" });
            Shortcuts.Add(new URLShortcut() { Shortcut = new List<string>() { "Win", "P", }, URL = "https://www.bing.com" });
            Shortcuts.Add(new URLShortcut() { Shortcut = new List<string>() { "Shift", "Win", "M" }, URL = "https://www.windows.com" });
            Shortcuts.Add(new URLShortcut() { Shortcut = new List<string>() { "Win", "U", }, URL = "https://www.bing.com" });
            Shortcuts.Add(new URLShortcut() { Shortcut = new List<string>() { "Ctrl", "P" }, URL = "https://www.surface.com" });
            Shortcuts.Add(new URLShortcut() { Shortcut = new List<string>() { "Alt", "Ctrl", "Shift" }, URL = "https://www.bing.com" });
            */
        }

        public static string GetKeyDisplayName(int keyCode)
        {
            var keyName = new StringBuilder(64);
            GetKeyDisplayName(keyCode, keyName, keyName.Capacity);
            return keyName.ToString();
        }

        private async void NewShortcutBtn_Click(object sender, RoutedEventArgs e)
        {
            await KeyDialog.ShowAsync();
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            await KeyDialog.ShowAsync();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _mappingService?.Dispose();
                    _mappingService = null;
                }

                _disposed = true;
            }
        }
    }
}
