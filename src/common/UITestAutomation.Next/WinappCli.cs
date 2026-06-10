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
    private static readonly Lazy<string> ExecutablePath = new(ResolveExecutable);

    public sealed record Result(int ExitCode, string StdOut, string StdErr)
    {
        public bool Success => ExitCode == 0;

        public JsonDocument ParseJson()
        {
            try
            {
                return JsonDocument.Parse(StdOut);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"winappcli stdout was not valid JSON (exit {ExitCode}). stdout={StdOut.Trim()} stderr={StdErr.Trim()}",
                    ex);
            }
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
            $"Failed to start winapp.exe ({ExecutablePath.Value})");

        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();
        p.WaitForExit();

        return new Result(p.ExitCode, stdoutTask.GetAwaiter().GetResult(), stderrTask.GetAwaiter().GetResult());
    }

    /// <summary>Run and throw if the exit code is non-zero. Use for fire-and-forget commands.</summary>
    public static Result InvokeAssertSuccess(params string[] args)
    {
        var r = Invoke(args);
        Assert.AreEqual(0, r.ExitCode, $"winappcli failed: winapp {string.Join(' ', args)}\nstdout: {r.StdOut}\nstderr: {r.StdErr}");
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
                Assert.Fail($"winappcli {string.Join(' ', args)} exited with {r.ExitCode} and non-JSON stdout: {r.StdOut.Trim()} stderr: {r.StdErr.Trim()}");
                return default;
            }
        }

        using var ok = JsonDocument.Parse(r.StdOut);
        return ok.RootElement.Clone();
    }

    private static string ResolveExecutable()
    {
        // 1) Explicit override (CI / dev convenience).
        var env = Environment.GetEnvironmentVariable("WINAPP_CLI_PATH");
        if (!string.IsNullOrEmpty(env) && File.Exists(env))
        {
            return env;
        }

        // 2) Standard winget install location.
        var winget = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "WindowsApps",
            "winapp.exe");
        if (File.Exists(winget))
        {
            return winget;
        }

        // 3) Anything on PATH.
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator))
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
                    return candidate;
                }
            }
            catch
            {
            }
        }

        Assert.Fail(
            "winapp.exe not found. Install once with: winget install Microsoft.winappcli " +
            "or set WINAPP_CLI_PATH to its full path.");
        return string.Empty;
    }
}
