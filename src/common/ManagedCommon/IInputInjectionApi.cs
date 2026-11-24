// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace ManagedCommon
{
    /// <summary>
    /// The Win32 calls <see cref="InputInjection"/> makes. Tests replace them with fakes.
    /// </summary>
    internal interface IInputInjectionApi
    {
        uint SendInput(NativeMethods.INPUT[] inputs, out int lastError);

        bool IsKeyDown(ushort virtualKey);

        void Sleep(TimeSpan duration);
    }
}
