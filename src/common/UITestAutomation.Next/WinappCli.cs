// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
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

        using var p = Process.Start(psi) ?? throw new InvalidOperationException(
            $"Failed to start winapp.exe ({ExecutablePath.Value}). {InstallHint}");

        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();
        p.WaitForExit();

        return new Result(
            p.ExitCode,
            stdoutTask.GetAwaiter().GetResult(),
            stderrTask.GetAwaiter().GetResult(),
            args);
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

    private static string ResolveExecutable()
    {
        if (TryResolveExecutable(out var path))
        {
            return path;
        }

        throw new InvalidOperationException(InstallHint);
    }
}
