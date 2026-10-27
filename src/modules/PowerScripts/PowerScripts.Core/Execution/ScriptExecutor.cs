// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        IReadOnlyDictionary<string, string?>? parameters = null,
        string? text = null,
        JsonElement? image = null,
        JsonElement? audio = null)
    {
        if (!ScriptParameterResolver.TryResolve(manifest, parameters ?? new Dictionary<string, string?>(), out var resolved, out var error))
        {
            throw new InvalidDataException(error);
        }

        parameters = resolved;
        var input = new PythonTransformInput
        {
            Text = text,
            Image = image,
            Audio = audio,
            FilePaths = files?.ToList(),
            Params = parameters?.ToDictionary(pair => pair.Key, pair => pair.Value ?? string.Empty),
        };
        var request = input.CreateRequest(manifest);
        // A descriptor-authored script (explicit .tool.json) carries its own launch recipe and is run
        // through the language-agnostic descriptor executor, regardless of runtime.
        if (manifest.Execute is not null)
        {
            return ValidateOutput(manifest, DescriptorExecutor.Run(manifest, files, parameters, mxcSettings: _mxcSettings, text: text, image: image, audio: audio));
        }

        if (manifest.Runtime == ScriptRuntime.Python)
        {
            return ExecutePython(manifest, input);
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
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
            WorkingDirectory = manifest.FolderPath,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };

        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        // JSON plus native splatting preserves booleans and path arrays, including on PowerShell 5.1.
        var command = """
            [Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
            $request = ConvertFrom-Json -InputObject ([Console]::In.ReadToEnd())
            $arguments = @{}
            foreach ($property in $request.parameters.PSObject.Properties) {
                $arguments[$property.Name] = $property.Value
            }
            if ($request.PSObject.Properties['text']) { $arguments['Text'] = $request.text }
            if ($request.PSObject.Properties['files']) { $arguments['Files'] = [string[]]$request.files }
            if ($request.PSObject.Properties['image']) { $arguments['Image'] = $request.image }
            if ($request.PSObject.Properties['audio']) { $arguments['Audio'] = $request.audio }
            """ + Environment.NewLine + $"& '{manifest.EntryFullPath.Replace("'", "''")}' @arguments";
        psi.ArgumentList.Add("-EncodedCommand");
        psi.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(command)));

        if (files.Count > 0)
        {
            psi.Environment[FilesEnvironmentVariable] = string.Join('\n', files);
        }

        var context = Security.ProcessExecutionContext.FromManifest(manifest, files, parameters);
        var result = Security.ProcessRunner.Run(psi, context, request.ToJsonString(), mxcSettings: _mxcSettings);

        return ValidateOutput(manifest, new ScriptExecutionResult
        {
            ExitCode = result.ExitCode,
            StdOut = result.StdOut,
            StdErr = result.StdErr,
        });
    }

    /// <summary>
    /// Runs a Python PowerScript for the file/hotkey surfaces by adapting the file list + parameters
    /// onto the shared <see cref="PythonRuntime"/> transform contract. Text outputs are returned
    /// as stdout; produced file paths are returned newline-separated so the CLI surfaces can
    /// report them.
    /// </summary>
    private ScriptExecutionResult ExecutePython(
        PowerScriptManifest manifest,
        PythonTransformInput input)
    {
        var result = new PythonRuntime(mxcSettings: _mxcSettings).Run(manifest, input);
        if (result.Succeeded && manifest.OutputFormat is PowerScriptDataFormat.Image or PowerScriptDataFormat.Audio)
        {
            return new ScriptExecutionResult
            {
                ExitCode = result.ExitCode,
                StdOut = JsonSerializer.Serialize(result, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }),
                StdErr = result.StdErr,
            };
        }

        var stdoutParts = new List<string>();
        if (!string.IsNullOrEmpty(result.Text))
        {
            stdoutParts.Add(result.Text);
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

    private static ScriptExecutionResult ValidateOutput(PowerScriptManifest manifest, ScriptExecutionResult result)
    {
        if (!result.Succeeded)
        {
            return result;
        }

        try
        {
            var output = manifest.OutputFormat switch
            {
                PowerScriptDataFormat.None => new PythonTransformResult(),
                PowerScriptDataFormat.Text => new PythonTransformResult { Text = result.StdOut },
                _ => PythonTransformResult.Parse(result.StdOut, manifest.OutputFormat, manifest.OutputSchema),
            };
            ScriptSchema.Validate(manifest.OutputSchema, JsonSerializer.SerializeToElement(output, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            }), "output");
            return result;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            return new ScriptExecutionResult { ExitCode = 2, StdErr = $"{result.StdErr}{Environment.NewLine}Invalid script output: {ex.Message}" };
        }
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
