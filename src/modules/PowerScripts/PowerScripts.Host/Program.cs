// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using PowerScripts.Core;
using PowerScripts.Core.Execution;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Registry;

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
                "kbm" => RunKbm(registry, positional, options.ContainsKey("json")),
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
            var projection = registry.Scripts.Select(s => new
            {
                s.Id,
                s.Name,
                s.Description,
                kind = s.Kind.ToString(),
                runtime = s.Runtime.ToString(),
                s.Surfaces,
                s.Capabilities,
                input = s.Input,
                parameters = s.Parameters,
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
        Console.WriteLine("  run <id> [--files <f1> <f2> ...] [--set name=value ...] [--root <dir>]");
        Console.WriteLine("  kbm <id> [--json] [--root <dir>]    (Keyboard Manager 'Run Program' mapping)");
        Console.WriteLine("  shell-install [--root <dir>]        (register the Explorer right-click submenu)");
        Console.WriteLine("  shell-uninstall [--root <dir>]      (remove the Explorer right-click submenu)");
        return 0;
    }
}
