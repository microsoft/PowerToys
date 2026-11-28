// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using KeyboardManagerEditorUI.Interop;

namespace KeyboardManagerEditorUI.Settings
{
    public class EditorSettings
    {
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "This JSON DTO retains its writable deserialization contract, including existing omitted-member defaults and explicit-null behavior.")]
        public Dictionary<string, ShortcutSettings> ShortcutSettingsDictionary { get; set; } = new Dictionary<string, ShortcutSettings>();

        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "This JSON DTO retains its writable deserialization contract, including existing omitted-member defaults and explicit-null behavior.")]
        public Dictionary<string, List<string>> ProfileDictionary { get; set; } = new Dictionary<string, List<string>>();

        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "This JSON DTO retains its writable deserialization contract, including existing omitted-member defaults and explicit-null behavior.")]
        public Dictionary<ShortcutOperationType, List<string>> ShortcutsByOperationType { get; set; } = new Dictionary<ShortcutOperationType, List<string>>();

        public string ActiveProfile { get; set; } = string.Empty;
    }
}
