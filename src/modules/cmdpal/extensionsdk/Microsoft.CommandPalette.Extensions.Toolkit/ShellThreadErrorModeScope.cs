// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Suppresses system critical-error and legacy OpenFile dialogs for synchronous Shell work.
/// </summary>
/// <remarks>
/// This is a ref struct so the scope cannot accidentally survive an asynchronous suspension.
/// </remarks>
internal readonly ref partial struct ShellThreadErrorModeScope
{
    private const uint SemFailCriticalErrors = 0x00000001;
    private const uint SemNoOpenFileErrorBox = 0x00008000;

    internal const uint SuppressedModes = SemFailCriticalErrors | SemNoOpenFileErrorBox;

    private readonly uint _previousMode;
    private readonly bool _restorePreviousMode;

    private ShellThreadErrorModeScope(uint previousMode, bool restorePreviousMode)
    {
        _previousMode = previousMode;
        _restorePreviousMode = restorePreviousMode;
    }

    internal static uint CurrentMode => NativeMethods.GetThreadErrorMode();

    public static ShellThreadErrorModeScope SuppressShellDialogs()
    {
        var currentMode = CurrentMode;
        var requestedMode = currentMode | SuppressedModes;
        if (requestedMode == currentMode)
        {
            return new ShellThreadErrorModeScope(default, restorePreviousMode: false);
        }

        // This is best-effort hardening. A failure must not prevent the Shell call itself.
        return NativeMethods.SetThreadErrorMode(requestedMode, out var previousMode) != 0
            ? new ShellThreadErrorModeScope(previousMode, restorePreviousMode: true)
            : new ShellThreadErrorModeScope(default, restorePreviousMode: false);
    }

    public void Dispose()
    {
        if (_restorePreviousMode)
        {
            _ = NativeMethods.SetThreadErrorMode(_previousMode, out _);
        }
    }

    private static partial class NativeMethods
    {
        [LibraryImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial uint GetThreadErrorMode();

        [LibraryImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial int SetThreadErrorMode(uint newMode, out uint oldMode);
    }
}
