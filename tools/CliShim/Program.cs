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
    // The exit code cmd.exe returns for "command not found". Used only when the shim is
    // invoked under a name that is not in the Targets table (for example a renamed copy).
    private const int ExitCommandNotMapped = 9009;

    // A distinct code for "the command is known, but its target could not be launched"
    // (missing target, Process.Start failure). Keeping this separate from 9009 lets a
    // calling script tell a typo'd/unmapped command from a broken install.
    private const int ExitLaunchFailed = 1;

    // Command name (the shim's own file name without extension) -> target CLI path,
    // relative to the shim's own directory. The shims are installed in "<install>\cli\",
    // so the targets sit one level up (install root) or under the WinUI3Apps subfolder.
    //
    // This dictionary is the single source of truth for the command names: tools/build/
    // publish-cli-shims.ps1 parses the keys below to decide which exe copies to stage, and
    // validates that CliShims.wxs and ESRPSigning_core.json reference exactly the same set.
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
            return ExitCommandNotMapped;
        }

        string targetPath = Path.GetFullPath(Path.Combine(shimDirectory, relativeTarget));

        if (!File.Exists(targetPath))
        {
            Console.Error.WriteLine($"cli-shim: target not found: \"{targetPath}\".");
            return ExitLaunchFailed;
        }

        // Forward the user's arguments byte-for-byte. Environment.CommandLine is the raw
        // command line (the managed equivalent of GetCommandLineW); stripping argv[0]
        // preserves the user's exact quoting, which re-quoting parsed args would corrupt.
        string forwardedArguments = CommandLine.StripArgumentZero(Environment.CommandLine);

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
                return ExitLaunchFailed;
            }

            child.WaitForExit();
            return child.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"cli-shim: failed to launch \"{targetPath}\": {ex.Message}");
            return ExitLaunchFailed;
        }
    }
}
