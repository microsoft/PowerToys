// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ManagedCommon
{
    internal sealed class Win32InputInjectionApi : IInputInjectionApi
    {
        public uint SendInput(NativeMethods.INPUT[] inputs, out int lastError)
        {
            uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, NativeMethods.INPUT.Size);
            lastError = sent == inputs.Length ? 0 : Marshal.GetLastPInvokeError();
            return sent;
        }

        public bool IsKeyDown(ushort virtualKey)
        {
            // The most significant bit is set if the key is down.
            return (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        public void Sleep(TimeSpan duration)
        {
            Thread.Sleep(duration);
        }
    }
}
