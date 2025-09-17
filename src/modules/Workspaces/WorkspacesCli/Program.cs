// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

#nullable enable

namespace PowerToys.WorkspacesCli
{
    internal static class Program
    {
        private const string SnapshotToolName = "PowerToys.WorkspacesSnapshotTool.exe";
        private const string EditorToolName = "PowerToys.WorkspacesEditor.exe";
        private const string LauncherToolName = "PowerToys.WorkspacesLauncher.exe";

        private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static readonly string WorkspacesDirectory = Path.Combine(LocalAppData, "Microsoft", "PowerToys", "Workspaces");
        private static readonly string WorkspacesFilePath = Path.Combine(WorkspacesDirectory, "workspaces.json");

        public static async Task<int> Main(string[] args)
        {
            var root = new RootCommand("PowerToys Workspaces command line helper");

            root.AddCommand(BuildSnapshotCommand());
            root.AddCommand(BuildEditorCommand());
            root.AddCommand(BuildWorkspaceCommand());

            return await root.InvokeAsync(args).ConfigureAwait(false);
        }

        private static Command BuildSnapshotCommand()
        {
            var idOption = new Option<string?>(
                "--id",
                () => null,
                "Optional workspace id to pass through to the snapshot tool.");

            var forceOption = new Option<bool>(
                "--force",
                "Write the snapshot directly to workspaces.json using the new force mode.");
            forceOption.AddAlias("-f");

            var command = new Command(
                "snapshot",
                "Take a workspace snapshot using WorkspacesSnapshotTool.")
            {
                idOption,
                forceOption,
            };
            command.AddAlias("snap");

            command.SetHandler(
                async (InvocationContext context) =>
                {
                    var workspaceId = context.ParseResult.GetValueForOption(idOption);
                    var force = context.ParseResult.GetValueForOption(forceOption);

                    var arguments = BuildSnapshotArguments(workspaceId, force);
                    var exitCode = await RunProcessAsync(SnapshotToolName, arguments, waitForExit: true)
                        .ConfigureAwait(false);
                    context.ExitCode = exitCode;
                });

            return command;
        }

        private static Command BuildEditorCommand()
        {
            var command = new Command("open-editor", "Launch the Workspaces editor UI.");
            command.AddAlias("editor");

            command.SetHandler(
                async (InvocationContext context) =>
                {
                    var exitCode = await RunProcessAsync(EditorToolName, string.Empty, waitForExit: false)
                        .ConfigureAwait(false);
                    context.ExitCode = exitCode;
                });

            return command;
        }

        private static Command BuildWorkspaceCommand()
        {
            var command = new Command("workspace", "Workspace management commands");
            command.AddAlias("workspaces");

            command.AddCommand(BuildWorkspaceListCommand());
            command.AddCommand(BuildWorkspaceLaunchCommand());

            return command;
        }

        private static Command BuildWorkspaceListCommand()
        {
            var quietOption = new Option<bool>("--quiet", "Only output workspace ids.");
            quietOption.AddAlias("-q");

            var command = new Command(
                "list",
                "List the workspaces saved in workspaces.json")
            {
                quietOption,
            };
            command.AddAlias("ls");
            command.AddAlias("list-workspaces");

            command.SetHandler(
                async (InvocationContext context) =>
                {
                    var quiet = context.ParseResult.GetValueForOption(quietOption);
                    var workspaces = await LoadWorkspacesAsync().ConfigureAwait(false);
                    if (workspaces.Count == 0)
                    {
                        Console.Error.WriteLine($"No workspaces found at '{WorkspacesFilePath}'.");
                        context.ExitCode = 1;
                        return;
                    }

                    foreach (var workspace in workspaces)
                    {
                        if (quiet)
                        {
                            Console.WriteLine(workspace.Id);
                            continue;
                        }

                        var name = string.IsNullOrWhiteSpace(workspace.Name) ? "<unnamed>" : workspace.Name;
                        Console.WriteLine($"{workspace.Id}\t{name}");
                    }

                    context.ExitCode = 0;
                });

            return command;
        }

