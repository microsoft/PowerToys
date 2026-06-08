// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Windows.Win32;

namespace MouseJump.HotKeys;

internal static class HotKeyHelper
{
    [SuppressMessage("SA1310", "SA1310:FieldNamesMustNotContainUnderscore", Justification = "Names match Win32 api")]
    public const uint WM_PRIV_UNREGISTER_HOTKEY = PInvoke.WM_USER + 2;

    [SuppressMessage("SA1310", "SA1310:FieldNamesMustNotContainUnderscore", Justification = "Names match Win32 api")]
    public const uint WM_PRIV_REGISTER_HOTKEY = PInvoke.WM_USER + 1;
}
