// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// A single PowerScript shown in the Settings list. Mirrors the projection emitted by
    /// <c>PowerScripts.Host.exe list --json</c>.
    /// </summary>
    public sealed class PowerScriptListItem
    {
        private string _extensionsText;

        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public PowerScriptInput Input { get; set; }

        public string KindGlyph => string.Equals(Kind, "file", StringComparison.OrdinalIgnoreCase)
            ? "\uE8A5" // file action
            : "\uE756"; // system action

        /// <summary>True for file scripts, which can be triggered from the Explorer right-click menu.</summary>
        public bool IsFileScript => string.Equals(Kind, "file", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Comma-separated list of the file extensions this script triggers on, editable from the
        /// Settings page. Initialized from <see cref="Input"/> and written back via the host.
        /// </summary>
        public string ExtensionsText
        {
            get => _extensionsText ??= Input?.Extensions is { Count: > 0 } exts
                ? string.Join(", ", exts)
                : string.Empty;
            set => _extensionsText = value;
        }
    }
}
