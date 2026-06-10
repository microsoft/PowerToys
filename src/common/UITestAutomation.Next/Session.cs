// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// A test session bound to a specific HWND. All <see cref="Find{T}"/>/<see cref="FindAll{T}"/>
/// calls route to <c>winapp ui search</c> with <c>-w &lt;hex hwnd&gt;</c> for stable targeting.
/// </summary>
public sealed class Session
{
    /// <summary>Decimal HWND of the target window (as returned by <c>list-windows --json</c>).</summary>
    public long WindowHandle { get; }

    /// <summary>String form of <see cref="WindowHandle"/> for passing to winappcli's <c>-w</c> flag.</summary>
    public string WindowHandleArg { get; }

    public string WindowTitle { get; }

    public int ProcessId { get; }

    public string ProcessName { get; }

    public PowerToysModule InitScope { get; }

    internal Session(PowerToysModule scope, long hwnd, string title, int pid, string processName)
    {
        InitScope = scope;
        WindowHandle = hwnd;
        WindowHandleArg = hwnd.ToString(CultureInfo.InvariantCulture);
        WindowTitle = title;
        ProcessId = pid;
        ProcessName = processName;
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

    /// <summary>Capture a PNG of the session's window via <c>winapp ui screenshot</c>.</summary>
    public string Screenshot(string outputPath)
    {
        WinappCli.InvokeAssertSuccess("ui", "screenshot", "-w", WindowHandleArg, "-o", outputPath);
        return outputPath;
    }

    /// <summary>
    /// Dump the full UIA tree for this session's window via <c>winapp ui inspect --json</c>.
    /// Returned shape: <c>{ "windows": [{ "elements": [{ "type", "name", "value", "children": [...] }] }] }</c>.
    /// </summary>
    public JsonElement Inspect(int depth = 6)
    {
        return WinappCli.InvokeJson(
            "ui", "inspect",
            "-w", WindowHandleArg,
            "--json",
            "-d", depth.ToString(CultureInfo.InvariantCulture));
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
        var root = WinappCli.InvokeJson("ui", "search", by.Value, "-w", WindowHandleArg, "--json");

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
