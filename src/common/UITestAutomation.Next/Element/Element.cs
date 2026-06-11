// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Reference to a UI element resolved via winappcli. Wraps the resolved <see cref="Selector"/>
/// (slug or text query), the owning <see cref="Session"/>, and the metadata captured at lookup
/// time (control type, class name, name).
/// </summary>
/// <remarks>
/// Element instances are <i>stateless on the wire</i> — every property read and every action
/// shells out to <c>winapp ui …</c>. The cached <see cref="ControlType"/>, <see cref="ClassName"/>,
/// and <see cref="Name"/> are the values seen at <c>Find</c> time; for fresh values, re-find.
/// </remarks>
public class Element
{
    internal Session? Owner { get; set; }

    /// <summary>The selector winappcli will use to address this element (semantic slug, ID, or text query).</summary>
    public string Selector { get; internal set; } = string.Empty;

    /// <summary>Cached control type at lookup time (e.g. "Button", "ToggleSwitch").</summary>
    public string ControlType { get; internal set; } = string.Empty;

    /// <summary>Cached class name at lookup time (e.g. "ToggleSwitch", "TextBlock").</summary>
    public string ClassName { get; internal set; } = string.Empty;

    /// <summary>Cached Name property at lookup time.</summary>
    public string Name { get; internal set; } = string.Empty;

    /// <summary>Top-left X (screen pixels) reported by <c>search</c> at lookup time.</summary>
    public int X { get; internal set; }

    /// <summary>Top-left Y (screen pixels) reported by <c>search</c> at lookup time.</summary>
    public int Y { get; internal set; }

    /// <summary>Bounding-box width reported by <c>search</c> at lookup time.</summary>
    public int Width { get; internal set; }

    /// <summary>Bounding-box height reported by <c>search</c> at lookup time.</summary>
    public int Height { get; internal set; }

    /// <summary>UIA control type that this wrapper subclass expects (e.g. <c>"Button"</c>). Null = match anything.</summary>
    protected string? TargetControlType { get; set; }

    /// <summary>Optional ClassName filter applied alongside <see cref="TargetControlType"/>.</summary>
    protected string? TargetClassName { get; set; }

