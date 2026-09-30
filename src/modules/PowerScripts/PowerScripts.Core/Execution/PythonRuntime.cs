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

/// <summary>The typed input handed to a Python PowerScript transform.</summary>
public sealed class PythonTransformInput
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("html")]
    public string? Html { get; set; }

    [JsonPropertyName("image_path")]
    public string? ImagePath { get; set; }

    [JsonPropertyName("file_paths")]
    public List<string>? FilePaths { get; set; }

    [JsonPropertyName("audio_path")]
    public string? AudioPath { get; set; }

    [JsonPropertyName("video_path")]
    public string? VideoPath { get; set; }

    [JsonPropertyName("params")]
    public Dictionary<string, string>? Params { get; set; }

    /// <summary>
    /// Optional explicit entry-function name. When present the runner calls this function instead of
    /// discovering it by the <c>powerscript_from_*_to_*</c> naming convention.
    /// </summary>
    [JsonPropertyName("entry")]
    public string? Entry { get; set; }

    /// <summary>
    /// The input data shape (<c>none</c>/<c>text</c>/<c>files</c>/…) used to build the argument passed
    /// to an explicit <see cref="Entry"/> function (the convention path derives this from the name).
    /// </summary>
    [JsonPropertyName("input_format")]
    public string? InputFormat { get; set; }
}

/// <summary>The typed result produced by a Python PowerScript transform.</summary>
public sealed class PythonTransformResult
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("html")]
    public string? Html { get; set; }

    [JsonPropertyName("image_path")]
    public string? ImagePath { get; set; }

    [JsonPropertyName("file_paths")]
    public List<string>? FilePaths { get; set; }

    [JsonPropertyName("audio_path")]
    public string? AudioPath { get; set; }

    [JsonPropertyName("video_path")]
    public string? VideoPath { get; set; }

    [JsonIgnore]
    public int ExitCode { get; set; }

    [JsonIgnore]
    public string StdErr { get; set; } = string.Empty;

    [JsonIgnore]
    public bool Succeeded => ExitCode == 0;
}

