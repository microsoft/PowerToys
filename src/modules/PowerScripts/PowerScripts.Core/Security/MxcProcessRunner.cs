// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PowerScripts.Core.Execution;

namespace PowerScripts.Core.Security;

/// <summary>
/// Builds a stable-schema MXC request and launches the native <c>wxc-exec.exe</c>. The executor
/// itself is launched through <see cref="ProcessRunner"/>'s secure launcher, preserving the
/// elevated-host de-elevation guarantee before any sandbox setup occurs.
/// </summary>
public static class MxcProcessRunner
{
    public const string ExecutorEnvironmentVariable = "POWERSCRIPTS_MXC_EXECUTOR";
    public const string WorkspaceEnvironmentVariable = "POWERSCRIPTS_MXC_WORKSPACE";
    public const int UnavailableExitCode = 125;
    public const int LaunchFailedExitCode = 127;

    private const string SchemaVersion = "0.8.0-alpha";
    private static readonly TimeSpan RetainedOutputLifetime = TimeSpan.FromDays(1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Runs one target inside MXC. The optional seams keep failure/config tests independent from an
    /// installed MXC binary and from real child processes.
    /// </summary>
    public static ProcessRunResult Run(
        ProcessStartInfo target,
        ProcessExecutionContext context,
        MxcEffectivePolicy policy,
        MxcSettings settings,
        string? standardInput = null,
        int timeoutMs = 0,
        IProcessLauncher? launcher = null,
        Func<MxcSettings, string?>? executorResolver = null,
        Func<string>? runDirectoryFactory = null)
    {
        executorResolver ??= ResolveExecutor;
        var executorPath = executorResolver(settings);
        if (string.IsNullOrWhiteSpace(executorPath))
        {
            return new ProcessRunResult(
                UnavailableExitCode,
                string.Empty,
                "PowerScripts refused to run this script: MXC is enabled, but wxc-exec.exe is unavailable. " +
                "Configure mxc.executorPath, set POWERSCRIPTS_MXC_EXECUTOR, place it beside PowerScripts.Host.exe, or add it to PATH.");
        }

        launcher ??= ProcessRunner.SecureLauncher;
        runDirectoryFactory ??= CreateRunDirectory;
        string? runDirectory = null;
        string? configPath = null;
        var preserveWorkspace = false;

        try
        {
            runDirectory = runDirectoryFactory();
            var workspace = Path.Combine(runDirectory, "workspace");
            Directory.CreateDirectory(workspace);

            EnsureWorkspaceOutsideScripts(context, workspace);

            var resolvedTarget = ResolveCommandPath(target.FileName) ?? target.FileName;
            var configJson = BuildConfigurationJson(target, resolvedTarget, context, policy, workspace, timeoutMs);
            configPath = Path.Combine(runDirectory, "mxc-config.json");
            File.WriteAllText(configPath, configJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var executor = new ProcessStartInfo
            {
                FileName = executorPath,
                WorkingDirectory = runDirectory,
                StandardInputEncoding = target.StandardInputEncoding,
                StandardOutputEncoding = target.StandardOutputEncoding,
                StandardErrorEncoding = target.StandardErrorEncoding,
            };
            executor.ArgumentList.Add(configPath);
            executor.ArgumentList.Add("--");
            executor.ArgumentList.Add(resolvedTarget);
            foreach (var argument in target.ArgumentList)
            {
                executor.ArgumentList.Add(argument);
            }

            // MXC owns the contained timeout. A small outer grace period lets it tear down the
            // container and return its normal timeout result before the host kills wxc-exec itself.
            var executorTimeout = timeoutMs > 0 ? checked(timeoutMs + 5000) : 0;
            var result = launcher.Run(executor, standardInput, executorTimeout);

            // Produced files commonly live below cwd/TEMP. Keep successful non-empty workspaces so
            // returned paths remain valid after this method returns; stale runs are pruned later.
            preserveWorkspace = result.ExitCode == 0 &&
                                Directory.EnumerateFileSystemEntries(workspace).Any();
            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            Win32Exception or InvalidOperationException or ArgumentException or OverflowException)
        {
            return new ProcessRunResult(
                LaunchFailedExitCode,
                string.Empty,
                $"PowerScripts refused to run this script because MXC configuration or startup failed: {ex.Message}");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(runDirectory))
            {
                try
                {
                    if (preserveWorkspace)
                    {
                        if (!string.IsNullOrWhiteSpace(configPath) && File.Exists(configPath))
                        {
                            File.Delete(configPath);
                        }
                    }
                    else if (Directory.Exists(runDirectory))
                    {
                        Directory.Delete(runDirectory, recursive: true);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // The run has already ended; cleanup is best effort and never triggers a direct fallback.
                }
            }
        }
    }

    /// <summary>Creates the schema 0.8 request without launching MXC.</summary>
    public static string BuildConfigurationJson(
        ProcessStartInfo target,
        string resolvedTargetPath,
        ProcessExecutionContext context,
        MxcEffectivePolicy policy,
        string workspace,
        int timeoutMs)
    {
        if (!policy.Enabled)
        {
            throw new InvalidOperationException("An MXC configuration cannot be built when MXC is disabled.");
        }

        EnsureWorkspaceOutsideScripts(context, workspace);

        var process = new Dictionary<string, object?>
        {
            ["cwd"] = workspace,
            ["env"] = BuildSanitizedEnvironment(target, resolvedTargetPath, workspace),
            ["timeout"] = timeoutMs,
        };

        var config = new Dictionary<string, object?>
        {
            ["version"] = SchemaVersion,
            ["containerId"] = "powerscripts-" + Guid.NewGuid().ToString("N"),
            ["containment"] = "processcontainer",
            ["process"] = process,
            ["lifecycle"] = new Dictionary<string, object?>
            {
                ["destroyOnExit"] = true,
                ["preservePolicy"] = false,
            },
        };

        var restrictFilesystem = policy.IsEnabled(MxcPolicies.Filesystem);
        config["filesystem"] = new Dictionary<string, object?>
        {
            ["readonlyPaths"] = restrictFilesystem
                ? BuildReadonlyPaths(target, resolvedTargetPath, context)
                : BuildProtectedScriptPaths(context),
            ["readwritePaths"] = restrictFilesystem
                ? new[] { Path.GetFullPath(workspace) }
                : BuildHostFilesystemReadwritePaths(workspace),
        };

        if (policy.IsEnabled(MxcPolicies.Network))
        {
            config["network"] = new Dictionary<string, object?>
            {
                ["egress"] = new Dictionary<string, object?> { ["default"] = "deny" },
                ["ingress"] = new Dictionary<string, object?>
                {
                    ["default"] = "deny",
                    ["hostLoopback"] = "deny",
                },
            };
        }

        var processContainer = new Dictionary<string, object?>();
        if (policy.IsEnabled(MxcPolicies.LeastPrivilege))
        {
            processContainer["leastPrivilege"] = true;
        }

        if (policy.IsEnabled(MxcPolicies.Ui))
        {
            config["ui"] = new Dictionary<string, object?>
            {
                // PowerShell and other console runtimes initialize Win32k even without showing UI.
                // Keep Win32k available while isolating the desktop and denying host interaction.
                ["disable"] = false,
                ["clipboard"] = "none",
                ["injection"] = false,
            };
            processContainer["ui"] = new Dictionary<string, object?>
            {
                ["isolation"] = "container",
                ["desktopSystemControl"] = false,
                ["systemSettings"] = "none",
                ["ime"] = false,
            };
        }

        if (processContainer.Count > 0)
        {
            config["processContainer"] = processContainer;
        }

        return JsonSerializer.Serialize(config, JsonOptions);
    }

    /// <summary>
    /// Resolves <c>wxc-exec.exe</c> in priority order: explicit setting, environment override,
    /// adjacent to the host, then PATH.
    /// </summary>
    public static string? ResolveExecutor(MxcSettings settings) =>
        ResolveExecutor(
            settings,
            Environment.GetEnvironmentVariable(ExecutorEnvironmentVariable),
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable("PATH"));

    public static string? ResolveExecutor(
        MxcSettings settings,
        string? environmentOverride,
        string appBaseDirectory,
        string? pathEnvironment)
    {
        foreach (var candidate in new[]
        {
            settings.ExecutorPath,
            environmentOverride,
            Path.Combine(appBaseDirectory, "wxc-exec.exe"),
        })
        {
            var resolved = ResolveCandidate(candidate, pathEnvironment);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return FindOnPath("wxc-exec.exe", pathEnvironment);
    }

    private static string CreateRunDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "PowerScripts");
        DeleteStaleRunDirectories(root);
        var path = Path.Combine(root, "mxc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteStaleRunDirectories(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        var cutoff = DateTime.UtcNow - RetainedOutputLifetime;
        foreach (var directory in Directory.EnumerateDirectories(root, "mxc-*"))
        {
            try
            {
                if (Directory.GetLastWriteTimeUtc(directory) < cutoff)
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Cleanup is best effort and must not prevent a new isolated run.
            }
        }
    }

    private static void EnsureWorkspaceOutsideScripts(ProcessExecutionContext context, string workspace)
    {
        foreach (var protectedRoot in new[] { context.ScriptsRoot, context.ScriptDirectory })
        {
            if (!string.IsNullOrWhiteSpace(protectedRoot) && IsSameOrChild(protectedRoot, workspace))
            {
                throw new InvalidOperationException(
                    "The MXC temporary workspace resolves inside the configured scripts directory.");
            }
        }
    }

    private static List<string> BuildReadonlyPaths(
        ProcessStartInfo target,
        string resolvedTargetPath,
        ProcessExecutionContext context)
    {
        var paths = new HashSet<string>(BuildProtectedScriptPaths(context), StringComparer.OrdinalIgnoreCase);

        AddExistingPath(paths, AppContext.BaseDirectory);
        AddExistingPath(paths, Environment.SystemDirectory);

        if (Path.IsPathRooted(resolvedTargetPath))
        {
            AddExistingPath(paths, Directory.Exists(resolvedTargetPath)
                ? resolvedTargetPath
                : Path.GetDirectoryName(resolvedTargetPath));
        }

        if (!string.IsNullOrWhiteSpace(target.WorkingDirectory))
        {
            AddExistingPath(paths, target.WorkingDirectory);
        }

        foreach (var inputPath in context.InputPaths)
        {
            AddExistingPath(paths, inputPath);
        }

        // MXC's Windows process-container backend needs an explicit volume-root grant before a
        // narrower read-only path can remove inherited user-token write access on that volume.
        foreach (var path in paths.ToArray())
        {
            AddExistingPath(paths, Path.GetPathRoot(path));
        }

        return paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> BuildProtectedScriptPaths(ProcessExecutionContext context)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddExistingPath(paths, context.ScriptsRoot);
        AddExistingPath(paths, context.ScriptDirectory);
        return paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> BuildHostFilesystemReadwritePaths(string workspace)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in Environment.GetLogicalDrives())
        {
            AddExistingPath(paths, root);
        }

        AddExistingPath(paths, workspace);
        return paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> BuildSanitizedEnvironment(
        ProcessStartInfo target,
        string resolvedTargetPath,
        string workspace)
    {
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        CopyEnvironment(target, environment, "SystemRoot");
        CopyEnvironment(target, environment, "WINDIR");
        CopyEnvironment(target, environment, "ComSpec");
        CopyEnvironment(target, environment, "LOCALAPPDATA");
        CopyEnvironment(target, environment, "PATHEXT");
        CopyEnvironment(target, environment, ScriptExecutor.FilesEnvironmentVariable);

        environment["TEMP"] = workspace;
        environment["TMP"] = workspace;
        environment[WorkspaceEnvironmentVariable] = workspace;

        var pathDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Environment.SystemDirectory,
        };
        if (Path.IsPathRooted(resolvedTargetPath) && Path.GetDirectoryName(resolvedTargetPath) is { Length: > 0 } targetDirectory)
        {
            pathDirectories.Add(targetDirectory);
        }

        environment["PATH"] = string.Join(Path.PathSeparator, pathDirectories.Where(Directory.Exists));
        return environment
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => $"{entry.Key}={entry.Value}")
            .ToList();
    }

