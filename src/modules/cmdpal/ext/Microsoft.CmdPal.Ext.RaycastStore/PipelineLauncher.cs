// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;

namespace Microsoft.CmdPal.Ext.RaycastStore;

internal static class PipelineLauncher
{
    private static readonly string PipelineRelativePath = Path.Combine(
        "src",
        "modules",
        "cmdpal",
        "extensionsdk",
        "raycast-compat",
        "tools",
        "pipeline",
        "dist",
        "cli.js");

    private static string GetPipelineScriptPath()
    {
        string baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (string? current = baseDir; current != null; current = Path.GetDirectoryName(current))
        {
            string candidate = Path.Combine(current, PipelineRelativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string fallback = Path.Combine(baseDir, "Assets", "raycast-pipeline", "cli.js");
        if (File.Exists(fallback))
        {
            return fallback;
        }

        throw new FileNotFoundException("Raycast pipeline script not found. Searched up from: " + baseDir + " and " + fallback);
    }

    public static async Task<PipelineResult> InstallAsync(string extensionName, Action<string, string>? onProgress = null, CancellationToken cancellationToken = default)
    {
        return await RunPipelineAsync("install", extensionName, onProgress, cancellationToken);
    }

    public static async Task<PipelineResult> UninstallAsync(string extensionName, Action<string, string>? onProgress = null, CancellationToken cancellationToken = default)
    {
        return await RunPipelineAsync("uninstall", extensionName, onProgress, cancellationToken);
    }

    private static async Task<PipelineResult> RunPipelineAsync(string command, string extensionName, Action<string, string>? onProgress, CancellationToken cancellationToken)
    {
        string scriptPath;
        try
        {
            scriptPath = GetPipelineScriptPath();
        }
        catch (FileNotFoundException ex)
        {
            return new PipelineResult
            {
                Success = false,
                Error = ex.Message,
            };
        }

        string arguments = $"\"{scriptPath}\" {command} {extensionName}";
        string? githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrEmpty(githubToken) && command == "install")
        {
            arguments = arguments + " --token " + githubToken;
        }

        try
        {
            using Process process = new();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            process.Start();

            StringBuilder outputBuilder = new();
            StringBuilder errorBuilder = new();

            Task stdoutTask = Task.Run(
                async () =>
                {
                    while (!process.StandardOutput.EndOfStream)
                    {
                        string? line = await process.StandardOutput.ReadLineAsync(cancellationToken);
                        if (line != null)
                        {
                            outputBuilder.AppendLine(line);
                            ParseProgressLine(line, onProgress);
                        }
                    }
                },
                cancellationToken);

            Task stderrTask = Task.Run(
                async () =>
                {
                    string stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                    if (!string.IsNullOrWhiteSpace(stderr))
                    {
                        errorBuilder.Append(stderr);
                    }
                },
                cancellationToken);

            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(cancellationToken);

            string output = outputBuilder.ToString();
            string stderrText = errorBuilder.ToString();

            if (process.ExitCode == 0)
            {
                string? extensionPath = null;
                string[] lines = output.Split('\n');
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("✓ Installed to:", StringComparison.Ordinal))
                    {
                        extensionPath = trimmed.Substring("✓ Installed to:".Length).Trim();
                    }
                    else if (trimmed.StartsWith("✓ Uninstalled", StringComparison.Ordinal))
                    {
                        extensionPath = trimmed;
                    }
                }

                return new PipelineResult
                {
                    Success = true,
                    ExtensionPath = extensionPath,
                    Output = output,
                };
            }

            string errorMsg = !string.IsNullOrWhiteSpace(stderrText) ? stderrText : output;
            Logger.LogError($"Pipeline {command} failed (exit {process.ExitCode}): {errorMsg}");
            return new PipelineResult
            {
                Success = false,
                Error = $"Pipeline failed with exit code {process.ExitCode}: {errorMsg.Trim()}",
                Output = output,
            };
        }
        catch (OperationCanceledException)
        {
            return new PipelineResult
            {
                Success = false,
                Error = "Operation cancelled.",
            };
        }
        catch (Exception ex)
        {
            Logger.LogError("Pipeline " + command + " exception: " + ex.Message);
            return new PipelineResult
            {
                Success = false,
                Error = ex.Message,
            };
        }
    }

    private static void ParseProgressLine(string line, Action<string, string>? onProgress)
    {
        if (onProgress == null)
        {
            return;
        }

        string trimmed = line.TrimStart();
        if (trimmed.StartsWith('['))
        {
            int closeBracket = trimmed.IndexOf(']');
            if (closeBracket > 1)
            {
                string stage = trimmed.Substring(1, closeBracket - 1);
                string message = trimmed.Substring(closeBracket + 1).TrimStart();
                onProgress(stage, message);
            }
        }
    }
}
