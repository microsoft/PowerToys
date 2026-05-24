// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace KeyboardManagerEditorUI.Controls
{
    public class KeyChangedEventArgs : EventArgs
    {
        public string OldKeyName { get; }

        public string NewKeyName { get; }

        public int NewKeyCode { get; }

        public KeyChangedEventArgs(string oldKeyName, string newKeyName, int newKeyCode)
        {
            OldKeyName = oldKeyName;
            NewKeyName = newKeyName;
            NewKeyCode = newKeyCode;
        }
    }
}
