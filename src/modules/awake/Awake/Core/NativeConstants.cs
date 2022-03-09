// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable SA1310 // Field names should not contain underscore

namespace Awake.Core
{
    internal class NativeConstants
    {
        internal const int WM_COMMAND = 0x111;
        internal const int WM_USER = 0x400;
        internal const uint WM_GETTEXT = 0x000D;

        // Popup menu constants.
        internal const int MF_BYPOSITION = 1024;
        internal const int MF_STRING = 0;
        internal const uint MF_MENUBREAK = 0x00000040;
        internal const uint MF_SEPARATOR = 0x00000800;
        internal const uint MF_POPUP = 0x00000010;
        internal const uint MF_UNCHECKED = 0x00000000;
        internal const uint MF_CHECKED = 0x00000008;
        internal const uint MF_OWNERDRAW = 0x00000100;
    }
}
