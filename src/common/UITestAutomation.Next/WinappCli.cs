// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Thin wrapper around the winappcli executable. Every public method shells out to
/// <c>winapp.exe</c>, captures stdout/stderr/exit-code, and (where requested) parses the
/// <c>--json</c> envelope using <see cref="JsonDocument"/>.
/// </summary>
/// <remarks>
/// <para>
/// Engine prerequisites: install once with <c>winget install Microsoft.winappcli</c>. The CLI
/// lands on PATH at <c>%LOCALAPPDATA%\Microsoft\WindowsApps\winapp.exe</c>.
/// </para>
/// <para>
/// All invocations set <c>WINAPP_CLI_TELEMETRY_OPTOUT=1</c> and disable update checks via
/// <c>WINAPP_CLI_UPDATE_CHECK=0</c> so the CLI never injects extra lines into stdout.
/// </para>
/// </remarks>
public static class WinappCli
{
    /// <summary>Stable hint surfaced when the CLI is missing or fails — used in all error paths.</summary>
    public const string InstallHint =
        "winapp.exe not found. Install once with: winget install Microsoft.winappcli " +
        "(or set the WINAPP_CLI_PATH environment variable to its full path).";

    private static readonly Lazy<string> ExecutablePath = new(ResolveExecutable);

    /// <summary>
    /// Per-invocation guard. A hung <c>winapp.exe</c> call must fail fast and name the offending
    /// command instead of blocking until the suite's outer timeout fires (which buries the cause).
    /// Commands that pass a longer <c>-t</c> wait extend this; see <see cref="ResolveInvokeTimeout"/>.
    /// </summary>
    private static readonly TimeSpan DefaultInvokeTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Serializes winapp.exe invocations. Two CLI UIA clients querying the same target at once can hang
    /// each other (worst against the live Measure Tool overlay), and the stray-process guard in
    /// <see cref="Invoke"/> must never race a legitimate in-flight call — so invocations run one at a time.
    /// </summary>
    private static readonly object InvokeGate = new();

    public sealed record Result(int ExitCode, string StdOut, string StdErr, IReadOnlyList<string> Args)
    {
        public bool Success => ExitCode == 0;

        /// <summary>
        /// One-line, assertion-friendly description of a failed invocation. Format:
        /// <c>"winapp ui invoke X -w 12345 -> exit 1; stderr: not found"</c>. Falls back to
        /// stdout if stderr is empty.
        /// </summary>
        public string DescribeFailure()
        {
            var sb = new StringBuilder();
            sb.Append("winapp ");
            sb.AppendJoin(' ', Args);
            sb.Append(" -> exit ").Append(ExitCode);
            if (!string.IsNullOrWhiteSpace(StdErr))
            {
                sb.Append("; stderr: ").Append(StdErr.Trim());
            }
            else if (!string.IsNullOrWhiteSpace(StdOut))
            {
                sb.Append("; stdout: ").Append(StdOut.Trim());
            }

            return sb.ToString();
        }

