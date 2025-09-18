// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using TopToolbar.Actions;
using TopToolbar.Models;

namespace TopToolbar.Services
{
    public sealed class ToolbarActionExecutor
    {
        private readonly ActionProviderService _providerService;
        private readonly ActionContextFactory _contextFactory;

        public ToolbarActionExecutor(ActionProviderService providerService, ActionContextFactory contextFactory)
        {
            _providerService = providerService ?? throw new ArgumentNullException(nameof(providerService));
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public Task ExecuteAsync(ButtonGroup group, ToolbarButton button, CancellationToken cancellationToken = default)
        {
            if (button?.Action == null)
            {
                return Task.CompletedTask;
            }

            return button.Action.Type switch
            {
                ToolbarActionType.CommandLine => ExecuteCommandLineAsync(button.Action),
                ToolbarActionType.Provider => ExecuteProviderActionAsync(group, button, cancellationToken),
                _ => Task.CompletedTask,
            };
        }

        private static Task ExecuteCommandLineAsync(ToolbarAction action)
        {
            LaunchProcess(action);
            return Task.CompletedTask;
        }

        private async Task ExecuteProviderActionAsync(ButtonGroup group, ToolbarButton button, CancellationToken cancellationToken)
        {
            var action = button.Action;
            if (string.IsNullOrWhiteSpace(action.ProviderId) || string.IsNullOrWhiteSpace(action.ProviderActionId))
            {
                Logger.LogWarning("ToolbarActionExecutor: provider metadata missing for dynamic action.");
                return;
            }

            button.IsExecuting = true;
            button.ProgressMessage = string.Empty;
            button.ProgressValue = null;
            button.StatusMessage = string.Empty;

            JsonElement? args = null;
            if (!string.IsNullOrWhiteSpace(action.ProviderArgumentsJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(action.ProviderArgumentsJson);
                    args = doc.RootElement.Clone();
                }
                catch (JsonException ex)
                {
                    Logger.LogWarning($"ToolbarActionExecutor: failed to parse provider arguments. - {ex.Message}");
                }
            }

            var context = _contextFactory.CreateForInvocation(group, button);
            var progress = new Progress<ActionProgress>(update =>
            {
                if (update == null)
                {
                    return;
                }

                if (update.Percent.HasValue)
                {
                    button.ProgressValue = update.Percent.Value;
                }

                if (!string.IsNullOrWhiteSpace(update.Note))
                {
                    button.ProgressMessage = update.Note;
                }
            });

            try
            {
                var result = await _providerService
                    .InvokeAsync(action.ProviderId, action.ProviderActionId, args, context, progress, cancellationToken)
                    .ConfigureAwait(false);

                if (result != null)
                {
                    button.StatusMessage = string.IsNullOrWhiteSpace(result.Message)
                        ? (result.Ok ? string.Empty : "Action failed.")
                        : result.Message;
                }
            }
            catch (OperationCanceledException)
            {
                button.StatusMessage = "Cancelled.";
                throw;
            }
            catch (Exception ex)
            {
                button.StatusMessage = ex.Message;
                Logger.LogError($"ToolbarActionExecutor: provider invocation threw an exception. - {ex.Message}");
            }
            finally
            {
                button.ProgressValue = null;
                button.ProgressMessage = string.Empty;
                button.IsExecuting = false;
            }
        }

        private static void LaunchProcess(ToolbarAction action)
        {
            if (string.IsNullOrWhiteSpace(action.Command))
            {
                return;
            }

            try
            {
                var file = action.Command!.Trim();
                var args = action.Arguments ?? string.Empty;

                // Expand environment variables
                file = Environment.ExpandEnvironmentVariables(file);

                // If quoted path, extract path
                if (file.StartsWith('"'))
                {
                    int end = file.IndexOf('"', 1);
                    if (end > 1)
                    {
                        args = file.Substring(end + 1).TrimStart() + (string.IsNullOrEmpty(args) ? string.Empty : (" " + args));
                        file = file.Substring(1, end - 1);
                    }
                }

                var workingDir = string.IsNullOrWhiteSpace(action.WorkingDirectory) ? Environment.CurrentDirectory : action.WorkingDirectory;

                // Resolve via WorkingDirectory and PATH if needed (handles name-only commands like `code`)
                var resolved = ResolveCommandToFilePath(file, workingDir);
                if (!string.IsNullOrEmpty(resolved))
                {
                    file = resolved;
                }

                var ext = Path.GetExtension(file)?.ToLowerInvariant();

                ProcessStartInfo psi;

                if (ext == ".ps1")
                {
                    // PowerShell script: prefer PowerShell 7 if available, else Windows PowerShell
                    var host = "pwsh.exe";
                    psi = new ProcessStartInfo
                    {
                        FileName = host,
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{file}\" {args}".Trim(),
                        WorkingDirectory = workingDir,
                        UseShellExecute = true,
                        Verb = action.RunAsAdmin ? "runas" : "open",
                    };
                }
                else if (ext == ".bat" || ext == ".cmd")
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"\"{file}\" {args}\"".Trim(),
                        WorkingDirectory = workingDir,
                        UseShellExecute = true,
                        Verb = action.RunAsAdmin ? "runas" : "open",
                    };
                }
                else if (ext == ".vbs" || ext == ".js")
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = "wscript.exe",
                        Arguments = $"\"{file}\" {args}".Trim(),
                        WorkingDirectory = workingDir,
                        UseShellExecute = true,
                        Verb = action.RunAsAdmin ? "runas" : "open",
                    };
                }
                else
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = file,
                        Arguments = args,
                        WorkingDirectory = workingDir,
                        UseShellExecute = true,
                        Verb = action.RunAsAdmin ? "runas" : "open",
                    };
                }

                Logger.LogInfo($"Launch: file='{file}', ext='{ext}', args='{args}', wd='{workingDir}', runAsAdmin={action.RunAsAdmin}");
                var p = Process.Start(psi);
                if (p != null)
                {
                    Logger.LogInfo($"Launch: started pid={p.Id}");
                }
                else
                {
                    Logger.LogWarning("Launch: Process.Start returned null; attempting fallback without shell execute");

                    psi.UseShellExecute = false;
                    psi.Verb = string.Empty;
                    p = Process.Start(psi);
                    if (p != null)
                    {
                        Logger.LogInfo($"Launch fallback: started pid={p.Id}");
                    }
                    else
                    {
                        Logger.LogError("Launch: Process.Start returned null even without shell execute");
                    }
                }
            }
            catch (Win32Exception ex)
            {
                Logger.LogError($"Launch: Win32Exception {ex.NativeErrorCode} {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Launch: Exception {ex.GetType().Name} {ex.Message}");
            }
        }

        private static string ResolveCommandToFilePath(string file, string workingDir)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                return null;
            }

            try
            {
                var candidate = file.Trim();
                candidate = Environment.ExpandEnvironmentVariables(candidate);

                bool hasRoot = Path.IsPathRooted(candidate);
                bool hasExt = Path.HasExtension(candidate);

                if (hasRoot || candidate.Contains('\\') || candidate.Contains('/'))
                {
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }

                    // Try alternate PATHEXT extensions as a fallback
                    var dirName = Path.GetDirectoryName(candidate) ?? string.Empty;
                    var nameNoExtOnly = Path.GetFileNameWithoutExtension(candidate);
                    var nameNoExt = string.IsNullOrEmpty(dirName) ? nameNoExtOnly : Path.Combine(dirName, nameNoExtOnly);
                    foreach (var ext in GetPathExtensions())
                    {
                        var p = nameNoExt + ext;
                        if (File.Exists(p))
                        {
                            return p;
                        }
                    }

                    return null;
                }

                var dirs = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(workingDir) && Directory.Exists(workingDir))
                {
                    dirs.Add(workingDir);
                }

                dirs.Add(Environment.CurrentDirectory);
                var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                foreach (var d in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    dirs.Add(d);
                }

                foreach (var dir in dirs)
                {
                    var basePath = Path.Combine(dir, candidate);
                    if (hasExt)
                    {
                        if (File.Exists(basePath))
                        {
                            return basePath;
                        }

                        var nameNoExtOnly = Path.GetFileNameWithoutExtension(candidate);
                        var nameNoExt = Path.Combine(dir, nameNoExtOnly);
                        foreach (var ext in GetPathExtensions())
                        {
                            var p = nameNoExt + ext;
                            if (File.Exists(p))
                            {
                                return p;
                            }
                        }
                    }
                    else
                    {
                        foreach (var ext in GetPathExtensions())
                        {
                            var p = basePath + ext;
                            if (File.Exists(p))
                            {
                                return p;
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static System.Collections.Generic.IEnumerable<string> GetPathExtensions()
        {
            var pathext = Environment.GetEnvironmentVariable("PATHEXT");
            if (string.IsNullOrWhiteSpace(pathext))
            {
                return new[] { ".COM", ".EXE", ".BAT", ".CMD", ".VBS", ".JS", ".WS", ".MSC", ".PS1" };
            }

            return pathext.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
        }
    }
}
