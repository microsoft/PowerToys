// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TopToolbar.Models;

namespace TopToolbar.Services
{
    public static class ToolbarActionExecutor
    {
        public static void Execute(ToolbarAction action)
        {
            if (action == null)
            {
                return;
            }

            switch (action.Type)
            {
                case ToolbarActionType.CommandLine:
                    LaunchProcess(action);
                    break;
                default:
                    break;
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

                ManagedCommon.Logger.LogInfo($"Launch: file='{file}', ext='{ext}', args='{args}', wd='{workingDir}', runAsAdmin={action.RunAsAdmin}");
                var p = Process.Start(psi);
                if (p != null)
                {
                    ManagedCommon.Logger.LogInfo($"Launch: started pid={p.Id}");
                }
                else
                {
                    ManagedCommon.Logger.LogWarning("Launch: Process.Start returned null; attempting fallback without shell execute");

                    // Fallback: try without shell execute
                    var psi2 = new ProcessStartInfo
                    {
                        FileName = psi.FileName,
                        Arguments = psi.Arguments,
                        WorkingDirectory = psi.WorkingDirectory,
                        UseShellExecute = false,
                        CreateNoWindow = false,
                        Verb = action.RunAsAdmin ? "runas" : string.Empty,
                    };
                    try
                    {
                        var p2 = Process.Start(psi2);
                        if (p2 != null)
                        {
                            ManagedCommon.Logger.LogInfo($"Launch fallback: started pid={p2.Id}");
                        }
                        else
                        {
                            ManagedCommon.Logger.LogWarning("Launch fallback: Process.Start returned null again; attempting cmd /c start");
                            var psi3 = new ProcessStartInfo
                            {
                                FileName = "cmd.exe",
                                Arguments = $"/c start \"\" \"{file}\" {args}",
                                WorkingDirectory = workingDir,
                                UseShellExecute = true,
                                Verb = action.RunAsAdmin ? "runas" : "open",
                            };
                            var p3 = Process.Start(psi3);
                            if (p3 != null)
                            {
                                ManagedCommon.Logger.LogInfo($"Launch cmd start: started pid={p3.Id}");
                            }
                            else
                            {
                                ManagedCommon.Logger.LogError("Launch cmd start: Process.Start returned null");
                            }
                        }
                    }
                    catch (Exception ex2)
                    {
                        ManagedCommon.Logger.LogError("Launch fallback failed", ex2);
                        throw;
                    }
                }
            }
            catch (Win32Exception ex)
            {
                ManagedCommon.Logger.LogError($"Launch: Win32Exception {ex.NativeErrorCode} {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                ManagedCommon.Logger.LogError($"Launch: Exception {ex.GetType().Name} {ex.Message}", ex);
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
