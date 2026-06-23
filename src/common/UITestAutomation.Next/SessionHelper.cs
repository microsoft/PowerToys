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
    // Generous window-appearance budget. On a cold/busy CI agent the runner spends tens of seconds
    // enabling every module and the Settings WinUI process cold-starts before its window appears.
    // When the whole test job runs elevated (required so the legacy WinAppDriver harness can bind
    // :4723) the runner's startup is slower still — ~100s to the first Settings window observed on a
    // slow platform — so the budget is 150s. We wait patiently (and only re-issue the launch when
    // nothing is alive) rather than kill-and-relaunch on a short deadline, which only resets a
    // slow-but-healthy startup and never converges.
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(150);

    private readonly PowerToysModule scope;

    // True when this helper's Init/Restart actually launched the scope (vs. attaching to an
    // already-running instance). StopIfStarted only tears down what we created.
    private bool launchedByUs;

    public SessionHelper(PowerToysModule scope)
    {
        this.scope = scope;
    }

    public Session Init()
    {
        launchedByUs = EnsureRunning(scope, LaunchTimeout);
        return ResolveMainWindowOrFail();
    }

    /// <summary>
    /// Force a clean restart of this helper's scope: kill the scope process (plus the runner for the
    /// Settings scope), relaunch, and rebind to the fresh window. Marks the session launched-by-us so
    /// <see cref="StopIfStarted"/> tears it down. Mirrors the net effect of the legacy <c>RestartScopeExe</c>.
    /// </summary>
    public Session Restart()
    {
        StopScope();
        EnsureRunning(scope, LaunchTimeout);
        launchedByUs = true;
        return ResolveMainWindowOrFail();
    }

    /// <summary>
    /// Stop the process(es) this helper launched. No-op when the target was already running at
    /// <see cref="Init"/> time — we never kill state the test didn't create. Mirrors the legacy
    /// <c>ExitScopeExe</c>, scoped to "only what we started".
    /// </summary>
    public void StopIfStarted()
    {
        if (!launchedByUs)
        {
            return;
        }

        StopScope();
        launchedByUs = false;
    }

    private Session ResolveMainWindowOrFail()
    {
        var window = WaitForMainWindow(scope, LaunchTimeout);
        Assert.IsNotNull(window, $"Main window for {scope} did not appear via winappcli within {LaunchTimeout.TotalSeconds:0}s");
        return window!;
    }

    /// <summary>
    /// Kill the scope's process and, for the Settings scope, the runner that owns it (the runner's
    /// exit also stops the modules it spawned). Uses exact-name matching so unrelated processes that
    /// merely contain "PowerToys" in their name (e.g. the test host) are left alone. Waits briefly
    /// for the scope process to disappear.
    /// </summary>
    private void StopScope() => KillScopeProcessesAndWait(scope);

    /// <summary>Process name as winappcli's <c>-a</c> flag (and <see cref="Process.GetProcessesByName(string)"/>) accept it.</summary>
    public static string GetProcessName(PowerToysModule scope) => ModulePaths.ProcessNameFor(scope);

    /// <summary>Returns <c>true</c> if at least one process matching <paramref name="scope"/> is running.</summary>
    public static bool IsRunning(PowerToysModule scope) =>
        Process.GetProcessesByName(GetProcessName(scope)).Length > 0;

    /// <summary>
    /// Ensure the runner-owned environment for <paramref name="scope"/> is up and has presented a
    /// UIA-visible window. Returns <c>false</c> when the target was already running (nothing
    /// launched), <c>true</c> when a launch was needed — callers track this so cleanup only kills
    /// what the test itself started.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The PowerToys <b>runner</b> (<c>PowerToys.exe</c>) is the single entry point. It installs the
    /// centralized keyboard hook and owns every module's start/stop lifecycle. Tests therefore
    /// launch the runner and drive modules through the Settings UI — they never launch a module's
    /// UI exe (e.g. <c>PowerToys.ColorPickerUI.exe</c>) standalone. A standalone module process has
    /// no runner behind it, so its activation hotkey never fires and toggling it in Settings does
    /// nothing. For the <see cref="PowerToysModule.PowerToysSettings"/> scope we launch
    /// <c>PowerToys.exe --open-settings</c>: the runner starts (or, being single-instance, the
    /// already-running one is signalled) and presents the Settings window.
    /// </para>
    /// <para>
    /// <c>UseShellExecute = true</c> is intentional: with <c>UseShellExecute = false</c> the
    /// spawned process inherits this test-host's stdin/stdout/stderr handles, and the
    /// Microsoft.Testing.Platform / MSTest runner won't declare the test run complete until
    /// those pipes drain — which never happens until the target exits. Going through
    /// ShellExecute gives the child its own console and detaches the handles.
    /// </para>
    /// <para>
    /// PowerToys processes with single-instance gates (runner, Settings, ColorPicker) often hand
    /// off to an existing instance and let the launcher PID exit with code 0 immediately. The
    /// launcher PID is therefore intentionally discarded; readiness is judged purely by whether a
    /// UIA window owned by the target process becomes visible.
    /// </para>
    /// </remarks>
    public static bool EnsureRunning(PowerToysModule scope, TimeSpan timeout)
    {
        // Whether or not the scope process already exists, the test needs its WINDOW. EnsureWindow
        // waits patiently and (idempotently) re-issues the launch as needed; it only kills/relaunches
        // a genuinely-dead fresh launch, never a slow-but-healthy or class-shared (reused) window.
        var alreadyRunning = IsRunning(scope);
        EnsureWindow(scope, timeout, alreadyRunning);
        return !alreadyRunning;
    }

    /// <summary>
    /// Wait for a UIA-visible window from <paramref name="scope"/> to appear, launching / re-issuing
    /// the launch as needed. The Settings scope is launched through the runner
    /// (<c>PowerToys.exe --open-settings</c>); see <see cref="EnsureRunning"/> remarks.
    /// </summary>
    /// <remarks>
    /// On a busy/cold CI agent the runner spends tens of seconds enabling every module before the
    /// Settings window appears (~30-50s observed). A "kill + relaunch every 20s" loop kept resetting
    /// that slow-but-healthy startup so it never converged (the "runner: 1, Settings: 2, no window"
    /// failures). Instead this waits a single generous <paramref name="timeout"/> and only acts when
    /// the window is still missing after a grace period: it re-issues the launch — idempotent, since
    /// the runner is single-instance, so <c>--open-settings</c> just (re)shows Settings — and
    /// additionally clears the single-instance mutex first only for a fresh launch that has gone
    /// completely dead (nothing running), i.e. the handoff-to-a-now-exited-instance race. A
    /// class-shared (reused) window is never killed.
    /// </remarks>
    private static void EnsureWindow(PowerToysModule scope, TimeSpan timeout, bool alreadyRunning)
    {
        var processName = GetProcessName(scope);
        var runnerName = GetProcessName(PowerToysModule.Runner);
        var nudgeInterval = TimeSpan.FromSeconds(25);

        if (!alreadyRunning)
        {
            // Release the single-instance mutex any stale/half-launched instance still holds (pre-test
            // hygiene kills without waiting), then launch.
            KillScopeProcessesAndWait(scope);
            LaunchScope(scope);
        }

        var deadline = DateTime.UtcNow + timeout;
        var lastLaunch = DateTime.UtcNow;

        while (DateTime.UtcNow < deadline)
        {
            if (WindowsFinder.ListByApp(processName).Count > 0)
            {
                // Give XAML a moment to populate the visual tree.
                Thread.Sleep(750);
                return;
            }

            if (DateTime.UtcNow - lastLaunch > nudgeInterval)
            {
                // Re-issue the launch ONLY when nothing is alive to present the window — the genuine
                // "launcher handed off to an instance that then exited" race. If the runner is still
                // alive it already owns the queued --open-settings request and, on a slow agent, may
                // need tens of seconds to enable every module before it spawns Settings. Re-launching
                // there is NOT free: each extra --open-settings queues another request that the runner
                // honours with a SEPARATE Settings.exe (the "Settings: 3" pile-up seen in CI), and the
                // competing single-instance processes plus the launch contention push the window past
                // the deadline. So when anything is alive, keep waiting instead of piling on.
                var alive = IsRunning(scope) || Process.GetProcessesByName(runnerName).Length > 0;
                if (!alive)
                {
                    if (!alreadyRunning)
                    {
                        KillScopeProcessesAndWait(scope);
                    }

                    LaunchScope(scope);
                    lastLaunch = DateTime.UtcNow;
                }
            }

            Thread.Sleep(500);
        }

        Assert.Fail(
            $"No UIA-visible window from process '{processName}' appeared within {timeout.TotalSeconds:0}s. " +
            $"Live processes — runner '{runnerName}': {Process.GetProcessesByName(runnerName).Length}, " +
            $"'{processName}': {Process.GetProcessesByName(processName).Length}.");
    }

    /// <summary>
    /// Issue a single detached launch for <paramref name="scope"/>: the runner with
    /// <c>--open-settings</c> for the Settings scope (the runner owns the Settings UI — see
    /// <see cref="EnsureRunning"/> remarks), or the scope's own exe otherwise.
    /// </summary>
    private static void LaunchScope(PowerToysModule scope)
    {
        if (scope == PowerToysModule.PowerToysSettings)
        {
            LaunchViaShell(ModulePaths.ExePathFor(PowerToysModule.Runner), "--open-settings");
        }
        else
        {
            LaunchViaShell(ModulePaths.ExePathFor(scope), null);
        }
    }

    /// <summary>
    /// Kill the scope's process — plus the runner for the Settings scope, which owns the
    /// single-instance mutex that <c>--open-settings</c> hands off to — and wait for them to exit.
    /// The wait is the point: relaunching while a just-killed runner still holds its mutex hands the
    /// new launch off to the dying instance, which never presents a window.
    /// </summary>
    private static void KillScopeProcessesAndWait(PowerToysModule scope)
    {
        var names = scope == PowerToysModule.PowerToysSettings
            ? new[] { GetProcessName(PowerToysModule.PowerToysSettings), GetProcessName(PowerToysModule.Runner) }
            : new[] { GetProcessName(scope) };

        foreach (var name in names)
        {
            WindowControl.TryKillProcessByName(name);
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && names.Any(n => Process.GetProcessesByName(n).Length > 0))
        {
            Thread.Sleep(150);
        }
    }

    /// <summary>
    /// Launch <paramref name="exe"/> detached via ShellExecute (see <see cref="EnsureRunning"/>
    /// remarks for why <c>UseShellExecute = true</c> is required). The launcher PID is discarded;
    /// readiness is judged by window presence, not the process handle.
    /// </summary>
    private static void LaunchViaShell(string exe, string? arguments)
    {
        Assert.IsTrue(File.Exists(exe), $"Executable not found: {exe}");

        try
        {
            using (Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = arguments ?? string.Empty,
                WorkingDirectory = Path.GetDirectoryName(exe)!,
                UseShellExecute = true,
            }) ?? throw new InvalidOperationException($"Process.Start returned null for {exe}"))
            {
                // Fire and forget — see EnsureRunning <remarks>.
            }
        }
        catch (Exception ex)
        {
            Assert.Fail($"Failed to launch '{exe} {arguments}': {ex.Message}");
        }
    }

    /// <summary>
    /// Force a clean restart of the module: kill any running instance, wait for it to exit, then
    /// launch a fresh one and wait for its window. Returns true once a window is visible.
    /// </summary>
    public static bool RestartScope(PowerToysModule scope, TimeSpan timeout)
    {
        var processName = GetProcessName(scope);
        WindowControl.TryKillProcess(processName);

        var killDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < killDeadline && Process.GetProcessesByName(processName).Length > 0)
        {
            Thread.Sleep(150);
        }

        return EnsureRunning(scope, timeout);
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
