// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public enum ContextMenuEntryScope
    {
        CurrentUser,
        AllUsers,
    }

    public enum ContextMenuEntrySource
    {
        // Classic, registry-based ("Show more options") handler.
        Classic,

        // Windows 11 packaged (sparse-package) top-level handler. Read-only in this tool.
        Modern,
    }

    // Display/edit model for one Explorer context-menu entry found by ContextMenuManagerViewModel's
    // registry enumeration. Not a persisted settings object - the registry is the source of truth,
    // so this is re-created fresh on every enumeration pass rather than loaded/saved as JSON.
    public class ContextMenuEntry : Observable
    {
        // Friendly name shown to the user (CLSID default name, or the handler DLL's FileDescription,
        // or the raw registry key name as a last resort).
        public string DisplayName { get; set; }

        // Raw ContextMenuHandlers subkey name, e.g. "WinRAR".
        public string HandlerKeyName { get; set; }

        // The shellex\ContextMenuHandlers root this entry was found under, e.g. "*", "Directory".
        public string RegistryRootPath { get; set; }

        public ContextMenuEntryScope Scope { get; set; }

        public ContextMenuEntrySource Source { get; set; }

        // Original default value of the handler key (the CLSID string, without any "disabled_"
        // prefix). Needed to restore the entry exactly when re-enabling it.
        public string OriginalClsidValue { get; set; }

        // False for Modern entries (Windows has no per-verb toggle for someone else's installed
        // package) and for denylisted/built-in Windows entries this tool refuses to touch.
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

        public string ScopeDisplayName => ResourceLoaderInstance.ResourceLoader.GetString(
            Scope == ContextMenuEntryScope.CurrentUser
                ? "ContextMenuManager_ScopeCurrentUser"
                : "ContextMenuManager_ScopeAllUsers");

        public string SourceDisplayName => ResourceLoaderInstance.ResourceLoader.GetString(
            Source == ContextMenuEntrySource.Classic
                ? "ContextMenuManager_SourceClassic"
                : "ContextMenuManager_SourceModern");

        public string ScopeAndSourceDisplayName => $"{ScopeDisplayName} · {SourceDisplayName}";
    }
}
