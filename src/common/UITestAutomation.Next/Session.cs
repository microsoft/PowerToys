// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// A test session bound to either a specific window (HWND) or a whole process (name or PID).
/// All <see cref="Find{T}"/>/<see cref="FindAll{T}"/> calls route to <c>winapp ui search</c>
/// scoped by <see cref="TargetFlag"/>/<see cref="TargetValue"/>.
/// </summary>
/// <remarks>
/// Two scopes are supported:
/// <list type="bullet">
///   <item><description><c>Window</c> (<c>-w &lt;hwnd&gt;</c>) — the default. Use when the
///   process owns multiple windows and the test needs to pin one (e.g. ColorPickerUI's
///   overlay vs editor; Settings vs PopupHost).</description></item>
///   <item><description><c>Process</c> (<c>-a &lt;name|pid&gt;</c>) — simpler when the target
///   process owns exactly one user-facing window. Built via <see cref="FromProcess"/>. Matches
///   the pattern in <see href="https://github.com/microsoft/PowerToys/pull/48414"/>.</description></item>
/// </list>
/// </remarks>
public sealed class Session
{
    public enum TargetScope
    {
        /// <summary>Scope all CLI calls to a specific HWND via <c>-w</c>.</summary>
        Window,

        /// <summary>Scope all CLI calls to a process (name substring or PID) via <c>-a</c>.</summary>
        Process,
    }

    /// <summary>Decimal HWND of the target window, or 0 when bound by <see cref="TargetScope.Process"/>.</summary>
    public long WindowHandle { get; }

    /// <summary>String form of <see cref="WindowHandle"/> for passing to winappcli's <c>-w</c> flag.</summary>
    public string WindowHandleArg { get; }

    /// <summary>The scope these calls run against (window or process).</summary>
    public TargetScope Scope { get; }

    /// <summary>winappcli flag for the active scope (<c>-w</c> or <c>-a</c>).</summary>
    public string TargetFlag { get; }

    /// <summary>Value to pass after <see cref="TargetFlag"/> — the decimal HWND or the process name/PID.</summary>
    public string TargetValue { get; }

    public string WindowTitle { get; }

    public int ProcessId { get; }

    public string ProcessName { get; }

    public PowerToysModule InitScope { get; }

    internal Session(PowerToysModule scope, long hwnd, string title, int pid, string processName)
    {
        InitScope = scope;
        WindowHandle = hwnd;
        WindowHandleArg = hwnd.ToString(CultureInfo.InvariantCulture);
        Scope = TargetScope.Window;
        TargetFlag = "-w";
        TargetValue = WindowHandleArg;
        WindowTitle = title;
        ProcessId = pid;
        ProcessName = processName;
    }

    private Session(PowerToysModule scope, string appNameOrPid, int pid, string processName, string title)
    {
        InitScope = scope;
        WindowHandle = 0;
        WindowHandleArg = "0";
        Scope = TargetScope.Process;
        TargetFlag = "-a";
        TargetValue = appNameOrPid;
        WindowTitle = title;
        ProcessId = pid;
        ProcessName = processName;
    }

    /// <summary>
    /// Build a session scoped to a whole process via <c>winapp ... -a &lt;app&gt;</c>. Cheaper than
    /// resolving a HWND and ideal for the single-window-per-process case (e.g. Settings smoke
    /// tests). The first matching window's PID/name/title are captured for reporting only — all
    /// subsequent CLI calls re-resolve via <c>-a</c>, so window-replacement during the test
    /// (re-navigation, page swap) is handled transparently.
    /// </summary>
    /// <param name="appNameOrPid">Process name substring (e.g. <c>"PowerToys.Settings"</c>) or PID as a string.</param>
    /// <param name="attributeAs">Module label used for diagnostics only.</param>
    /// <param name="timeoutMS">How long to wait for the process to expose at least one UIA window.</param>
    public static Session FromProcess(
        string appNameOrPid,
        PowerToysModule attributeAs = PowerToysModule.Runner,
        int timeoutMS = 10_000)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        while (DateTime.UtcNow < deadline)
        {
            var windows = WindowsFinder.ListByApp(appNameOrPid);
            if (windows.Count > 0)
            {
                var w = windows[0];
                return new Session(attributeAs, appNameOrPid, w.ProcessId, w.ProcessName, w.Title);
            }

            Thread.Sleep(250);
        }

