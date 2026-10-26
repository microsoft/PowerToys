// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PowerScripts.Core.Execution;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Security;

/// <summary>Runs Python in an MXC-managed Linux image without changing the user's WSL distribution.</summary>
public static class MxcLinuxProcessRunner
{
    public const string Image = "python:3.12-alpine";

    public static string ImageStorePath => Path.Combine(PowerScriptsPaths.ModuleDirectory, "linux-container-images");

    /// <summary>Rejects unsupported requests before any script or executor is started.</summary>
    public static string? GetPolicyError(ProcessExecutionContext context, MxcEffectivePolicy policy)
    {
        if (!policy.Enabled)
        {
            return "Managed Linux containers require MXC. Require MXC in this script's isolation settings; no direct fallback is available.";
        }

        if (!policy.IsEnabled(MxcPolicies.Filesystem))
        {
            return "Managed Linux containers require restricted Windows file access. Enable the filesystem restriction for this script.";
        }

        var local = context.LocalMxc;
        if (policy.IsEnabled(MxcPolicies.Ui) || policy.IsEnabled(MxcPolicies.LeastPrivilege) ||
            !context.HasLocalMxcSettings || local is not { RiskAccepted: true } ||
            !local.DisabledPolicies.Contains(MxcPolicies.Ui, StringComparer.Ordinal) ||
            !local.DisabledPolicies.Contains(MxcPolicies.LeastPrivilege, StringComparer.Ordinal))
        {
            return "Blocked by isolation requirements: the managed Linux image cannot enforce the Windows UI and least-privilege policies and runs as guest root. " +
                "Review this script's MXC settings and explicitly accept those two unsupported-policy exceptions. Global exceptions alone are not sufficient.";
        }

        return null;
    }

