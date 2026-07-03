// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>
    /// A saved Keyboard Manager hotkey that runs a PowerScript. Although it is persisted as a
    /// "Run Program" mapping (the engine's execution primitive), it is presented in the editor as a
    /// first-class PowerScript action so the user sees "PowerScript: &lt;name&gt;" rather than a raw
    /// program-path card.
    /// </summary>
    public class PowerScriptShortcut : IToggleableShortcut
    {
        public List<string> Shortcut { get; set; } = new List<string>();

        /// <summary>The PowerScript id this hotkey runs.</summary>
        public string ScriptId { get; set; } = string.Empty;

        /// <summary>The PowerScript's friendly name, for display.</summary>
        public string ScriptName { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public string Id { get; set; } = string.Empty;

        public string AppName { get; set; } = string.Empty;
    }
}
