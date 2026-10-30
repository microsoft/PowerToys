// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PowerScripts.Core.Execution;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Security;

/// <summary>Runs Python in an MXC-managed Linux image without changing the user's WSL distribution.</summary>
public static class MxcLinuxProcessRunner
{
    // The pinned state-aware daemon uses MXC's default image store.
    public static string ImageStorePath => Path.Combine(Path.GetTempPath(), "mxc-wslc-sessions");

    /// <summary>Rejects unsupported requests before any script or executor is started.</summary>
    public static string? GetPolicyError(MxcEffectivePolicy policy)
    {
        if (!policy.Enabled)
        {
            return "Managed Linux containers require global MXC isolation; no direct fallback is available.";
        }

        if (!policy.IsEnabled(MxcPolicies.Filesystem))
        {
            return "Managed Linux containers require the global filesystem restriction.";
        }

        if (policy.IsEnabled(MxcPolicies.Ui) || policy.IsEnabled(MxcPolicies.LeastPrivilege))
        {
            return "Blocked by isolation requirements: the managed Linux image cannot enforce the Windows UI and least-privilege policies and runs as guest root. " +
                "Review the global MXC settings and explicitly accept those two unsupported-policy exceptions.";
        }

        return null;
    }

    public static ProcessRunResult Run(
        string scriptPath,
        string payload,
        PowerScriptDataFormat outputFormat,
        ProcessExecutionContext context,
        MxcEffectivePolicy policy,
        MxcSettings settings,
        string image,
        int timeoutMs,
        IProcessLauncher? launcher = null,
        string? sessionDirectory = null)
    {
        var policyError = GetPolicyError(policy);
        if (policyError is not null)
        {
            return new ProcessRunResult(ProcessRunner.UnsupportedIsolationExitCode, string.Empty, policyError);
        }

        if (GetImageError(image) is { } imageError)
        {
            return new ProcessRunResult(2, string.Empty, imageError);
        }

        launcher ??= ProcessRunner.SecureLauncher;
        var executor = ResolveRuntime(settings);
        if (executor is null)
        {
            return MissingRuntime();
        }

        string? directory = null;
        string? requestDirectory = null;
        MxcLinuxSession? session = null;
        var retainOutputs = false;
        try
        {
            session = new MxcLinuxSession(sessionDirectory ?? MxcLinuxSession.DefaultDirectory, executor, launcher);
            MxcLinuxSession.EnsureInside(context.ScriptsRoot, scriptPath);
            _ = ValidateExistingPath(scriptPath);
            var id = Guid.NewGuid().ToString("N");
            directory = Path.Combine(session.WorkspacesDirectory, "mxc-" + id);
            var workspace = Path.Combine(directory, "workspace");
            requestDirectory = Path.Combine(session.RequestsDirectory, "mxc-" + id);
            MxcProcessRunner.EnsureWorkspaceOutsideScripts(context, workspace);
            Directory.CreateDirectory(workspace);
            Directory.CreateDirectory(requestDirectory);
            var inputPath = Path.Combine(requestDirectory, "input.json");
            File.WriteAllText(inputPath, StageInputs(payload, context, requestDirectory), new UTF8Encoding(false));

            var ready = session.EnsureStarted(image, context, policy.IsEnabled(MxcPolicies.Network));
            if (ready.ExitCode != 0)
            {
                return ready;
            }
            // The pinned WSLC executor ignores process.cwd, so set it explicitly in the shell.
            var command = $"cd {Quote(ToContainerPath(workspace))} && " +
                $"env -i PATH=/usr/local/bin:/usr/bin:/bin HOME=/tmp PYTHONDONTWRITEBYTECODE=1 PYTHONNOUSERSITE=1 PYTHONUNBUFFERED=1 " +
                $"TMPDIR={Quote(ToContainerPath(workspace))} " +
                $"{MxcProcessRunner.WorkspaceEnvironmentVariable}={Quote(ToContainerPath(workspace))} " +
                $"python3 -X utf8 {Quote(ToContainerPath(scriptPath))} < {Quote(ToContainerPath(inputPath))}";
            var result = session.Execute(command, timeoutMs);
            if (result.ExitCode != 0 || outputFormat != PowerScriptDataFormat.Files)
            {
                return result;
            }

            var output = PythonTransformResult.Parse(result.StdOut, outputFormat);
            output.FilePaths = output.FilePaths!.Select(path => MapOutputPath(path, workspace, session.WorkspacesDirectory)).ToList();
            retainOutputs = output.FilePaths.Count > 0;
            return result with { StdOut = JsonSerializer.Serialize(new { files = output.FilePaths }) };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or
            InvalidOperationException or ArgumentException or NotSupportedException or JsonException or OverflowException or KeyNotFoundException)
        {
            return new ProcessRunResult(2, string.Empty, $"Managed Linux execution failed: {ex.Message}");
        }
        finally
        {
            try
            {
                if (requestDirectory is not null && Directory.Exists(requestDirectory))
                {
                    Cleanup(requestDirectory);
                }

                if (directory is not null && !retainOutputs && Directory.Exists(directory))
                {
                    Cleanup(directory);
                }
            }
            finally
            {
                session?.Dispose();
            }
        }
    }

