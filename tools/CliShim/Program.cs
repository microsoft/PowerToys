// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace PowerToys.CliShim;

/// <summary>
/// A tiny multi-call launcher ("shim"). One Native AOT binary is copied to several
/// command names (for example fancyzones.exe). At run time it resolves its own file
/// name to the matching PowerToys CLI, forwards the user's arguments verbatim, shares
/// the console, and returns the launched process's exit code unchanged.
/// </summary>
internal static class Program
{
    // Command name (the shim's own file name without extension) -> target CLI path,
    // relative to the shim's own directory. The shims are installed in "<install>\cli\",
    // so the targets sit one level up (install root) or under the WinUI3Apps subfolder.
    private static readonly Dictionary<string, string> Targets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fancyzones"] = @"..\FancyZonesCLI.exe",
        ["imageresizer"] = @"..\WinUI3Apps\PowerToys.ImageResizerCLI.exe",
        ["filelocksmith"] = @"..\FileLocksmithCLI.exe",
    };

    private static int Main()
    {
        // Stay alive on Ctrl+C / Ctrl+Break so we can still capture the child's exit
        // code; the child shares the console group and receives the signal directly.
        Console.CancelKeyPress += static (_, e) => e.Cancel = true;

        string shimPath = Environment.ProcessPath ?? string.Empty;
        string shimDirectory = Path.GetDirectoryName(shimPath) ?? Directory.GetCurrentDirectory();
        string commandName = Path.GetFileNameWithoutExtension(shimPath);

        if (!Targets.TryGetValue(commandName, out string? relativeTarget))
        {
            Console.Error.WriteLine($"cli-shim: no PowerToys CLI is mapped to the command '{commandName}'.");
            Console.Error.WriteLine($"cli-shim: known commands: {string.Join(", ", Targets.Keys)}.");
            return 9009; // The exit code cmd.exe returns for "command not found".
        }

        string targetPath = Path.GetFullPath(Path.Combine(shimDirectory, relativeTarget));

        if (!File.Exists(targetPath))
        {
            Console.Error.WriteLine($"cli-shim: target not found: \"{targetPath}\".");
            return 9009;
        }

        // Forward the user's arguments byte-for-byte. Environment.CommandLine is the raw
        // command line (the managed equivalent of GetCommandLineW); stripping argv[0]
        // preserves the user's exact quoting, which re-quoting parsed args would corrupt.
        string forwardedArguments = StripArgumentZero(Environment.CommandLine);

        ProcessStartInfo startInfo = new()
        {
            FileName = targetPath,
            Arguments = forwardedArguments,
            UseShellExecute = false, // Inherit stdin/stdout/stderr and stay in this console.
        };

        try
        {
            using Process? child = Process.Start(startInfo);
            if (child is null)
            {
                Console.Error.WriteLine($"cli-shim: failed to start \"{targetPath}\".");
                return 9009;
            }

            child.WaitForExit();
            return child.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"cli-shim: failed to launch \"{targetPath}\": {ex.Message}");
            return 9009;
        }
    }

    /// <summary>
    /// Returns the command line with its first token (argv[0]) removed, following the C
    /// runtime rule for the program name: when it starts with a quote it ends at the next
    /// quote, otherwise it ends at the first whitespace. Whitespace before the first real
    /// argument is then trimmed.
    /// </summary>
    /// <param name="commandLine">The raw process command line.</param>
    /// <returns>The remaining arguments, verbatim.</returns>
    private static string StripArgumentZero(string commandLine)
    {
        int index = 0;

        if (index < commandLine.Length && commandLine[index] == '"')
        {
            index++;
            while (index < commandLine.Length && commandLine[index] != '"')
            {
                index++;
            }

            if (index < commandLine.Length)
            {
                index++; // Consume the closing quote.
            }
        }
        else
        {
            while (index < commandLine.Length && commandLine[index] != ' ' && commandLine[index] != '\t')
            {
                index++;
            }
        }

        while (index < commandLine.Length && (commandLine[index] == ' ' || commandLine[index] == '\t'))
        {
            index++;
        }

        return commandLine[index..];
    }
}
