// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public static class ShellHelpers
{
    public static bool OpenCommandInShell(string? path, string? pattern, string? arguments, string? workingDir = null, ShellRunAsType runAs = ShellRunAsType.None, bool runWithHiddenWindow = false)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            // Log.Warn($"Trying to run OpenCommandInShell with an empty pattern. The default browser definition might have issues. Path: '${path ?? string.Empty}' ; Arguments: '${arguments ?? string.Empty}' ; Working Directory: '${workingDir ?? string.Empty}'", typeof(ShellHelpers));
        }
        else if (pattern.Contains("%1", StringComparison.Ordinal))
        {
            arguments = pattern.Replace("%1", arguments);
        }

        return OpenInShell(path, arguments, workingDir, runAs, runWithHiddenWindow);
    }

    public static bool OpenInShell(string? path, string? arguments = null, string? workingDir = null, ShellRunAsType runAs = ShellRunAsType.None, bool runWithHiddenWindow = false)
    {
        using var process = new Process();
        process.StartInfo.FileName = path;
        process.StartInfo.WorkingDirectory = string.IsNullOrWhiteSpace(workingDir) ? string.Empty : workingDir;
        process.StartInfo.Arguments = string.IsNullOrWhiteSpace(arguments) ? string.Empty : arguments;
        process.StartInfo.WindowStyle = runWithHiddenWindow ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal;
        process.StartInfo.UseShellExecute = true;

        if (runAs == ShellRunAsType.Administrator)
        {
            process.StartInfo.Verb = "RunAs";
        }
        else if (runAs == ShellRunAsType.OtherUser)
        {
            process.StartInfo.Verb = "RunAsUser";
        }

        try
        {
            process.Start();
            return true;
        }
        catch (Win32Exception)
        {
            // Log.Exception($"Unable to open {path}: {ex.Message}", ex, MethodBase.GetCurrentMethod().DeclaringType);
            return false;
        }
    }

    public enum ShellRunAsType
    {
        None,
        Administrator,
        OtherUser,
    }
}
