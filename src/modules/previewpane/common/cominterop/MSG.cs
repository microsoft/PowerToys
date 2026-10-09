// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Common.ComInterlop
{
    /// <summary>
    /// Contains message information from a thread's message queue.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        /// <summary>
        /// Gets or sets a handle to the window whose window procedure receives the message. This member is NULL when the message is a thread message.
        /// </summary>
        public IntPtr Hwnd { get; set; }

        /// <summary>
        /// Gets or sets the message identifier. Applications can only use the low word; the high word is reserved by the system.
        /// </summary>
        public int Message { get; set; }

        /// <summary>
        /// Gets or sets additional information about the message. The exact meaning depends on the value of the message member.
        /// </summary>
        public IntPtr WParam { get; set; }

        /// <summary>
        /// Gets or sets additional information about the message. The exact meaning depends on the value of the message member.
        /// </summary>
        public IntPtr LParam { get; set; }

        /// <summary>
        /// Gets or sets the time at which the message was posted.
        /// </summary>
        public int Time { get; set; }

        /// <summary>
        /// Gets or sets the x coordinate of cursor position, in screen coordinates, when the message was posted.
        /// </summary>
        public int PtX { get; set; }

        /// <summary>
        /// Gets or sets the y coordinate of cursor position, in screen coordinates, when the message was posted.
        /// </summary>
        public int PtY { get; set; }
    }
}