    public static ProcessRunResult Run(
        string runnerPath,
        string scriptPath,
        string payload,
        PowerScriptDataFormat outputFormat,
        ProcessExecutionContext context,
        MxcEffectivePolicy policy,
        MxcSettings settings,
        int timeoutMs,
        IProcessLauncher? launcher = null,
        Func<string>? runDirectoryFactory = null)
    {
        var policyError = GetPolicyError(context, policy);
        if (policyError is not null)
        {
            return new ProcessRunResult(ProcessRunner.UnsupportedIsolationExitCode, string.Empty, policyError);
        }

        launcher ??= ProcessRunner.SecureLauncher;
        string? directory = null;
        var retainOutputs = false;
        try
        {
            directory = (runDirectoryFactory ?? MxcProcessRunner.CreateRunDirectory)();
            var workspace = Path.Combine(directory, "workspace");
            var requestDirectory = Path.Combine(directory, "request");
            MxcProcessRunner.EnsureWorkspaceOutsideScripts(context, workspace);
            Directory.CreateDirectory(workspace);
            Directory.CreateDirectory(requestDirectory);
            var inputPath = Path.Combine(requestDirectory, "input.json");
            File.WriteAllText(inputPath, payload, new UTF8Encoding(false));

            var readonlyPaths = new[] { context.ScriptDirectory, runnerPath, requestDirectory }
                .Concat(context.InputPaths)
                .Select(ValidateExistingPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            // The pinned WSLC executor ignores process.cwd, so set it explicitly in the shell.
            var command = $"cd {Quote(ToContainerPath(workspace))} && " +
                $"env -i PATH=/usr/local/bin:/usr/bin:/bin HOME=/tmp PYTHONDONTWRITEBYTECODE=1 PYTHONNOUSERSITE=1 PYTHONUNBUFFERED=1 " +
                $"TMPDIR={Quote(ToContainerPath(workspace))} " +
                $"{MxcProcessRunner.WorkspaceEnvironmentVariable}={Quote(ToContainerPath(workspace))} " +
                $"python3 -X utf8 {Quote(ToContainerPath(runnerPath))} {Quote(ToContainerPath(scriptPath))} < {Quote(ToContainerPath(inputPath))}";
            var config = BuildConfiguration(command, workspace, readonlyPaths, policy.IsEnabled(MxcPolicies.Network), timeoutMs);
            var result = Execute(config, directory, settings, launcher, timeoutMs);
            if (result.ExitCode != 0 || outputFormat != PowerScriptDataFormat.Files)
            {
                return result;
            }

            var output = PythonTransformResult.Parse(result.StdOut, outputFormat);
            output.FilePaths = output.FilePaths!.Select(path => MapOutputPath(path, workspace)).ToList();
            retainOutputs = output.FilePaths.Count > 0;
            return result with { StdOut = JsonSerializer.Serialize(output) };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or
            InvalidOperationException or ArgumentException or NotSupportedException or JsonException or OverflowException)
        {
            return new ProcessRunResult(2, string.Empty, $"Managed Linux execution failed: {ex.Message}");
        }
        finally
        {
            if (directory is not null)
            {
                Cleanup(directory, retainOutputs);
            }
        }
    }

    /// <summary>Checks a constant Python command with networking disabled; never pulls an image.</summary>
    public static ProcessRunResult Probe(MxcSettings? settings = null)
    {
        var directory = MxcProcessRunner.CreateRunDirectory();
        try
        {
            var workspace = Path.Combine(directory, "workspace");
            Directory.CreateDirectory(workspace);
            var config = BuildConfiguration(
                "python3 -I -c 'import platform; print(\"Python \" + platform.python_version() + \" (managed Linux container)\")'",
                workspace,
                Array.Empty<string>(),
                restrictNetwork: true,
                timeoutMs: 30000);
            return Execute(config, directory, settings ?? MxcSettings.Load(), ProcessRunner.SecureLauncher, 45000);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        {
            return new ProcessRunResult(2, string.Empty, ex.Message);
        }
        finally
        {
            Cleanup(directory, retainOutputs: false);
        }
    }

    /// <summary>Explicit user-initiated image preparation, outside any script's network policy.</summary>
    public static ProcessRunResult Setup(MxcSettings? settings = null)
    {
        var executor = MxcProcessRunner.ResolveExecutor(settings ?? MxcSettings.Load());
        if (executor is null || !File.Exists(Path.Combine(Path.GetDirectoryName(executor)!, "wslcsdk.dll")))
        {
            return MissingRuntime();
        }

        var start = NewExecutor(executor);
        foreach (var argument in new[] { "--setup-wslc", "--image", Image, "--storage-path", ImageStorePath })
        {
            start.ArgumentList.Add(argument);
        }

        return ProcessRunner.SecureLauncher.Run(start, null, 600000);
    }

    internal static string BuildConfiguration(
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
            experimental = new { wslc = new { image = Image, storagePath = ImageStorePath } },
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

    internal static string MapOutputPath(string path, string workspace)
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

        var current = workspace;
        foreach (var part in relative.Split(Path.DirectorySeparatorChar))
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

    private static ProcessRunResult Execute(
        string config,
        string directory,
        MxcSettings settings,
        IProcessLauncher launcher,
        int timeoutMs)
    {
        var executor = MxcProcessRunner.ResolveExecutor(settings);
        if (executor is null || !File.Exists(Path.Combine(Path.GetDirectoryName(executor)!, "wslcsdk.dll")))
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

    private static ProcessRunResult MissingRuntime() => new(
        MxcProcessRunner.UnavailableExitCode,
        string.Empty,
        "The bundled MXC Linux runtime or wslcsdk.dll is missing. Repair PowerToys or rebuild PowerScripts.Host.");

    private static string Quote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static void Cleanup(string directory, bool retainOutputs)
    {
        try
        {
            if (retainOutputs)
            {
                Directory.Delete(Path.Combine(directory, "request"), recursive: true);
                File.Delete(Path.Combine(directory, "mxc-config.json"));
            }
            else
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"PowerScripts could not clean up the managed Linux run directory '{directory}': {ex.Message}");
        }
    }
}
