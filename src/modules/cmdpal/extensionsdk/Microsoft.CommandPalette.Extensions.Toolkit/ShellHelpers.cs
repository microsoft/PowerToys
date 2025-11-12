// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Win32;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public static class ShellHelpers
{
    /// <summary>
    /// These are the executable file extensions that Windows Shell recognizes. Unlike CMD/PowerShell,
    /// Shell does not use PATHEXT, but has a magic fixed list.
    /// </summary>
    public static string[] ExecutableExtensions { get; } = [".PIF", ".COM", ".EXE", ".BAT", ".CMD"];

    /// <summary>
    /// Determines whether the specified file name represents an executable file
    /// by examining its extension against the known list of Windows Shell
    /// executable extensions (a fixed list that does not honor PATHEXT).
    /// </summary>
    /// <param name="fileName">The file name (with or without path) whose extension will be evaluated.</param>
    /// <returns>
    /// True if the file name has an extension that matches one of the recognized executable
    /// extensions; otherwise, false. Returns false for null, empty, or whitespace input.
    /// </returns>
    public static bool IsExecutableFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var fileExtension = Path.GetExtension(fileName);
        return IsExecutableExtension(fileExtension);
    }

    /// <summary>
    /// Determines whether the provided file extension (including the leading dot)
    /// is one of the Windows Shell recognized executable extensions.
    /// </summary>
    /// <param name="fileExtension">The file extension to test. Should include the leading dot (e.g. ".exe").</param>
    /// <returns>
    /// True if the extension matches (case-insensitive) one of the known executable
    /// extensions; false if it does not match or if the input is null/whitespace.
    /// </returns>
    public static bool IsExecutableExtension(string fileExtension)
    {
        if (string.IsNullOrWhiteSpace(fileExtension))
        {
            // Shell won't execute app with a filename without an extension
            return false;
        }

        foreach (var extension in ExecutableExtensions)
        {
            if (string.Equals(fileExtension, extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

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
            if (values is not null)
            {
                foreach (var path in values.Split(Path.PathSeparator))
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
            }

            return false;
        }
    }

    private static bool TryResolveFromAppPaths(string name, [NotNullWhen(true)] out string? fullPath)
    {
        try
        {
            fullPath = TryHiveView(RegistryHive.CurrentUser, RegistryView.Registry64) ??
                       TryHiveView(RegistryHive.CurrentUser, RegistryView.Registry32) ??
                       TryHiveView(RegistryHive.LocalMachine, RegistryView.Registry64) ??
                       TryHiveView(RegistryHive.LocalMachine, RegistryView.Registry32) ?? string.Empty;

            return !string.IsNullOrEmpty(fullPath);

            string? TryHiveView(RegistryHive hive, RegistryView view)
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var k1 = baseKey.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{name}.exe");
                var val = (k1?.GetValue(null) as string)?.Trim('"');
                if (!string.IsNullOrEmpty(val))
                {
                    return val;
                }

                // Some vendors create keys without .exe in the subkey name; check that too.
                using var k2 = baseKey.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{name}");
                return (k2?.GetValue(null) as string)?.Trim('"');
            }
        }
        catch (Exception)
        {
            fullPath = null;
            return false;
        }
    }

    /// <summary>
    /// Mimics Windows Shell behavior to resolve an executable name to a full path.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="fullPath"></param>
    /// <returns></returns>
    public static bool TryResolveExecutableAsShell(string name, out string fullPath)
    {
        // First check if we can find the file in the registry
        if (TryResolveFromAppPaths(name, out var path))
        {
            fullPath = path;
            return true;
        }

        // If the name does not have an extension, try adding common executable extensions
        // this order mimics Windows Shell behavior
        // Note: HasExtension check follows Shell behavior, but differs from the
        // Start Menu search results, which will offer file name with extensions + ".exe"
        var nameHasExtension = Path.HasExtension(name);
        if (!nameHasExtension)
        {
            foreach (var ext in ExecutableExtensions)
            {
                var nameWithExt = name + ext;
                if (FileExistInPath(nameWithExt, out fullPath))
                {
                    return true;
                }
            }
        }

        fullPath = string.Empty;
        return false;
    }
}
