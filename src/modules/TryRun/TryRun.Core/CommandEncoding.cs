// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace PowerToys.TryRun.Core;

public static class CommandEncoding
{
    public static string WindowsArgument(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Contains('\0'))
        {
            throw new ArgumentException("Arguments cannot contain null characters.");
        }

        var result = new StringBuilder("\"");
        var slashes = 0;
        foreach (var character in value)
        {
            if (character == '\\')
            {
                slashes++;
                continue;
            }

            result.Append('\\', character == '"' ? (slashes * 2) + 1 : slashes);
            result.Append(character);
            slashes = 0;
        }

        result.Append('\\', slashes * 2);
        return result.Append('"').ToString();
    }

    public static string ShellArgument(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Contains('\0'))
        {
            throw new ArgumentException("Arguments cannot contain null characters.");
        }

        return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }

    public static string LinuxPath(string windowsPath)
    {
        var path = WorkspacePath.LocalPath(windowsPath);
        return "/mnt/" + char.ToLowerInvariant(path[0]) + path[2..].Replace('\\', '/');
    }
}
