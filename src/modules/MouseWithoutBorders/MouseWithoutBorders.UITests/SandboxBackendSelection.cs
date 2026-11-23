// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.MouseWithoutBorders.UITests;

internal static class SandboxBackendSelection
{
    public static string Resolve(string? requested, int windowsBuild)
    {
        var mode = requested ?? "Legacy";
        if (string.Equals(mode, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            return windowsBuild >= 26100 ? "WinApp" : "Legacy";
        }

        if (string.Equals(mode, "Legacy", StringComparison.OrdinalIgnoreCase))
        {
            return "Legacy";
        }

        if (string.Equals(mode, "WinApp", StringComparison.OrdinalIgnoreCase))
        {
            if (windowsBuild < 26100)
            {
                throw new PlatformNotSupportedException("The WinApp Sandbox backend requires Windows 11 24H2 or newer. Use Legacy on Windows 10.");
            }

            return "WinApp";
        }

        throw new ArgumentException($"Unknown Sandbox backend '{requested}'. Expected Auto, Legacy, or WinApp.", nameof(requested));
    }
}