    /// <summary>Checks a constant Python command with networking disabled; never pulls an image.</summary>
    public static ProcessRunResult Probe(
        string image,
        MxcSettings? settings = null,
        IProcessLauncher? launcher = null,
        Func<string>? runDirectoryFactory = null,
        string? sessionDirectory = null)
    {
        if (GetImageError(image) is { } imageError)
        {
            return new ProcessRunResult(2, string.Empty, imageError);
        }

        settings ??= MxcSettings.Load();
        launcher ??= ProcessRunner.SecureLauncher;
        var directory = (runDirectoryFactory ?? MxcProcessRunner.CreateRunDirectory)();
        try
        {
            var workspace = Path.Combine(directory, "workspace");
            Directory.CreateDirectory(workspace);
            var config = BuildConfiguration(
                image,
                "python3 -I -c 'import platform; print(\"Python \" + platform.python_version() + \" (managed Linux container)\")'",
                workspace,
                Array.Empty<string>(),
                restrictNetwork: true,
                timeoutMs: 30000);
            return RunWithFreshSession(settings, launcher, sessionDirectory, () => Execute(config, directory, settings, launcher, 45000));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        {
            return new ProcessRunResult(2, string.Empty, ex.Message);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>Explicit user-initiated image preparation, outside any script's network policy.</summary>
    public static ProcessRunResult Setup(string image, MxcSettings? settings = null, IProcessLauncher? launcher = null, string? sessionDirectory = null)
    {
        if (GetImageError(image) is { } imageError)
        {
            return new ProcessRunResult(2, string.Empty, imageError);
        }

        settings ??= MxcSettings.Load();
        launcher ??= ProcessRunner.SecureLauncher;
        var executor = ResolveRuntime(settings);
        if (executor is null)
        {
            return MissingRuntime();
        }

        var start = NewExecutor(executor);
        foreach (var argument in new[] { "--setup-wslc", "--image", image, "--storage-path", ImageStorePath })
        {
            start.ArgumentList.Add(argument);
        }

        return RunWithFreshSession(settings, launcher, sessionDirectory, () => launcher.Run(start, null, 600000));
    }

    private static string? GetImageError(string image) =>
        string.IsNullOrWhiteSpace(image) || image.StartsWith('-') || image.Any(character => char.IsWhiteSpace(character) || char.IsControl(character))
            ? "A managed Linux container image reference is required, without whitespace or command-line options."
            : null;

    internal static string BuildConfiguration(
        string image,
        string command,
        string workspace,
        IEnumerable<string> readonlyPaths,
        bool restrictNetwork,
        int timeoutMs) =>
        JsonSerializer.Serialize(new
        {
            version = "0.8.0-alpha",
            containerId = "powerscripts-linux-" + Guid.NewGuid().ToString("N"),
            containment = "wslc",
            process = new { commandLine = command, cwd = ToContainerPath(workspace), timeout = timeoutMs },
            filesystem = new { readonlyPaths, readwritePaths = new[] { Path.GetFullPath(workspace) } },
            network = new { defaultPolicy = restrictNetwork ? "block" : "allow" },
            lifecycle = new { destroyOnExit = true, preservePolicy = false },
            experimental = new { wslc = new { image, storagePath = ImageStorePath } },
        });

    internal static string ToContainerPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (fullPath.Length < 3 || !char.IsAsciiLetter(fullPath[0]) || fullPath[1] != ':' || fullPath[2] != '\\')
        {
            throw new NotSupportedException("Managed Linux requires local Windows drive paths; UNC and device paths are not supported.");
        }

        return PythonRuntime.ToWslPath(fullPath)!;
    }

    internal static string MapOutputPath(string path, string workspace, string? workspaceRoot = null)
    {
        if (!path.StartsWith("/mnt/", StringComparison.Ordinal) ||
            path.Length < 7 || !char.IsAsciiLetter(path[5]) || path[6] != '/')
        {
            throw new InvalidDataException("Managed Linux file outputs must be created in the writable workspace (TMPDIR), not the container's temporary filesystem.");
        }

        var windowsPath = Path.GetFullPath(PythonRuntime.FromWslPath(path)!);
        var relative = Path.GetRelativePath(workspace, windowsPath);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith("..\\", StringComparison.Ordinal) || relative == ".")
        {
            throw new InvalidDataException("Managed Linux file outputs must remain inside the writable workspace.");
        }

        workspaceRoot ??= workspace;
        MxcLinuxSession.EnsureInside(workspaceRoot, workspace);
        var current = workspaceRoot;
        if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Managed Linux workspaces cannot be symbolic links or other reparse points.");
        }

        foreach (var part in Path.GetRelativePath(workspaceRoot, windowsPath).Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, part);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Managed Linux file outputs cannot contain symbolic links or other reparse points.");
            }
        }

        if (!File.Exists(windowsPath))
        {
            throw new InvalidDataException("The managed Linux output is not an existing Windows-accessible file.");
        }

        return windowsPath;
    }

    private static string ValidateExistingPath(string path)
    {
        _ = ToContainerPath(path);
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            throw new FileNotFoundException("A managed Linux input or script path does not exist.", fullPath);
        }

        return fullPath;
    }

    private static string StageInputs(string payload, ProcessExecutionContext context, string directory)
    {
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in context.InputPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var source = ValidateExistingPath(path);
            if (!File.Exists(source))
            {
                throw new InvalidDataException("Managed Linux input paths must be files, not directories.");
            }

            var inputDirectory = Path.Combine(directory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(inputDirectory);
            var target = Path.Combine(inputDirectory, Path.GetFileName(source));
            File.Copy(source, target);
            mappings.Add(ToContainerPath(source), ToContainerPath(target));
        }

        var request = JsonNode.Parse(payload)?.AsObject()
            ?? throw new InvalidDataException("Managed Linux input must be an object.");
        if (request["files"] is JsonArray files)
        {
            for (var index = 0; index < files.Count; index++)
            {
                files[index] = mappings.TryGetValue(files[index]!.GetValue<string>(), out var path)
                    ? path
                    : throw new InvalidDataException("A managed Linux input file was not authorized for this invocation.");
            }
        }

        if (request["parameters"] is JsonObject parameters)
        {
            foreach (var name in context.FileParameterNames)
            {
                if (parameters[name] is JsonValue value)
                {
                    parameters[name] = mappings.TryGetValue(value.GetValue<string>(), out var path)
                        ? path
                        : throw new InvalidDataException("A managed Linux file parameter was not authorized for this invocation.");
                }
            }
        }

        return request.ToJsonString();
    }

    private static ProcessRunResult Execute(
        string config,
        string directory,
        MxcSettings settings,
        IProcessLauncher launcher,
        int timeoutMs)
    {
        var executor = ResolveRuntime(settings);
        if (executor is null)
        {
            return MissingRuntime();
        }

        var configPath = Path.Combine(directory, "mxc-config.json");
        File.WriteAllText(configPath, config, new UTF8Encoding(false));
        var start = NewExecutor(executor);
        start.ArgumentList.Add("--experimental");
        start.ArgumentList.Add(configPath);
        return launcher.Run(start, null, checked(timeoutMs + 5000));
    }

    private static ProcessStartInfo NewExecutor(string executor) => new(executor)
    {
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8,
    };

    private static string? ResolveRuntime(MxcSettings settings)
    {
        var executor = MxcProcessRunner.ResolveExecutor(settings);
        return executor is not null &&
            File.Exists(Path.Combine(Path.GetDirectoryName(executor)!, "wslcsdk.dll")) &&
            File.Exists(Path.Combine(Path.GetDirectoryName(executor)!, "wxc-wslc-daemon.exe"))
                ? executor
                : null;
    }

    private static ProcessRunResult MissingRuntime() => new(
        MxcProcessRunner.UnavailableExitCode,
        string.Empty,
        "The bundled MXC Linux runtime, wxc-wslc-daemon.exe, or wslcsdk.dll is missing. Repair PowerToys or rebuild PowerScripts.Host.");

    private static string Quote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static ProcessRunResult RunWithFreshSession(MxcSettings settings, IProcessLauncher launcher, string? directory, Func<ProcessRunResult> action)
    {
        directory ??= MxcLinuxSession.DefaultDirectory;
        try
        {
            var executor = MxcProcessRunner.ResolveExecutor(settings);
            if (executor is null)
            {
                return MissingRuntime();
            }

            using var session = new MxcLinuxSession(directory, executor, launcher);
            var reset = session.Reset();
            if (reset.ExitCode != 0)
            {
                return reset;
            }

            var wait = Stopwatch.StartNew();
            while (true)
            {
                var result = action();
                if (result.ExitCode == 0 ||
                    (!result.StdErr.Contains("0x80070020", StringComparison.OrdinalIgnoreCase) &&
                     !result.StdOut.Contains("0x80070020", StringComparison.OrdinalIgnoreCase)))
                {
                    return result;
                }

                if (wait.Elapsed >= TimeSpan.FromSeconds(10))
                {
                    return result with { StdErr = result.StdErr + "\nThe Linux image cache is still in use. Close other MXC Linux sessions and retry the image operation." };
                }

                // Only idempotent image setup / constant probes may wait for SDK teardown, never scripts.
                Thread.Sleep(250);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException or JsonException)
        {
            return new ProcessRunResult(2, string.Empty, $"Could not reset the shared managed Linux container: {ex.Message}");
        }
    }

    private static void Cleanup(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"PowerScripts could not clean up the managed Linux run directory '{directory}': {ex.Message}");
        }
    }
}
