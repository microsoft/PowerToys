// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>Classic/modern Shell popup discovery and content readiness, without module-owned commands.</summary>
public static class ShellMenu
{
    public const string ClassicWindowClassName = "#32768";
    public const string ModernWindowClassName = "Microsoft.UI.Content.PopupWindowSiteBridge";

    private const uint GetMenuHandleMessage = 0x01E1;
    private const uint MenuByPosition = 0x00000400;
    private const uint AbortIfHung = 0x0002;
    private const string ShowMoreOptionsCaption = "Show more options";

    public static bool IsClassicWindow(WindowsFinder.WindowInfo window) =>
        WindowClassMatches(window.ClassName, ClassicWindowClassName);

    public static bool IsModernWindow(WindowsFinder.WindowInfo window) =>
        WindowClassMatches(window.ClassName, ModernWindowClassName);

    public static bool IsMenuWindow(WindowsFinder.WindowInfo window) => IsClassicWindow(window) || IsModernWindow(window);

    public static Session? WaitForWindow(string windowClass, int timeoutMS = 25_000, string? processNameOrId = null) =>
        WaitForWindow(new[] { windowClass }, timeoutMS, processNameOrId);

    public static Session? WaitForWindow(IReadOnlyCollection<string> windowClasses, int timeoutMS = 25_000, string? processNameOrId = null)
    {
        ArgumentNullException.ThrowIfNull(windowClasses);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMS);
        if (windowClasses.Count == 0 || windowClasses.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one non-empty popup class name is required.", nameof(windowClasses));
        }

        if (processNameOrId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(processNameOrId);
        }

