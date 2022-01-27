// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Wox.Plugin.Common.Win32
{
#pragma warning disable CA1707 // same resason:
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "These are the names used by win32.")]
    public static class Constants
    {
        /// <summary>
        /// GetWindowLong index to retrieves the extended window styles.
        /// </summary>
        public const int GWL_EXSTYLE = -20;

        /// <summary>
        /// A window receives this message when the user chooses a command from the Window menu (formerly known as the system or control menu)
        /// or when the user chooses the maximize button, minimize button, restore button, or close button.
        /// </summary>
        public const int WM_SYSCOMMAND = 0x0112;

        /// <summary>
        /// Restores the window to its normal position and size.
        /// </summary>
        public const int SC_RESTORE = 0xf120;
    }
}
