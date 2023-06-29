// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Awake.Core.Native
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Win32 API convention.")]
    internal sealed class Constants
    {
        internal const uint WM_COMMAND = 0x111;
        internal const uint WM_USER = 0x400;
        internal const uint WM_GETTEXT = 0x000D;
        internal const uint WM_CLOSE = 0x0010;

        // Popup menu constants.
        internal const uint MF_BYPOSITION = 1024;
        internal const uint MF_STRING = 0;
        internal const uint MF_MENUBREAK = 0x00000040;
        internal const uint MF_SEPARATOR = 0x00000800;
        internal const uint MF_POPUP = 0x00000010;
        internal const uint MF_UNCHECKED = 0x00000000;
        internal const uint MF_CHECKED = 0x00000008;
        internal const uint MF_OWNERDRAW = 0x00000100;

        internal const uint MF_ENABLED = 0x00000000;
        internal const uint MF_DISABLED = 0x00000002;

        internal const int STD_OUTPUT_HANDLE = -11;
        internal const uint GENERIC_WRITE = 0x40000000;
        internal const uint GENERIC_READ = 0x80000000;
    }
}
