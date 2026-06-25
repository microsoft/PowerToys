// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>
    /// A single PowerScript entry as surfaced to the Keyboard Manager editor's "PowerScript" action picker.
    /// </summary>
    public sealed class PowerScriptInfo
    {
        public string Id { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string Kind { get; init; } = string.Empty;

        public override string ToString() => Name;
    }
}