    internal bool MatchesFilter()
    {
        if (TargetControlType is not null &&
            !string.Equals(ControlType, TargetControlType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (TargetClassName is not null &&
            !string.Equals(ClassName, TargetClassName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Activate the element. winappcli's <c>invoke</c> tries InvokePattern → TogglePattern →
    /// SelectionItemPattern → ExpandCollapsePattern in order; <c>rightClick</c> falls back to
    /// <c>click --right</c> via real mouse input.
    /// </summary>
    public virtual void Click(bool rightClick = false, int msPostAction = 200)
    {
        EnsureBound();

        if (rightClick)
        {
            WinappCli.InvokeAssertSuccess("ui", "click", Selector, Owner!.TargetFlag, Owner!.TargetValue, "--right");
        }
        else
        {
            WinappCli.InvokeAssertSuccess("ui", "invoke", Selector, Owner!.TargetFlag, Owner!.TargetValue);
        }

        if (msPostAction > 0)
        {
            Thread.Sleep(msPostAction);
        }
    }

    /// <summary>
    /// Mouse-simulation left-click via <c>winapp ui click &lt;slug&gt;</c>. Use for elements that
    /// don't expose an InvokePattern (e.g. TextBlocks, ListItems, column headers), where the
    /// click is handled by an ancestor's Click handler rather than by the element itself.
    /// </summary>
    public void MouseClick(int msPostAction = 200)
    {
        EnsureBound();
        WinappCli.InvokeAssertSuccess("ui", "click", Selector, Owner!.TargetFlag, Owner!.TargetValue);
        if (msPostAction > 0)
        {
            Thread.Sleep(msPostAction);
        }
    }

    /// <summary>
    /// Double-click via <c>winapp ui click &lt;slug&gt; --double</c> (real mouse simulation). Use
    /// for controls where a double-click has distinct behavior (list items, headers).
    /// </summary>
    public void DoubleClick(int msPostAction = 200)
    {
        EnsureBound();
        WinappCli.InvokeAssertSuccess("ui", "click", Selector, Owner!.TargetFlag, Owner!.TargetValue, "--double");
        if (msPostAction > 0)
        {
            Thread.Sleep(msPostAction);
        }
    }

    /// <summary>Scroll this element into the visible area via <c>winapp ui scroll-into-view</c>.</summary>
    public void ScrollIntoView()
    {
        EnsureBound();
        WinappCli.InvokeAssertSuccess("ui", "scroll-into-view", Selector, Owner!.TargetFlag, Owner!.TargetValue);
    }

    /// <summary>Move keyboard focus to this element.</summary>
    public void Focus()
    {
        EnsureBound();
        WinappCli.InvokeAssertSuccess("ui", "focus", Selector, Owner!.TargetFlag, Owner!.TargetValue);
    }

    /// <summary>
    /// Read a single UIA property via <c>winapp ui get-property … --json</c>. Returns the raw string
    /// value as winappcli reports it (e.g. <c>"On"</c>/<c>"Off"</c> for <c>ToggleState</c>).
    /// </summary>
    public string GetProperty(string propertyName)
    {
        EnsureBound();
        var r = WinappCli.Invoke("ui", "get-property", Selector, "-p", propertyName, Owner!.TargetFlag, Owner!.TargetValue, "--json");
        if (string.IsNullOrEmpty(r.StdOut))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(r.StdOut);
            if (doc.RootElement.TryGetProperty("properties", out var props) &&
                props.TryGetProperty(propertyName, out var v))
            {
                return JsonValueToString(v);
            }
        }
        catch
        {
            // Non-JSON / error output (e.g. property unsupported on this element) — treat as empty.
        }

        return string.Empty;
    }

    /// <summary>
    /// UIA <c>HelpText</c> (from <c>AutomationProperties.HelpText</c>). Used by the Settings UI
    /// ShortcutControl to surface the current shortcut as readable text on the EditButton
    /// (e.g. <c>"Win + Shift + C"</c>).
    /// </summary>
    public string HelpText => GetProperty("HelpText");

    /// <summary>True when UIA reports the element as enabled (defaults to true when unknown).</summary>
    public bool IsEnabled => ParseBool(GetProperty("IsEnabled"), defaultValue: true);

    /// <summary>True when UIA reports the element off-screen (defaults to false when unknown).</summary>
    public bool IsOffscreen => ParseBool(GetProperty("IsOffscreen"), defaultValue: false);

    /// <summary>Convenience inverse of <see cref="IsOffscreen"/> — mirrors the legacy harness's <c>Displayed</c>.</summary>
    public bool Displayed => !IsOffscreen;

    /// <summary>True when the element is selected (UIA SelectionItemPattern.IsSelected).</summary>
    public bool Selected => ParseBool(GetProperty("IsSelected"), defaultValue: false);

    /// <summary>The element's UIA AutomationId (empty when it has none).</summary>
    public string AutomationId => GetProperty("AutomationId");

    /// <summary>
    /// Read any UIA property by name via <c>winapp ui get-property</c>. Alias of
    /// <see cref="GetProperty"/> kept for parity with the legacy harness's <c>GetAttribute</c>.
    /// </summary>
    public string GetAttribute(string attributeName) => GetProperty(attributeName);

    /// <summary>
    /// Read the element's value via <c>winapp ui get-value … --json</c>. winappcli walks
    /// TextPattern → ValuePattern → SelectionPattern → Name to find a value, so this returns
    /// the rendered text content of TextBlocks (e.g. ColorPicker's <c>ColorTextBlock</c>
    /// where <c>AutomationProperties.Name</c> overrides the UIA Name with the color's friendly
    /// name, but the actual <c>Text</c> binding holds the HEX value we want).
    /// </summary>
    public string GetValue()
    {
        EnsureBound();
        var root = WinappCli.InvokeJson("ui", "get-value", Selector, Owner!.TargetFlag, Owner!.TargetValue, "--json");
        if (root.TryGetProperty("text", out var t))
        {
            return t.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    /// <summary>
    /// Wait for this element to reach <paramref name="expectedValue"/> on <paramref name="propertyName"/>.
    /// Mirrors <c>winapp ui wait-for --property X --value Y -t T</c>; returns true on success, false on timeout.
    /// </summary>
    public bool WaitForProperty(string propertyName, string expectedValue, int timeoutMS = 5000)
    {
        EnsureBound();
        var r = WinappCli.Invoke(
            "ui", "wait-for", Selector,
            Owner!.TargetFlag, Owner!.TargetValue,
            "--property", propertyName,
            "--value", expectedValue,
            "-t", timeoutMS.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return r.ExitCode == 0;
    }

    /// <summary>
    /// Wait for this element's value (smart fallback: TextPattern → ValuePattern →
    /// SelectionPattern → Name) to match <paramref name="expectedValue"/>. When
    /// <paramref name="contains"/> is true, matches on substring instead of equality
    /// (<c>winapp ui wait-for … --value … --contains</c>). Returns true on match, false on timeout.
    /// </summary>
    public bool WaitForValue(string expectedValue, bool contains = false, int timeoutMS = 5000)
    {
        EnsureBound();
        var args = new List<string>
        {
            "ui", "wait-for", Selector,
            Owner!.TargetFlag, Owner!.TargetValue,
            "--value", expectedValue,
            "-t", timeoutMS.ToString(CultureInfo.InvariantCulture),
        };
        if (contains)
        {
            args.Add("--contains");
        }

        return WinappCli.Invoke(args.ToArray()).ExitCode == 0;
    }

    /// <summary>
    /// Wait for any element matching the original selector to disappear from the tree
    /// (<c>winapp ui wait-for … --gone</c>).
    /// </summary>
    public bool WaitForGone(int timeoutMS = 5000)
    {
        EnsureBound();
        var r = WinappCli.Invoke(
            "ui", "wait-for", Selector,
            Owner!.TargetFlag, Owner!.TargetValue,
            "--gone",
            "-t", timeoutMS.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return r.ExitCode == 0;
    }

    /// <summary>Find a descendant matching <paramref name="by"/>, scoped under this element via its slug.</summary>
    public T Find<T>(By by, int timeoutMS = 5000)
        where T : Element, new()
    {
        EnsureBound();

        // winappcli scopes a search beneath an element by passing the parent's selector to inspect.
        // For most cases (within the same window) the global search is fine and faster; if you need
        // strict scoping under a subtree, use a slug By that prefixes with the parent's slug.
        return Owner!.FindUnder<T>(by, timeoutMS);
    }

    public T Find<T>(string name, int timeoutMS = 5000)
        where T : Element, new() => Find<T>(By.Name(name), timeoutMS);

    protected void EnsureBound()
    {
        Assert.IsNotNull(Owner, "Element is not bound to a Session.");
        Assert.IsFalse(string.IsNullOrEmpty(Selector), "Element has no selector.");
    }

    /// <summary>Stringify a JSON property value regardless of kind (string / bool / number).</summary>
    private static string JsonValueToString(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString() ?? string.Empty,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Number => v.GetRawText(),
        JsonValueKind.Null => string.Empty,
        _ => v.GetRawText(),
    };

    /// <summary>Parse a winappcli boolean-ish property string; falls back to <paramref name="defaultValue"/> when empty.</summary>
    private static bool ParseBool(string raw, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return raw.Trim().ToLowerInvariant() is "true" or "on" or "1" or "yes";
    }
}