    private static void CopyEnvironment(
        ProcessStartInfo source,
        IDictionary<string, string> destination,
        string name)
    {
        if (source.Environment.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value))
        {
            destination[name] = value;
        }
    }

    private static void AddExistingPath(ISet<string> paths, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath) || Directory.Exists(fullPath))
        {
            paths.Add(fullPath);
        }
    }

    private static bool IsSameOrChild(string root, string candidate)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(candidate));
        return relative == "." ||
            (!Path.IsPathRooted(relative) &&
             !relative.Equals("..", StringComparison.Ordinal) &&
             !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }

    private static string? ResolveCandidate(string? candidate, string? pathEnvironment)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        candidate = Environment.ExpandEnvironmentVariables(candidate.Trim().Trim('"'));
        if (File.Exists(candidate))
        {
            return Path.GetFullPath(candidate);
        }

        return Path.GetDirectoryName(candidate) is null
            ? FindOnPath(candidate, pathEnvironment)
            : null;
    }

    private static string? ResolveCommandPath(string command)
    {
        if (File.Exists(command))
        {
            return Path.GetFullPath(command);
        }

        return Path.GetDirectoryName(command) is null
            ? FindOnPath(command, Environment.GetEnvironmentVariable("PATH"))
            : null;
    }

    private static string? FindOnPath(string fileName, string? pathEnvironment)
    {
        if (string.IsNullOrWhiteSpace(pathEnvironment))
        {
            return null;
        }

        var extensions = Path.HasExtension(fileName)
            ? new[] { string.Empty }
            : (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var directory in pathEnvironment.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var extension in extensions)
            {
                try
                {
                    var candidate = Path.Combine(directory.Trim('"'), fileName + extension);
                    if (File.Exists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    // Ignore malformed PATH entries.
                }
            }
        }

        return null;
    }
}
