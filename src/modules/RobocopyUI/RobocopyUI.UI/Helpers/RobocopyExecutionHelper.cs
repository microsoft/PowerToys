// Copyright (c) Microsoft Corporation.
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace RobocopyUI.Helpers;

internal static class RobocopyExecutionHelper
{
    internal static string GetStatusResourceKey(int exitCode)
    {
        if ((exitCode & 16) != 0)
        {
            return "Status_Fail";
        }

        if ((exitCode & 8) != 0)
        {
            return "Status_8";
        }

        return "Status_" + (exitCode & 7);
    }

    internal static string EscapeCommandForCmd(string command)
    {
        return command
            .Replace("^", "^^", System.StringComparison.Ordinal)
            .Replace("&", "^&", System.StringComparison.Ordinal)
            .Replace("|", "^|", System.StringComparison.Ordinal)
            .Replace("<", "^<", System.StringComparison.Ordinal)
            .Replace(">", "^>", System.StringComparison.Ordinal)
            .Replace("(", "^(", System.StringComparison.Ordinal)
            .Replace(")", "^)", System.StringComparison.Ordinal)
            .Replace("%", "^%", System.StringComparison.Ordinal)
            .Replace("!", "^!", System.StringComparison.Ordinal);
    }
}
