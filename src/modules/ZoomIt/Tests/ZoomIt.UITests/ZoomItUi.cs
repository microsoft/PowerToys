// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.ZoomIt.UITests;

internal sealed class ZoomItUi(Session session, TestContext context)
{
    internal const string ProcessName = "PowerToys.ZoomIt";
    internal const string OverlayClass = "ZoomitClass";
    internal const string LiveZoomClass = "MagnifierClass";

    internal void Step(string message) => context.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");

    internal void Navigate()
    {
        session = Session.FromProcess("PowerToys.Settings", PowerToysModule.PowerToysSettings);
        Step("Navigating to ZoomIt Settings");
        if (!session.Has(By.AccessibilityId("ZoomItNavItem"), 500))
        {
            session.Find<NavigationViewItem>(By.AccessibilityId("SystemToolsNavItem")).Invoke(msPostAction: 0);
        }

        session.Find<NavigationViewItem>(By.AccessibilityId("ZoomItNavItem")).Invoke(msPostAction: 0);
        Assert.IsTrue(session.Has(By.AccessibilityId("ZoomItEnableToggleControlHeaderText"), 15_000), "ZoomIt Settings did not appear.");
        WaitForProcess(true);
    }

    internal T Control<T>(string cardId, string controlType, string? automationId = null)
        where T : Element, new()
    {
        if (!session.Has(By.AccessibilityId(cardId), 0))
        {
            var sectionId = cardId.StartsWith("ZoomItBreak", StringComparison.Ordinal) ? "ZoomItBreakShortcut"
                : cardId.StartsWith("ZoomItDemoType", StringComparison.Ordinal) ? "ZoomItDemoTypeShortcut"
                : cardId.StartsWith("ZoomItRecord", StringComparison.Ordinal) ? "ZoomItRecordShortcut"
                : cardId;
            session.Find<Element>(By.AccessibilityId(sectionId)).ScrollIntoView();
        }

        // Element.Find currently searches the whole session. Resolve the card's actual subtree
        // because every shortcut contains an EditButton and most controls have no individual ID.
        var card = session.Find<Element>(By.AccessibilityId(cardId), 15_000);
        var tree = WinappCli.InvokeJson("ui", "inspect", card.Selector, session.TargetFlag, session.TargetValue, "--json", "-d", "12");
        var matches = Nodes(tree).Where(node =>
            (Property(node, "type") == controlType || Property(node, "className") == controlType) &&
            Property(node, "className") != "Microsoft.UI.Xaml.Controls.Expander" &&
            (automationId is null || Property(node, "automationId") == automationId)).ToArray();
        Assert.HasCount(1, matches, $"Expected one {controlType}/{automationId} inside {cardId}. Tree: {tree}");
        var selector = Property(matches[0], "selector");
        Assert.IsFalse(string.IsNullOrWhiteSpace(selector), $"No selector for the control in {cardId}.");
        return session.Find<T>(By.Slug(selector));
    }

    internal Key[] Shortcut(string cardId)
    {
        var text = Control<Button>(cardId, "Button", "EditButton").HelpText;
        Step($"Reading {cardId}: {text}");
        Assert.IsFalse(string.IsNullOrWhiteSpace(text), $"No shortcut was exposed for {cardId}.");
        return text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(part => part.ToUpperInvariant() switch
        {
            "CTRL" or "CONTROL" => Key.Ctrl,
            "SHIFT" => Key.Shift,
            "ALT" => Key.Alt,
            "WIN" or "WINDOWS" => Key.LWin,
            _ when part.Length == 1 && char.IsAsciiDigit(part[0]) => Enum.Parse<Key>($"Num{part}"),
            _ => Enum.Parse<Key>(part, ignoreCase: true),
        }).ToArray();
    }

    internal void SetToggle(string cardId, bool value, string? registryName = null)
    {
        Step($"Setting {cardId} to {value}");
        var toggle = Control<ToggleSwitch>(cardId, "ToggleSwitch");
        if (toggle.IsOn != value)
        {
            toggle.Invoke(msPostAction: 0);
        }

        Assert.IsTrue(toggle.WaitForProperty("ToggleState", value ? "On" : "Off", 10_000), $"{cardId} did not change.");
        if (registryName is not null)
        {
            WaitForSetting(registryName, value ? 1 : 0);
        }
    }

