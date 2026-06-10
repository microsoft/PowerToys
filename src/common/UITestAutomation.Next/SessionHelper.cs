// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Owns process launch + window resolution for a <see cref="PowerToysModule"/>. Equivalent to
/// the old <c>SessionHelper</c> but the engine is winappcli — no WinAppDriver, no Appium.
/// </summary>
/// <remarks>
/// <para>
/// Two consumption shapes:
/// <list type="bullet">
///   <item><description>Per-test (HWND-scoped): construct + call <see cref="Init"/>. <see cref="UITestBase"/>
///   does this in <c>[TestInitialize]</c>.</description></item>
///   <item><description>Class-scoped or process-scoped: the static helpers (<see cref="EnsureRunning"/>,
///   <see cref="IsRunning"/>, <see cref="GetProcessName"/>) let a smoke-test <c>[ClassInitialize]</c>
///   reuse the launch+wait flow without taking on a HWND binding.</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class SessionHelper
{
    private readonly PowerToysModule scope;

    public SessionHelper(PowerToysModule scope)
    {
        this.scope = scope;
    }

    public Session Init()
    {
        EnsureRunning(scope, TimeSpan.FromSeconds(20));

        var window = WaitForMainWindow(scope, TimeSpan.FromSeconds(20));
        Assert.IsNotNull(window, $"Main window for {scope} did not appear via winappcli within 20s");
        return window!;
    }

    /// <summary>Process name as winappcli's <c>-a</c> flag (and <see cref="Process.GetProcessesByName(string)"/>) accept it.</summary>
    public static string GetProcessName(PowerToysModule scope) => ModulePaths.ProcessNameFor(scope);

    /// <summary>Returns <c>true</c> if at least one process matching <paramref name="scope"/> is running.</summary>
    public static bool IsRunning(PowerToysModule scope) =>
        Process.GetProcessesByName(GetProcessName(scope)).Length > 0;

    /// <summary>
    /// Ensure the module's process is running and has presented a UIA-visible window. If the
    /// module is already running, this returns <c>false</c> without launching anything. If a
    /// launch was needed, returns <c>true</c> — callers track this so cleanup only kills what
    /// the test itself started.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>UseShellExecute = true</c> is intentional: with <c>UseShellExecute = false</c> the
    /// spawned process inherits this test-host's stdin/stdout/stderr handles, and the
    /// Microsoft.Testing.Platform / MSTest runner won't declare the test run complete until
    /// those pipes drain — which never happens until the target exits. Going through
    /// ShellExecute gives the child its own console and detaches the handles.
    /// </para>
    /// <para>
    /// PowerToys modules with single-instance gates (Settings, ColorPicker) often hand off to an
    /// existing instance and let the launcher PID exit with code 0 immediately. The launcher
    /// PID is therefore intentionally discarded; readiness is judged purely by whether a UIA
    /// window owned by the target process becomes visible.
    /// </para>
    /// </remarks>
    public static bool EnsureRunning(PowerToysModule scope, TimeSpan timeout)
    {
        var processName = GetProcessName(scope);

        if (IsRunning(scope))
        {
            WaitForAnyWindow(processName, timeout);
            return false;
        }

        var exe = ModulePaths.ExePathFor(scope);
        Assert.IsTrue(File.Exists(exe), $"Module exe not found: {exe}");

        try
        {
            using (Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = Path.GetDirectoryName(exe)!,
                UseShellExecute = true,
            }) ?? throw new InvalidOperationException($"Process.Start returned null for {exe}"))
            {
                // Fire and forget — see <remarks>.
            }
        }
        catch (Exception ex)
        {
            Assert.Fail($"Failed to launch {exe}: {ex.Message}");
        }

        WaitForAnyWindow(processName, timeout);
        return true;
    }

    private static void WaitForAnyWindow(string processName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (WindowsFinder.ListByApp(processName).Count > 0)
            {
                // Give XAML a moment to populate the visual tree.
                Thread.Sleep(750);
                return;
            }

            Thread.Sleep(250);
        }

        Assert.Fail(
            $"No UIA-visible window from process '{processName}' appeared within {timeout.TotalSeconds}s.");
    }

    /// <summary>
    /// Poll <c>winapp ui list-windows --json</c> until a window matching the target module appears.
    /// Returns a <see cref="Session"/> bound to its HWND.
    /// </summary>
    /// <remarks>
    /// When the same process owns multiple windows (Settings exe also owns the <c>PopupHost</c>
    /// overlay), we strictly prefer a window whose title contains the expected title. Process-name
    /// match is only used as a fallback for modules that don't pin a specific title.
    /// </remarks>
    private static Session? WaitForMainWindow(PowerToysModule scope, TimeSpan timeout)
    {
        var processName = ModulePaths.ProcessNameFor(scope);
        var expectedTitle = ModulePaths.MainWindowTitleFor(scope);
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var r = WinappCli.Invoke("ui", "list-windows", "--json");
            if (r.Success && !string.IsNullOrEmpty(r.StdOut))
            {
                try
                {
                    using var doc = JsonDocument.Parse(r.StdOut);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        Session? processFallback = null;

                        foreach (var w in doc.RootElement.EnumerateArray())
                        {
                            var pn = w.TryGetProperty("processName", out var pnEl) ? (pnEl.GetString() ?? string.Empty) : string.Empty;
                            var title = w.TryGetProperty("title", out var tEl) ? (tEl.GetString() ?? string.Empty) : string.Empty;
                            var hwnd = w.TryGetProperty("hwnd", out var hwndEl) && hwndEl.ValueKind == JsonValueKind.Number ? hwndEl.GetInt64() : 0L;
                            var pid = w.TryGetProperty("processId", out var pidEl) && pidEl.ValueKind == JsonValueKind.Number ? pidEl.GetInt32() : 0;

                            if (hwnd == 0)
                            {
                                continue;
                            }

                            // Strict title match wins immediately — disambiguates from sibling
                            // windows owned by the same process (e.g. Settings + PopupHost).
                            if (!string.IsNullOrEmpty(expectedTitle) &&
                                title.Contains(expectedTitle, StringComparison.OrdinalIgnoreCase))
                            {
                                return new Session(scope, hwnd, title, pid, pn);
                            }

                            // Track the first process-name match as a fallback for modules where no
                            // expected title is configured.
                            if (processFallback is null &&
                                !string.IsNullOrEmpty(processName) &&
                                pn.Contains(processName, StringComparison.OrdinalIgnoreCase))
                            {
                                processFallback = new Session(scope, hwnd, title, pid, pn);
                            }
                        }

                        // No title match yet — only fall back to the process match if the module
                        // really has no expected title configured.
                        if (string.IsNullOrEmpty(expectedTitle) && processFallback is not null)
                        {
                            return processFallback;
                        }
                    }
                }
                catch
                {
                    // Bad JSON during startup — keep polling.
                }
            }

            Thread.Sleep(250);
        }

        return null;
    }
}
