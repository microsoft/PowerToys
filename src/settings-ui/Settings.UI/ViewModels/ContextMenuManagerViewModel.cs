// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.Win32;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class ContextMenuManagerViewModel : Observable
    {
        // Roots this tool inspects for "...\shellex\ContextMenuHandlers\<Name>" subkeys.
        // v1 deliberately stops here - the per-extension SystemFileAssociations\<ext>\... tree is
        // a much bigger (and much lower-value) surface, skipped for now.
        private static readonly string[] ContextMenuRoots =
        {
            "*",
            "Directory",
            "Directory\\Background",
            "AllFilesystemObjects",
            "Drive",
        };

        private const string DisabledValuePrefix = "disabled_";

        // Static hardcoded denylist for v1, not a live Microsoft-maintained feed.
        // Expand as false negatives get reported. Handler key names are matched case-insensitively.
        private static readonly HashSet<string> BuiltInHandlerDenylist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "New", // Explorer's built-in "New" verb submenu - see NewPlus's own hide/show of this same key.
            "Sharing", // ntshrui.dll network sharing UI.
            "OneDrive",
            "WorkFolders",
            "Library Location",
        };

        private SettingsUtils SettingsUtils { get; set; }

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        private bool _isEnabled;
        private string _searchText = string.Empty;
        private bool _needsExplorerRestart;

        public ObservableCollection<ContextMenuEntry> Entries { get; } = new ObservableCollection<ContextMenuEntry>();

        public ObservableCollection<ContextMenuEntry> FilteredEntries { get; } = new ObservableCollection<ContextMenuEntry>();

        public bool IsElevated { get; }

        public bool IsNotElevated => !IsElevated;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (value != _isEnabled)
                {
                    _isEnabled = value;
                    GeneralSettingsConfig.Enabled.ContextMenuManager = value;
                    OutGoingGeneralSettings snd = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(snd.ToString());
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (value != _searchText)
                {
                    _searchText = value ?? string.Empty;
                    OnPropertyChanged(nameof(SearchText));
                    ApplyFilter();
                }
            }
        }

        public bool NeedsExplorerRestart
        {
            get => _needsExplorerRestart;
            set
            {
                if (value != _needsExplorerRestart)
                {
                    _needsExplorerRestart = value;
                    OnPropertyChanged(nameof(NeedsExplorerRestart));
                }
            }
        }

        public ButtonClickCommand RefreshCommand => new ButtonClickCommand(LoadEntries);

        public ButtonClickCommand RestartExplorerCommand => new ButtonClickCommand(RestartExplorer);

        public ContextMenuManagerViewModel(SettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc, bool isElevated)
        {
            SettingsUtils = settingsUtils;
            GeneralSettingsConfig = settingsRepository.SettingsConfig;
            SendConfigMSG = ipcMSGCallBackFunc;
            IsElevated = isElevated;
            _isEnabled = GeneralSettingsConfig.Enabled.ContextMenuManager;

            LoadEntries();
        }

        public void RefreshEnabledState()
        {
            _isEnabled = GeneralSettingsConfig.Enabled.ContextMenuManager;
            OnPropertyChanged(nameof(IsEnabled));
        }

        // The registry IS the persisted state for this module - no settings JSON, no cache to keep
        // in sync. Re-enumerate live every time the page loads and every time the user hits Refresh.
        public void LoadEntries()
        {
            Entries.Clear();

            foreach (var entry in EnumerateClassicEntries(RegistryHive.CurrentUser))
            {
                Entries.Add(entry);
            }

            foreach (var entry in EnumerateClassicEntries(RegistryHive.LocalMachine))
            {
                Entries.Add(entry);
            }

            foreach (var entry in EnumerateModernEntries())
            {
                Entries.Add(entry);
            }

            // All-users entries need elevation to write - gate the checkbox itself here rather than
            // only in ToggleEntry, so the UI reflects reality instead of silently no-op'ing a click.
            if (!IsElevated)
            {
                foreach (var entry in Entries.Where(e => e.Scope == ContextMenuEntryScope.AllUsers))
                {
                    entry.IsToggleable = false;
                }
            }

            NeedsExplorerRestart = false;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            FilteredEntries.Clear();

            IEnumerable<ContextMenuEntry> filtered = Entries;
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(e => e.DisplayName?.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0
                    || e.HandlerKeyName?.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            foreach (var entry in filtered.OrderBy(e => e.Scope).ThenBy(e => e.Source).ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                FilteredEntries.Add(entry);
            }
        }

        // Writes the toggle to the registry using the New+ convention: overwrite the handler key's
        // default value with a "disabled_"-prefixed copy of the original CLSID to disable, restore
        // the original value to enable. Never deletes the handler key. Caller (the page code-behind)
        // is responsible for gating on IsToggleable / IsElevated / confirmation dialogs before
        // calling this - this method assumes the caller already decided the write is allowed.
        public bool ToggleEntry(ContextMenuEntry entry, bool enable)
        {
            if (entry == null || !entry.IsToggleable)
            {
                return false;
            }

            if (entry.Scope == ContextMenuEntryScope.AllUsers && !IsElevated)
            {
                return false;
            }

            RegistryKey baseKey = entry.Scope == ContextMenuEntryScope.CurrentUser ? Registry.CurrentUser : Registry.LocalMachine;
            string path = $"Software\\Classes\\{entry.RegistryRootPath}\\shellex\\ContextMenuHandlers\\{entry.HandlerKeyName}";

            try
            {
                using (var key = baseKey.OpenSubKey(path, writable: true))
                {
                    if (key == null)
                    {
                        return false;
                    }

                    key.SetValue(null, enable ? entry.OriginalClsidValue : DisabledValuePrefix + entry.OriginalClsidValue, RegistryValueKind.String);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"ContextMenuManager: failed to toggle '{entry.HandlerKeyName}' under {entry.RegistryRootPath}: {ex.Message}");
                return false;
            }

            entry.IsEnabled = enable;
            NeedsExplorerRestart = true;
            return true;
        }

        public void RestartExplorer()
        {
            try
            {
                foreach (var proc in Process.GetProcessesByName("explorer"))
                {
                    proc.Kill();
                }

                Process.Start("explorer.exe");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"ContextMenuManager: failed to restart Explorer: {ex.Message}");
            }

            NeedsExplorerRestart = false;
        }

        private static IEnumerable<ContextMenuEntry> EnumerateClassicEntries(RegistryHive hive)
        {
            var results = new List<ContextMenuEntry>();
            RegistryKey baseKey = hive == RegistryHive.CurrentUser ? Registry.CurrentUser : Registry.LocalMachine;
            ContextMenuEntryScope scope = hive == RegistryHive.CurrentUser ? ContextMenuEntryScope.CurrentUser : ContextMenuEntryScope.AllUsers;

            foreach (var root in ContextMenuRoots)
            {
                string handlersPath = $"Software\\Classes\\{root}\\shellex\\ContextMenuHandlers";

                RegistryKey handlersKey;
                try
                {
                    handlersKey = baseKey.OpenSubKey(handlersPath, writable: false);
                }
                catch (Exception)
                {
                    continue;
                }

                if (handlersKey == null)
                {
                    continue;
                }

                using (handlersKey)
                {
                    foreach (var handlerName in handlersKey.GetSubKeyNames())
                    {
                        RegistryKey handlerKey;
                        try
                        {
                            handlerKey = handlersKey.OpenSubKey(handlerName, writable: false);
                        }
                        catch (Exception)
                        {
                            continue;
                        }

                        if (handlerKey == null)
                        {
                            continue;
                        }

                        using (handlerKey)
                        {
                            string rawValue = handlerKey.GetValue(null) as string;
                            if (string.IsNullOrWhiteSpace(rawValue))
                            {
                                continue;
                            }

                            bool disabled = rawValue.StartsWith(DisabledValuePrefix, StringComparison.OrdinalIgnoreCase);
                            string clsid = disabled ? rawValue.Substring(DisabledValuePrefix.Length) : rawValue;

                            ResolveClsid(clsid, out string displayName, out bool isWindowsOwned);

                            bool toggleable = !BuiltInHandlerDenylist.Contains(handlerName);

                            results.Add(new ContextMenuEntry
                            {
                                DisplayName = string.IsNullOrWhiteSpace(displayName) ? handlerName : displayName,
                                HandlerKeyName = handlerName,
                                RegistryRootPath = root,
                                Scope = scope,
                                Source = ContextMenuEntrySource.Classic,
                                OriginalClsidValue = clsid,
                                IsEnabled = !disabled,
                                IsToggleable = toggleable,
                                IsLikelyWindowsOwned = isWindowsOwned,
                            });
                        }
                    }
                }
            }

            return results;
        }

        // CLSID lookup uses HKEY_CLASSES_ROOT, the OS's own merged view of HKCU+HKLM Software\Classes,
        // since a handler found under either hive can point at a CLSID registered in either.
        private static void ResolveClsid(string clsid, out string displayName, out bool isWindowsOwned)
        {
            displayName = null;
            isWindowsOwned = false;

            if (string.IsNullOrWhiteSpace(clsid))
            {
                return;
            }

            try
            {
                using (var clsidKey = Registry.ClassesRoot.OpenSubKey($"CLSID\\{clsid}", writable: false))
                {
                    if (clsidKey == null)
                    {
                        return;
                    }

                    displayName = clsidKey.GetValue(null) as string;

                    using (var inprocKey = clsidKey.OpenSubKey("InprocServer32", writable: false))
                    {
                        string dllPath = inprocKey?.GetValue(null) as string;
                        if (string.IsNullOrWhiteSpace(dllPath))
                        {
                            return;
                        }

                        string expandedPath = Environment.ExpandEnvironmentVariables(dllPath.Trim('"'));

                        string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                        isWindowsOwned = expandedPath.StartsWith(systemRoot + "\\System32", StringComparison.OrdinalIgnoreCase)
                            || expandedPath.StartsWith(systemRoot + "\\SysWOW64", StringComparison.OrdinalIgnoreCase);

                        if (string.IsNullOrWhiteSpace(displayName))
                        {
                            try
                            {
                                displayName = FileVersionInfo.GetVersionInfo(expandedPath).FileDescription;
                            }
                            catch (Exception)
                            {
                                // Missing/inaccessible DLL - fall back to the handler key name at the call site.
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Malformed/inaccessible CLSID entry - leave displayName null, caller falls back to the handler key name.
            }
        }

        // Windows 11 packaged (sparse-package) context-menu entries, listed read-only: Windows has no
        // per-verb toggle for someone else's already-installed package. Enumerated directly in C#
        // via the same WinRT surface src/common/utils/package.h wraps for PowerToys' own package.
        private static IEnumerable<ContextMenuEntry> EnumerateModernEntries()
        {
            var results = new List<ContextMenuEntry>();

            try
            {
                var packageManager = new PackageManager();
                foreach (Package package in packageManager.FindPackagesForUser(string.Empty))
                {
                    // One bad/corrupted package registration must not abort enumeration of the rest -
                    // every property access below (IsFramework, InstalledPath, DisplayName, Id) can
                    // throw for an individual package.
                    try
                    {
                        if (package.IsFramework || package.IsResourcePackage || package.IsBundle)
                        {
                            continue;
                        }

                        string manifestPath = System.IO.Path.Combine(package.InstalledPath, "AppxManifest.xml");
                        if (!System.IO.File.Exists(manifestPath))
                        {
                            continue;
                        }

                        XDocument manifest = XDocument.Load(manifestPath);
                        bool declaresContextMenu = manifest.Descendants()
                            .Where(el => el.Name.LocalName == "Extension")
                            .Any(el => (string)el.Attribute("Category") == "windows.fileExplorerContextMenus");

                        if (!declaresContextMenu)
                        {
                            continue;
                        }

                        results.Add(new ContextMenuEntry
                        {
                            DisplayName = package.DisplayName,
                            HandlerKeyName = package.Id.FamilyName,
                            RegistryRootPath = string.Empty,
                            Scope = ContextMenuEntryScope.CurrentUser,
                            Source = ContextMenuEntrySource.Modern,
                            IsEnabled = true,
                            IsToggleable = false,
                        });
                    }
                    catch (Exception)
                    {
                        // Skip this one package, keep enumerating the rest.
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"ContextMenuManager: failed to enumerate modern context menu packages: {ex.Message}");
            }

            return results;
        }
    }
}