/// <summary>
/// Executes Python PowerScripts either on native Windows Python or inside WSL, via the bundled
/// <c>_runner.py</c> and a JSON stdin/stdout protocol. This is the single Python execution path
/// shared by every surface (Keyboard Manager, context menu, Advanced Paste), so behavior and the
/// input/output contract stay identical wherever a script is invoked.
/// </summary>
public sealed class PythonRuntime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly PythonSettings _settings;
    private readonly MxcSettings? _mxcSettings;

    public PythonRuntime(PythonSettings? settings = null, MxcSettings? mxcSettings = null)
    {
        _settings = settings ?? PythonSettings.Load();
        _mxcSettings = mxcSettings;
    }

    /// <summary>Whether Python PowerScripts are enabled at all (mode is not Disabled).</summary>
    public bool IsEnabled => _settings.Mode != PythonRuntimeMode.Disabled;

    /// <summary>
    /// Runs a Python PowerScript transform. Resolves the runner + interpreter, serializes the input
    /// to the runner's stdin, and parses its stdout back into a typed result.
    /// </summary>
    public PythonTransformResult Run(PowerScriptManifest manifest, PythonTransformInput input)
    {
        if (manifest.Runtime != ScriptRuntime.Python)
        {
            throw new InvalidOperationException($"Script '{manifest.Id}' is not a Python script.");
        }

        if (_settings.Mode == PythonRuntimeMode.Disabled)
        {
            return new PythonTransformResult { ExitCode = 2, StdErr = "Python PowerScripts are disabled. Enable them in PowerScripts settings (Windows or WSL)." };
        }

        var runnerPath = ResolveRunnerPath();
        if (runnerPath is null)
        {
            return new PythonTransformResult { ExitCode = 2, StdErr = "Could not locate the PowerScripts Python runner (_runner.py)." };
        }

        var scriptPath = manifest.EntryFullPath;
        if (!File.Exists(scriptPath))
        {
            return new PythonTransformResult { ExitCode = 2, StdErr = $"Script entry file not found: {scriptPath}" };
        }

        // Decouple the entry-function name from the naming convention: when the descriptor/header
        // declares an explicit function, tell the runner to call it (and pass the declared input shape
        // so it can build the argument). Absent that, the runner falls back to the powerscript_from_*
        // convention, so existing zero-config scripts are unaffected.
        if (!string.IsNullOrWhiteSpace(manifest.EntryFunction))
        {
            input.Entry = manifest.EntryFunction;
            input.InputFormat = ScriptIo.ToToken((manifest.InputFormat ?? PowerScriptDataFormat.None));
        }

        return _settings.Mode == PythonRuntimeMode.Wsl
            ? RunViaWsl(manifest, runnerPath, scriptPath, input)
            : RunOnWindows(manifest, runnerPath, scriptPath, input);
    }

    private PythonTransformResult RunOnWindows(
        PowerScriptManifest manifest,
        string runnerPath,
        string scriptPath,
        PythonTransformInput input)
    {
        var interpreter = ResolveWindowsInterpreter();
        if (interpreter is null)
        {
            return new PythonTransformResult { ExitCode = 2, StdErr = "No Python interpreter found. Install Python or set an interpreter path in PowerScripts settings." };
        }

        var psi = new ProcessStartInfo
        {
            FileName = interpreter.Value.FileName,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardInputEncoding = Utf8NoBom,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var arg in interpreter.Value.LeadingArgs)
        {
            psi.ArgumentList.Add(arg);
        }

        psi.ArgumentList.Add("-X");
        psi.ArgumentList.Add("utf8");
        psi.ArgumentList.Add(runnerPath);
        psi.ArgumentList.Add(scriptPath);

        return RunProcess(
            psi,
            manifest,
            input,
            JsonSerializer.Serialize(input, JsonOptions),
            translateResultPaths: false);
    }

    private PythonTransformResult RunViaWsl(
        PowerScriptManifest manifest,
        string runnerPath,
        string scriptPath,
        PythonTransformInput input)
    {
        // Translate the runner + script + any input paths to /mnt/... so the Linux Python can read them.
        var wslInput = new PythonTransformInput
        {
            Text = input.Text,
            Html = input.Html,
            ImagePath = ToWslPath(input.ImagePath),
            AudioPath = ToWslPath(input.AudioPath),
            VideoPath = ToWslPath(input.VideoPath),
            FilePaths = input.FilePaths?.Select(p => ToWslPath(p)!).ToList(),
            Params = input.Params,
            Entry = input.Entry,
            InputFormat = input.InputFormat,
        };

        var psi = new ProcessStartInfo
        {
            FileName = "wsl.exe",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardInputEncoding = Utf8NoBom,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        if (!string.IsNullOrWhiteSpace(_settings.Wsl.Distribution))
        {
            psi.ArgumentList.Add("-d");
            psi.ArgumentList.Add(_settings.Wsl.Distribution);
        }

        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add("bash");
        psi.ArgumentList.Add("-l");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add($"python3 -X utf8 {ShellQuote(ToWslPath(runnerPath)!)} {ShellQuote(ToWslPath(scriptPath)!)}");

        var result = RunProcess(
            psi,
            manifest,
            input,
            JsonSerializer.Serialize(wslInput, JsonOptions),
            translateResultPaths: true);
        return result;
    }

    private PythonTransformResult RunProcess(
        ProcessStartInfo psi,
        PowerScriptManifest manifest,
        PythonTransformInput input,
        string payload,
        bool translateResultPaths)
    {
        try
        {
            var timeoutMs = Math.Max(1, _settings.TimeoutSeconds) * 1000;
            var inputPaths = new[]
            {
                input.ImagePath,
                input.AudioPath,
                input.VideoPath,
            }.Concat(input.FilePaths ?? Enumerable.Empty<string>());
            var parameterValues = input.Params?.ToDictionary(
                parameter => parameter.Key,
                parameter => (string?)parameter.Value,
                StringComparer.OrdinalIgnoreCase);
            var context = Security.ProcessExecutionContext.FromManifest(manifest, inputPaths, parameterValues);
            var run = Security.ProcessRunner.Run(psi, context, payload, timeoutMs, _mxcSettings);

            if (run.ExitCode == 124)
            {
                return new PythonTransformResult { ExitCode = 124, StdErr = $"Python script timed out after {_settings.TimeoutSeconds}s." };
            }

            var stdout = run.StdOut;
            var stderr = run.StdErr;

            if (run.ExitCode != 0)
            {
                return new PythonTransformResult { ExitCode = run.ExitCode, StdErr = string.IsNullOrEmpty(stderr) ? "Python script failed." : stderr };
            }

            PythonTransformResult result;
            try
            {
                result = string.IsNullOrWhiteSpace(stdout)
                    ? new PythonTransformResult()
                    : JsonSerializer.Deserialize<PythonTransformResult>(stdout, JsonOptions) ?? new PythonTransformResult();
            }
            catch (JsonException)
            {
                // A script that just printed text (didn't return via the protocol) still yields output.
                result = new PythonTransformResult { Text = stdout };
            }

            result.ExitCode = 0;
            result.StdErr = stderr;

            if (translateResultPaths)
            {
                result.ImagePath = FromWslPath(result.ImagePath);
                result.AudioPath = FromWslPath(result.AudioPath);
                result.VideoPath = FromWslPath(result.VideoPath);
                result.FilePaths = result.FilePaths?.Select(p => FromWslPath(p)!).ToList();
            }

            return result;
        }
        catch (Exception ex)
        {
            return new PythonTransformResult { ExitCode = 2, StdErr = ex.Message };
        }
    }

    /// <summary>Locates <c>_runner.py</c>, bundled next to the executing assembly.</summary>
    private static string? ResolveRunnerPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "_runner.py"),
            Path.Combine(AppContext.BaseDirectory, "Execution", "_runner.py"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Resolves the Windows Python interpreter: an explicit configured path, then a concrete
    /// <c>python.exe</c>/<c>python3.exe</c> on PATH, then the <c>py</c> launcher. Preferring the
    /// concrete interpreter lets MXC grant its runtime directory read-only.
    /// </summary>
    private (string FileName, string[] LeadingArgs)? ResolveWindowsInterpreter()
    {
        var configured = _settings.Windows.InterpreterPath;
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return (configured, Array.Empty<string>());
        }

        if (ExistsOnPath("python.exe"))
        {
            return ("python.exe", Array.Empty<string>());
        }

        if (ExistsOnPath("python3.exe"))
        {
            return ("python3.exe", Array.Empty<string>());
        }

        if (ExistsOnPath("py.exe"))
        {
            return ("py.exe", new[] { "-3" });
        }

        return null;
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
            catch (Exception)
            {
                // Ignore malformed PATH entries.
            }
        }

        return false;
    }

    /// <summary>Converts a Windows path (e.g. <c>C:\a\b</c>) to a WSL path (<c>/mnt/c/a/b</c>).</summary>
    internal static string? ToWslPath(string? windowsPath)
    {
        if (string.IsNullOrEmpty(windowsPath))
        {
            return windowsPath;
        }

        if (windowsPath.Length >= 2 && windowsPath[1] == ':')
        {
            var drive = char.ToLowerInvariant(windowsPath[0]);
            var rest = windowsPath[2..].Replace('\\', '/');
            return $"/mnt/{drive}{rest}";
        }

        return windowsPath.Replace('\\', '/');
    }

    /// <summary>Converts a WSL path (<c>/mnt/c/a/b</c>) back to a Windows path (<c>C:\a\b</c>).</summary>
    internal static string? FromWslPath(string? wslPath)
    {
        if (string.IsNullOrEmpty(wslPath))
        {
            return wslPath;
        }

        if (wslPath.StartsWith("/mnt/", StringComparison.Ordinal) && wslPath.Length >= 6)
        {
            var drive = char.ToUpperInvariant(wslPath[5]);
            var rest = wslPath.Length > 6 ? wslPath[6..].Replace('/', '\\') : string.Empty;
            return $"{drive}:{rest}";
        }

        return wslPath;
    }

    private static string ShellQuote(string value) => "'" + value.Replace("'", "'\\''") + "'";
}
