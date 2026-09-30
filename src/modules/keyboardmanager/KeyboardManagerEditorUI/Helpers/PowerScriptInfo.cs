// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

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

        /// <summary>The declared I/O shapes; the old system/file "kind" is fully derived from this.</summary>
        public PowerScriptIoInfo? Io { get; init; }

        public List<PowerScriptParameterInfo> Parameters { get; init; } = new();

        /// <summary>
        /// True when the script consumes no input (its input shape is <c>none</c>) — i.e. a hotkey-runnable
        /// action. This replaces the removed "kind == system" check.
        /// </summary>
        public bool IsAction =>
            Io is null || string.IsNullOrEmpty(Io.Input) || string.Equals(Io.Input, "none", StringComparison.OrdinalIgnoreCase);

        public override string ToString() => Name;
    }
}
