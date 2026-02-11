// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KeyboardManagerEditorUI.Interop;

namespace KeyboardManagerEditorUI.Settings
{
    public class ShortcutSettings
    {
        public string Id { get; set; } = string.Empty;

        public ShortcutKeyMapping Shortcut { get; set; } = new ShortcutKeyMapping();

        public List<string> Profiles { get; set; } = new List<string>();

        public bool IsActive { get; set; } = true;
    }
}