    internal void SetCheck(string cardId, bool value, string registryName)
    {
        Step($"Setting {cardId} to {value}");
        var checkbox = Control<CheckBox>(cardId, "CheckBox");
        if (checkbox.IsChecked != value)
        {
            checkbox.Invoke(msPostAction: 0);
        }

        Assert.IsTrue(checkbox.WaitForProperty("ToggleState", value ? "On" : "Off", 10_000), $"{cardId} did not change.");
        WaitForSetting(registryName, value ? 1 : 0);
    }

    internal void SetSlider(string cardId, double value, string registryName, int expectedRegistryValue)
    {
        Step($"Setting {cardId} to {value}");
        var slider = Control<Slider>(cardId, "Slider");
        slider.SetValue(value);
        WaitForSetting(registryName, expectedRegistryValue);
    }

    internal void Select(string cardId, string caption, string registryName, int expectedValue)
    {
        Step($"Selecting {caption} in {cardId}");
        var combo = Control<ComboBox>(cardId, "ComboBox");
        combo.ScrollIntoView();
        combo.Invoke(msPostAction: 0);
        var settings = Session.FromProcess("PowerToys.Settings");
        var choices = settings.FindAll<Element>(By.Name(caption), 10_000)
            .Where(item => item.Name == caption && item.ControlType == "ListItem")
            .DistinctBy(item => item.Selector).ToArray();
        Assert.HasCount(1, choices, $"Expected the '{caption}' combo-box choice.");
        choices[0].Invoke(msPostAction: 0);
        WaitForSetting(registryName, expectedValue);
    }

    internal void PickFile(string cardId, string path, string registryName)
    {
        Step($"Choosing {path} for {cardId}");
        var dialog = OpenDialog(cardId);
        var filename = dialog.Find<TextBox>(By.AccessibilityId("1148"));
        filename.SetText(path);
        Assert.IsTrue(WindowControl.WaitForForeground(new IntPtr(dialog.WindowHandle), 10_000), "The file picker did not acquire foreground.");
        filename.Focus();
        KeyboardHelper.SendKeys(Key.Enter);
        WaitForSetting(registryName, path);
    }

    internal Session OpenDialog(string cardId)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var existing = WindowsFinder.WaitForWindowByApp("PowerToys.Settings", candidate => candidate.ClassName == "#32770", 100);
            if (existing is not null)
            {
                return existing;
            }

