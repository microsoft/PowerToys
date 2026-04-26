// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace PowerToys.MacroEngine;

public interface IAppScopeChecker
{
    bool IsForegroundAppMatch(string processName);
}

internal sealed class AppScopeChecker : IAppScopeChecker
{
    public bool IsForegroundAppMatch(string processName)
    {
        HWND hwnd = PInvoke.GetForegroundWindow();
        if (hwnd == HWND.Null) return false;

        unsafe
        {
            uint processId;
            PInvoke.GetWindowThreadProcessId(hwnd, &processId);
            try
            {
                using var proc = Process.GetProcessById((int)processId);
                var nameToMatch = Path.GetFileNameWithoutExtension(processName);
                return proc.ProcessName.Equals(nameToMatch, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
