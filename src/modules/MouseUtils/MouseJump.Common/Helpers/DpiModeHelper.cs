// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.Versioning;

using Windows.Win32;
using Windows.Win32.UI.HiDpi;

namespace MouseJump.Common.Helpers;

/// <summary>
/// Based on WinForms Application.HighDpiMode
/// see https://github.com/dotnet/winforms/blob/1c324d074280ab5de6342d973069faa687f2c165/src/System.Windows.Forms/System/Windows/Forms/Application.cs#L18
///     https://github.com/dotnet/winforms/blob/1c324d074280ab5de6342d973069faa687f2c165/src/System.Windows.Forms.Primitives/src/System/Windows/Forms/Internals/ScaleHelper.cs#L14
/// </summary>
public static class DpiModeHelper
{
    /// <summary>
    /// See https://github.com/dotnet/winforms/blob/bd91bfb26ce90ac31e950c01dcb2b6e0776453a7/src/System.Private.Windows.Core/src/System/Private/Windows/OsVersion.cs#L9
    /// </summary>
    private static bool IsWindows10_1607OrGreater()
        => OperatingSystem.IsWindowsVersionAtLeast(major: 10, build: 14393);

    /// <summary>
    /// Ensures that the application is running in per-monitor v2 DPI awareness mode.
    /// </summary>
    /// <remarks>
    /// See https://github.com/dotnet/winforms/blob/bd91bfb26ce90ac31e950c01dcb2b6e0776453a7/src/System.Windows.Forms.Primitives/src/System/Windows/Forms/Internals/ScaleHelper.cs#L368
    /// </remarks>
    [SupportedOSPlatform("windows10.0.14393")]
    public static void EnsurePerMonitorV2Enabled()
    {
        // PowerToys supports the following operating systems:
        //
        // * Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.
        //
        // so we'll do the same(ish)
        if (!DpiModeHelper.IsWindows10_1607OrGreater())
        {
            throw new PlatformNotSupportedException(
                "Windows 10 version1607 or higher is required to use this application.");
        }

        // there's a weird problem where AreDpiAwarenessContextsEqual was returning TRUE in debug mode
        // but FALSE in release mode and i couldn't work out why, so we can't do it the *right* way.
        // we'll just use GetAwarenessFromDpiAwarenessContext instead as a near-enough workaround.
        var dpiAwarenessContext = PInvoke.GetThreadDpiAwarenessContext();
        var dpiAwareness = PInvoke.GetAwarenessFromDpiAwarenessContext(dpiAwarenessContext);
        if (dpiAwareness != DPI_AWARENESS.DPI_AWARENESS_PER_MONITOR_AWARE)
        {
            throw new InvalidOperationException($"high dpi mode is not set to {nameof(DPI_AWARENESS.DPI_AWARENESS_PER_MONITOR_AWARE)}");
        }
    }
}
