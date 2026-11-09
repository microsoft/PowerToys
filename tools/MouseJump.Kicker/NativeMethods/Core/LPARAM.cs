// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseJump.Kicker.NativeMethods;

internal static partial class Core
{
    /// <summary>
    /// A message parameter. This type contains information about a message.
    /// This type is declared in WinDef.h as follows:
    /// typedef LONG_PTR LPARAM;
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/winprog/windows-data-types
    /// </remarks>
    internal readonly struct LPARAM
    {
        public static readonly LPARAM Zero = new(0);

        public readonly nint Value;

        public LPARAM(nint value)
        {
            this.Value = value;
        }

        public static implicit operator nint(LPARAM value) => value.Value;

        public static explicit operator LPARAM(nint value) => new(value);

        public override string ToString()
        {
            return $"{this.GetType().Name}({this.Value})";
        }
    }
}
