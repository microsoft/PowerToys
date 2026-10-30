// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using PowerScripts.Core;
using PowerScripts.Core.Execution;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Registry;
using PowerScripts.Core.Security;
using PowerScripts.Core.Storage;
using PowerScripts.Core.Templates;

namespace PowerScripts.Host;

/// <summary>
/// The shared PowerScripts executor / catalogue CLI.
///
/// This is the single invocation entry point every surface points at:
///   - Keyboard Manager maps a hotkey to:        PowerScripts.Host.exe run &lt;id&gt;
///   - The Explorer context menu invokes:         PowerScripts.Host.exe run &lt;id&gt; --files &lt;paths&gt;
///   - The KBM editor / agents enumerate via:     PowerScripts.Host.exe list --json
///
/// Usage:
///   PowerScripts.Host list [--json] [--include-disabled] [--root &lt;dir&gt;]
///   PowerScripts.Host run &lt;id&gt; [--files &lt;f1&gt; &lt;f2&gt; ...] [--set name=value ...] [--root &lt;dir&gt;]
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            var (positional, options) = ParseArgs(args.Skip(1).ToArray());
            var root = options.TryGetValue("root", out var r) ? r.FirstOrDefault() : null;

            if (args[0].Equals("template", StringComparison.OrdinalIgnoreCase))
            {
                if (positional.Count == 1 && positional[0] == "list" && options.Count == 0)
                {
                    Console.WriteLine(JsonSerializer.Serialize(ScriptTemplates.All, ToolDescriptorParser.JsonOptions));
                    return 0;
                }

                if ((positional.Count == 2 || (positional.Count == 1 && options.ContainsKey("configure"))) && positional[0] == "create" &&
                    options.Keys.All(key => key.Equals("root", StringComparison.OrdinalIgnoreCase) || key.Equals("configure", StringComparison.OrdinalIgnoreCase)) &&
                    (!options.TryGetValue("root", out var roots) || roots.Count == 1) &&
                    (!options.TryGetValue("configure", out var configure) || configure.Count == 0))
                {
                    ScriptTemplateOptions? templateOptions = null;
                    if (options.ContainsKey("configure"))
                    {
                        using var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
                        var buffer = new char[16_385];
                        var length = reader.ReadBlock(buffer, 0, buffer.Length);
                        if (length == buffer.Length)
                        {
                            throw new InvalidDataException("Template configuration is too large.");
                        }

                        templateOptions = JsonSerializer.Deserialize<ScriptTemplateOptions>(new string(buffer, 0, length), ToolDescriptorParser.JsonOptions)
                            ?? throw new InvalidDataException("Template configuration must be a JSON object.");
                    }

                    Console.WriteLine(JsonSerializer.Serialize(
                        positional.Count == 1
                            ? ScriptTemplates.Create(PowerScriptsPaths.ResolveScriptsRoot(root), templateOptions!)
                            : ScriptTemplates.Create(positional[1], PowerScriptsPaths.ResolveScriptsRoot(root), templateOptions),
                        ToolDescriptorParser.JsonOptions));
                    return 0;
                }

                Console.Error.WriteLine("template: use list, create <template-id> [--configure] [--root <dir>], or create --configure [--root <dir>]. Without a template ID, configuration includes projectName, description, runtime, input, output, fileExtensions, minFiles and maxFiles.");
                return 1;
            }

            if (args[0].ToLowerInvariant() is "linux-container-status" or "linux-container-setup")
            {
                if (args.Length != 1)
                {
                    Console.Error.WriteLine("Managed Linux setup and status commands do not accept arguments.");
                    return 1;
                }

                var image = PythonSettings.Load().Wsl.ContainerImage;
                if (args[0].Equals("linux-container-setup", StringComparison.OrdinalIgnoreCase))
                {
                    var setup = MxcLinuxProcessRunner.Setup(image);
                    if (setup.ExitCode != 0)
                    {
                        Console.Error.WriteLine(setup.StdErr);
                        return setup.ExitCode;
                    }
                }

                var check = MxcLinuxProcessRunner.Probe(image);
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    isReady = check.ExitCode == 0,
                    image,
                    message = check.ExitCode == 0 ? check.StdOut.Trim() : check.StdErr.Trim(),
                }));
                return 0;
            }

            var registry = new ScriptRegistry(root);
            registry.Load();

            return args[0].ToLowerInvariant() switch
            {
                "list" => RunList(registry, options),
                "run" => RunScript(registry, positional, options),
                "transform" => RunTransform(registry, positional, options),
                "mxc-support" => RunMxcSupport(options.ContainsKey("json")),
                "trust" => RunTrust(registry, positional),
                "-h" or "--help" or "help" => PrintUsage(),
                _ => Unknown(args[0]),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"PowerScripts error: {ex.Message}");
            return 2;
        }
    }

    private static int RunList(ScriptRegistry registry, IReadOnlyDictionary<string, List<string>> options)
    {
        foreach (var option in options.Keys)
        {
            if (option.ToLowerInvariant() is not ("json" or "input" or "output" or "no-input" or "include-disabled" or "include-unconfigured" or "root"))
            {
                Console.Error.WriteLine($"list: unknown option '--{option}'.");
                return 1;
            }
        }

        bool asJson = options.ContainsKey("json");

        // Optional discovery filters so any consumer — including native/C++ callers that only shell
        // out to this CLI — can narrow the catalogue without reimplementing the I/O model:
        //   list --input text      only scripts consuming text
        //   list --output text     only scripts producing text
        //   list --no-input        only actions (consume nothing)   [alias for --input none]
        if (!TryParseListFilters(options, out var wantInput, out var wantOutput))
        {
            return 1;
        }

        var catalogue = options.ContainsKey("include-unconfigured")
            ? registry.Scripts.Concat(registry.UnconfiguredScripts)
            : registry.Scripts;
        var scripts = options.ContainsKey("include-disabled")
            ? catalogue
            : FilterEnabledRuntimes(catalogue);
        var rows = scripts
            .Select(s =>
            {
                var (input, output) = ScriptIo.Resolve(s);
                return (Script: s, Input: input, Output: output);
            })
            .Where(r => wantInput is null || r.Input == wantInput)
            .Where(r => wantOutput is null || r.Output == wantOutput)
            .ToList();

        if (asJson)
        {
            foreach (var error in registry.Errors)
            {
                Console.Error.WriteLine($"  ! {error.FolderPath}: {error.Message}");
            }

            // Structured, permissioned capability list — also the shape the KBM editor picker and
            // future agents/MCP servers consume.
            var trustStore = new TrustStore(SettingsStore.Current);
            var projection = rows.Select(r =>
            {
                var s = r.Script;

                // Scripts declare only their I/O; each consuming module discovers the scripts it can
                // use by filtering on this shape (e.g. Advanced Paste wants scripts accepting Text).
                var ioInput = r.Input;
                var ioOutput = r.Output;

                return new
                {
                    s.Id,
                    s.Name,
                    s.Description,
                    runtime = s.Runtime.ToString(),
                    hasExplicitExecution = s.Execute is not null,
                    s.Publisher,
                    s.Version,
                    s.Source,
                    s.IconPath,
                    s.IconWarning,
                    s.HasDescriptor,
                    s.ConfigurationErrors,
                    io = new
                    {
                        input = ScriptIo.ToToken(ioInput),
                        output = ScriptIo.ToToken(ioOutput),
                    },

                    s.Capabilities,
                    s.InputSchema,
                    s.OutputSchema,
                    folderPath = s.FolderPath,
                    entryFullPath = s.EntryFullPath,
                    trusted = s.ConfigurationErrors.Count == 0 && trustStore.IsTrusted(s.Id, ScriptIntegrity.ComputeHash(s)),
                    input = s.Input,
                    parameters = s.Parameters.Select(parameter => new
                    {
                        parameter.Name,
                        parameter.Type,
                        parameter.IsRequired,
                        parameter.Label,
                        parameter.Description,
                        parameter.Default,
                        parameter.Options,
                        parameter.Min,
                        parameter.Max,
                    }),
                };
            });

            Console.WriteLine(JsonSerializer.Serialize(
                projection,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                }));
            return 0;
        }

        Console.WriteLine($"Scripts root: {registry.Root}");
        if (rows.Count == 0)
        {
            Console.WriteLine("(no scripts found)");
        }

        foreach (var r in rows)
        {
            var s = r.Script;
            var io = $"{ScriptIo.ToToken(r.Input)}->{ScriptIo.ToToken(r.Output)}";
            Console.WriteLine($"  {s.Id,-24} {s.Runtime,-10} {io,-14} {s.Name}");
        }

        foreach (var e in registry.Errors)
        {
            Console.Error.WriteLine($"  ! {e.FolderPath}: {e.Message}");
        }

        return 0;
    }

    private static int RunMxcSupport(bool asJson)
    {
        var settings = MxcSettings.Load();
        var support = settings.PlatformSupport ?? MxcPlatformSupport.Probe(settings);

        if (asJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                support.IsSupported,
                support.Reason,
                support.WindowsBuild,
                support.ExecutorPath,
                support.Tier,
                support.Warnings,
            }, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }));
        }
        else
        {
            Console.WriteLine(support.IsSupported
                ? $"MXC supported: {support.Reason}"
                : $"MXC unsupported: {support.Reason}");
        }

        return 0;
    }

    private static int RunScript(
        ScriptRegistry registry,
        IReadOnlyList<string> positional,
        IReadOnlyDictionary<string, List<string>> options)
    {
        if (positional.Count == 0)
        {
            Console.Error.WriteLine("run: missing <id>.");
            return 1;
        }

        var id = positional[0];
        var manifest = registry.Get(id);
        if (manifest is null)
        {
            Console.Error.WriteLine($"run: no script with id '{id}'. Try 'list'.");
            return 1;
        }

        var files = options.TryGetValue("files", out var f) ? f : new List<string>();

        var gate = EnsureRunnable(manifest, options, "run");
        if (gate != 0)
        {
            return gate;
        }

        var parameters = new Dictionary<string, string?>();
        if (options.TryGetValue("set", out var sets))
        {
            foreach (var kv in sets)
            {
                var idx = kv.IndexOf('=');
                if (idx <= 0)
                {
                    Console.Error.WriteLine($"run: --set expects name=value, got '{kv}'.");
                    return 1;
                }

                parameters[kv[..idx]] = kv[(idx + 1)..];
            }
        }

        if (options.TryGetValue("set-base64", out var encodedSets))
        {
            foreach (var encodedSet in encodedSets)
            {
                string decoded;
                try
                {
                    decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encodedSet));
                }
                catch (FormatException)
                {
                    Console.Error.WriteLine("run: --set-base64 received an invalid value.");
                    return 1;
                }

                var separator = decoded.IndexOf('\0');
                if (separator <= 0)
                {
                    Console.Error.WriteLine("run: --set-base64 received an invalid parameter.");
                    return 1;
                }

                parameters[decoded[..separator]] = decoded[(separator + 1)..];
            }
        }

        if (!ScriptParameterResolver.TryResolve(manifest, parameters, out var resolvedParameters, out var parameterError))
        {
            Console.Error.WriteLine($"run: {parameterError}");
            return 1;
        }

        var executor = new ScriptExecutor();
        var result = executor.Execute(manifest, files, resolvedParameters);

        if (!string.IsNullOrEmpty(result.StdOut))
        {
            Console.Out.Write(result.StdOut);
        }

        if (!string.IsNullOrEmpty(result.StdErr))
        {
            Console.Error.Write(result.StdErr);
        }

        return result.ExitCode;
    }

    /// <summary>
    /// The shared enabled + trust gate for any surface that runs a script. Returns 0 when the script
    /// may run, or a non-zero exit code (with a message on stderr) when it must not. Records trust on
    /// first interactive approval, exactly like <c>run</c>.
    /// </summary>
    private static int EnsureRunnable(
        PowerScriptManifest manifest,
        IReadOnlyDictionary<string, List<string>> options,
        string command)
    {
        // Central enabled gate: every surface runs scripts through this path, so a single check here
        // makes all bindings (Keyboard Manager, context menu, Advanced Paste, future modules) inert
        // when the user turns PowerScripts off — without deleting or rewriting them.
        if (!ModuleState.IsPowerScriptsEnabled())
        {
            Console.Error.WriteLine($"{command}: PowerScripts is disabled in PowerToys settings; refusing to run. Enable PowerScripts to use this binding.");
            return 4;
        }

        // Trust-on-first-use gate. A script only runs once the user has approved its exact current
        // content, and is re-prompted whenever the script body or its declared capabilities change.
        var trustStore = new TrustStore(SettingsStore.Current);
        var contentHash = ScriptIntegrity.ComputeHash(manifest);
        if (trustStore.IsTrusted(manifest.Id, contentHash))
        {
            return 0;
        }

        var nonInteractive = options.ContainsKey("no-consent")
            || string.Equals(Environment.GetEnvironmentVariable("POWERSCRIPTS_NO_CONSENT"), "1", StringComparison.Ordinal);

        if (nonInteractive)
        {
            Console.Error.WriteLine($"{command}: script '{manifest.Id}' is not trusted and consent is disabled; refusing to run. Approve it with 'trust approve {manifest.Id}'.");
            return 3;
        }

        if (!ConsentPrompt.Confirm(manifest))
        {
            Console.Error.WriteLine($"{command}: user declined to trust script '{manifest.Id}'.");
            return 3;
        }

        trustStore.Trust(new TrustRecord
        {
            Id = manifest.Id,
            Hash = contentHash,
            Capabilities = manifest.Capabilities,
            Source = manifest.Source,
            Publisher = manifest.Publisher,
            ApprovedUtc = DateTimeOffset.UtcNow,
        });

        return 0;
    }

    /// <summary>
    /// Runs a script as a typed data transform for surfaces like Advanced Paste. Reads a single JSON
    /// payload from stdin (<c>text</c> or <c>files</c>, and <c>parameters</c>), runs the script
    /// through the shared runtime, and writes a schema-validated result object to stdout. This
    /// is the same execution + trust path as <c>run</c>, so a script behaves identically whether it
    /// is triggered by a hotkey, the context menu, or a paste.
    /// </summary>
    private static int RunTransform(
        ScriptRegistry registry,
        IReadOnlyList<string> positional,
        IReadOnlyDictionary<string, List<string>> options)
    {
        if (positional.Count == 0)
        {
            Console.Error.WriteLine("transform: missing <id>.");
            return 1;
        }

        var manifest = registry.Get(positional[0]);
        if (manifest is null)
        {
            Console.Error.WriteLine($"transform: no script with id '{positional[0]}'. Try 'list'.");
            return 1;
        }

        var gate = EnsureRunnable(manifest, options, "transform");
        if (gate != 0)
        {
            return gate;
        }

        using var stdinReader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
        PythonTransformInput input;
        try
        {
            var builder = new StringBuilder();
            var buffer = new char[8192];
            int count;
            while ((count = stdinReader.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (manifest.InputFormat is PowerScriptDataFormat.Image or PowerScriptDataFormat.Audio &&
                    builder.Length + count > ScriptMedia.MaxRequestCharacters)
                {
                    throw new InvalidDataException("Input exceeds the bounded transform request size. Use a file-based contract for large media.");
                }

                builder.Append(buffer, 0, count);
            }

            var stdin = builder.ToString();
            input = PythonTransformInput.Parse(manifest, string.IsNullOrWhiteSpace(stdin) ? "{}" : stdin);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            Console.Error.WriteLine($"transform: invalid input JSON: {ex.Message}");
            return 1;
        }

        var suppliedParameters = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (input.Params is not null)
        {
            foreach (var pair in input.Params)
            {
                suppliedParameters[pair.Key] = pair.Value;
            }
        }

        if (!ScriptParameterResolver.TryResolve(
            manifest,
            suppliedParameters,
            out var resolvedParameters,
            out var parameterError))
        {
            Console.Error.WriteLine($"transform: {parameterError}");
            return 1;
        }

        input.Params = resolvedParameters.ToDictionary(
            pair => pair.Key,
            pair => pair.Value ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);

        if (manifest.Execute is null && manifest.Runtime == ScriptRuntime.Python)
        {
            var result = new PythonRuntime().Run(manifest, input);
            if (!string.IsNullOrEmpty(result.StdErr))
            {
                Console.Error.Write(result.StdErr);
            }

            Console.Out.Write(JsonSerializer.Serialize(result, TransformJsonOptions));
            return result.ExitCode;
        }

        // PowerShell transform and descriptor-authored scripts: hand text in as a parameter plus
        // any files. File outputs use the same explicit JSON object as Python; text stays literal.
        var parameters = new Dictionary<string, string?>(resolvedParameters, StringComparer.OrdinalIgnoreCase);
        var psResult = new ScriptExecutor().Execute(manifest, input.FilePaths, parameters, input.Text, input.Image, input.Audio);
        if (!string.IsNullOrEmpty(psResult.StdErr))
        {
            Console.Error.Write(psResult.StdErr);
        }

        if (psResult.ExitCode != 0)
        {
            Console.Error.Write(psResult.StdOut);
            return psResult.ExitCode;
        }

        PythonTransformResult output;
        try
        {
            output = ScriptIo.Resolve(manifest).Output switch
            {
                PowerScriptDataFormat.None => new PythonTransformResult(),
                PowerScriptDataFormat.Text => new PythonTransformResult { Text = psResult.StdOut },
                _ => PythonTransformResult.Parse(psResult.StdOut, ScriptIo.Resolve(manifest).Output, manifest.OutputSchema),
            };
            ScriptSchema.Validate(manifest.OutputSchema, JsonSerializer.SerializeToElement(output, TransformJsonOptions), "output");
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            Console.Error.WriteLine($"transform: invalid output: {ex.Message}");
            return 2;
        }

        if (ScriptIo.Resolve(manifest).Output == PowerScriptDataFormat.None)
        {
            Console.Error.Write(psResult.StdOut);
        }

        Console.Out.Write(JsonSerializer.Serialize(output, TransformJsonOptions));
        return 0;
    }

    private static readonly JsonSerializerOptions TransformJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Manages the trust store — the record of which script contents the user has approved to run.
    ///   trust list                 show every approved script id + the content hash approved
    ///   trust approve &lt;id&gt;         approve the script's current content without running it
    ///   trust revoke &lt;id&gt;          forget approval, so the next run re-prompts
    /// </summary>
    private static int RunTrust(ScriptRegistry registry, IReadOnlyList<string> positional)
    {
        var sub = positional.Count > 0 ? positional[0].ToLowerInvariant() : "list";
        var trustStore = new TrustStore(SettingsStore.Current);

        switch (sub)
        {
            case "list":
                if (trustStore.Records.Count == 0)
                {
                    Console.WriteLine("(no scripts trusted yet)");
                    return 0;
                }

                foreach (var record in trustStore.Records.OrderBy(r => r.Id, StringComparer.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"  {record.Id,-24} {record.Hash[..Math.Min(12, record.Hash.Length)]}  approved {record.ApprovedUtc:u}");
                }

                return 0;

            case "approve":
            {
                if (positional.Count < 2)
                {
                    Console.Error.WriteLine("trust approve: missing <id>.");
                    return 1;
                }

                var manifest = registry.Get(positional[1]);
                if (manifest is null)
                {
                    Console.Error.WriteLine($"trust approve: no script with id '{positional[1]}'. Try 'list'.");
                    return 1;
                }

                trustStore.Trust(new TrustRecord
                {
                    Id = manifest.Id,
                    Hash = ScriptIntegrity.ComputeHash(manifest),
                    Capabilities = manifest.Capabilities,
                    Source = manifest.Source,
                    Publisher = manifest.Publisher,
                    ApprovedUtc = DateTimeOffset.UtcNow,
                });

                Console.WriteLine($"trust approve: '{manifest.Id}' approved.");
                return 0;
            }

            case "revoke":
                if (positional.Count < 2)
                {
                    Console.Error.WriteLine("trust revoke: missing <id>.");
                    return 1;
                }

                if (trustStore.Revoke(positional[1]))
                {
                    Console.WriteLine($"trust revoke: '{positional[1]}' will be re-prompted on next run.");
                    return 0;
                }

                Console.Error.WriteLine($"trust revoke: '{positional[1]}' was not trusted.");
                return 1;

            default:
                Console.Error.WriteLine($"trust: unknown subcommand '{sub}'. Use list | approve <id> | revoke <id>.");
                return 1;
        }
    }

    /// <summary>
    /// Applies the global Python availability gate to ordinary consumer discovery.
    /// </summary>
    private static IEnumerable<PowerScriptManifest> FilterEnabledRuntimes(IEnumerable<PowerScriptManifest> scripts)
    {
        var pythonEnabled = PythonSettings.Load().Mode != PythonRuntimeMode.Disabled;
        return scripts.Where(script => script.Runtime != ScriptRuntime.Python || pythonEnabled);
    }

    /// <summary>
    /// Parses the optional <c>list</c> discovery filters (<c>--input</c>, <c>--output</c>,
    /// <c>--no-input</c>). Returns false (after printing an error) on an unknown token.
    /// </summary>
    private static bool TryParseListFilters(
        IReadOnlyDictionary<string, List<string>> options,
        out PowerScriptDataFormat? wantInput,
        out PowerScriptDataFormat? wantOutput)
    {
        wantInput = null;
        wantOutput = null;

        // --no-input is a convenience alias for --input none (actions that consume nothing).
        string? inputToken = options.ContainsKey("no-input")
            ? "none"
            : options.TryGetValue("input", out var i) ? i.FirstOrDefault() : null;
        string? outputToken = options.TryGetValue("output", out var o) ? o.FirstOrDefault() : null;

        return TryResolveIoFilter("--input", inputToken, ref wantInput)
            && TryResolveIoFilter("--output", outputToken, ref wantOutput);
    }

    private static bool TryResolveIoFilter(string flag, string? token, ref PowerScriptDataFormat? result)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return true;
        }

        var parsed = ScriptIo.TryParse(token);
        if (parsed is null)
        {
            Console.Error.WriteLine($"list: unknown {flag} '{token}'. Use one of: none, text, files, image, audio.");
            return false;
        }

        result = parsed;
        return true;
    }

    /// <summary>
    /// Minimal parser. Recognizes <c>--name value [value ...]</c> (multi-value, e.g. --files) and
    /// <c>--flag</c> (no value, e.g. --json). Everything else is positional.
    /// </summary>
    private static (List<string> Positional, Dictionary<string, List<string>> Options) ParseArgs(string[] args)
    {
        var positional = new List<string>();
        var options = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        string? current = null;
        foreach (var arg in args)
        {
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                current = arg[2..];
                if (!options.ContainsKey(current))
                {
                    options[current] = new List<string>();
                }
            }
            else if (current is not null)
            {
                options[current].Add(arg);
            }
            else
            {
                positional.Add(arg);
            }
        }

        return (positional, options);
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintUsage();
        return 1;
    }

    private static int PrintUsage()
    {
        Console.WriteLine("PowerScripts.Host — run and enumerate PowerScripts.");
        Console.WriteLine();
        Console.WriteLine("  list [--json] [--input <shape>] [--output <shape>] [--no-input] [--include-disabled] [--include-unconfigured] [--root <dir>]");
        Console.WriteLine("       Input and output: none | text | files.");
        Console.WriteLine("       Disabled runtimes are omitted; --include-disabled lists all installed scripts for configuration.");
        Console.WriteLine("  run <id> [--files <f1> <f2> ...] [--set name=value ...] [--no-consent] [--root <dir>]");
        Console.WriteLine("  transform <id> [--no-consent]       (run as a data transform; JSON payload on stdin, JSON result on stdout)");
        Console.WriteLine("  mxc-support [--json]                (probe whether MXC is available on this host)");
        Console.WriteLine("  linux-container-status             (check the managed Python image without downloading)");
        Console.WriteLine("  linux-container-setup              (explicitly download/cache the managed Python image)");
        Console.WriteLine("  trust list | approve <id> | revoke <id>   (manage which scripts are allowed to run)");
        Console.WriteLine("  template list | template create <template-id> [--configure] [--root <dir>]");
        Console.WriteLine("  template create --configure [--root <dir>] (generate from projectName, description, runtime, input, output, fileExtensions, minFiles and maxFiles JSON on stdin)");
        Console.WriteLine("       --configure reads {\"projectName\":\"My script\",\"description\":\"...\",\"fileExtensions\":\".txt, .md\"} from stdin. Extensions apply only to file-input templates.");
        Console.WriteLine("       Per-script MXC security settings only; save reads {\"mxc\":{...}} from stdin. Metadata belongs in .tool.json.");
        return 0;
    }
}
