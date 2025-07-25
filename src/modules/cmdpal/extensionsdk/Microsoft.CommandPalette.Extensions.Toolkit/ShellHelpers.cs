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

    /// <summary>
    /// Parses the input string to extract the executable and its arguments.
    /// </summary>
    public static void ParseExecutableAndArgs(string input, out string executable, out string arguments)
    {
        input = input.Trim();
        executable = string.Empty;
        arguments = string.Empty;

        if (string.IsNullOrEmpty(input))
        {
            return;
        }

        if (input.StartsWith("\"", System.StringComparison.InvariantCultureIgnoreCase))
        {
            // Find the closing quote
            var closingQuoteIndex = input.IndexOf('\"', 1);
            if (closingQuoteIndex > 0)
            {
                executable = input.Substring(1, closingQuoteIndex - 1);
                if (closingQuoteIndex + 1 < input.Length)
                {
                    arguments = input.Substring(closingQuoteIndex + 1).TrimStart();
                }
            }
        }
        else
        {
            // Executable ends at first space
            var firstSpaceIndex = input.IndexOf(' ');
            if (firstSpaceIndex > 0)
            {
                executable = input.Substring(0, firstSpaceIndex);
                arguments = input[(firstSpaceIndex + 1)..].TrimStart();
            }
            else
            {
                executable = input;
            }
        }
    }

    /// <summary>
    /// Checks if a file exists somewhere in the PATH.
    /// If it exists, returns the full path to the file in the out parameter.
    /// If it does not exist, returns false and the out parameter is set to an empty string.
    /// <param name="filename">The name of the file to check.</param>
    /// <param name="fullPath">The full path to the file if it exists; otherwise an empty string.</param>
    /// <param name="token">An optional cancellation token to cancel the operation.</param>
    /// <returns>True if the file exists in the PATH; otherwise false.</returns>
    /// </summary>
    public static bool FileExistInPath(string filename, out string fullPath, CancellationToken? token = null)
    {
        fullPath = string.Empty;

        if (File.Exists(filename))
        {
            token?.ThrowIfCancellationRequested();
            fullPath = Path.GetFullPath(filename);
            return true;
        }
        else
        {
            var values = Environment.GetEnvironmentVariable("PATH");
            if (values != null)
            {
                foreach (var path in values.Split(';'))
                {
                    var path1 = Path.Combine(path, filename);
                    if (File.Exists(path1))
                    {
                        fullPath = Path.GetFullPath(path1);
                        return true;
                    }

                    token?.ThrowIfCancellationRequested();

                    var path2 = Path.Combine(path, filename + ".exe");
                    if (File.Exists(path2))
                    {
                        fullPath = Path.GetFullPath(path2);
                        return true;
                    }

                    token?.ThrowIfCancellationRequested();
                }

                return false;
            }
            else
            {
                return false;
            }
        }
    }
}