        Assert.Fail(
            $"FromProcess('{appNameOrPid}'): no UIA-visible window appeared within {timeoutMS}ms. " +
            $"Is the app running? Run 'winapp ui list-windows -a {appNameOrPid}' to confirm.");
        return null!;
    }

    public T Find<T>(By by, int timeoutMS = 5000)
        where T : Element, new() => FindUnder<T>(by, timeoutMS);

    public T Find<T>(string name, int timeoutMS = 5000)
        where T : Element, new() => FindUnder<T>(By.Name(name), timeoutMS);

    public Element Find(By by, int timeoutMS = 5000) => FindUnder<Element>(by, timeoutMS);

    public Element Find(string name, int timeoutMS = 5000) => FindUnder<Element>(By.Name(name), timeoutMS);

    public bool Has<T>(By by, int timeoutMS = 1000)
        where T : Element, new() => FindAll<T>(by, timeoutMS).Count >= 1;

    public bool Has(By by, int timeoutMS = 1000) => Has<Element>(by, timeoutMS);

    public bool Has(string name, int timeoutMS = 1000) => Has<Element>(By.Name(name), timeoutMS);

    public bool HasOne<T>(By by, int timeoutMS = 1000)
        where T : Element, new() => FindAll<T>(by, timeoutMS).Count == 1;

    /// <summary>
    /// All elements matching <paramref name="by"/> on this session's window, optionally polling
    /// for up to <paramref name="timeoutMS"/> if none are present initially.
    /// </summary>
    public ReadOnlyCollection<T> FindAll<T>(By by, int timeoutMS = 5000)
        where T : Element, new()
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);

        while (true)
        {
            var matches = ExecuteSearch(by);
            var typed = new List<T>(matches.Count);
            foreach (var m in matches)
            {
                var e = new T
                {
                    Owner = this,
                    Selector = m.Selector,
                    ControlType = m.ControlType,
                    ClassName = m.ClassName,
                    Name = m.Name,
                    X = m.X,
                    Y = m.Y,
                    Width = m.Width,
                    Height = m.Height,
                };
                if (e.MatchesFilter())
                {
                    typed.Add(e);
                }
            }

            if (typed.Count > 0 || DateTime.UtcNow >= deadline)
            {
                return new ReadOnlyCollection<T>(typed);
            }

            Thread.Sleep(100);
        }
    }

    internal T FindUnder<T>(By by, int timeoutMS)
        where T : Element, new()
    {
        var collection = FindAll<T>(by, timeoutMS);
        Assert.IsTrue(collection.Count > 0, $"UI-Element({typeof(T).Name}) not found using selector: {by}");
        return collection[0];
    }

    /// <summary>
    /// Generic polling helper, equivalent to winappcli's <c>wait-for --value</c> but evaluated in C#
    /// so the predicate can read multiple properties / compose conditions.
    /// </summary>
    public bool WaitFor(Func<bool> condition, int timeoutMS = 5000, int pollIntervalMS = 100)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (condition())
                {
                    return true;
                }
            }
            catch
            {
                // Treat property reads on stale elements as "not yet true".
            }

            Thread.Sleep(pollIntervalMS);
        }

        return false;
    }

    /// <summary>
    /// Wait for an element matching <paramref name="by"/> to appear in the tree via
    /// <c>winapp ui wait-for</c>. Returns true if it appeared within <paramref name="timeoutMS"/>.
    /// </summary>
    public bool WaitForElement(By by, int timeoutMS = 5000)
    {
        var r = WinappCli.Invoke(
            "ui", "wait-for", by.Value,
            TargetFlag, TargetValue,
            "-t", timeoutMS.ToString(CultureInfo.InvariantCulture));
        return r.ExitCode == 0;
    }

    /// <summary>
    /// Capture a PNG of the session's target via <c>winapp ui screenshot</c>. Pass an
    /// <paramref name="element"/> to crop to that element's bounds, or set
    /// <paramref name="captureScreen"/> to grab from the screen (includes popups / overlays /
    /// flyouts that <c>PrintWindow</c> misses).
    /// </summary>
    public string Screenshot(string outputPath, Element? element = null, bool captureScreen = false)
    {
        WinappCli.InvokeAssertSuccess(BuildScreenshotArgs(outputPath, element, captureScreen));
        return outputPath;
    }

    /// <summary>Non-asserting screenshot for cleanup / failure-artifact paths. Returns false on error.</summary>
    public bool TryScreenshot(string outputPath, Element? element = null, bool captureScreen = false)
    {
        try
        {
            return WinappCli.Invoke(BuildScreenshotArgs(outputPath, element, captureScreen)).Success;
        }
        catch
        {
            return false;
        }
    }

    private string[] BuildScreenshotArgs(string outputPath, Element? element, bool captureScreen)
    {
        var args = new List<string> { "ui", "screenshot" };
        if (element is not null && !string.IsNullOrEmpty(element.Selector))
        {
            args.Add(element.Selector);
        }

        args.Add(TargetFlag);
        args.Add(TargetValue);
        args.Add("-o");
        args.Add(outputPath);
        if (captureScreen)
        {
            args.Add("--capture-screen");
        }

        return args.ToArray();
    }

    /// <summary>
    /// Dump the UIA tree for this session's target via <c>winapp ui inspect --json</c>.
    /// Returned shape: <c>{ "windows": [{ "elements": [{ "type", "name", "value", "children": [...] }] }] }</c>.
    /// </summary>
    /// <param name="depth">Tree depth (ignored by winappcli when <paramref name="interactive"/> is set).</param>
    /// <param name="interactive">Only invokable elements (auto-depth), as a flat list.</param>
    /// <param name="hideDisabled">Omit disabled elements.</param>
    /// <param name="hideOffscreen">Omit off-screen elements.</param>
    public JsonElement Inspect(int depth = 6, bool interactive = false, bool hideDisabled = false, bool hideOffscreen = false)
    {
        var args = new List<string>
        {
            "ui", "inspect",
            TargetFlag, TargetValue,
            "--json",
            "-d", depth.ToString(CultureInfo.InvariantCulture),
        };
        if (interactive)
        {
            args.Add("--interactive");
        }

        if (hideDisabled)
        {
            args.Add("--hide-disabled");
        }

        if (hideOffscreen)
        {
            args.Add("--hide-offscreen");
        }

        return WinappCli.InvokeJson(args.ToArray());
    }

    /// <summary>
    /// Walk the ancestor chain from <paramref name="element"/> up to the root via
    /// <c>winapp ui inspect --ancestors</c>.
    /// </summary>
    public JsonElement InspectAncestors(Element element) =>
        WinappCli.InvokeJson("ui", "inspect", "--ancestors", element.Selector, TargetFlag, TargetValue, "--json");

    /// <summary>The element that currently has keyboard focus, via <c>winapp ui get-focused --json</c>.</summary>
    public JsonElement GetFocused() => WinappCli.InvokeJson("ui", "get-focused", TargetFlag, TargetValue, "--json");

    /// <summary>
    /// Convenience reader for the focused element's Name (empty if none / unknown). Useful for
    /// keyboard-navigation assertions.
    /// </summary>
    public string GetFocusedName()
    {
        try
        {
            var root = GetFocused();
            foreach (var prop in new[] { "name", "Name" })
            {
                if (root.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String)
                {
                    return v.GetString() ?? string.Empty;
                }
            }
        }
        catch
        {
            // Best effort — no focused element or unexpected envelope.
        }

        return string.Empty;
    }

    /// <summary>Send keystrokes via Win32 <c>keybd_event</c>. Required for global PowerToys hotkeys.</summary>
    public void SendKeys(params Key[] keys) => KeyboardHelper.SendKeys(keys);

    public void Cleanup()
    {
        // Stateless — nothing to release on the wire.
    }

    private List<SearchHit> ExecuteSearch(By by)
    {
        // winappcli accepts the selector text directly as the first positional argument.
        var root = WinappCli.InvokeJson("ui", "search", by.Value, TargetFlag, TargetValue, "--json");

        var result = new List<SearchHit>();
        if (root.TryGetProperty("matches", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var m in arr.EnumerateArray())
            {
                result.Add(new SearchHit(
                    Selector: m.TryGetProperty("selector", out var s) ? (s.GetString() ?? string.Empty) : string.Empty,
                    Name: m.TryGetProperty("name", out var n) ? (n.GetString() ?? string.Empty) : string.Empty,
                    ControlType: m.TryGetProperty("type", out var t) ? (t.GetString() ?? string.Empty) : string.Empty,
                    ClassName: m.TryGetProperty("className", out var c) ? (c.GetString() ?? string.Empty) : string.Empty,
                    X: ReadInt(m, "x"),
                    Y: ReadInt(m, "y"),
                    Width: ReadInt(m, "width"),
                    Height: ReadInt(m, "height")));
            }
        }

        return result;

        static int ReadInt(JsonElement el, string name) =>
            el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
    }

    private sealed record SearchHit(string Selector, string Name, string ControlType, string ClassName, int X, int Y, int Width, int Height);
}
