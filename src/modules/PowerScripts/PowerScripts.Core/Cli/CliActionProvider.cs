// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PowerScripts.Core.Execution;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Security;

namespace PowerScripts.Core.Cli;

/// <summary>Executes a trusted script's data entry using only the installed operation catalogue.</summary>
public static class CliActionProvider
{
    public const string ProviderName = "powertoys-cli";
    public const int MaximumActionBytes = 256 * 1024;
    public const int DefaultTimeoutMs = 60000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        MaxDepth = 16,
    };

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>
    /// Describes a known incompatibility with the effective global isolation settings. A null result
    /// does not establish that the executable is installed or its owning module is running.
    /// </summary>
    public static string? GetUnavailableReason(BuiltinCliDefinition definition, MxcSettings? mxcSettings = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var policy = MxcPolicyResolver.Resolve(mxcSettings ?? MxcSettings.Load());
        if (definition.RequiresHostProcessVisibility && policy.Enabled)
        {
            return "This operation requires visibility of processes running on the Windows host. " +
                "The effective MXC sandbox hides host processes, so the query cannot return a reliable result. " +
                "The operation is unavailable under the current global sandbox settings.";
        }

        if (definition.RequiresHostDesktop && policy.IsEnabled(MxcPolicies.Ui))
        {
            return "This operation communicates with its running PowerToys module through the Windows host desktop. " +
                "The effective desktop-isolation policy prevents that communication, so the module's state cannot be queried reliably. " +
                "The operation is unavailable under the current global sandbox settings.";
        }

        return null;
    }

    /// <summary>Reads a bounded, unambiguous entry without evaluating or launching anything.</summary>
    public static CliAction Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > MaximumActionBytes || Encoding.UTF8.GetByteCount(json) > MaximumActionBytes)
        {
            throw new InvalidDataException("The PowerToys action entry exceeds 256 KiB.");
        }

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            CheckDuplicateProperties(document.RootElement);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("schemaVersion", out var version) || !version.TryGetInt32(out var schemaVersion) || schemaVersion != 1 ||
                !root.TryGetProperty("commandId", out var command) || command.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(command.GetString()) ||
                !root.TryGetProperty("bindings", out var bindings) || bindings.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("A PowerToys action requires schemaVersion 1, a commandId and object bindings.");
            }

            foreach (var binding in bindings.EnumerateObject())
            {
                if (binding.Value.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException($"Binding '{binding.Name}' must be an object.");
                }

                var members = binding.Value.EnumerateObject().ToList();
                if (members.Count != 1 || members[0].Name is not ("constant" or "parameter") ||
                    (members[0].Name == "parameter" &&
                     (members[0].Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(members[0].Value.GetString()))))
                {
                    throw new InvalidDataException($"Binding '{binding.Name}' must declare exactly one constant or parameter reference.");
                }
            }

            return JsonSerializer.Deserialize<CliAction>(json, JsonOptions)
                ?? throw new InvalidDataException("A PowerToys action must be an object.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new InvalidDataException("The PowerToys action entry is invalid.", ex);
        }
    }

    /// <summary>Configuration checks only; creating a script does not require the target to be installed.</summary>
    public static IReadOnlyList<string> Validate(PowerScriptManifest manifest, CliAction action)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(action);
        var errors = ValidateRecipe(manifest);
        if (action.SchemaVersion != 1)
        {
            errors.Add("PowerToys actions require schemaVersion 1.");
        }

        var definition = BuiltinCliCatalog.Load().Get(action.CommandId);
        if (definition is null)
        {
            errors.Add($"Unknown PowerToys operation '{action.CommandId}'.");
            return errors;
        }

        foreach (var capability in definition.RequiredCapabilities)
        {
            if (manifest.Capabilities?.Contains(capability, StringComparer.Ordinal) != true)
            {
                errors.Add($"Operation '{definition.Id}' requires the declared capability '{capability}'.");
            }
        }

        if (action.Bindings is null)
        {
            errors.Add("PowerToys action bindings must be an object.");
            return errors;
        }

        if (manifest.Parameters is null)
        {
            errors.Add("PowerToys operation parameters must be a collection.");
            return errors;
        }

        var declarations = new Dictionary<string, ScriptParameter>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in manifest.Parameters)
        {
            if (parameter is null || string.IsNullOrWhiteSpace(parameter.Name) || !declarations.TryAdd(parameter.Name, parameter))
            {
                errors.Add("PowerToys operation parameters require unique non-empty names.");
            }
        }

        var targets = definition.Parameters.ToDictionary(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase);
        var bound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, binding) in action.Bindings)
        {
            if (!targets.TryGetValue(name, out var target) || !bound.Add(name) || binding is null)
            {
                errors.Add($"Unknown, duplicate or invalid command argument '{name}'.");
                continue;
            }

            var constant = binding.Constant.ValueKind != JsonValueKind.Undefined;
            var reference = binding.Parameter is not null;
            if (constant == reference)
            {
                errors.Add($"Binding '{name}' must declare exactly one constant or parameter reference.");
                continue;
            }

            if (constant)
            {
                if (!CliParameterResolver.TryReadConstant(target, binding.Constant, out _, out var error))
                {
                    errors.Add(error);
                }
            }
            else if (string.IsNullOrWhiteSpace(binding.Parameter) || !declarations.TryGetValue(binding.Parameter, out var source))
            {
                errors.Add($"Binding '{name}' references an undeclared parameter '{binding.Parameter}'.");
            }
            else
            {
                referenced.Add(source.Name);
                ValidateReference(target, source, errors);
            }
        }

        foreach (var target in definition.Parameters)
        {
            if (!bound.Contains(target.Name) && target.IsRequired && target.Default is null)
            {
                errors.Add($"Missing binding for required command argument '{target.Name}'.");
            }
        }

        foreach (var parameter in declarations.Values)
        {
            if (!referenced.Contains(parameter.Name))
            {
                errors.Add($"Script parameter '{parameter.Name}' is not referenced by the PowerToys action.");
            }
        }

        return errors;
    }

    /// <summary>Checks an already published package, including its real data entry.</summary>
    public static IReadOnlyList<string> Validate(PowerScriptManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var errors = ValidateRecipe(manifest);
        if (errors.Count > 0 || string.IsNullOrEmpty(manifest.FolderPath))
        {
            return errors;
        }

        try
        {
            return Validate(manifest, ReadEntry(manifest));
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            errors.Add($"PowerToys action configuration could not be read: {ex.Message}");
            return errors;
        }
    }

    public static ScriptExecutionResult Run(
        PowerScriptManifest manifest,
        IReadOnlyList<string>? files,
        IReadOnlyDictionary<string, string?>? parameters,
        MxcSettings? mxcSettings = null) =>
        Run(manifest, files, parameters, mxcSettings, DefaultInstallationRoot(),
            (start, context, timeout, settings) => ProcessRunner.Run(start, context, timeoutMs: timeout, mxcSettings: settings));

    internal static ScriptExecutionResult Run(
        PowerScriptManifest manifest,
        IReadOnlyList<string>? files,
        IReadOnlyDictionary<string, string?>? parameters,
        MxcSettings? mxcSettings,
        string installationRoot,
        Func<ProcessStartInfo, ProcessExecutionContext, int, MxcSettings?, ProcessRunResult> run)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(run);
        try
        {
            var action = ReadEntry(manifest);
            var errors = Validate(manifest, action);
            if (errors.Count > 0)
            {
                return Failure(string.Join(Environment.NewLine, errors));
            }

            if (files is { Count: > 0 })
            {
                return Failure("This PowerToys operation does not accept primary file input.");
            }

            if (!ScriptParameterResolver.TryResolve(manifest, parameters ?? new Dictionary<string, string?>(), out var exposed, out var parameterError))
            {
                return Failure(parameterError);
            }

            var definition = BuiltinCliCatalog.Load().Get(action.CommandId)!;
            // Resolve once: the guard and actual launch must observe the same caller policy.
            var effectiveSettings = mxcSettings ?? MxcSettings.Load();
            if (GetUnavailableReason(definition, effectiveSettings) is { } isolationError)
            {
                return new ScriptExecutionResult
                {
                    ExitCode = ProcessRunner.UnsupportedIsolationExitCode,
                    StdErr = isolationError,
                };
            }

            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var target in definition.Parameters)
            {
                var binding = action.Bindings.FirstOrDefault(pair => string.Equals(pair.Key, target.Name, StringComparison.OrdinalIgnoreCase)).Value;
                string? value = null;
                var supplied = false;
                if (binding is not null && binding.Constant.ValueKind != JsonValueKind.Undefined)
                {
                    if (!CliParameterResolver.TryReadConstant(target, binding.Constant, out value, out var constantError))
                    {
                        return Failure(constantError);
                    }

                    supplied = true;
                }
                else if (binding?.Parameter is { } reference)
                {
                    supplied = exposed.TryGetValue(reference, out value);
                }

                value = supplied ? value : target.Default;
                if (value is null)
                {
                    continue;
                }

                if (!CliParameterResolver.TryNormalize(target, value, requireExistingFile: true, out var normalized, out var error))
                {
                    return Failure(error);
                }

                values[target.Name] = normalized;
            }

            if (!CliExecutableResolver.TryResolve(installationRoot, definition.Executable, out var executablePath, out var unavailable))
            {
                return Failure(unavailable);
            }

            var start = CreateStartInfo(definition, executablePath!, manifest.FolderPath, values);
            var inputPaths = definition.Parameters.Where(parameter => parameter.IsFile && values.ContainsKey(parameter.Name))
                .Select(parameter => values[parameter.Name]!).ToArray();
            var context = ProcessExecutionContext.FromManifest(manifest, inputPaths, exposed);
            var result = run(start, context, DefaultTimeoutMs, effectiveSettings);
            return new ScriptExecutionResult { ExitCode = result.ExitCode, StdOut = result.StdOut, StdErr = result.StdErr };
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or InvalidOperationException or Win32Exception)
        {
            return Failure($"The PowerToys operation could not run: {ex.Message}");
        }
    }

    internal static ProcessStartInfo CreateStartInfo(
        BuiltinCliDefinition definition,
        string executablePath,
        string workingDirectory,
        IReadOnlyDictionary<string, string?> values)
    {
        var start = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in definition.FixedArguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var binding in definition.Bindings)
        {
            if (!values.TryGetValue(binding.Parameter, out var value) || value is null)
            {
                continue;
            }

            switch (binding.Kind)
            {
                case CliArgumentKind.Positional:
                    start.ArgumentList.Add(value);
                    break;
                case CliArgumentKind.Option:
                    if (binding.AttachedValue && value.Length != 0)
                    {
                        start.ArgumentList.Add(binding.Name + "=" + value);
                    }
                    else
                    {
                        start.ArgumentList.Add(binding.Name!);
                        start.ArgumentList.Add(value);
                    }

                    break;
                case CliArgumentKind.Switch:
                    if (value == "true")
                    {
                        start.ArgumentList.Add(binding.Name!);
                    }

                    break;
                default:
                    throw new InvalidDataException("The installed command declares an unsupported argument binding.");
            }
        }

        return start;
    }

    private static List<string> ValidateRecipe(PowerScriptManifest manifest)
    {
        var errors = new List<string>();
        var execute = manifest.Execute;
        if (execute?.Provider != ProviderName)
        {
            errors.Add("Unknown PowerToys operation provider.");
        }

        if (execute is not null && (execute.Command is not null || execute.ArgMap is not null || execute.Stdin is not null ||
            execute.ExitCodes is not null || execute.AdditionalProperties is { Count: > 0 }))
        {
            errors.Add("A PowerToys operation provider cannot be combined with an arbitrary execution recipe.");
        }

        if (manifest.Runtime != ScriptRuntime.PowerToysOperation ||
            !string.Equals(Path.GetExtension(manifest.Entry), ".json", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(manifest.Entry) != manifest.Entry)
        {
            errors.Add("PowerToys operations require a package-local .json entry and their native runtime.");
        }

        if (manifest.InputFormat != PowerScriptDataFormat.None || manifest.OutputFormat != PowerScriptDataFormat.Text)
        {
            errors.Add("These PowerToys operations require no primary input and a text output contract.");
        }

        return errors;
    }

    private static void ValidateReference(ScriptParameter target, ScriptParameter source, List<string> errors)
    {
        var compatible = target.Type == source.Type ||
            (target.Type == ScriptParameter.ParameterTypeString && source.IsChoice);
        if (!compatible || (target.IsChoice && (source.Options.Count == 0 || source.Options.Any(option => !target.Options.Contains(option, StringComparer.Ordinal)))) ||
            (target.Min is { } minimum && (source.Min is null || source.Min < minimum)) ||
            (target.Max is { } maximum && (source.Max is null || source.Max > maximum)))
        {
            errors.Add($"Parameter '{source.Name}' must preserve or narrow the type and constraints of command argument '{target.Name}'.");
        }

        if (target.IsRequired && target.Default is null && !source.IsRequired && source.Default is null)
        {
            errors.Add($"Parameter '{source.Name}' must be required or have a default for command argument '{target.Name}'.");
        }

        if (source.Default is { } defaultValue &&
            !CliParameterResolver.TryNormalize(target, defaultValue, requireExistingFile: false, out _, out var error))
        {
            errors.Add(error);
        }
    }

    private static CliAction ReadEntry(PowerScriptManifest manifest)
    {
        using var stream = File.OpenRead(manifest.EntryFullPath);
        var bytes = new byte[MaximumActionBytes + 1];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = stream.Read(bytes.AsSpan(count));
            if (read == 0)
            {
                break;
            }

            count += read;
        }

        if (count > MaximumActionBytes)
        {
            throw new InvalidDataException("The PowerToys action entry exceeds 256 KiB.");
        }

        try
        {
            return Parse(StrictUtf8.GetString(bytes, 0, count).TrimStart('\uFEFF'));
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException("PowerToys action entries must use valid UTF-8.", ex);
        }
    }

    internal static void CheckDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!seen.Add(property.Name))
                {
                    throw new InvalidDataException($"Duplicate action property '{property.Name}'.");
                }

                CheckDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CheckDuplicateProperties(item);
            }
        }
    }

    private static string DefaultInstallationRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        var deployedSubdirectory = string.Equals(directory.Name, "PowerScripts", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(directory.Name, "WinUI3Apps", StringComparison.OrdinalIgnoreCase);
        return deployedSubdirectory ? directory.Parent?.FullName ?? directory.FullName : directory.FullName;
    }

    private static ScriptExecutionResult Failure(string error) => new() { ExitCode = 2, StdErr = error };
}
