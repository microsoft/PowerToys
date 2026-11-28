// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>
/// The notice the engine shows at the text cursor once a snippet that left a spot empty is in, such as "No email address
/// found": a window of <see cref="NoticeBubble.WindowClassName"/> that belongs to PowerToys.TextExpander.exe, kept for
/// every notice.
/// </summary>
internal static class EngineNotice
{
    /// <summary>Waits for the engine's notice to show, and returns its window, or zero when none did.</summary>
    public static IntPtr WaitForShown(int timeoutMS = 5_000)
    {
        var shown = WaitHelper.WaitForStable(
            () => Windows().FirstOrDefault(Native.IsWindowVisible),
            window => window != IntPtr.Zero,
            timeoutMS,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 50);
        return shown.Succeeded ? shown.LastObservation : IntPtr.Zero;
    }

    /// <summary>Whether the notice went within <paramref name="timeoutMS"/>.</summary>
    public static bool WaitForHidden(IntPtr notice, int timeoutMS) =>
        WaitHelper.WaitForStable(() => Native.IsWindowVisible(notice), visible => !visible, timeoutMS, requiredConsecutiveMatches: 2, pollIntervalMS: 50).Succeeded;

    /// <summary>
    /// Whether UI Automation names the notice with <paramref name="heading"/>, read from outside the engine's process, as
    /// Narrator reads it. <paramref name="read"/> is what was read, for a failure message.
    /// </summary>
    public static bool IsNamed(IntPtr notice, string heading, out string read)
    {
        string window = notice.ToInt64().ToString(CultureInfo.InvariantCulture);
        JsonElement tree = default;
        bool named = WaitHelper.WaitForStable(
            () =>
            {
                tree = WinappCli.InvokeJson("ui", "inspect", "-w", window, "--json", "-d", "1");
                return Strings(tree).Any(text => text.Contains(heading, StringComparison.Ordinal));
            },
            found => found,
            timeoutMS: 3_000,
            pollIntervalMS: 100).Succeeded;
        read = tree.ValueKind == JsonValueKind.Undefined ? string.Empty : tree.ToString();
        return named;
    }

    private static List<IntPtr> Windows()
    {
        var engine = TextExpanderTestBase.ProcessIds(TextExpanderTestBase.EngineProcessName).Select(id => (uint)id).ToHashSet();
        var found = new List<IntPtr>();
        var name = new char[64];
        Native.EnumWindows(
            (window, context) =>
            {
                int length = Native.GetClassNameW(window, name, name.Length);
                _ = Native.GetWindowThreadProcessId(window, out uint process);
                if (engine.Contains(process) && new string(name, 0, Math.Max(0, length)) == NoticeBubble.WindowClassName)
                {
                    found.Add(window);
                }

                return true;
            },
            IntPtr.Zero);
        return found;
    }

    private static IEnumerable<string> Strings(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => [element.GetString() ?? string.Empty],
        JsonValueKind.Object => element.EnumerateObject().SelectMany(property => Strings(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(Strings),
        _ => [],
    };
}
