// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using PowerScripts.Core;
using PowerScripts.Core.Execution;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Registry;
using PowerScripts.Core.Security;

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
///   PowerScripts.Host list [--json] [--root &lt;dir&gt;]
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

            var registry = new ScriptRegistry(root);
            registry.Load();

            return args[0].ToLowerInvariant() switch
            {
                "list" => RunList(registry, options.ContainsKey("json")),
                "run" => RunScript(registry, positional, options),
                "transform" => RunTransform(registry, positional, options),
                "trust" => RunTrust(registry, positional),
                "kbm" => RunKbm(registry, positional, options.ContainsKey("json")),
                "shell-menu" => RunShellMenu(registry, options),
                "shell-install" => ShellRegistration.Install(registry, Environment.ProcessPath ?? "PowerScripts.Host.exe"),
                "shell-uninstall" => ShellRegistration.Uninstall(registry),
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

    private static int RunList(ScriptRegistry registry, bool asJson)
    {
        if (asJson)
        {
            // Structured, permissioned capability list — also the shape the KBM editor picker and
            // future agents/MCP servers consume.
            var trustStore = new TrustStore(PowerScriptsPaths.TrustFilePath);
            var projection = registry.Scripts.Select(s =>
            {
                // For Python scripts, derive the transform contract from the single
                // powerscript_from_<input>_to_<output> function so surfaces (e.g. Advanced Paste) know
                // which data shapes the script consumes/produces without running it.
                var signature = s.Runtime == ScriptRuntime.Python
                    ? PowerScriptPythonConvention.ParseFile(s.EntryFullPath)
                    : null;

                return new
                {
                    s.Id,
                    s.Name,
                    s.Description,
                    kind = s.Kind.ToString(),
                    runtime = s.Runtime.ToString(),
                    s.Publisher,
                    s.Version,
                    s.Source,
                    s.Surfaces,
                    s.Capabilities,
                    folderPath = s.FolderPath,
                    entryFullPath = s.EntryFullPath,
                    trusted = trustStore.IsTrusted(s.Id, ScriptIntegrity.ComputeHash(s)),
                    input = s.Input,
                    parameters = s.Parameters,
                    promptForParameters = s.PromptForParameters,
                    transform = signature is null ? null : new
                    {
                        function = signature.FunctionName,
                        inputFormat = signature.Input.ToString(),
                        outputFormat = signature.Output.ToString(),
                    },
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
        if (registry.Scripts.Count == 0)
        {
            Console.WriteLine("(no scripts found)");
        }

        foreach (var s in registry.Scripts)
        {
            Console.WriteLine($"  {s.Id,-24} [{s.Kind,-6}] {s.Name}");
        }

        foreach (var e in registry.Errors)
        {
            Console.Error.WriteLine($"  ! {e.FolderPath}: {e.Message}");
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

        // Optional parameter prompt: only when the manifest opts in and declares parameters. The
        // dialog is pre-filled with each parameter's default and any --set override. --no-prompt
        // suppresses it (e.g. for automated runs), keeping the current --set-only behavior.
        if (manifest.PromptForParameters &&
            manifest.Parameters.Count > 0 &&
            !options.ContainsKey("no-prompt"))
        {
            var initial = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var p in manifest.Parameters)
            {
                initial[p.Name] = parameters.TryGetValue(p.Name, out var overridden) ? overridden : p.Default;
            }

            if (!PromptLauncher.TryPrompt(manifest, initial, out var chosen))
            {
                Console.Error.WriteLine("run: cancelled at the parameter prompt.");
                return 2;
            }

            foreach (var (name, value) in chosen)
            {
                parameters[name] = value;
            }
        }

        var executor = new ScriptExecutor();
        var result = executor.Execute(manifest, files, parameters);

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
        var trustStore = new TrustStore(PowerScriptsPaths.TrustFilePath);
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
    /// payload from stdin (<c>text</c>, <c>html</c>, <c>image_path</c>, <c>file_paths</c>,
    /// <c>params</c>), runs the script through the shared runtime, and writes a single JSON result
    /// object (<c>text</c>, <c>html</c>, <c>image_path</c>, <c>file_paths</c>, ...) to stdout. This
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

        var stdin = Console.In.ReadToEnd();
        PythonTransformInput input;
        try
        {
            input = string.IsNullOrWhiteSpace(stdin)
                ? new PythonTransformInput()
                : JsonSerializer.Deserialize<PythonTransformInput>(stdin, TransformJsonOptions) ?? new PythonTransformInput();
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"transform: invalid input JSON: {ex.Message}");
            return 1;
        }

        if (manifest.Runtime == ScriptRuntime.Python)
        {
            var result = new PythonRuntime().Run(manifest, input);
            if (!string.IsNullOrEmpty(result.StdErr))
            {
                Console.Error.Write(result.StdErr);
            }

            Console.Out.Write(JsonSerializer.Serialize(result, TransformJsonOptions));
            return result.ExitCode;
        }

        // PowerShell transform: hand the text/html in as parameters and any files, and return the
        // script's stdout as the transformed text.
        var parameters = new Dictionary<string, string?>
        {
            ["Text"] = input.Text ?? string.Empty,
            ["Html"] = input.Html ?? string.Empty,
        };

        var psResult = new ScriptExecutor().Execute(manifest, input.FilePaths, parameters);
        if (!string.IsNullOrEmpty(psResult.StdErr))
        {
            Console.Error.Write(psResult.StdErr);
        }

        Console.Out.Write(JsonSerializer.Serialize(new PythonTransformResult { Text = psResult.StdOut }, TransformJsonOptions));
        return psResult.ExitCode;
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
        var trustStore = new TrustStore(PowerScriptsPaths.TrustFilePath);

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
    /// Emits the Keyboard Manager "Run Program" mapping for a system PowerScript so a user (or the
    /// future KBM editor picker) can bind a hotkey to it. KBM's existing RunProgram action already
    /// supports this — no KBM engine change is needed. The app path + args go straight into the
    /// editor's "Run Program" fields; <c>--json</c> emits the on-disk mapping shape (the user still
    /// chooses the trigger keys, so <c>originalKeys</c> is left as a placeholder).
    /// </summary>
    private static int RunKbm(ScriptRegistry registry, IReadOnlyList<string> positional, bool asJson)
    {
        if (positional.Count == 0)
        {
            Console.Error.WriteLine("kbm: missing <id>.");
            return 1;
        }

        var manifest = registry.Get(positional[0]);
        if (manifest is null)
        {
            Console.Error.WriteLine($"kbm: no script with id '{positional[0]}'. Try 'list'.");
            return 1;
        }

        var hostPath = Environment.ProcessPath ?? "PowerScripts.Host.exe";
        var programArgs = $"run {manifest.Id}";

        if (asJson)
        {
            // Field names match the KBM engine (see common/KeyboardManagerConstants.h /
            // MappingConfiguration.cpp). Append this to remapShortcutsToRunProgram and set
            // originalKeys to your chosen trigger (e.g. "162;91;83" for Ctrl+Win+S).
            var mapping = new Dictionary<string, object>
            {
                ["originalKeys"] = "<set-your-trigger-keys>",
                ["operationType"] = 1,
                ["runProgramFilePath"] = hostPath,
                ["runProgramArgs"] = programArgs,
                ["runProgramStartInDir"] = string.Empty,
                ["runProgramElevationLevel"] = 0,
                ["runProgramAlreadyRunningAction"] = 0,
                ["runProgramStartWindowType"] = 0,
                ["unicodeText"] = "*Unsupported*",
            };

            Console.WriteLine(JsonSerializer.Serialize(mapping, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.WriteLine($"PowerScript '{manifest.Id}' ({manifest.Name}) — Keyboard Manager 'Run Program' action:");
        Console.WriteLine($"  Program:    {hostPath}");
        Console.WriteLine($"  Arguments:  {programArgs}");
        Console.WriteLine();
        Console.WriteLine("In Keyboard Manager: Remap a shortcut -> action 'Run Program', paste the values above,");
        Console.WriteLine("then pick the trigger shortcut. (Use 'kbm <id> --json' for the raw mapping object.)");
        return 0;
    }

    /// <summary>
    /// Emits the file scripts that match a right-clicked selection as tab-separated
    /// <c>&lt;id&gt;\t&lt;name&gt;</c> lines (one per script). This is the machine-readable feed the
    /// Windows 11 modern context-menu handler (IExplorerCommand) consumes to build its submenu; a
    /// line-based format keeps the native handler free of a JSON parser.
    /// </summary>
    private static int RunShellMenu(ScriptRegistry registry, IReadOnlyDictionary<string, List<string>> options)
    {
        // When the module is disabled, emit nothing so the Explorer submenu has no items to show.
        if (!ModuleState.IsPowerScriptsEnabled())
        {
            return 0;
        }

        var files = options.TryGetValue("files", out var f) ? f : new List<string>();
        if (files.Count == 0)
        {
            return 0;
        }

        foreach (var script in registry.FileScriptsForSelection(files))
        {
            Console.WriteLine($"{script.Id}\t{script.Name}");
        }

        return 0;
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
        Console.WriteLine("  list [--json] [--root <dir>]");
        Console.WriteLine("  run <id> [--files <f1> <f2> ...] [--set name=value ...] [--no-prompt] [--no-consent] [--root <dir>]");
        Console.WriteLine("  transform <id> [--no-consent]       (run as a data transform; JSON payload on stdin, JSON result on stdout)");
        Console.WriteLine("  trust list | approve <id> | revoke <id>   (manage which scripts are allowed to run)");
        Console.WriteLine("  kbm <id> [--json] [--root <dir>]    (Keyboard Manager 'Run Program' mapping)");
        Console.WriteLine("  shell-menu --files <f1> <f2> ...    (tab-separated id/name of matching file scripts)");
        Console.WriteLine("  shell-install [--root <dir>]        (register the Explorer right-click submenu)");
        Console.WriteLine("  shell-uninstall [--root <dir>]      (remove the Explorer right-click submenu)");
        return 0;
    }
}
