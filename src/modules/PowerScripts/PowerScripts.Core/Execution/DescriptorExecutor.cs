// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Security;

namespace PowerScripts.Core.Execution;

/// <summary>
/// Runs a PowerScript authored via an explicit <c>.tool.json</c> descriptor, using its
/// <see cref="ScriptExecute"/> (<c>x-execute</c>) recipe. Unlike the header path, this is
/// language-agnostic: it launches the declared interpreter with the script directly, maps scalar
/// inputs to argv (via <c>argMap</c> or <c>--name</c>), passes selected files positionally (and via
/// <see cref="ScriptExecutor.FilesEnvironmentVariable"/>), and — for large / structured inputs —
/// writes one JSON object to stdin. Every launch goes through <see cref="ProcessRunner"/>, so a
/// descriptor script is subject to the same never-elevated guarantee as any other.
/// </summary>
public static class DescriptorExecutor
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static ScriptExecutionResult Run(
        PowerScriptManifest manifest,
        IReadOnlyList<string>? files,
        IReadOnlyDictionary<string, string?>? parameters,
        int timeoutMs = 0,
        MxcSettings? mxcSettings = null)
    {
        var execute = manifest.Execute
            ?? throw new InvalidOperationException($"Script '{manifest.Id}' has no x-execute recipe.");

        if (execute.Command is not { Count: > 0 } command)
        {
            return new ScriptExecutionResult { ExitCode = 2, StdErr = $"Script '{manifest.Id}' declares an empty x-execute.command." };
        }

        if (!File.Exists(manifest.EntryFullPath))
        {
            return new ScriptExecutionResult { ExitCode = 2, StdErr = $"Script entry file not found: {manifest.EntryFullPath}" };
        }

        files ??= Array.Empty<string>();

        var psi = new ProcessStartInfo
        {
            FileName = ResolveInterpreter(command[0]),
            WorkingDirectory = manifest.FolderPath,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // Fixed leading arguments from the recipe (command[1..]); a token that names the entry script
        // is resolved to its full path so the launch doesn't depend on the working directory.
        for (var i = 1; i < command.Count; i++)
        {
            psi.ArgumentList.Add(ResolveArgToken(command[i], manifest));
        }

        var usesJsonStdin = execute.UsesJsonStdin;
        if (!usesJsonStdin)
        {
            // Scalars go on argv: an explicit argMap flag, else --name.
            if (parameters is not null)
            {
                foreach (var (name, value) in parameters)
                {
                    psi.ArgumentList.Add(FlagFor(execute, name));
                    psi.ArgumentList.Add(value ?? string.Empty);
                }
            }

            // Files are passed positionally (the universal "operate on these paths" convention).
            foreach (var file in files)
            {
                psi.ArgumentList.Add(file);
            }
        }

        if (files.Count > 0)
        {
            psi.Environment[ScriptExecutor.FilesEnvironmentVariable] = string.Join('\n', files);
        }

        var standardInput = usesJsonStdin ? BuildJsonStdin(parameters, files) : null;

        if (usesJsonStdin)
        {
            psi.StandardInputEncoding = Utf8NoBom;
        }

        var context = ProcessExecutionContext.FromManifest(manifest, files, parameters);
        var result = ProcessRunner.Run(psi, context, standardInput, timeoutMs, mxcSettings);

        return new ScriptExecutionResult
        {
            ExitCode = result.ExitCode,
            StdOut = result.StdOut,
            StdErr = DecorateExitMeaning(result, execute),
        };
    }

    /// <summary>Maps a param name to its command-line flag: the declared argMap entry, else <c>--name</c>.</summary>
    private static string FlagFor(ScriptExecute execute, string name) =>
        execute.ArgMap is not null && execute.ArgMap.TryGetValue(name, out var flag) && !string.IsNullOrWhiteSpace(flag)
            ? flag
            : "--" + name;

    /// <summary>Serializes the params + selected file paths into the single JSON object for stdin.</summary>
    private static string BuildJsonStdin(IReadOnlyDictionary<string, string?>? parameters, IReadOnlyList<string> files)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (parameters is not null)
        {
            foreach (var (name, value) in parameters)
            {
                payload[name] = value;
            }
        }

        if (files.Count > 0)
        {
            payload["file_paths"] = files;
        }

        return JsonSerializer.Serialize(payload);
    }

    /// <summary>
    /// Resolves a command token: the entry-script name becomes its absolute path; any other token that
    /// names an existing file in the script folder is likewise made absolute; everything else is passed
    /// through unchanged.
    /// </summary>
    private static string ResolveArgToken(string token, PowerScriptManifest manifest)
    {
        if (string.Equals(token, manifest.Entry, StringComparison.OrdinalIgnoreCase))
        {
            return manifest.EntryFullPath;
        }

        var candidate = Path.Combine(manifest.FolderPath, token);
        return File.Exists(candidate) ? candidate : token;
    }

    /// <summary>
    /// Resolves the interpreter named in <c>command[0]</c>. Well-known logical names (<c>python</c>,
    /// <c>pwsh</c>) are mapped to an executable actually present on this machine; anything else
    /// (an absolute path, <c>cmd</c>, <c>wsl</c>, …) is used as-is and resolved via PATH by the OS.
    /// </summary>
    private static string ResolveInterpreter(string command)
    {
        switch (command.ToLowerInvariant())
        {
            case "python":
            case "python3":
                return FirstOnPath("python.exe", "python3.exe", "py.exe") ?? "python.exe";
            case "pwsh":
            case "powershell":
                return FirstOnPath("pwsh.exe", "powershell.exe") ?? "powershell.exe";
            default:
                return command;
        }
    }

    private static string? FirstOnPath(params string[] fileNames)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var directories = pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var fileName in fileNames)
        {
            foreach (var directory in directories)
            {
                try
                {
                    if (File.Exists(Path.Combine(directory.Trim(), fileName)))
                    {
                        return fileName;
                    }
                }
                catch (Exception)
                {
                    // Ignore malformed PATH entries.
                }
            }
        }

        return null;
    }

    /// <summary>Appends the declared human meaning of a non-zero exit code to stderr, when present.</summary>
    private static string DecorateExitMeaning(ProcessRunResult result, ScriptExecute execute)
    {
        if (result.ExitCode == 0 || execute.ExitCodes is null)
        {
            return result.StdErr;
        }

        if (!execute.ExitCodes.TryGetValue(result.ExitCode.ToString(), out var meaning) || string.IsNullOrWhiteSpace(meaning))
        {
            return result.StdErr;
        }

        return string.IsNullOrEmpty(result.StdErr) ? meaning : $"{meaning}{Environment.NewLine}{result.StdErr}";
    }
}
