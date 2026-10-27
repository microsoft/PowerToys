// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Globalization;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Security;

namespace PowerScripts.Core.Execution;

/// <summary>The typed input handed to a Python PowerScript transform: text, file paths, and parameters.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PythonTransformInput
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("files")]
    public List<string>? FilePaths { get; set; }

    [JsonPropertyName("image")]
    public JsonElement? Image { get; set; }

    [JsonPropertyName("audio")]
    public JsonElement? Audio { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, string>? Params { get; set; }

    public JsonObject CreateRequest(PowerScriptManifest manifest, bool validate = true)
    {
        if ((manifest.InputFormat != PowerScriptDataFormat.Files && FilePaths is { Count: > 0 }) ||
            (manifest.InputFormat != PowerScriptDataFormat.Text && Text is not null) ||
            (manifest.InputFormat != PowerScriptDataFormat.Image && Image is not null) ||
            (manifest.InputFormat != PowerScriptDataFormat.Audio && Audio is not null))
        {
            throw new InvalidDataException("The supplied primary data does not match inputSchema.");
        }

        var request = new JsonObject();
        if (manifest.InputFormat == PowerScriptDataFormat.Files)
        {
            request["files"] = JsonSerializer.SerializeToNode(FilePaths ?? new());
        }
        else if (manifest.InputFormat == PowerScriptDataFormat.Text)
        {
            request["text"] = Text ?? string.Empty;
        }
        else if (manifest.InputFormat is PowerScriptDataFormat.Image or PowerScriptDataFormat.Audio)
        {
            var value = manifest.InputFormat == PowerScriptDataFormat.Image ? Image : Audio;
            if (value is { } media)
            {
                request[ScriptIo.ToToken(manifest.InputFormat)] = JsonNode.Parse(media.GetRawText());
            }
        }

        if (manifest.InputSchema.Properties?.ContainsKey("parameters") == true)
        {
            var parameters = new JsonObject();
            foreach (var (name, value) in Params ?? new())
            {
                var declaration = manifest.Parameters.SingleOrDefault(parameter => parameter.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException($"Unknown parameter '{name}'.");
                parameters[declaration.Name] = declaration.IsBool
                    ? JsonValue.Create(bool.Parse(value))
                    : declaration.IsInt
                        ? JsonValue.Create(int.Parse(value, CultureInfo.InvariantCulture))
                        : JsonValue.Create(value);
            }

            request["parameters"] = parameters;
        }
        else if (Params is { Count: > 0 })
        {
            throw new InvalidDataException("This script does not declare parameters.");
        }

        ScriptSchema.ApplyDefaults(manifest.InputSchema, request);
        if (validate)
        {
            ScriptSchema.Validate(manifest.InputSchema, JsonSerializer.SerializeToElement(request), "input");
        }

        return request;
    }

    public static PythonTransformInput Parse(PowerScriptManifest manifest, string json)
    {
        if (manifest.InputFormat is PowerScriptDataFormat.Image or PowerScriptDataFormat.Audio && json.Length > ScriptMedia.MaxRequestCharacters)
        {
            throw new InvalidDataException("Inline media request exceeds the bounded payload size.");
        }

        var request = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidDataException("Input must be an object.");
        if (manifest.InputSchema.Properties?.ContainsKey("parameters") == true && !request.ContainsKey("parameters"))
        {
            request["parameters"] = new JsonObject();
        }

        ScriptSchema.ApplyDefaults(manifest.InputSchema, request);
        var value = JsonSerializer.SerializeToElement(request);
        ScriptSchema.Validate(manifest.InputSchema, value, "input");
        return new PythonTransformInput
        {
            Text = request["text"]?.GetValue<string>(),
            FilePaths = request["files"]?.Deserialize<List<string>>(),
            Image = request["image"]?.Deserialize<JsonElement>(),
            Audio = request["audio"]?.Deserialize<JsonElement>(),
            Params = request["parameters"]?.AsObject().ToDictionary(
                pair => pair.Key,
                pair => pair.Value is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text : pair.Value!.ToJsonString(),
                StringComparer.Ordinal),
        };
    }
}

/// <summary>The typed result produced by a Python PowerScript transform.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PythonTransformResult
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("files")]
    public List<string>? FilePaths { get; set; }

    [JsonPropertyName("image")]
    public JsonElement? Image { get; set; }

    [JsonPropertyName("audio")]
    public JsonElement? Audio { get; set; }

    [JsonIgnore]
    public int ExitCode { get; set; }

    [JsonIgnore]
    public string StdErr { get; set; } = string.Empty;

    [JsonIgnore]
    public bool Succeeded => ExitCode == 0;

    /// <summary>Reads an output object and checks that it matches the descriptor, without guessing paths from text.</summary>
    public static PythonTransformResult Parse(string json, PowerScriptDataFormat format, JsonSchemaObject? schema = null)
    {
        if (format is PowerScriptDataFormat.Image or PowerScriptDataFormat.Audio && json.Length > ScriptMedia.MaxRequestCharacters)
        {
            throw new InvalidDataException("Inline media result exceeds the bounded payload size.");
        }

        if (schema is not null)
        {
            using var document = JsonDocument.Parse(json);
            ScriptSchema.Validate(schema, document.RootElement, "output");
        }

        var result = JsonSerializer.Deserialize<PythonTransformResult>(json)
            ?? throw new JsonException("Expected an output object.");
        var populated = (result.Text is not null ? 1 : 0) + (result.FilePaths is not null ? 1 : 0) +
            (result.Image is not null ? 1 : 0) + (result.Audio is not null ? 1 : 0);
        var valid = format switch
        {
            PowerScriptDataFormat.None => populated == 0,
            PowerScriptDataFormat.Text => populated == 1 && result.Text is not null,
            PowerScriptDataFormat.Image => populated == 1 && result.Image is { ValueKind: JsonValueKind.Object or JsonValueKind.Array },
            PowerScriptDataFormat.Audio => populated == 1 && result.Audio is { ValueKind: JsonValueKind.Object or JsonValueKind.Array },
            PowerScriptDataFormat.Files => populated == 1 && result.FilePaths is not null &&
                result.FilePaths.All(path => !string.IsNullOrWhiteSpace(path)),
            _ => false,
        };
        if (!valid)
        {
            throw new JsonException($"Output does not match '{ScriptIo.ToToken(format)}'. Return one declared primary field: text, files, image, or audio; return {{}} for none.");
        }

        return result;
    }
}

