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
    public static class EditorConstants
    {
        // Default notification timeout
        public const int DefaultNotificationTimeout = 1500;

        public const int MaxShortcutModifiers = 4;

        public const int MaxShortcutActions = 1;

        public const int MaxChordActions = 2;

        public const int MaxShortcutSize = MaxShortcutModifiers + MaxShortcutActions;

        public const int MaxChordSize = MaxShortcutModifiers + MaxChordActions;
    }
}
