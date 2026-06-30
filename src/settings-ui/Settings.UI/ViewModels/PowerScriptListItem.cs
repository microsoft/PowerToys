// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// A single PowerScript shown in the Settings list. This is a read-only projection of the
    /// script's <c>manifest.json</c> (the source of truth), as emitted by
    /// <c>PowerScripts.Host.exe list --json</c>. The Settings page only displays this information;
    /// authors change it by editing the manifest.
    /// </summary>
    public sealed class PowerScriptListItem
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string Runtime { get; set; } = string.Empty;

        public PowerScriptInput Input { get; set; }

        public List<string> Surfaces { get; set; } = new();

        public List<string> Capabilities { get; set; } = new();

        public string KindGlyph => string.Equals(Kind, "file", StringComparison.OrdinalIgnoreCase)
            ? "\uE8A5" // file action
            : "\uE756"; // system action

        /// <summary>True for file scripts, which can be triggered from the Explorer right-click menu.</summary>
        public bool IsFileScript => string.Equals(Kind, "file", StringComparison.OrdinalIgnoreCase);

        /// <summary>Comma-separated trigger extensions declared in the manifest (file scripts only).</summary>
        public string ExtensionsDisplay => Input?.Extensions is { Count: > 0 } exts
            ? string.Join(", ", exts)
            : "—";

        /// <summary>Comma-separated list of the surfaces this script appears on.</summary>
        public string SurfacesDisplay => Surfaces is { Count: > 0 }
            ? string.Join(", ", Surfaces)
            : "—";

        /// <summary>Comma-separated list of the capabilities the script declares.</summary>
        public string CapabilitiesDisplay => Capabilities is { Count: > 0 }
            ? string.Join(", ", Capabilities)
            : "—";

        /// <summary>Friendly runtime label (e.g. "PowerShell").</summary>
        public string RuntimeDisplay => string.IsNullOrEmpty(Runtime) ? "—" : Runtime;
    }
}
