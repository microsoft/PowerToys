// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace PowerScripts.Core.Security;

/// <summary>
/// Tells whether the current process is running elevated (as an administrator). PowerToys itself may
/// be launched elevated, but a user PowerScript must <b>never</b> inherit that admin token — a
/// downloaded or AI-authored script running with full administrator rights is exactly the security
/// risk PowerScripts refuses to take. The executor uses this to decide whether a script can be
/// launched directly or must first be dropped to the interactive user's token (see
/// <c>ProcessRunner</c>).
/// </summary>
public static class ProcessElevation
{
    private static readonly Lazy<bool> ElevatedState = new(DetectElevated);

    /// <summary>True when this process holds a full administrator token.</summary>
    public static bool IsElevated => ElevatedState.Value;

    [SupportedOSPlatform("windows")]
    private static bool DetectElevated()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception)
        {
            // If we can't determine elevation, assume elevated so the executor takes the safe
            // (de-elevate-or-refuse) path rather than risking an elevated script launch.
            return true;
        }
    }
}
