// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace MouseJumpUI.Interop;

internal static partial class Shcore
{
    [SuppressMessage(
        "StyleCop.CSharp.NamingRules",
        "SA1310:Field names should not contain underscore",
        Justification = "Native Win32 name")]
    public const uint S_OK = 0x00000000;
}
