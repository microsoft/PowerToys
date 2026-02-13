// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyboardManagerEditorUI.Interop
{
    public enum ShortcutOperationType
    {
        RemapShortcut = 0,
        RunProgram = 1,
        OpenUri = 2,
        RemapText = 3,
        RemapMouseButton = 4,
        RemapKeyToMouse = 5,
    }
}
