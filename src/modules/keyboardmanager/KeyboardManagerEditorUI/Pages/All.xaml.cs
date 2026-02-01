// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using KeyboardManagerEditorUI.Helpers;
using KeyboardManagerEditorUI.Interop;
using KeyboardManagerEditorUI.Settings;
using ManagedCommon;
using Microsoft.UI.Xaml.Controls;

namespace KeyboardManagerEditorUI.Pages
{
    /// <summary>
    /// A consolidated page that displays all mappings from Remappings, Text, Programs, and URLs pages.
    /// </summary>
    public sealed partial class All : Page, IDisposable
    {
        private KeyboardMappingService? _mappingService;
        private bool _disposed;

        public ObservableCollection<Remapping> RemappingList { get; } = new ObservableCollection<Remapping>();

        public ObservableCollection<TextMapping> TextMappings { get; } = new ObservableCollection<TextMapping>();

        public ObservableCollection<ProgramShortcut> ProgramShortcuts { get; } = new ObservableCollection<ProgramShortcut>();

        public ObservableCollection<URLShortcut> UrlShortcuts { get; } = new ObservableCollection<URLShortcut>();

        [DllImport("PowerToys.KeyboardManagerEditorLibraryWrapper.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern void GetKeyDisplayName(int keyCode, [Out] StringBuilder keyName, int maxLength);

        public All()
        {
            this.InitializeComponent();

            try
            {
                _mappingService = new KeyboardMappingService();
                LoadAllMappings();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize KeyboardMappingService in All page: " + ex.Message);
            }

            this.Unloaded += All_Unloaded;
        }

        private void All_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Dispose();
        }

        private void LoadAllMappings()
        {
            LoadRemappings();
            LoadTextMappings();
            LoadProgramShortcuts();
            LoadUrlShortcuts();
        }

        private void LoadRemappings()
        {
            if (_mappingService == null)
            {
                return;
            }

            RemappingList.Clear();

            // Load all single key mappings
            foreach (var mapping in _mappingService.GetSingleKeyMappings())
            {
                string[] targetKeyCodes = mapping.TargetKey.Split(';');
                var targetKeyNames = new List<string>();

                foreach (var keyCode in targetKeyCodes)
                {
                    if (int.TryParse(keyCode, out int code))
                    {
                        targetKeyNames.Add(_mappingService.GetKeyDisplayName(code));
                    }
                }

                RemappingList.Add(new Remapping
                {
                    OriginalKeys = new List<string> { _mappingService.GetKeyDisplayName(mapping.OriginalKey) },
                    RemappedKeys = targetKeyNames,
                    IsAllApps = true,
                });
            }

            // Load all shortcut key mappings
            foreach (var mapping in _mappingService.GetShortcutMappingsByType(ShortcutOperationType.RemapShortcut))
            {
                string[] originalKeyCodes = mapping.OriginalKeys.Split(';');
                string[] targetKeyCodes = mapping.TargetKeys.Split(';');

                var originalKeyNames = new List<string>();
                var targetKeyNames = new List<string>();

                foreach (var keyCode in originalKeyCodes)
                {
                    if (int.TryParse(keyCode, out int code))
                    {
                        originalKeyNames.Add(_mappingService.GetKeyDisplayName(code));
                    }
                }

                foreach (var keyCode in targetKeyCodes)
                {
                    if (int.TryParse(keyCode, out int code))
                    {
                        targetKeyNames.Add(_mappingService.GetKeyDisplayName(code));
                    }
                }

                RemappingList.Add(new Remapping
                {
                    OriginalKeys = originalKeyNames,
                    RemappedKeys = targetKeyNames,
                    IsAllApps = string.IsNullOrEmpty(mapping.TargetApp),
                    AppName = string.IsNullOrEmpty(mapping.TargetApp) ? "All Apps" : mapping.TargetApp,
                });
            }
        }

        private void LoadTextMappings()
        {
            if (_mappingService == null)
            {
                return;
            }

            TextMappings.Clear();

            // Load key-to-text mappings
            var keyToTextMappings = _mappingService.GetKeyToTextMappings();
            foreach (var mapping in keyToTextMappings)
            {
                TextMappings.Add(new TextMapping
                {
                    Keys = new List<string> { _mappingService.GetKeyDisplayName(mapping.OriginalKey) },
                    Text = mapping.TargetText,
                    IsAllApps = true,
                    AppName = "All Apps",
                });
            }

            // Load shortcut-to-text mappings
            foreach (var mapping in _mappingService.GetShortcutMappingsByType(ShortcutOperationType.RemapText))
            {
                string[] originalKeyCodes = mapping.OriginalKeys.Split(';');
                var originalKeyNames = new List<string>();
                foreach (var keyCode in originalKeyCodes)
                {
                    if (int.TryParse(keyCode, out int code))
                    {
                        originalKeyNames.Add(_mappingService.GetKeyDisplayName(code));
                    }
                }

                TextMappings.Add(new TextMapping
                {
                    Keys = originalKeyNames,
                    Text = mapping.TargetText,
                    IsAllApps = string.IsNullOrEmpty(mapping.TargetApp),
                    AppName = string.IsNullOrEmpty(mapping.TargetApp) ? "All Apps" : mapping.TargetApp,
                });
            }
        }

        private void LoadProgramShortcuts()
        {
            if (_mappingService == null)
            {
                return;
            }

            ProgramShortcuts.Clear();

            foreach (var shortcutSettings in SettingsManager.GetShortcutSettingsByOperationType(ShortcutOperationType.RunProgram))
            {
                ShortcutKeyMapping mapping = shortcutSettings.Shortcut;
                string[] originalKeyCodes = mapping.OriginalKeys.Split(';');
                var originalKeyNames = new List<string>();
                foreach (var keyCode in originalKeyCodes)
                {
                    if (int.TryParse(keyCode, out int code))
                    {
                        originalKeyNames.Add(_mappingService.GetKeyDisplayName(code));
                    }
                }

                ProgramShortcuts.Add(new ProgramShortcut
                {
                    Shortcut = originalKeyNames,
                    AppToRun = mapping.ProgramPath,
                    Args = mapping.ProgramArgs,
                    IsActive = shortcutSettings.IsActive,
                    Id = shortcutSettings.Id,
                });
            }
        }

        private void LoadUrlShortcuts()
        {
            if (_mappingService == null)
            {
                return;
            }

            UrlShortcuts.Clear();

            foreach (var mapping in _mappingService.GetShortcutMappingsByType(ShortcutOperationType.OpenUri))
            {
                string[] originalKeyCodes = mapping.OriginalKeys.Split(';');
                var originalKeyNames = new List<string>();
                foreach (var keyCode in originalKeyCodes)
                {
                    if (int.TryParse(keyCode, out int code))
                    {
                        originalKeyNames.Add(GetKeyDisplayNameFromCode(code));
                    }
                }

                UrlShortcuts.Add(new URLShortcut
                {
                    Shortcut = originalKeyNames,
                    URL = mapping.UriToOpen,
                });
            }
        }

        private static string GetKeyDisplayNameFromCode(int keyCode)
        {
            var keyName = new StringBuilder(64);
            GetKeyDisplayName(keyCode, keyName, keyName.Capacity);
            return keyName.ToString();
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
                    _mappingService?.Dispose();
                    _mappingService = null;
                }

                _disposed = true;
            }
        }
    }
}
