// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PowerScripts.Core.Storage;

namespace PowerScripts.Core.Security;

/// <summary>Serializes Host processes around one reusable, daemon-owned WSLC container.</summary>
internal sealed class MxcLinuxSession : IDisposable
{
    private readonly string _directory;
    private readonly string _executor;
    private readonly IProcessLauncher _launcher;
    private readonly FileStream _lock;
    private SessionState? _state;

    internal static string DefaultDirectory => Path.Combine(
        SettingsStore.Current is FileSettingsStore files ? files.BaseDirectory : PowerScriptsPaths.ModuleDirectory, "linux-session");

    internal string DaemonDirectory => Path.Combine(_directory, "daemon");

    internal string RequestsDirectory => Path.Combine(_directory, "requests");

    internal string WorkspacesDirectory => Path.Combine(_directory, "workspaces");

    private string StatePath => Path.Combine(_directory, "session.json");

    internal MxcLinuxSession(string directory, string executor, IProcessLauncher launcher, bool maintenance = true, bool waitForLock = true)
    {
        _directory = Path.GetFullPath(directory);
        _executor = executor;
        _launcher = launcher;
        Directory.CreateDirectory(_directory);
        var timer = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                // A file lock also covers different interactive sessions for the same Windows user.
                _lock = new FileStream(Path.Combine(_directory, "session.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                break;
            }
            catch (IOException ex) when (waitForLock && timer.Elapsed < TimeSpan.FromSeconds(60) && (ex.HResult & 0xffff) is 32 or 33)
            {
                Thread.Sleep(50);
            }
        }

        try
        {
            _state = File.Exists(StatePath)
                ? JsonSerializer.Deserialize<SessionState>(File.ReadAllText(StatePath))
                    ?? throw new InvalidDataException("The shared Linux session state is empty.")
                : null;
            if (_state is not null && !IsSandboxId(_state.SandboxId))
            {
                throw new InvalidDataException("The saved shared Linux sandbox identifier is invalid.");
            }

            if (_state?.DaemonDirectory is { } daemonDirectory &&
                !string.Equals(daemonDirectory, DaemonDirectory, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The saved Linux daemon directory is not owned by this PowerScripts session.");
            }

            if (maintenance && _state?.Reusable != false)
            {
                MxcProcessRunner.DeleteStaleRunDirectories(RequestsDirectory, TimeSpan.Zero);
                MxcProcessRunner.DeleteStaleRunDirectories(WorkspacesDirectory);
            }
        }
        catch
        {
            _lock.Dispose();
            throw;
        }
    }

    internal ProcessRunResult EnsureStarted(string image, ProcessExecutionContext context, bool restrictNetwork)
    {
        var root = Path.GetFullPath(context.ScriptsRoot);
        MxcProcessRunner.EnsureWorkspaceOutsideScripts(context, _directory);
        EnsureInside(root, context.ScriptDirectory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException("The managed Linux script catalogue does not exist.");
        }

        var identity = string.Join("\n", new[]
        {
            image, root.ToUpperInvariant(), restrictNetwork.ToString(),
            RuntimeIdentity(_executor),
            RuntimeIdentity(Path.Combine(Path.GetDirectoryName(_executor)!, "wxc-wslc-daemon.exe")),
            RuntimeIdentity(Path.Combine(Path.GetDirectoryName(_executor)!, "wslcsdk.dll")),
        });
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        if (_state is not null && (_state.Key != key || !_state.Reusable || _state.DaemonDirectory is null))
        {
            var reset = Reset();
            if (reset.ExitCode != 0)
            {
                return reset;
            }
        }

        if (_state is not null)
        {
            // Start is not idempotent. Check the handle before user code, never by retrying its stdout.
            var check = Invoke(new
            {
                version = "0.8.0-alpha",
                phase = "exec",
                sandboxId = _state.SandboxId,
                process = new { commandLine = ":", timeout = 5000 },
            }, 10000);
            if (check.ExitCode == 0)
            {
                return check;
            }

            if (!HasError(check, "not_provisioned"))
            {
                var reset = Reset();
                if (reset.ExitCode != 0)
                {
                    var failure = Failure("liveness check", check);
                    return failure with { StdErr = failure.StdErr + "\n" + reset.StdErr };
                }
            }
            else
            {
                File.Delete(StatePath);
                _state = null;
            }
        }

        Directory.CreateDirectory(RequestsDirectory);
        Directory.CreateDirectory(WorkspacesDirectory);
        var provision = Invoke(new
        {
            version = "0.8.0-alpha",
            phase = "provision",
            containment = "wslc",
            filesystem = new
            {
                readonlyPaths = new[] { root, RequestsDirectory },
                readwritePaths = new[] { WorkspacesDirectory },
            },
            network = new { defaultPolicy = restrictNetwork ? "block" : "allow" },
            experimental = new { wslc = new { provision = new { image } } },
        }, 60000);
        if (provision.ExitCode != 0)
        {
            return Failure("provision", provision);
        }

        using var document = JsonDocument.Parse(provision.StdOut);
        var id = document.RootElement.GetProperty("result").GetProperty("sandboxId").GetString();
        if (!IsSandboxId(id))
        {
            throw new InvalidDataException("MXC returned an invalid shared Linux sandbox identifier.");
        }

        _state = new SessionState(key, id!, Reusable: false, image, restrictNetwork, DaemonDirectory);
        SaveState();
        var started = Phase("start");
        return started.ExitCode == 0 ? started : Failure("start", started);
    }

    internal ProcessRunResult Execute(string command, int timeoutMs)
    {
        _state = _state! with { Reusable = false };
        SaveState();
        var result = Invoke(new
        {
            version = "0.8.0-alpha",
            phase = "exec",
            sandboxId = _state.SandboxId,
            process = new { commandLine = command, timeout = timeoutMs },
        }, timeoutMs > 0 ? checked(timeoutMs + 5000) : 0);
        if (result.ExitCode == 0)
        {
            _state = _state with { Reusable = true };
            SaveState();
            return result;
        }

        var reset = Reset();
        return reset.ExitCode == 0 ? result : result with { StdErr = result.StdErr + "\n" + reset.StdErr };
    }

    internal ProcessRunResult Reset()
    {
        if (_state is null)
        {
            return new ProcessRunResult(0, string.Empty, string.Empty);
        }

        _state = _state with { Reusable = false };
        SaveState();
        var stop = Phase("stop");
        var delete = Phase("deprovision");
        if (delete.ExitCode != 0 && !HasError(delete, "not_provisioned"))
        {
            return Failure("deprovision", delete);
        }

        File.Delete(StatePath);
        _state = null;
        if (stop.ExitCode != 0 && !HasError(stop, "not_provisioned") && !HasError(stop, "not_started"))
        {
            return Failure("stop", stop);
        }

        return new ProcessRunResult(0, string.Empty, string.Empty);
    }

    internal ProcessRunResult? ProbeExisting(string image)
    {
        if (_state is null)
        {
            return null;
        }

        if (!_state.Reusable || _state.Image != image || _state.RestrictNetwork != true)
        {
            return new ProcessRunResult(2, string.Empty, MxcLinuxRecovery.RecoveryRequired);
        }

        var result = Invoke(new
        {
            version = "0.8.0-alpha",
            phase = "exec",
            sandboxId = _state.SandboxId,
            process = new { commandLine = MxcLinuxProcessRunner.ProbeCommand, timeout = 30000 },
        }, 35000);
        return result.ExitCode == 0 ? result : Failure("readiness check", result);
    }

    internal ProcessRunResult Recover()
    {
        if (_state is null)
        {
            return new ProcessRunResult(0, string.Empty, string.Empty);
        }

        // An admitted constant check completes on the SDK worker after any queued exec.
        // Busy, timeout and unrelated failures never authorize cleanup.
        var check = Invoke(new
        {
            version = "0.8.0-alpha",
            phase = "exec",
            sandboxId = _state.SandboxId,
            process = new { commandLine = ":", timeout = 5000 },
        }, 10000);
        if (check.ExitCode != 0 && !HasError(check, "not_provisioned") &&
            !HasError(check, "not_started") && !MxcLinuxRecovery.IsRpcUnavailable(check))
        {
            return Failure("recovery safety check", check);
        }

        var reset = Reset();
        if (reset.ExitCode == 0 || !MxcLinuxRecovery.IsRpcUnavailable(reset))
        {
            return reset;
        }

        if (_state?.DaemonDirectory is null)
        {
            return reset with { StdErr = MxcLinuxRecovery.LegacySharedRuntime + "\n" + reset.StdErr };
        }

        // Process termination can leave the SDK's image VHD attached. Keep the failed
        // session record and require a Windows restart rather than losing cleanup ownership.
        return reset with { StdErr = MxcLinuxRecovery.RestartWindowsRequired + "\n" + reset.StdErr };
    }

    public void Dispose() => _lock.Dispose();

    internal static void EnsureInside(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith("..\\", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The managed Linux script must remain inside its configured catalogue.");
        }
    }

    private static string RuntimeIdentity(string path)
    {
        var file = new FileInfo(path);
        return $"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}";
    }

    private static bool IsSandboxId(string? id) =>
        id is not null && id.Length == 37 && id.StartsWith("wslc:", StringComparison.Ordinal) && id[5..].All(char.IsAsciiHexDigit);

    private void SaveState()
    {
        var temporary = StatePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_state));
        File.Move(temporary, StatePath, overwrite: true);
    }

    private ProcessRunResult Phase(string phase) => Invoke(new
    {
        version = "0.8.0-alpha",
        phase,
        sandboxId = _state!.SandboxId,
    }, 60000);

    private ProcessRunResult Invoke(object configuration, int timeoutMs)
    {
        var path = Path.Combine(_directory, "phase.json");
        File.WriteAllText(path, JsonSerializer.Serialize(configuration), new UTF8Encoding(false));
        try
        {
            var start = new ProcessStartInfo(_executor)
            {
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            start.ArgumentList.Add("--experimental");
            start.ArgumentList.Add(path);
            // Live containers stay warm. Once released, promptly unlock the SDK image store for setup/checks.
            start.Environment["MXC_WSLC_DAEMON_IDLE_TIMEOUT_SECS"] = "1";
            start.Environment["MXC_WSLC_DAEMON_IDLE_POLL_SECS"] = "1";
            if (_state is null || _state.DaemonDirectory is not null)
            {
                start.Environment["MXC_WSLC_STATE_ROOT"] = DaemonDirectory;
            }

            return _launcher.Run(start, null, timeoutMs);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static bool HasError(ProcessRunResult result, string code)
    {
        try
        {
            using var document = JsonDocument.Parse(result.StdOut);
            return document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("code", out var value) && value.GetString() == code;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ProcessRunResult Failure(string phase, ProcessRunResult result) =>
        result with { StdOut = string.Empty, StdErr = $"Managed Linux {phase} failed: {result.StdErr}\n{result.StdOut}".TrimEnd() };

    private sealed record SessionState(
        string Key, string SandboxId, bool Reusable, string? Image = null,
        bool? RestrictNetwork = null, string? DaemonDirectory = null);
}