/// <summary>
/// Executes top-level Python scripts on Windows, WSL, or managed Linux using JSON stdin.
/// This is the single Python execution path
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
    /// Runs a Python script with schema-validated arguments and normalizes its stdout.
    /// </summary>
    public PythonTransformResult Run(PowerScriptManifest manifest, PythonTransformInput input)
    {
        if (manifest.Runtime != ScriptRuntime.Python)
        {
            throw new InvalidOperationException($"Script '{manifest.Id}' is not a Python script.");
        }

        if (_settings.Mode == PythonRuntimeMode.Disabled)
        {
            return new PythonTransformResult { ExitCode = 2, StdErr = "Python PowerScripts are disabled. Select Windows or Linux (WSL) in PowerScripts settings." };
        }

        var inputFormat = ScriptIo.Resolve(manifest).Input;
        if (!ScriptIo.IsSupported(inputFormat))
        {
            return new PythonTransformResult { ExitCode = 2, StdErr = "Unsupported primary input. Use none, text, files, image, or audio." };
        }

        if (!ScriptIo.IsSupported(ScriptIo.Resolve(manifest).Output))
        {
            return new PythonTransformResult { ExitCode = 2, StdErr = "Unsupported output. Use none, text, files, image, or audio." };
        }

        var scriptPath = manifest.EntryFullPath;
        if (!File.Exists(scriptPath))
        {
            return new PythonTransformResult { ExitCode = 2, StdErr = $"Script entry file not found: {scriptPath}" };
        }

        _ = input.CreateRequest(manifest);
        var settings = _mxcSettings ?? MxcSettings.Load();
        return _settings.Mode switch
        {
            PythonRuntimeMode.Windows => RunOnWindows(manifest, scriptPath, input, settings),
            PythonRuntimeMode.Wsl => MxcPolicyResolver.Resolve(settings).Enabled
                ? RunInManagedLinux(manifest, scriptPath, input, settings)
                : RunViaWsl(manifest, scriptPath, input, settings),
            _ => new PythonTransformResult { ExitCode = 2, StdErr = "Unknown Python execution environment." },
        };
    }

    private PythonTransformResult RunInManagedLinux(
        PowerScriptManifest manifest,
        string scriptPath,
        PythonTransformInput input,
        MxcSettings settings)
    {
        try
        {
            var translated = new PythonTransformInput
            {
                Text = input.Text,
                Image = input.Image,
                Audio = input.Audio,
                FilePaths = input.FilePaths?.Select(path => MxcLinuxProcessRunner.ToContainerPath(path)).ToList(),
                Params = TranslateWslFileParameters(manifest, input.Params),
            };
            var payload = translated.CreateRequest(manifest, validate: false).ToJsonString();
            return RunAndParse(
                manifest,
                input,
                (context, timeout) =>
                {
                    var policy = MxcPolicyResolver.Resolve(settings);
                    return MxcLinuxProcessRunner.Run(
                        scriptPath, payload, ScriptIo.Resolve(manifest).Output, context, policy, settings, _settings.Wsl.ContainerImage, timeout);
                },
                translateResultPaths: false);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return new PythonTransformResult { ExitCode = 2, StdErr = ex.Message };
        }
    }

    private PythonTransformResult RunOnWindows(
        PowerScriptManifest manifest,
        string scriptPath,
        PythonTransformInput input,
        MxcSettings settings)
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
        psi.ArgumentList.Add(scriptPath);

        return RunProcess(
            psi,
            manifest,
            input,
            input.CreateRequest(manifest).ToJsonString(),
            settings,
            translateResultPaths: false);
    }

    private PythonTransformResult RunViaWsl(
        PowerScriptManifest manifest,
        string scriptPath,
        PythonTransformInput input,
        MxcSettings settings)
    {
        Dictionary<string, string>? wslParameters;
        try
        {
            wslParameters = TranslateWslFileParameters(manifest, input.Params);
        }
        catch (NotSupportedException ex)
        {
            return new PythonTransformResult { ExitCode = 2, StdErr = ex.Message };
        }

        // Validate Windows paths before translating the separate Linux payload.
        var wslInput = new PythonTransformInput
        {
            Text = input.Text,
            Image = input.Image,
            Audio = input.Audio,
            FilePaths = input.FilePaths?.Select(p => ToWslPath(p)!).ToList(),
            Params = wslParameters,
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
        var payload = wslInput.CreateRequest(manifest, validate: false).ToJsonString();
        var workspace = manifest.OutputFormat == PowerScriptDataFormat.Files
            ? MxcProcessRunner.CreateRunDirectory() : null;
        var environment = workspace is null ? string.Empty : $"TMPDIR={ShellQuote(ToWslPath(workspace)!)} ";
        psi.ArgumentList.Add($"{environment}python3 -X utf8 {ShellQuote(ToWslPath(scriptPath)!)}");

        var result = RunProcess(
            psi,
            manifest,
            input,
            payload,
            settings,
            translateResultPaths: true);
        if (workspace is not null && (!result.Succeeded || result.FilePaths is not { Count: > 0 }))
        {
            try
            {
                Directory.Delete(workspace, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                result.StdErr += $"{Environment.NewLine}Could not clean up the WSL output workspace: {ex.Message}";
            }
        }

        return result;
    }

    private PythonTransformResult RunProcess(
        ProcessStartInfo psi,
        PowerScriptManifest manifest,
        PythonTransformInput input,
        string payload,
        MxcSettings settings,
        bool translateResultPaths) =>
        RunAndParse(
            manifest,
            input,
            (context, timeout) => ProcessRunner.Run(psi, context, payload, timeout, settings),
            translateResultPaths);

    private PythonTransformResult RunAndParse(
        PowerScriptManifest manifest,
        PythonTransformInput input,
        Func<ProcessExecutionContext, int, ProcessRunResult> execute,
        bool translateResultPaths)
    {
        try
        {
            var timeoutMs = Math.Max(1, _settings.TimeoutSeconds) * 1000;
            var inputPaths = input.FilePaths ?? Enumerable.Empty<string>();
            var parameterValues = input.Params?.ToDictionary(
                parameter => parameter.Key,
                parameter => (string?)parameter.Value,
                StringComparer.OrdinalIgnoreCase);
            var context = Security.ProcessExecutionContext.FromManifest(manifest, inputPaths, parameterValues);
            var run = execute(context, timeoutMs);

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
                result = ScriptIo.Resolve(manifest).Output switch
                {
                    PowerScriptDataFormat.None => new PythonTransformResult(),
                    PowerScriptDataFormat.Text => new PythonTransformResult { Text = stdout },
                    _ => PythonTransformResult.Parse(stdout, ScriptIo.Resolve(manifest).Output,
                        translateResultPaths && manifest.OutputFormat == PowerScriptDataFormat.Files ? null : manifest.OutputSchema),
                };
                if (translateResultPaths)
                {
                    result.FilePaths = result.FilePaths?.Select(p => FromWslPath(p)!).ToList();
                }

                ScriptSchema.Validate(manifest.OutputSchema, JsonSerializer.SerializeToElement(result, JsonOptions), "output");
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException)
            {
                var error = $"Invalid Python output: {ex.Message}";
                return new PythonTransformResult
                {
                    ExitCode = 2,
                    StdErr = string.IsNullOrEmpty(stderr) ? error : $"{stderr}{Environment.NewLine}{error}",
                };
            }

            result.ExitCode = 0;
            result.StdErr = manifest.OutputFormat == PowerScriptDataFormat.None
                ? string.Concat(stderr, string.IsNullOrEmpty(stderr) || string.IsNullOrEmpty(stdout) ? string.Empty : Environment.NewLine, stdout)
                : stderr;

            return result;
        }
        catch (Exception ex)
        {
            return new PythonTransformResult { ExitCode = 2, StdErr = ex.Message };
        }
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

    internal static Dictionary<string, string>? TranslateWslFileParameters(
        PowerScriptManifest manifest,
        IReadOnlyDictionary<string, string>? parameters)
    {
        if (parameters is null)
        {
            return null;
        }

        var translated = new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in manifest.Parameters)
        {
            if (!string.Equals(parameter.Type, ScriptParameter.ParameterTypeFile, StringComparison.OrdinalIgnoreCase) ||
                !translated.TryGetValue(parameter.Name, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(value);
            if (fullPath.Length < 3 || !char.IsAsciiLetter(fullPath[0]) || fullPath[1] != ':')
            {
                throw new NotSupportedException(
                    $"WSL file parameter '{parameter.Name}' requires a local drive path; UNC and device paths are not supported.");
            }

            translated[parameter.Name] = ToWslPath(fullPath)!;
        }

        return translated;
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