            var window = WindowsFinder.WaitForWindowByApp(
                "PowerToys.Settings",
                candidate => candidate.Title == "PowerToys Settings" && candidate.Width > 500 && candidate.Height > 300,
                10_000);
            Assert.IsNotNull(window, "The main Settings window was not available for its native dialog.");
            Assert.IsTrue(WindowControl.WaitForForeground(new IntPtr(window.WindowHandle), 10_000), "Settings did not acquire foreground for its native dialog.");
            var bounds = WindowHelper.GetVisibleBounds(new IntPtr(window.WindowHandle));
            var middleTop = bounds.Top + ((bounds.Bottom - bounds.Top) / 4);
            var middleBottom = bounds.Top + (((bounds.Bottom - bounds.Top) * 2) / 3);
            MouseHelper.MoveTo(bounds.Left + 50, bounds.Top + 50);
            Control<Button>(cardId, "Button").ScrollIntoView();
            Control<Button>(cardId, "Button").Focus();
            var ready = WaitHelper.WaitForStable(
                () => Control<Button>(cardId, "Button"),
                button => button is not null && button.Width > 0 && button.Height > 0 &&
                    button.Y + (button.Height / 2) >= middleTop && button.Y + (button.Height / 2) <= middleBottom &&
                    WindowControl.IsPointOwnedByWindow(new IntPtr(window.WindowHandle), button.X + (button.Width / 2), button.Y + (button.Height / 2)),
                20_000,
                2,
                recover: button =>
                {
                    WindowControl.TryBringToForeground(new IntPtr(window.WindowHandle));
                    MouseHelper.MoveTo(bounds.Right - 100, bounds.Top + ((bounds.Bottom - bounds.Top) / 2));
                    if (button is null || button.Y + (button.Height / 2) > middleBottom)
                    {
                        MouseHelper.ScrollDown();
                    }
                    else if (button.Y + (button.Height / 2) < middleTop)
                    {
                        MouseHelper.ScrollUp();
                    }
                });
            Assert.IsTrue(ready.Succeeded, $"The {cardId} button was not ready for physical input.");
            var button = ready.LastObservation!;
            Step($"Opening {cardId} native dialog (attempt {attempt})");
            MouseHelper.LeftClickAt(button.X + (button.Width / 2), button.Y + (button.Height / 2));
            var dialog = WindowsFinder.WaitForWindowByApp("PowerToys.Settings", candidate => candidate.ClassName == "#32770", 5_000);
            if (dialog is not null)
            {
                return dialog;
            }
        }

        Assert.Fail($"The native dialog for {cardId} did not open.");
        return null!;
    }

    internal void WaitForSetting(string name, object expected)
    {
        var result = WaitHelper.WaitForStable(() => ZoomItState.Read(name), value => Equals(value, expected), 10_000, 2);
        Assert.IsTrue(result.Succeeded, $"Registry setting {name}: expected '{expected}', actual '{result.LastObservation}'.");
    }

    internal Session Activate(Key[] shortcut, string windowClass = OverlayClass)
    {
        Step($"Activating {windowClass} with {string.Join("+", shortcut)}");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.IsFalse(IsVisible(windowClass), $"Cannot activate an already visible {windowClass}; the hotkey would toggle it off.");
            KeyboardHelper.SendKeys(shortcut);
            var window = WindowsFinder.WaitForWindowByApp(ProcessName, item => item.ClassName == windowClass, 10_000);
            if (window is not null)
            {
                return window;
            }
        }

        Assert.Fail($"No {windowClass} after activation. Windows: {string.Join("; ", WindowsFinder.ListByApp(ProcessName))}. Foreground: {WindowControl.GetForegroundWindowInfo()}");
        return null!;
    }

    internal void Exit(Key[]? shortcut = null, string windowClass = OverlayClass)
    {
        Step($"Exiting {windowClass}");
        if (shortcut is null)
        {
            var window = WindowsFinder.WaitForWindowByApp(ProcessName, item => item.ClassName == windowClass, 2_000);
            Assert.IsNotNull(window, $"No active {windowClass} to exit.");
            Assert.IsTrue(WindowControl.WaitForForeground(new IntPtr(window.WindowHandle), 10_000), "ZoomIt must own foreground before Escape.");
        }

        KeyboardHelper.SendKeys(shortcut ?? [Key.Esc]);
        Assert.IsTrue(WaitHelper.WaitForStable(() => IsVisible(windowClass), visible => !visible, 15_000, 3).Succeeded, $"{windowClass} did not exit.");
    }

    internal static bool IsVisible(string windowClass) =>
        WindowsFinder.ListByApp(ProcessName).Any(window => window.ClassName == windowClass);

    internal static void WaitForProcess(bool expected)
    {
        var result = WaitHelper.WaitForStable(
            () =>
            {
                var processes = Process.GetProcessesByName(ProcessName);
                try
                {
                    return processes.Length;
                }
                finally
                {
                    foreach (var process in processes)
                    {
                        process.Dispose();
                    }
                }
            },
            count => expected ? count == 1 : count == 0,
            15_000,
            3);
        Assert.IsTrue(result.Succeeded, $"Expected {(expected ? "one ZoomIt process" : "ZoomIt to exit")}; found {result.LastObservation}.");
    }

    internal static IEnumerable<JsonElement> Nodes(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("type", out _))
            {
                yield return root;
            }

            foreach (var property in root.EnumerateObject())
            {
                foreach (var child in Nodes(property.Value))
                {
                    yield return child;
                }
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                foreach (var child in Nodes(item))
                {
                    yield return child;
                }
            }
        }
    }

    internal static string Property(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;
}
