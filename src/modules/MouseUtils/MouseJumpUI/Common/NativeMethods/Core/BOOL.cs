// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseJumpUI.Common.NativeMethods;

internal static partial class Core
{
    /// <summary>
    /// A Boolean variable (should be TRUE or FALSE).
    /// This type is declared in WinDef.h as follows:
    /// typedef int BOOL;
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/winprog/windows-data-types
    /// </remarks>
    internal readonly struct BOOL
    {
        public readonly int Value;

        public BOOL(int value)
        {
            this.Value = value;
        }

        public BOOL(bool value)
        {
            this.Value = value ? 1 : 0;
        }

        public static implicit operator bool(BOOL value) => value.Value != 0;

        public static implicit operator BOOL(bool value) => new(value);

        public static implicit operator int(BOOL value) => value.Value;

        public static implicit operator BOOL(int value) => new(value);

        public override string ToString()
        {
            return $"{this.GetType().Name}({this.Value})";
        }
    }
}
