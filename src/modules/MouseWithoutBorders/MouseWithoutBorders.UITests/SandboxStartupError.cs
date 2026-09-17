// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;

namespace Microsoft.MouseWithoutBorders.UITests;

internal static class SandboxStartupError
{
    public static string? Read(string executableName, string windowClass, Func<string[]> readStaticText)
    {
        if (!string.Equals(executableName, "WindowsSandbox.exe", StringComparison.OrdinalIgnoreCase) ||
            windowClass != "#32770")
        {
            return null;
        }

        var message = string.Join(Environment.NewLine, readStaticText());
        if (!message.Contains("Windows Sandbox failed to start", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var code = Regex.Match(message, @"\b0x[0-9A-Fa-f]{8}\b");
        return code.Success ? $"Windows Sandbox failed to start ({code.Value})." : "Windows Sandbox failed to start.";
    }
}
