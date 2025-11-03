// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using KeyboardManagerEditorUI.Helpers;
using KeyboardManagerEditorUI.Interop;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static KeyboardManagerEditorUI.Interop.ShortcutKeyMapping;

namespace KeyboardManagerEditorUI.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class URLs : Page, IDisposable
    {
        private KeyboardMappingService? _mappingService;

        private bool _disposed;

        private bool _isEditMode;
        private URLShortcut? _editingMapping;

        public ObservableCollection<URLShortcut> Shortcuts { get; set; }

        [DllImport("PowerToys.KeyboardManagerEditorLibraryWrapper.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern void GetKeyDisplayName(int keyCode, [Out] StringBuilder keyName, int maxLength);

        public URLs()
        {
            this.InitializeComponent();

            Shortcuts = new ObservableCollection<URLShortcut>();

            _mappingService = new KeyboardMappingService();

            LoadUrlShortcuts();
        }

        public void LoadUrlShortcuts()
        {
            if (_mappingService == null)
            {
                return;
            }

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
        }

        public static string GetKeyDisplayName(int keyCode)
        {
            var keyName = new StringBuilder(64);
            GetKeyDisplayName(keyCode, keyName, keyName.Capacity);
            return keyName.ToString();
        }

        private async void NewShortcutBtn_Click(object sender, RoutedEventArgs e)
        {
            _isEditMode = false;
            _editingMapping = null;

            UrlShortcutControl.ClearKeys();
            UrlShortcutControl.SetUrlPathContent(string.Empty);
            await KeyDialog.ShowAsync();
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is URLShortcut urlShortcut)
            {
                _isEditMode = true;
                _editingMapping = urlShortcut;

                UrlShortcutControl.SetShortcutKeys(urlShortcut.Shortcut);
                UrlShortcutControl.SetUrlPathContent(urlShortcut.URL);
                await KeyDialog.ShowAsync();
            }
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

        private void KeyDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (_mappingService == null)
            {
                return;
            }

            List<string> keys = UrlShortcutControl.GetShortcutKeys();
            string urlPath = UrlShortcutControl.GetUrlPathContent();

            // Validate inputs
            ValidationErrorType errorType = ValidationHelper.ValidateProgramOrUrlMapping(keys, false, string.Empty, _mappingService);

            if (errorType != ValidationErrorType.NoError)
            {
                ShowValidationError(errorType, args);
                return;
            }

            bool saved = false;

            try
            {
                // Delete existing mapping if in edit mode
                if (_isEditMode && _editingMapping != null)
                {
                    if (_editingMapping.Shortcut.Count == 1)
                    {
                        int originalKey = _mappingService.GetKeyCodeFromName(_editingMapping.Shortcut[0]);
                        if (originalKey != 0)
                        {
                            _mappingService.DeleteSingleKeyMapping(originalKey);
                        }
                    }
                    else
                    {
                        string originalKeys = string.Join(";", _editingMapping.Shortcut.Select(k => _mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
                        _mappingService.DeleteShortcutMapping(originalKeys);
                    }
                }

                // Shortcut to text mapping
                string originalKeysString = string.Join(";", keys.Select(k => _mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));

                // if (isAppSpecific && !string.IsNullOrEmpty(appName))
                // {
                //    saved = _mappingService.AddShortcutMapping(originalKeysString, programPath, appName, ShortcutOperationType.RemapText);
                // }
                // else
                // {
                ShortcutKeyMapping shortcutKeyMapping = new ShortcutKeyMapping()
                {
                    OperationType = ShortcutOperationType.OpenUri,
                    OriginalKeys = originalKeysString,
                    TargetKeys = originalKeysString,
                    UriToOpen = urlPath,
                };

                saved = _mappingService.AddShorcutMapping(shortcutKeyMapping);

                if (saved)
                {
                    _mappingService.SaveSettings();
                    LoadUrlShortcuts();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error saving text mapping: " + ex.Message);
                args.Cancel = true;
            }
        }

        private void ShowValidationError(ValidationErrorType errorType, ContentDialogButtonClickEventArgs args)
        {
            // if (ValidationHelper.ValidationMessages.TryGetValue(errorType, out (string Title, string Message) error))
            // {
            //    ValidationTip.Title = error.Title;
            //    ValidationTip.Subtitle = error.Message;
            //    ValidationTip.IsOpen = true;
            //    args.Cancel = true;
            // }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mappingService == null || !(sender is Button button) || !(button.DataContext is URLShortcut shortcut))
            {
                return;
            }

            try
            {
                bool deleted = false;
                if (shortcut.Shortcut.Count == 1)
                {
                    // Single key mapping
                    int originalKey = _mappingService.GetKeyCodeFromName(shortcut.Shortcut[0]);
                    if (originalKey != 0)
                    {
                        deleted = _mappingService.DeleteSingleKeyToTextMapping(originalKey);
                    }
                }
                else
                {
                    // Shortcut mapping
                    string originalKeys = string.Join(";", shortcut.Shortcut.Select(k => _mappingService.GetKeyCodeFromName(k)));
                    deleted = _mappingService.DeleteShortcutMapping(originalKeys);
                }

                if (deleted)
                {
                    _mappingService.SaveSettings();
                    Shortcuts.Remove(shortcut);
                    LoadUrlShortcuts();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error deleting text mapping: " + ex.Message);
            }
        }
    }
}
