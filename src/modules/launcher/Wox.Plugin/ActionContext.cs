// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Wox.Plugin
{
    public class ActionContext
    {
        public SpecialKeyState SpecialKeyState { get; set; }
    }

    public class SpecialKeyState
    {
        public bool CtrlPressed { get; set; }

        public bool ShiftPressed { get; set; }

        public bool AltPressed { get; set; }

        public bool WinPressed { get; set; }
    }
}
