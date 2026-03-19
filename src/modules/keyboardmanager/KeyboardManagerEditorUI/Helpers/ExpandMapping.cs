// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace KeyboardManagerEditorUI.Helpers
{
    public class ExpandMapping : IToggleableShortcut
    {
        public List<string> Shortcut { get; set; } = new List<string>();

        public string Abbreviation { get; set; } = string.Empty;

        public string TriggerKey { get; set; } = "Space";

        public string ExpandedText { get; set; } = string.Empty;

        public bool IsAllApps { get; set; } = true;

        public string AppName { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public string Id { get; set; } = string.Empty;
    }
}
