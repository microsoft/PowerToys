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

        /// <summary>
        /// True once the user has approved this script's current content to run (trust-on-first-use).
        /// Emitted by the Host as <c>trusted</c>; recomputed from the script's content hash, so it
        /// flips back to false if the script body or its declared capabilities change.
        /// </summary>
        public bool Trusted { get; set; }

        /// <summary>
        /// Absolute path to the folder containing this script's <c>manifest.json</c>. Surfaced with an
        /// "open folder" button so users can quickly locate a script on disk (e.g. to edit or inspect it).
        /// </summary>
        public string FolderPath { get; set; } = string.Empty;

        /// <summary>Absolute path to the script's entry file (<c>FolderPath</c> + the manifest's entry).</summary>
        public string EntryFullPath { get; set; } = string.Empty;

        /// <summary>
        /// The parameters this script declares in its manifest. Surfaced under each script so users
        /// know what a community-authored script will ask for before running it.
        /// </summary>
        public List<PowerScriptParameterItem> Parameters { get; set; } = new();

        /// <summary>
        /// True when the script opts in to a parameter-selection prompt (WinUI 3 dialog) before it runs.
        /// </summary>
        public bool PromptForParameters { get; set; }

        /// <summary>True when the script declares at least one parameter.</summary>
        public bool HasParameters => Parameters is { Count: > 0 };

        /// <summary>Header for the parameters card, noting whether the user is prompted before running.</summary>
        public string ParametersHeader => PromptForParameters
            ? "Parameters (you'll be prompted before running)"
            : "Parameters";

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

        /// <summary>Human-readable trust state shown in the Settings list.</summary>
        public string TrustDisplay => Trusted
            ? "Trusted"
            : "Not yet trusted — you'll be asked to allow it the first time it runs";

        /// <summary>True when a folder path is known, so the location card and open button can show.</summary>
        public bool HasFolderPath => !string.IsNullOrEmpty(FolderPath);
    }
}
