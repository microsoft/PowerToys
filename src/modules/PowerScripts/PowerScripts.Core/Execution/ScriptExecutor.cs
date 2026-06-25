// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Execution;

/// <summary>
/// The outcome of running a PowerScript.
/// </summary>
public sealed class ScriptExecutionResult
{
    public int ExitCode { get; init; }

    public bool Succeeded => ExitCode == 0;

    public string StdOut { get; init; } = string.Empty;

    public string StdErr { get; init; } = string.Empty;
}

/// <summary>
/// Runs a PowerScript. This is the single execution path shared by every surface (context menu,
/// Keyboard Manager, Command Palette, agents) so behavior and security posture stay consistent.
///
/// Prototype security posture: always runs non-elevated under the invoking user's token, with the
/// PowerShell profile disabled and a per-run execution policy of Bypass scoped to the launched
/// process only. Signing / capability enforcement is intentionally out of scope for the prototype.
/// </summary>
public sealed class ScriptExecutor
{
    /// <summary>Environment variable the script can read to get the newline-separated input files.</summary>
    public const string FilesEnvironmentVariable = "POWERSCRIPTS_FILES";

    public ScriptExecutionResult Execute(
        PowerScriptManifest manifest,
        IReadOnlyList<string>? files = null,
        IReadOnlyDictionary<string, string?>? parameters = null)
    {
        if (manifest.Runtime != ScriptRuntime.PowerShell)
        {
            throw new NotSupportedException($"Runtime '{manifest.Runtime}' is not supported in the prototype.");
        }

        if (!File.Exists(manifest.EntryFullPath))
        {
            throw new FileNotFoundException("Script entry file not found.", manifest.EntryFullPath);
        }

        files ??= Array.Empty<string>();

        var psi = new ProcessStartInfo
        {
            FileName = ResolvePowerShellExecutable(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = manifest.FolderPath,
        };

        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(manifest.EntryFullPath);

        // Files are passed both as a -Files parameter (array binding) and via an environment
        // variable so scripts can consume whichever is convenient.
        if (files.Count > 0)
        {
            psi.ArgumentList.Add("-Files");
            foreach (var file in files)
            {
                psi.ArgumentList.Add(file);
            }

            psi.Environment[FilesEnvironmentVariable] = string.Join('\n', files);
        }

        if (parameters is not null)
        {
            foreach (var (name, value) in parameters)
            {
                psi.ArgumentList.Add("-" + name);
                psi.ArgumentList.Add(value ?? string.Empty);
            }
        }

        using var process = new Process { StartInfo = psi };
        process.Start();

        // Read both streams concurrently to avoid pipe deadlock on large output.
        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        return new ScriptExecutionResult
        {
            ExitCode = process.ExitCode,
            StdOut = stdOutTask.GetAwaiter().GetResult(),
            StdErr = stdErrTask.GetAwaiter().GetResult(),
        };
    }

    /// <summary>
    /// Prefers PowerShell 7+ (<c>pwsh</c>); falls back to Windows PowerShell (<c>powershell</c>).
    /// </summary>
    private static string ResolvePowerShellExecutable()
    {
        return ExistsOnPath("pwsh.exe") ? "pwsh.exe" : "powershell.exe";
    }

    private static bool ExistsOnPath(string fileName)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (File.Exists(Path.Combine(dir.Trim(), fileName)))
                {
                    return true;
                }
            }
            catch
            {
                // Ignore malformed PATH entries.
            }
        }

        return false;
    }
}
