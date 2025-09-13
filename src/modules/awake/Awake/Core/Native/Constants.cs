// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Awake.Core.Native
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Win32 API convention.")]
    internal sealed class Constants
    {
        // Window Messages
        internal const uint WM_COMMAND = 0x0111;
        internal const uint WM_USER = 0x0400U;
        internal const uint WM_CLOSE = 0x0010;
        internal const int WM_CREATE = 0x0001;
        internal const int WM_DESTROY = 0x0002;
        internal const int WM_LBUTTONDOWN = 0x0201;
        internal const int WM_RBUTTONDOWN = 0x0204;

        // Menu Flags
        internal const uint MF_BYPOSITION = 1024;
        internal const uint MF_STRING = 0;
        internal const uint MF_SEPARATOR = 0x00000800;
        internal const uint MF_POPUP = 0x00000010;
        internal const uint MF_UNCHECKED = 0x00000000;
        internal const uint MF_CHECKED = 0x00000008;
        internal const uint MF_ENABLED = 0x00000000;
        internal const uint MF_DISABLED = 0x00000002;

        // Standard Handles
        internal const int STD_OUTPUT_HANDLE = -11;

        // Generic Access Rights
        internal const uint GENERIC_WRITE = 0x40000000;
        internal const uint GENERIC_READ = 0x80000000;

        // Notification Icons
        internal const int NIF_ICON = 0x00000002;
        internal const int NIF_MESSAGE = 0x00000001;
        internal const int NIF_TIP = 0x00000004;
        internal const int NIM_ADD = 0x00000000;
        internal const int NIM_DELETE = 0x00000002;
        internal const int NIM_MODIFY = 0x00000001;

        // Track Popup Menu Flags
        internal const uint TPM_LEFT_ALIGN = 0x0000;
        internal const uint TPM_BOTTOMALIGN = 0x0020;
        internal const uint TPM_LEFT_BUTTON = 0x0000;

        // Menu Item Info Flags
        internal const uint MNS_AUTO_DISMISS = 0x10000000;
        internal const uint MIM_STYLE = 0x00000010;

        // Attach Console
        internal const int ATTACH_PARENT_PROCESS = -1;
    }
}
