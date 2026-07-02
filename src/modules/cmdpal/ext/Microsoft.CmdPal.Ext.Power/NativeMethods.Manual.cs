// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Windows.Win32;

/// <summary>
/// powrprof overlay APIs not yet available in CsWin32 metadata.
/// </summary>
internal static partial class PInvoke
{
    [LibraryImport("powrprof.dll")]
    internal static partial uint PowerGetActualOverlayScheme(out Guid actualOverlayGuid);

    [LibraryImport("powrprof.dll")]
    internal static partial uint PowerGetEffectiveOverlayScheme(out Guid effectiveOverlayGuid);

    [LibraryImport("powrprof.dll")]
    internal static partial uint PowerSetActiveOverlayScheme(ref Guid overlaySchemeGuid);
}
