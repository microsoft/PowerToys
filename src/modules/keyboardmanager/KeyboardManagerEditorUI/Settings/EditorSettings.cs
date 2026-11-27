// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using KeyboardManagerEditorUI.Interop;

namespace KeyboardManagerEditorUI.Settings
{
    public class EditorSettings
    {
        /// <summary>Every row names the profiles it belongs to; legacy untagged rows have been migrated.</summary>
        internal const int CurrentSchemaVersion = 1;

        public Dictionary<string, ShortcutSettings> ShortcutSettingsDictionary { get; set; } = new Dictionary<string, ShortcutSettings>();

        public Dictionary<string, List<string>> ProfileDictionary { get; set; } = new Dictionary<string, List<string>>();

        public Dictionary<ShortcutOperationType, List<string>> ShortcutsByOperationType { get; set; } = new Dictionary<ShortcutOperationType, List<string>>();

        public string ActiveProfile { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the version of the profile-membership format. Files written before profiles
        /// could be created (v0.100, v0.101) have no value, which reads as 0: rows in them may name
        /// no profile at all. <see cref="CurrentSchemaVersion"/> means every row names its profiles.
        /// </summary>
        public int SchemaVersion { get; set; }
    }
}
