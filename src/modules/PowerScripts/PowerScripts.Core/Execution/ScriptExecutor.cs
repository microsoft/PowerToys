// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Security;

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
/// Security posture: all launches flow through the centrally resolved MXC policy and always run
/// non-elevated under the invoking user's token. PowerShell additionally disables profiles and uses
/// a per-process execution policy of Bypass.
/// </summary>
public sealed class ScriptExecutor
{
    /// <summary>Environment variable the script can read to get the newline-separated input files.</summary>
    public const string FilesEnvironmentVariable = "POWERSCRIPTS_FILES";

    private readonly MxcSettings? _mxcSettings;

    public ScriptExecutor(MxcSettings? mxcSettings = null)
    {
        _mxcSettings = mxcSettings;
    }

    public ScriptExecutionResult Execute(
        PowerScriptManifest manifest,
        IReadOnlyList<string>? files = null,
        IReadOnlyDictionary<string, string?>? parameters = null)
    {
        // A descriptor-authored script (explicit .tool.json) carries its own launch recipe and is run
        // through the language-agnostic descriptor executor, regardless of runtime.
        if (manifest.Execute is not null)
        {
            return DescriptorExecutor.Run(manifest, files, parameters, mxcSettings: _mxcSettings);
        }

        if (manifest.Runtime == ScriptRuntime.Python)
        {
            return ExecutePython(manifest, files, parameters);
        }

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

        var context = Security.ProcessExecutionContext.FromManifest(manifest, files, parameters);
        var result = Security.ProcessRunner.Run(psi, context, mxcSettings: _mxcSettings);

        return new ScriptExecutionResult
        {
            ExitCode = result.ExitCode,
            StdOut = result.StdOut,
            StdErr = result.StdErr,
        };
    }

    /// <summary>
    /// Runs a Python PowerScript for the file/hotkey surfaces by adapting the file list + parameters
    /// onto the shared <see cref="PythonRuntime"/> transform contract. Text/HTML outputs are returned
    /// as stdout; produced file/image paths are returned newline-separated so the CLI surfaces can
    /// report them.
    /// </summary>
    private ScriptExecutionResult ExecutePython(
        PowerScriptManifest manifest,
        IReadOnlyList<string>? files,
        IReadOnlyDictionary<string, string?>? parameters)
    {
        var input = new PythonTransformInput
        {
            FilePaths = files?.ToList() ?? new List<string>(),
            Params = parameters?
                .Where(kv => kv.Value is not null)
                .ToDictionary(kv => kv.Key, kv => kv.Value!),
        };

        var result = new PythonRuntime(mxcSettings: _mxcSettings).Run(manifest, input);

        var stdoutParts = new List<string>();
        if (!string.IsNullOrEmpty(result.Text))
        {
            stdoutParts.Add(result.Text);
        }

        if (!string.IsNullOrEmpty(result.Html))
        {
            stdoutParts.Add(result.Html);
        }

        if (!string.IsNullOrEmpty(result.ImagePath))
        {
            stdoutParts.Add(result.ImagePath);
        }

        if (result.FilePaths is { Count: > 0 })
        {
            stdoutParts.AddRange(result.FilePaths);
        }

        return new ScriptExecutionResult
        {
            ExitCode = result.ExitCode,
            StdOut = string.Join(Environment.NewLine, stdoutParts),
            StdErr = result.StdErr,
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
