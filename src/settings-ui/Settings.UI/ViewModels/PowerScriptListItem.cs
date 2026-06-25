// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// A single PowerScript shown in the Settings list. Mirrors the projection emitted by
    /// <c>PowerScripts.Host.exe list --json</c>.
    /// </summary>
    public sealed class PowerScriptListItem
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string KindGlyph => string.Equals(Kind, "file", System.StringComparison.OrdinalIgnoreCase)
            ? "\uE8A5" // file action
            : "\uE756"; // system action
    }
}