        public JsonDocument ParseJson()
        {
            try
            {
                return JsonDocument.Parse(StdOut);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"winappcli stdout was not valid JSON. {DescribeFailure()}",
                    ex);
            }
        }
    }

    /// <summary>
    /// Returns true when <c>winapp.exe</c> resolves to a real file AND responds to
    /// <c>--version</c>. Use from <c>[ClassInitialize]</c> / <c>[AssemblyInitialize]</c> /
    /// <see cref="UITestBase"/> to fail the entire suite once with a clear install hint,
    /// instead of letting every test produce its own opaque process-launch failure.
    /// </summary>
    public static bool IsAvailable()
    {
        if (!TryResolveExecutable(out _))
        {
            return false;
        }

        try
        {
            return Invoke("--version").Success;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Run <c>winapp.exe</c> with the given arguments. Returns exit code and captured streams.</summary>
    public static Result Invoke(params string[] args)
    {
        // Serialize invocations so two winapp.exe never run at once — and so the stray-process guard in
        // InvokeLocked can't race a legitimate in-flight call.
        lock (InvokeGate)
        {
            return InvokeLocked(args);
        }
    }

    private static Result InvokeLocked(string[] args)
    {
        // Before spinning up a new winapp.exe, kill any stray one left behind by a previous
        // timed-out/killed call: a second UIA client against the same target (e.g. the live Measure
        // Tool overlay) can wedge the new call. Serialized, so anything alive here is a leftover.
        KillStrayWinappProcesses(args);

        var psi = new ProcessStartInfo
        {
            FileName = ExecutablePath.Value,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // Suppress telemetry banner and update-check notice so --json output stays clean.
        psi.Environment["WINAPP_CLI_TELEMETRY_OPTOUT"] = "1";
        psi.Environment["WINAPP_CLI_UPDATE_CHECK"] = "0";

        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        var overall = Stopwatch.StartNew();
        using var p = StartWinappProcess(psi);

        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();

        var timeout = ResolveInvokeTimeout(args);
        if (!p.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try
            {
                Console.WriteLine($"[winappcli] killing hung call after {timeout.TotalSeconds:0}s: winapp {string.Join(' ', args)}");
                p.Kill(entireProcessTree: true);
                p.WaitForExit(5000); // make sure the tree is actually gone before returning
            }
            catch
            {
                // Raced with a natural exit between the wait timing out and the kill — nothing to do.
            }

            throw new TimeoutException(
                $"winapp {string.Join(' ', args)} did not exit within {timeout.TotalSeconds:0}s and was killed.");
        }

        // winapp.exe itself has now exited; capture how long that took.
        var processMs = overall.ElapsedMilliseconds;

        // Bound the wait for the async stdout/stderr readers. The output is already buffered, but a
        // child that inherited the redirected pipe keeps the handle open so the readers (and a
        // parameterless WaitForExit) never see EOF. After a short grace, clear strays — invocations are
        // serialized, so any winapp.exe alive now is that child — which closes the pipe so the reads
        // finish with the full, already-captured output.
        if (!Task.WhenAll(stdoutTask, stderrTask).Wait(TimeSpan.FromSeconds(2)))
        {
            Console.WriteLine(
                "[winappcli] output stalled after winapp.exe exit (lingering child held the pipe); clearing strays for: " +
                $"winapp {string.Join(' ', args)}");
            KillStrayWinappProcesses(args);
            Task.WhenAll(stdoutTask, stderrTask).Wait(TimeSpan.FromSeconds(3));
        }

        // Surface where slow calls actually spend their time — winapp.exe runtime vs waiting for the
        // output streams to drain after it exited — so a slow timestamp can be attributed correctly.
        var totalMs = overall.ElapsedMilliseconds;
        if (totalMs > 2000)
        {
            Console.WriteLine(
                $"[winappcli] slow call {totalMs}ms (winapp.exe ran {processMs}ms, output drain {totalMs - processMs}ms): " +
                $"winapp {string.Join(' ', args)}");
        }

        return new Result(
            p.ExitCode,
            stdoutTask.IsCompletedSuccessfully ? stdoutTask.Result : string.Empty,
            stderrTask.IsCompletedSuccessfully ? stderrTask.Result : string.Empty,
            args);
    }

    /// <summary>
    /// Kill any <c>winapp.exe</c> still running before a new invocation (or when output stalls). A stray
    /// instance is a leftover from a previous call that timed out / didn't fully exit; a second UIA
    /// client against the same target can wedge a call, so we clear them and log each kill. Best-effort
    /// and bounded — never throws.
    /// </summary>
    private static void KillStrayWinappProcesses(string[] args)
    {
        Process[] strays;
        try
        {
            strays = Process.GetProcessesByName("winapp");
        }
        catch
        {
            return;
        }

        foreach (var stray in strays)
        {
            try
            {
                Console.WriteLine(
                    $"[winappcli] found a stray winapp.exe (pid {stray.Id}) still running; killing it before: " +
                    $"winapp {string.Join(' ', args)}");
                stray.Kill(entireProcessTree: true);
                stray.WaitForExit(5000);
            }
            catch
            {
                // The stray may have exited on its own between enumeration and kill — fine.
            }
            finally
            {
                stray.Dispose();
            }
        }
    }

    /// <summary>
    /// Process-guard budget for one invocation. Defaults to <see cref="DefaultInvokeTimeout"/>; when the
    /// command carries its own <c>-t</c>/<c>--timeout</c> wait in milliseconds (e.g. <c>wait-for</c>), the
    /// guard is extended past that wait plus a grace margin so a legitimate long wait isn't killed early.
    /// </summary>
    private static TimeSpan ResolveInvokeTimeout(string[] args)
    {
        var budget = DefaultInvokeTimeout;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if ((string.Equals(args[i], "-t", StringComparison.Ordinal) ||
                 string.Equals(args[i], "--timeout", StringComparison.Ordinal)) &&
                int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms) &&
                ms > 0)
            {
                var withGrace = TimeSpan.FromMilliseconds(ms) + TimeSpan.FromSeconds(30);
                if (withGrace > budget)
                {
                    budget = withGrace;
                }
            }
        }

        return budget;
    }

    /// <summary>Run and throw if the exit code is non-zero. Use for fire-and-forget commands.</summary>
    public static Result InvokeAssertSuccess(params string[] args)
    {
        var r = Invoke(args);
        Assert.AreEqual(0, r.ExitCode, r.DescribeFailure());
        return r;
    }

    /// <summary>Run a <c>--json</c> command and return the parsed root <see cref="JsonElement"/>.</summary>
    public static JsonElement InvokeJson(params string[] args)
    {
        var r = Invoke(args);
        if (!r.Success)
        {
            // Many --json commands (search, wait-for) return exit 1 with a valid envelope on
            // "no match" / "timed out". Still parse so the caller can branch on envelope fields.
            try
            {
                using var doc = JsonDocument.Parse(r.StdOut);
                return doc.RootElement.Clone();
            }
            catch
            {
                Assert.Fail($"{r.DescribeFailure()} (stdout was not JSON)");
                return default;
            }
        }

        using var ok = JsonDocument.Parse(r.StdOut);
        return ok.RootElement.Clone();
    }

    /// <summary>
    /// Locate <c>winapp.exe</c> without throwing or asserting. <see cref="IsAvailable"/> uses
    /// this to probe quietly; the lazy <see cref="ResolveExecutable"/> wraps it for the
    /// first real call.
    /// </summary>
    public static bool TryResolveExecutable(out string path)
    {
        // 1) Explicit override (CI / dev convenience).
        var env = Environment.GetEnvironmentVariable("WINAPP_CLI_PATH");
        if (!string.IsNullOrEmpty(env) && File.Exists(env))
        {
            path = env;
            return true;
        }

        // 2) Standard winget install location.
        var winget = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "WindowsApps",
            "winapp.exe");
        if (File.Exists(winget))
        {
            path = winget;
            return true;
        }

        // 3) Anything on PATH.
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                continue;
            }

            try
            {
                var candidate = Path.Combine(dir, "winapp.exe");
                if (File.Exists(candidate))
                {
                    path = candidate;
                    return true;
                }
            }
            catch
            {
            }
        }

        path = string.Empty;
        return false;
    }

    /// <summary>
    /// Start <c>winapp.exe</c>, retrying the transient launch failure that affects Windows App
    /// Execution Aliases. The <c>winapp.exe</c> found on PATH is the reparse-point stub under
    /// <c>%LOCALAPPDATA%\Microsoft\WindowsApps</c>; launching an alias through <c>CreateProcess</c>
    /// (<c>UseShellExecute = false</c>) intermittently throws <see cref="Win32Exception"/> with
    /// <c>ERROR_INVALID_PARAMETER</c> (87, "The parameter is incorrect") before the alias resolves.
    /// The launch is atomic — nothing ran — so retrying with a short backoff is safe and
    /// idempotent. Other Win32 errors (missing file, access denied) propagate immediately so a
    /// genuine misconfiguration still fails fast.
    /// </summary>
    private static Process StartWinappProcess(ProcessStartInfo psi)
    {
        const int maxAttempts = 4;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return Process.Start(psi) ?? throw new InvalidOperationException(
                    $"Failed to start winapp.exe ({psi.FileName}). {InstallHint}");
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 87 && attempt < maxAttempts)
            {
                // App Execution Alias not resolved yet — back off briefly and retry.
                Thread.Sleep(100 * attempt);
            }
        }
    }

    private static string ResolveExecutable()
    {
        if (TryResolveExecutable(out var path))
        {
            return path;
        }

        throw new InvalidOperationException(InstallHint);
    }
}
