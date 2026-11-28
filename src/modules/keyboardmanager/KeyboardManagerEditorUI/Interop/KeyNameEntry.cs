// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace KeyboardManagerEditorUI.Interop
{
    public record KeyNameEntry(int KeyCode, string DisplayName)
    {
        // The key picker's ListViewItem automation peers fall back to ToString() for their name.
        public override string ToString() => DisplayName;
    }
}
