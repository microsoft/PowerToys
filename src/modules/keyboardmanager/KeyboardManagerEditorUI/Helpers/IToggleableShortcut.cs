// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyboardManagerEditorUI.Helpers
{
    internal interface IToggleableShortcut
    {
        public List<string> Shortcut { get; set; }

        bool IsActive { get; set; }

        string Id { get; set; }

        string AppName { get; set; }

        bool IsAllApps { get; set; }

        // Raw virtual-key codes of the trigger (original) keys, kept so the search/filter
        // bar can classify modifiers by VK code (locale-independent) instead of display name.
        IReadOnlyList<int> TriggerKeyCodes { get; set; }

        // Lowercased, pre-computed text used by the filter bar's text search.
        string SearchableText { get; set; }
    }
}
