// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Awake.Core.Native
{
    internal static class IdleTime
    {
        // Keep original native field names but suppress StyleCop (interop requires exact names).
        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
#pragma warning disable SA1307 // Interop field naming
            public uint cbSize;
            public uint dwTime;
#pragma warning restore SA1307
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        public static TimeSpan GetIdleTime()
        {
            LASTINPUTINFO info = new()
            {
                cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>(),
            };

            if (!GetLastInputInfo(ref info))
            {
                return TimeSpan.Zero;
            }

            // Calculate elapsed milliseconds since last input considering Environment.TickCount wrap.
            uint lastInputTicks = info.dwTime;
            uint nowTicks = (uint)Environment.TickCount;
            uint delta = nowTicks >= lastInputTicks ? nowTicks - lastInputTicks : (uint.MaxValue - lastInputTicks) + nowTicks + 1;
            return TimeSpan.FromMilliseconds(delta);
        }
    }
}