        bool Matches(WindowsFinder.WindowInfo window) => windowClasses.Any(name => WindowClassMatches(window.ClassName, name));
        return processNameOrId is null
            ? WindowsFinder.WaitForWindow(Matches, timeoutMS: timeoutMS, pollIntervalMS: 100)
            : WindowsFinder.WaitForWindowByApp(processNameOrId, Matches, timeoutMS: timeoutMS);
    }

    /// <summary>Wait for an exact caption, MenuItem type, and freshly observed positive bounds.</summary>
    public static Element? FindVisibleMenuItem(
        Session menu,
        string caption,
        int timeoutMS = 5_000,
        StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        ArgumentNullException.ThrowIfNull(menu);
        return WaitForVisibleMenuItem(
            () => menu.FindAll<Element>(By.Name(caption), timeoutMS: 0),
            caption,
            timeoutMS,
            comparison,
            isDisplayed: item => item.Displayed);
    }

    /// <summary>
    /// Discover a submenu by visible content rather than caching the first popup HWND. The root
    /// must be window-scoped; its process confines discovery to the owning application.
    /// </summary>
    public static Session? WaitForSubmenu(
        Session root,
        string markerCaption,
        int timeoutMS = 30_000,
        int requiredConsecutiveMatches = 2,
        StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        ValidateWindowSession(root);
        return WaitForSubmenuCore(
            root,
            markerCaption,
            () => WindowsFinder.ListByApp(root.ProcessId.ToString(CultureInfo.InvariantCulture)),
            FindVisibleMenuItem,
            timeoutMS,
            requiredConsecutiveMatches,
            comparison);
    }

    /// <summary>
    /// Open the focused control's menu, optionally navigating to the classic surface. Callers must
    /// establish selection/focus, dismiss old menus, and own any extended-verb modifier hold.
    /// The classic-navigation caption assumes an English Windows display language.
    /// </summary>
    public static Session? OpenForFocusedControl(Session owner, bool useClassicMenu = false, int timeoutMS = 25_000)
    {
        ValidateWindowSession(owner);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMS);
        var deadline = Stopwatch.StartNew();
        if (!WindowControl.TryOpenContextMenuForFocusedControl(new IntPtr(owner.WindowHandle)))
        {
            return null;
        }

        var processId = owner.ProcessId.ToString(CultureInfo.InvariantCulture);
        var remaining = timeoutMS - (int)deadline.ElapsedMilliseconds;
        if (remaining <= 0)
        {
            return null;
        }

        var surface = WaitForWindow(new[] { ClassicWindowClassName, ModernWindowClassName }, remaining, processId);
        if (surface is null || !useClassicMenu)
        {
            return surface;
        }

        var surfaceWindow = WindowsFinder.ListByApp(processId).FirstOrDefault(window => window.Hwnd == surface.WindowHandle);
        if (surfaceWindow is null)
        {
            return null;
        }

        if (IsClassicWindow(surfaceWindow))
        {
            return surface;
        }

        remaining = timeoutMS - (int)deadline.ElapsedMilliseconds;
        if (remaining <= 0)
        {
            return null;
        }

        var showMore = FindVisibleMenuItem(surface, ShowMoreOptionsCaption, Math.Min(5_000, remaining));
        if (showMore is null)
        {
            return null;
        }

        try
        {
            showMore.Invoke(msPostAction: 0);
        }
        catch (AssertFailedException ex) when (
            IsTransientElementException(ex) ||
            !WindowsFinder.ListByApp(processId).Any(window => window.Hwnd == surface.WindowHandle))
        {
            return null;
        }

        remaining = timeoutMS - (int)deadline.ElapsedMilliseconds;
        return remaining > 0 ? WaitForWindow(ClassicWindowClassName, remaining, processId) : null;
    }

    public static void Dismiss(int escapeCount = 2)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(escapeCount);
        for (var index = 0; index < escapeCount; index++)
        {
            KeyboardHelper.SendKeys(Key.Esc);
        }
    }

    public static IReadOnlyList<string> ReadMenuNames(Session menu, int depth = 12, bool hideOffscreen = true)
    {
        ArgumentNullException.ThrowIfNull(menu);
        return ParseMenuNames(menu.Inspect(depth: depth, hideOffscreen: hideOffscreen));
    }

    /// <summary>Read captions from a classic popup's native HMENU; null means it is not readable yet.</summary>
    public static IReadOnlyList<string>? TryReadClassicItemCaptions(IntPtr menuWindow)
    {
        if (menuWindow == IntPtr.Zero ||
            SendMessageTimeoutW(menuWindow, GetMenuHandleMessage, IntPtr.Zero, IntPtr.Zero, AbortIfHung, 2_000, out var menu) == IntPtr.Zero ||
            menu == IntPtr.Zero)
        {
            return null;
        }

        var count = GetMenuItemCount(menu);
        if (count <= 0)
        {
            return null;
        }

        var captions = new List<string>(count);
        var buffer = new StringBuilder(512);
        for (var index = 0; index < count; index++)
        {
            buffer.Clear();
            var length = GetMenuStringW(menu, (uint)index, buffer, buffer.Capacity, MenuByPosition);
            captions.Add(NormalizeClassicCaption(length > 0 ? buffer.ToString() : string.Empty));
        }

        return captions;
    }

    public static bool IsTransientElementException(Exception exception) =>
        exception is AssertFailedException &&
        (exception.Message.Contains("stale_element", StringComparison.OrdinalIgnoreCase) ||
         exception.Message.Contains("element_not_found", StringComparison.OrdinalIgnoreCase) ||
         exception.Message.Contains("window_not_found", StringComparison.OrdinalIgnoreCase));

    internal static bool WindowClassMatches(string actual, string expected) =>
        expected.Equals(ModernWindowClassName, StringComparison.OrdinalIgnoreCase)
            ? actual.Contains(expected, StringComparison.OrdinalIgnoreCase)
            : actual.Equals(expected, StringComparison.OrdinalIgnoreCase);

    internal static Element? WaitForVisibleMenuItem(
        Func<IReadOnlyList<Element>> search,
        string caption,
        int timeoutMS,
        StringComparison comparison,
        int pollIntervalMS = 100,
        Func<Element, bool>? isDisplayed = null)
    {
        ArgumentNullException.ThrowIfNull(search);
        ArgumentException.ThrowIfNullOrWhiteSpace(caption);
        var ready = WaitHelper.WaitForStable(
            observe: () => search().FirstOrDefault(item =>
                item.Name.Equals(caption, comparison) &&
                item.ControlType.Equals("MenuItem", StringComparison.OrdinalIgnoreCase) &&
                item.Width > 0 &&
                item.Height > 0 &&
                (isDisplayed?.Invoke(item) ?? true)),
            isMatch: item => item is not null,
            timeoutMS: timeoutMS,
            pollIntervalMS: pollIntervalMS,
            shouldRetryException: IsTransientElementException);
        return ready.Succeeded ? ready.LastObservation : null;
    }

    internal static Session? WaitForSubmenuCore(
        Session root,
        string markerCaption,
        Func<IReadOnlyList<WindowsFinder.WindowInfo>> listWindows,
        Func<Session, string, int, StringComparison, Element?> findItem,
        int timeoutMS,
        int requiredConsecutiveMatches,
        StringComparison comparison,
        int pollIntervalMS = 200)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(markerCaption);
        var deadline = Stopwatch.StartNew();
        var ready = WaitHelper.WaitForStable(
            observe: () =>
            {
                // Each candidate's bounded UIA lookup runs before the outer polling interval.
                foreach (var window in listWindows().Where(window =>
                    window.Hwnd != root.WindowHandle && window.ProcessId == root.ProcessId && IsMenuWindow(window)))
                {
                    var remaining = Math.Min(500, timeoutMS - (int)deadline.ElapsedMilliseconds);
                    if (remaining <= 0)
                    {
                        return null;
                    }

                    var candidate = ExplorerControl.CreateSession(window, root.InitScope);
                    if (findItem(candidate, markerCaption, remaining, comparison) is not null)
                    {
                        return candidate;
                    }
                }

                return null;
            },
            isMatch: submenu => submenu is not null,
            timeoutMS: timeoutMS,
            requiredConsecutiveMatches: requiredConsecutiveMatches,
            pollIntervalMS: pollIntervalMS,
            shouldRetryException: IsTransientElementException);
        return ready.Succeeded ? ready.LastObservation : null;
    }

    internal static IReadOnlyList<string> ParseMenuNames(JsonElement root)
    {
        var names = new List<string>();
        CollectMenuNames(root, names);
        return names;
    }

    internal static string NormalizeClassicCaption(string caption)
    {
        var accelerator = caption.IndexOf('\t');
        var text = accelerator >= 0 ? caption[..accelerator] : caption;
        var normalized = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '&')
            {
                normalized.Append(text[index]);
            }
            else if (index + 1 < text.Length && text[index + 1] == '&')
            {
                normalized.Append('&');
                index++;
            }
        }

        return normalized.ToString().Trim();
    }

    private static void CollectMenuNames(JsonElement node, List<string> names)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String &&
                string.Equals(type.GetString(), "MenuItem", StringComparison.OrdinalIgnoreCase) &&
                node.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(name.GetString()))
            {
                names.Add(name.GetString()!);
            }

            foreach (var property in node.EnumerateObject())
            {
                if (property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                {
                    CollectMenuNames(property.Value, names);
                }
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in node.EnumerateArray())
            {
                CollectMenuNames(child, names);
            }
        }
    }

    private static void ValidateWindowSession(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.WindowHandle == 0 || session.ProcessId <= 0)
        {
            throw new ArgumentException("A window-scoped session with an owning process is required.", nameof(session));
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeoutW(
        IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeoutMS, out IntPtr result);

    [DllImport("user32.dll")]
    private static extern int GetMenuItemCount(IntPtr menu);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetMenuStringW(IntPtr menu, uint item, StringBuilder text, int maxCount, uint flags);
}
