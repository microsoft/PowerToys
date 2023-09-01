// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace FancyMouse.NativeMethods;

internal static partial class Core
{
    /// <summary>
    /// The return codes used by COM interfaces.For more information, see Structure of the COM Error Codes.
    /// To test an HRESULT value, use the FAILED and SUCCEEDED macros.
    /// This type is declared in WinNT.h as follows:
    /// typedef LONG HRESULT;
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/winprog/windows-data-types
    /// </remarks>
    internal readonly struct HRESULT
    {
        public readonly int Value;

        public HRESULT(int value)
        {
            this.Value = value;
        }

        public static int Size =>
            Marshal.SizeOf(typeof(HRESULT));

        public static implicit operator int(HRESULT value) => value.Value;

        public static implicit operator HRESULT(int value) => new(value);

        public override string ToString()
        {
            return $"{this.GetType().Name}({this.Value})";
        }
    }
}
