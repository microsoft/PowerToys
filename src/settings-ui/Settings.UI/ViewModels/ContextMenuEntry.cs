// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public enum ContextMenuEntryScope
    {
        CurrentUser,
        AllUsers,
    }

    public enum ContextMenuEntrySource
    {
        // Classic, registry-based ("Show more options") handler or static verb.
        Classic,

        // Windows 11 packaged (sparse-package) top-level handler.
        Modern,
    }

    // How a toggle is written to the registry. Each kind uses the mechanism Explorer itself honours
    // for that kind of entry, so a disabled entry is always restorable and nothing is ever deleted.
    public enum ContextMenuEntryKind
    {
        // shellex handler key whose default value is the CLSID: disabled with the New+ "disabled_" prefix.
        HandlerValue,

        // CLSIDs listed under "Shell Extensions\Blocked": used where there is no CLSID value to rewrite
        // (GUID-named handler keys, packaged Windows 11 verbs).
        BlockedClsid,

        // Static "shell\<verb>" key: disabled with the LegacyDisable value.
        Verb,
    }

    // Display/edit model for one Explorer context-menu entry found by ContextMenuManagerViewModel's
    // registry enumeration. Not a persisted settings object - the registry is the source of truth,
    // so this is re-created fresh on every enumeration pass rather than loaded/saved as JSON.
    // One entry covers every root (files, folders, drives...) the same handler/verb is registered
    // under, so toggling it hides it everywhere at once.
    public class ContextMenuEntry : Observable
    {
        // Friendly name shown to the user. Submenu items carry their parent path, e.g. "PowerShell 7 > Open here".
        public string DisplayName { get; set; }

        // Raw handler/verb key name, e.g. "WinRAR". Searched alongside DisplayName.
        public string HandlerKeyName { get; set; }

        public ContextMenuEntryScope Scope { get; set; }

        public ContextMenuEntrySource Source { get; set; }

        public ContextMenuEntryKind Kind { get; set; }

        // HandlerValue/Verb: key paths relative to Software\Classes, one per root the entry is registered under.
        public List<string> KeyPaths { get; } = new List<string>();

        // BlockedClsid: the braced CLSIDs to block.
        public List<string> Clsids { get; } = new List<string>();

        // Roots (e.g. "*", "Directory") this entry was found under, for the description line.
        public List<string> Roots { get; } = new List<string>();

        // HandlerValue: original default value (the CLSID, without any "disabled_" prefix).
        public string OriginalClsidValue { get; set; }

        // Modern: manifest ItemType values ("*", ".png", "Directory", "Directory\Background").
        public List<string> ItemTypes { get; } = new List<string>();

        // Verb: the submenu entry this item belongs to; null at top level.
        public ContextMenuEntry Parent { get; set; }

        // Verb: the "Position" value ("Top"/"Bottom"), used to place it in the preview.
        public string Position { get; set; }

        // Keeps submenu items directly below their parent when the list is sorted.
        public string SortKey { get; set; }

        public bool IsToggleable { get; set; } = true;

        // True when the underlying handler DLL resolves under %SystemRoot%\System32 or SysWOW64 -
        // still toggleable, but the UI asks for an extra confirmation before writing.
        public bool IsLikelyWindowsOwned { get; set; }

        private bool isEnabled;

        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                if (isEnabled != value)
                {
                    isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        // Where the icon comes from: a registry icon location ("path,index") or a package logo file.
        public string IconSpec { get; set; }

        private ImageSource icon;

        // Loaded in the background after enumeration; null until then or when there is no icon.
        public ImageSource Icon
        {
            get => icon;
            set
            {
                icon = value;
                OnPropertyChanged(nameof(Icon));
            }
        }

        public string ScopeDisplayName => ResourceLoaderInstance.ResourceLoader.GetString(
            Scope == ContextMenuEntryScope.CurrentUser
                ? "ContextMenuManager_ScopeCurrentUser"
                : "ContextMenuManager_ScopeAllUsers");

        public string SourceDisplayName => ResourceLoaderInstance.ResourceLoader.GetString(
            Source == ContextMenuEntrySource.Classic
                ? "ContextMenuManager_SourceClassic"
                : "ContextMenuManager_SourceModern");

        public string ScopeAndSourceDisplayName
        {
            get
            {
                string text = $"{ScopeDisplayName} · {SourceDisplayName}";
                if (Roots.Count == 0)
                {
                    return text;
                }

                var rootNames = Roots.Select(RootDisplayName).Distinct();
                return $"{text} · {string.Join(", ", rootNames)}";
            }
        }

        private static string RootDisplayName(string root) => root switch
        {
            "*" => ResourceLoaderInstance.ResourceLoader.GetString("ContextMenuManager_RootFiles"),
            "Directory" => ResourceLoaderInstance.ResourceLoader.GetString("ContextMenuManager_RootFolders"),
            "Directory\\Background" => ResourceLoaderInstance.ResourceLoader.GetString("ContextMenuManager_RootBackground"),
            "AllFilesystemObjects" => ResourceLoaderInstance.ResourceLoader.GetString("ContextMenuManager_RootFilesAndFolders"),
            "Drive" => ResourceLoaderInstance.ResourceLoader.GetString("ContextMenuManager_RootDrives"),
            "DesktopBackground" => ResourceLoaderInstance.ResourceLoader.GetString("ContextMenuManager_RootDesktop"),
            _ => root,
        };
    }
}