        private static Command BuildWorkspaceLaunchCommand()
        {
            var identifierArgument = new Argument<string>(
                "identifier",
                "Workspace id or name to launch.");

            var invokePointOption = new Option<InvokePoint>(
                "--invoke-point",
                () => InvokePoint.Shortcut,
                "Invoke point forwarded to WorkspacesLauncher. Accepts numeric values or names: EditorButton, Shortcut, LaunchAndEdit.");
            invokePointOption.AddAlias("-i");

            var command = new Command(
                "launch",
                "Launch a workspace through WorkspacesLauncher")
            {
                identifierArgument,
                invokePointOption,
            };
            command.AddAlias("start");

            command.SetHandler(
                async (InvocationContext context) =>
                {
                    var identifier = context.ParseResult.GetValueForArgument(identifierArgument);
                    var invokePoint = context.ParseResult.GetValueForOption(invokePointOption);
                    var workspaces = await LoadWorkspacesAsync().ConfigureAwait(false);
                    if (workspaces.Count == 0)
                    {
                        Console.Error.WriteLine($"No workspaces found at '{WorkspacesFilePath}'.");
                        context.ExitCode = 1;
                        return;
                    }

                    var matches = workspaces.Where(ws => ws.IsMatch(identifier)).ToList();
                    if (matches.Count == 0)
                    {
                        Console.Error.WriteLine($"Workspace '{identifier}' not found.");
                        context.ExitCode = 1;
                        return;
                    }

                    if (matches.Count > 1)
                    {
                        var options = string.Join(", ", matches.Select(m => m.DisplayName));
                        Console.Error.WriteLine($"Workspace identifier '{identifier}' is ambiguous. Matches: {options}.");
                        context.ExitCode = 1;
                        return;
                    }

                    var target = matches[0];
                    var launcherArgs = $"{target.Id} {(int)invokePoint}";
                    var exitCode = await RunProcessAsync(LauncherToolName, launcherArgs, waitForExit: true)
                        .ConfigureAwait(false);
                    context.ExitCode = exitCode;
                });

            return command;
        }

        private static string BuildSnapshotArguments(string? workspaceId, bool force)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(workspaceId))
            {
                parts.Add(workspaceId.Trim());
            }

            if (force)
            {
                parts.Add("-force");
            }

            return string.Join(' ', parts);
        }

        private static async Task<int> RunProcessAsync(string toolName, string arguments, bool waitForExit)
        {
            if (!TryResolveTool(toolName, out var resolvedPath))
            {
                Console.Error.WriteLine($"Unable to locate '{toolName}'. Expected alongside this CLI at '{AppContext.BaseDirectory}'.");
                return 1;
            }

            var startInfo = new ProcessStartInfo(resolvedPath)
            {
                Arguments = arguments,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(resolvedPath) ?? AppContext.BaseDirectory,
            };

            try
            {
                using var process = Process.Start(startInfo);
                if (process is null)
                {
                    Console.Error.WriteLine($"Failed to start '{toolName}'.");
                    return 1;
                }

                if (!waitForExit)
                {
                    return 0;
                }

                await process.WaitForExitAsync().ConfigureAwait(false);
                return process.ExitCode;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to launch '{toolName}': {ex.Message}");
                return 1;
            }
        }

        private static bool TryResolveTool(string toolName, out string path)
        {
            path = Path.Combine(AppContext.BaseDirectory, toolName);
            if (File.Exists(path))
            {
                return true;
            }

            // Fallback to repository root for developer scenarios.
            var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", ".."));
            var fallback = Path.Combine(repoRoot, toolName);
            if (File.Exists(fallback))
            {
                path = fallback;
                return true;
            }

            return false;
        }

        private static async Task<List<WorkspaceInfo>> LoadWorkspacesAsync()
        {
            if (!File.Exists(WorkspacesFilePath))
            {
                return new List<WorkspaceInfo>();
            }

            try
            {
                await using var stream = File.OpenRead(WorkspacesFilePath);
                using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

                if (!document.RootElement.TryGetProperty("workspaces", out var workspacesElement) || workspacesElement.ValueKind != JsonValueKind.Array)
                {
                    return new List<WorkspaceInfo>();
                }

                var result = new List<WorkspaceInfo>();
                foreach (var workspaceElement in workspacesElement.EnumerateArray())
                {
                    if (!workspaceElement.TryGetProperty("id", out var idElement))
                    {
                        continue;
                    }

                    var id = idElement.GetString();
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        continue;
                    }

                    var name = workspaceElement.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                    result.Add(new WorkspaceInfo(id.Trim(), name?.Trim()));
                }

                return result;
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Failed to parse '{WorkspacesFilePath}': {ex.Message}");
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"Unable to read '{WorkspacesFilePath}': {ex.Message}");
            }

            return new List<WorkspaceInfo>();
        }

        private sealed record WorkspaceInfo(string Id, string? Name)
        {
            public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : $"{Name} ({Id})";

            public bool IsMatch(string identifier)
            {
                if (string.Equals(Id, identifier, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(Name) && string.Equals(Name, identifier, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            }
        }

        private enum InvokePoint
        {
            EditorButton = 0,
            Shortcut = 1,
            LaunchAndEdit = 2,
        }
    }
}
