// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using Microsoft.PowerToys.Settings.UI.Helpers;
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
            "DesktopBackground",
        };

        private const string DisabledValuePrefix = "disabled_";

        private const string LegacyDisableValue = "LegacyDisable";

        private const string BlockedKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Shell Extensions\\Blocked";

        // Nested submenus deeper than this are not walked.
        private const int MaxSubmenuDepth = 3;

        // Joins SortKey segments; sorts below every printable character so children stay under their parent.
        private const char SortKeySeparator = '\u0001';

        // Handler keys another PowerToys module owns: NewPlus hides/shows Explorer's built-in "New"
        // submenu through this same key, so editing it here would fight that module. Everything else
        // (including Windows' own handlers) is toggleable; System32/SysWOW64 handlers get an extra
        // confirmation instead of a hard block.
        private static readonly HashSet<string> BuiltInHandlerDenylist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "New",
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

            foreach (var entry in EnumerateVerbEntries(RegistryHive.CurrentUser))
            {
                Entries.Add(entry);
            }

            foreach (var entry in EnumerateVerbEntries(RegistryHive.LocalMachine))
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

            foreach (var entry in filtered.OrderBy(e => e.Scope).ThenBy(e => e.Source).ThenBy(e => e.SortKey ?? e.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                FilteredEntries.Add(entry);
            }
        }

        // Writes the toggle to the registry with the mechanism matching entry.Kind (see
        // ContextMenuEntryKind), for every root the entry covers. Never deletes a handler or verb key.
        // Caller (the page code-behind) is responsible for gating on IsToggleable / IsElevated /
        // confirmation dialogs before calling this - this method assumes the write is allowed.
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

            try
            {
                if (entry.Kind == ContextMenuEntryKind.BlockedClsid)
                {
                    using var blockedKey = baseKey.CreateSubKey(BlockedKeyPath, writable: true);
                    foreach (var clsid in entry.Clsids)
                    {
                        if (enable)
                        {
                            blockedKey.DeleteValue(clsid, throwOnMissingValue: false);
                        }
                        else
                        {
                            blockedKey.SetValue(clsid, entry.DisplayName ?? string.Empty, RegistryValueKind.String);
                        }
                    }
                }
                else
                {
                    foreach (var path in entry.KeyPaths)
                    {
                        using var key = baseKey.OpenSubKey($"Software\\Classes\\{path}", writable: true);
                        if (key == null)
                        {
                            continue;
                        }

                        if (entry.Kind == ContextMenuEntryKind.HandlerValue)
                        {
                            key.SetValue(null, enable ? entry.OriginalClsidValue : DisabledValuePrefix + entry.OriginalClsidValue, RegistryValueKind.String);
                        }
                        else if (enable)
                        {
                            key.DeleteValue(LegacyDisableValue, throwOnMissingValue: false);
                        }
                        else
                        {
                            key.SetValue(LegacyDisableValue, string.Empty, RegistryValueKind.String);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"ContextMenuManager: failed to toggle '{entry.HandlerKeyName}': {ex.Message}");
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

        // One entry per distinct handler, however many roots it is registered under - the same handler
        // under "*", "Directory" and "Drive" is one item in Explorer's menu, and disabling it in only
        // some roots is rarely what anyone wants.
        private static IEnumerable<ContextMenuEntry> EnumerateClassicEntries(RegistryHive hive)
        {
            var results = new Dictionary<string, ContextMenuEntry>(StringComparer.OrdinalIgnoreCase);
            RegistryKey baseKey = hive == RegistryHive.CurrentUser ? Registry.CurrentUser : Registry.LocalMachine;
            ContextMenuEntryScope scope = hive == RegistryHive.CurrentUser ? ContextMenuEntryScope.CurrentUser : ContextMenuEntryScope.AllUsers;
            HashSet<string> blocked = ReadBlockedClsids(baseKey);

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
                            string keyPath = $"{root}\\shellex\\ContextMenuHandlers\\{handlerName}";

                            // Explorer also accepts the CLSID as the key name itself (default value then
                            // just a label, or empty). There is no CLSID value to prefix, so these are
                            // disabled through the Blocked list instead.
                            if (Guid.TryParse(handlerName, out Guid keyGuid))
                            {
                                string blockedClsid = keyGuid.ToString("B");
                                string groupKey = $"blocked|{blockedClsid}";
                                if (!results.TryGetValue(groupKey, out var blockedEntry))
                                {
                                    ResolveClsid(blockedClsid, out string clsidName, out bool clsidWindowsOwned);
                                    blockedEntry = new ContextMenuEntry
                                    {
                                        DisplayName = FirstNonEmpty(clsidName, rawValue, handlerName),
                                        HandlerKeyName = handlerName,
                                        Scope = scope,
                                        Source = ContextMenuEntrySource.Classic,
                                        Kind = ContextMenuEntryKind.BlockedClsid,
                                        IsEnabled = !blocked.Contains(blockedClsid),
                                        IsLikelyWindowsOwned = clsidWindowsOwned,
                                    };
                                    blockedEntry.Clsids.Add(blockedClsid);
                                    results.Add(groupKey, blockedEntry);
                                }

                                blockedEntry.Roots.Add(root);
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(rawValue))
                            {
                                continue;
                            }

                            bool disabled = rawValue.StartsWith(DisabledValuePrefix, StringComparison.OrdinalIgnoreCase);
                            string clsid = disabled ? rawValue.Substring(DisabledValuePrefix.Length) : rawValue;
                            string handlerGroupKey = $"handler|{handlerName}|{clsid}";

                            if (!results.TryGetValue(handlerGroupKey, out var entry))
                            {
                                ResolveClsid(clsid, out string displayName, out bool isWindowsOwned);
                                entry = new ContextMenuEntry
                                {
                                    DisplayName = FirstNonEmpty(displayName, handlerName),
                                    HandlerKeyName = handlerName,
                                    Scope = scope,
                                    Source = ContextMenuEntrySource.Classic,
                                    Kind = ContextMenuEntryKind.HandlerValue,
                                    OriginalClsidValue = clsid,
                                    IsToggleable = !BuiltInHandlerDenylist.Contains(handlerName),
                                    IsLikelyWindowsOwned = isWindowsOwned,
                                };
                                results.Add(handlerGroupKey, entry);
                            }

                            // Shown as disabled only when every root is disabled; toggling rewrites all of them.
                            entry.IsEnabled |= !disabled;
                            entry.KeyPaths.Add(keyPath);
                            entry.Roots.Add(root);
                        }
                    }
                }
            }

            return results.Values;
        }

        // Static "shell\<verb>" entries (e.g. "Open with Visual Studio", "PowerShell 7"), plus the
        // items of their cascading submenus declared through ExtendedSubCommandsKey.
        private static IEnumerable<ContextMenuEntry> EnumerateVerbEntries(RegistryHive hive)
        {
            var results = new Dictionary<string, ContextMenuEntry>(StringComparer.OrdinalIgnoreCase);
            RegistryKey baseKey = hive == RegistryHive.CurrentUser ? Registry.CurrentUser : Registry.LocalMachine;
            ContextMenuEntryScope scope = hive == RegistryHive.CurrentUser ? ContextMenuEntryScope.CurrentUser : ContextMenuEntryScope.AllUsers;

            foreach (var root in ContextMenuRoots)
            {
                AddVerbs(baseKey, scope, root, $"{root}\\shell", parent: null, depth: 0, results);
            }

            return results.Values;
        }

        private static void AddVerbs(RegistryKey baseKey, ContextMenuEntryScope scope, string root, string shellPath, ContextMenuEntry parent, int depth, Dictionary<string, ContextMenuEntry> results)
        {
            RegistryKey shellKey;
            try
            {
                shellKey = baseKey.OpenSubKey($"Software\\Classes\\{shellPath}", writable: false);
            }
            catch (Exception)
            {
                return;
            }

            if (shellKey == null)
            {
                return;
            }

            using (shellKey)
            {
                foreach (var verbName in shellKey.GetSubKeyNames())
                {
                    try
                    {
                        using var verbKey = shellKey.OpenSubKey(verbName, writable: false);

                        // Hidden from the menu by design - nothing for the user to toggle.
                        if (verbKey == null || verbKey.GetValue("ProgrammaticAccessOnly") != null)
                        {
                            continue;
                        }

                        string label = ResolveVerbLabel(verbKey, verbName);

                        // Top-level verbs group by key name across roots; submenu items by parent, since
                        // grouped parents share one ExtendedSubCommandsKey and must not list children twice.
                        string groupKey = parent == null ? $"verb|{verbName}" : $"{parent.SortKey}{SortKeySeparator}{verbName}";
                        bool isNew = !results.TryGetValue(groupKey, out var entry);
                        if (isNew)
                        {
                            entry = new ContextMenuEntry
                            {
                                DisplayName = parent == null ? label : $"{parent.DisplayName} > {label}",
                                HandlerKeyName = verbName,
                                Scope = scope,
                                Source = ContextMenuEntrySource.Classic,
                                Kind = ContextMenuEntryKind.Verb,
                                Depth = depth,
                                SortKey = parent == null ? label : $"{parent.SortKey}{SortKeySeparator}{label}",
                            };
                            results.Add(groupKey, entry);
                        }

                        string verbPath = $"{shellPath}\\{verbName}";
                        if (!entry.KeyPaths.Contains(verbPath, StringComparer.OrdinalIgnoreCase))
                        {
                            entry.KeyPaths.Add(verbPath);
                            entry.IsEnabled |= verbKey.GetValue(LegacyDisableValue) == null;
                        }

                        // Submenu items sit under their parent row, which already lists the roots.
                        if (parent == null && !entry.Roots.Contains(root))
                        {
                            entry.Roots.Add(root);
                        }

                        string subCommandsKey = verbKey.GetValue("ExtendedSubCommandsKey") as string;
                        if (isNew && depth < MaxSubmenuDepth && !string.IsNullOrWhiteSpace(subCommandsKey))
                        {
                            AddVerbs(baseKey, scope, root, $"{subCommandsKey.Trim('\\')}\\shell", entry, depth + 1, results);
                        }
                    }
                    catch (Exception)
                    {
                        // One unreadable verb must not hide the rest.
                        continue;
                    }
                }
            }
        }

        // Explorer's label precedence: MUIVerb, then the default value, then the key name. Either
        // can be an indirect "@dll,-id" string, and "&" marks the access key.
        private static string ResolveVerbLabel(RegistryKey verbKey, string verbName)
        {
            string raw = FirstNonEmpty(verbKey.GetValue("MUIVerb") as string, verbKey.GetValue(null) as string, verbName);
            if (raw.StartsWith('@'))
            {
                var buffer = new StringBuilder(512);
                if (NativeMethods.SHLoadIndirectString(raw, buffer, buffer.Capacity, IntPtr.Zero) == 0)
                {
                    raw = buffer.ToString();
                }
            }

            return raw.Replace("&&", "\u0000").Replace("&", string.Empty).Replace("\u0000", "&");
        }

        private static HashSet<string> ReadBlockedClsids(RegistryKey baseKey)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var key = baseKey.OpenSubKey(BlockedKeyPath, writable: false);
                if (key != null)
                {
                    foreach (var name in key.GetValueNames())
                    {
                        if (Guid.TryParse(name, out Guid guid))
                        {
                            result.Add(guid.ToString("B"));
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Unreadable Blocked list - treat everything as enabled.
            }

            return result;
        }

        private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

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

        // Windows 11 packaged (sparse-package) context-menu entries, one per package. Their verbs are
        // IExplorerCommand CLSIDs declared in the manifest; Explorer skips any CLSID on the per-user
        // "Shell Extensions\Blocked" list, which is how these are toggled. Enumerated directly in C#
        // via the same WinRT surface src/common/utils/package.h wraps for PowerToys' own package.
        private static IEnumerable<ContextMenuEntry> EnumerateModernEntries()
        {
            var results = new List<ContextMenuEntry>();
            HashSet<string> blocked = ReadBlockedClsids(Registry.CurrentUser);

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
                        var clsids = manifest.Descendants()
                            .Where(el => el.Name.LocalName == "Extension" && (string)el.Attribute("Category") == "windows.fileExplorerContextMenus")
                            .SelectMany(el => el.Descendants().Where(v => v.Name.LocalName == "Verb"))
                            .Select(v => Guid.TryParse((string)v.Attribute("Clsid"), out Guid guid) ? guid.ToString("B") : null)
                            .Where(c => c != null)
                            .Distinct()
                            .ToList();

                        if (clsids.Count == 0)
                        {
                            continue;
                        }

                        var entry = new ContextMenuEntry
                        {
                            DisplayName = package.DisplayName,
                            HandlerKeyName = package.Id.FamilyName,
                            Scope = ContextMenuEntryScope.CurrentUser,
                            Source = ContextMenuEntrySource.Modern,
                            Kind = ContextMenuEntryKind.BlockedClsid,
                            IsEnabled = !clsids.Any(blocked.Contains),
                        };
                        entry.Clsids.AddRange(clsids);
                        results.Add(entry);
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
