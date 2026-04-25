// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using KeyboardManagerEditorUI.Interop;

namespace KeyboardManagerEditorUI.Settings
{
    public class EditorSettings
    {
        public Dictionary<string, ShortcutSettings> ShortcutSettingsDictionary { get; set; } = new Dictionary<string, ShortcutSettings>();

        public Dictionary<string, List<string>> ProfileDictionary { get; set; } = new Dictionary<string, List<string>>();

        public Dictionary<ShortcutOperationType, List<string>> ShortcutsByOperationType { get; set; } = new Dictionary<ShortcutOperationType, List<string>>();

        public string ActiveProfile { get; set; } = string.Empty;
    }
}
