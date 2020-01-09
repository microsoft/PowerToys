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
    internal struct MSG
    {
        /// <summary>
        /// A handle to the window whose window procedure receives the message. This member is NULL when the message is a thread message.
        /// </summary>
        public IntPtr Hwnd;

        /// <summary>
        /// The message identifier. Applications can only use the low word; the high word is reserved by the system.
        /// </summary>
        public int Message;

        /// <summary>
        /// Additional information about the message. The exact meaning depends on the value of the message member.
        /// </summary>
        public IntPtr WParam;

        /// <summary>
        /// Additional information about the message. The exact meaning depends on the value of the message member.
        /// </summary>
        public IntPtr LParam;

        /// <summary>
        /// The time at which the message was posted.
        /// </summary>
        public int Time;

        /// <summary>
        /// The x coordinate of cursor position, in screen coordinates, when the message was posted.
        /// </summary>
        public int PtX;

        /// <summary>
        /// The y coordinate of cursor position, in screen coordinates, when the message was posted.
        /// </summary>
        public int PtY;
    }
}
