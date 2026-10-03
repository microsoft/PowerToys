// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security;
using ManagedCommon;
using Microsoft.Win32;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class WindowsRunHistory
{
    public static IReadOnlyList<string> Read()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU");
            return Read(key);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            Logger.LogError("Failed to read Windows Run history", ex);
            return [];
        }
    }

    internal static IReadOnlyList<string> Read(RegistryKey? key)
    {
        if (key?.GetValue("MRUList", null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not string order)
        {
            return [];
        }

        var history = new List<string>();
        var visited = new HashSet<char>();
        foreach (var entry in order)
        {
            if (entry is < 'a' or > 'z' || !visited.Add(entry))
            {
                continue;
            }

            if (key.GetValue(entry.ToString(), null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not string command)
            {
                continue;
            }

            // RunMRU appends a backslash and a show-command digit.
            if (command is [.., '\\', _] && char.IsAsciiDigit(command[^1]))
            {
                command = command[..^2];
            }

            if (command.Length > 0)
            {
                history.Add(command);
            }
        }

        return history;
    }
}
