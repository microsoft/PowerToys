// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Owns process launch + window resolution for a <see cref="PowerToysModule"/>. Equivalent to the
/// old <c>SessionHelper</c> but the engine is winappcli — no WinAppDriver, no Appium.
/// </summary>
internal sealed class SessionHelper
{
    private readonly PowerToysModule scope;

    public SessionHelper(PowerToysModule scope)
    {
        this.scope = scope;
    }

    public Session Init()
    {
        var exe = ModulePaths.ExePathFor(scope);
        Assert.IsTrue(File.Exists(exe), $"Module exe not found: {exe}");

        // Reuse a running instance if present (matches Settings UI single-instance behaviour).
        if (!IsRunning(exe))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                Assert.Fail($"Failed to launch {exe}: {ex.Message}");
            }
        }

        var window = WaitForMainWindow(scope, TimeSpan.FromSeconds(20));
        Assert.IsNotNull(window, $"Main window for {scope} did not appear via winappcli within 20s");
        return window!;
    }

    private static bool IsRunning(string exePath)
    {
        var name = Path.GetFileNameWithoutExtension(exePath);
        return Process.GetProcessesByName(name).Length > 0;
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
