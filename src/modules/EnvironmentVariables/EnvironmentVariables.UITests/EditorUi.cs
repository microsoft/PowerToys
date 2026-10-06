// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.EnvironmentVariables.UITests;

internal sealed class EditorUi(Session session, TestContext context)
{
    internal Session Session { get; } = session;

    internal void Step(string text) => context.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {text}");

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

    internal JsonElement Inspect(Element parent, int depth = 8) =>
        WinappCli.InvokeJson("ui", "inspect", parent.Selector, Session.TargetFlag, Session.TargetValue, "--json", "-d", depth.ToString(CultureInfo.InvariantCulture));

    internal T Child<T>(Element parent, string className, string? name = null, string? automationId = null)
        where T : Element, new()
    {
        // Element.Find currently searches the whole session; inspect the actual subtree to avoid
        // confusing User, System, profile, and Applied rows that share the same variable name.
        var match = WaitForDescendant(parent, node =>
            Property(node, "className") == className &&
            (name is null || Property(node, "name") == name) &&
            (automationId is null || Property(node, "automationId") == automationId),
            $"{className} '{name ?? automationId}'");
        return Resolve<T>(match);
    }

    private JsonElement WaitForDescendant(Element parent, Func<JsonElement, bool> predicate, string description)
    {
        JsonElement tree = default;
        var result = WaitHelper.WaitForStable(
            () =>
            {
                tree = Inspect(parent);
                return Nodes(tree).Where(predicate).ToArray();
            },
            matches => matches?.Length == 1,
            timeoutMS: 15_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 200,
            shouldRetryException: exception => exception is AssertFailedException &&
                exception.Message.Contains("stale_element", StringComparison.OrdinalIgnoreCase));
        if (!result.Succeeded)
        {
            string preview = tree.ValueKind == JsonValueKind.Undefined ? "<unavailable>" : tree.GetRawText();
            if (preview.Length > 2_000)
            {
                preview = preview[..2_000] + "... (truncated)";
            }

            Assert.Fail($"Expected one {description} below {parent.Selector}; last count: {result.LastObservation?.Length}. Tree: {preview}");
        }

        return result.LastObservation![0];
    }

    private T Resolve<T>(JsonElement node)
        where T : Element, new()
    {
        string selector = Property(node, "selector");
        Assert.IsFalse(string.IsNullOrEmpty(selector), $"UIA node has no selector: {node}");
        return Session.Find<T>(By.Slug(selector));
    }

    internal void PathEntryMenu(int index, string action)
    {
        Step($"{action} PATH list entry {index}");
        var buttons = Session.FindAll<Button>(By.AccessibilityId("PathEntryOptionsButton")).ToArray();
        Assert.IsTrue(index >= 0 && index < buttons.Length, $"PATH entry {index} was not found among {buttons.Length} entries.");
        buttons[index].Invoke(msPostAction: 0);
        Menu($"{action}PathEntryMenuItem");
    }

    internal void SetPathEntry(int index, string value)
    {
        Step($"Setting PATH list entry {index} to {value}");
        var entries = Session.FindAll<TextBox>(By.AccessibilityId("VariableListEntryValueTextBox")).ToArray();
        Assert.IsTrue(index >= 0 && index < entries.Length, $"PATH entry {index} was not found.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(entries[index].Name), "List-entry editors must expose an accessible name.");
        entries[index].SetText(value);
        // The product commits list-entry edits in EditVariableValuesListTextBox_LostFocus.
        Session.Find<TextBox>(By.AccessibilityId("EditVariableDialogNameTxtBox")).Focus();
    }

    internal void AssertPathEditor(string expected)
    {
        Assert.IsTrue(
            Session.Find<TextBox>(By.AccessibilityId("EditVariableDialogValueTxtBox")).WaitForValue(expected, timeoutMS: 10_000),
            $"PATH list editing did not produce '{expected}'.");
    }

    internal void AssertPathList(string profile, params string[] expected)
    {
        var card = VariableCard(Expand(profile), "PATH");
        var text = Nodes(Inspect(card)).Where(node => Property(node, "type") == "Text")
            .Select(node => Property(node, "name")).Where(name => name != "PATH").ToArray();
        CollectionAssert.AreEqual(expected, text, "PATH must be displayed as separate ordered entries, not a semicolon-delimited value.");
    }

    internal Element Expander(string name)
    {
        var matches = Session.FindAll<Element>(By.Name(name), 10_000)
            .Where(element => element.ClassName == "SettingsExpander" && element.Name == name).ToArray();
        Assert.HasCount(1, matches, $"Expected the '{name}' variables expander.");
        return matches[0];
    }

    internal Element Expand(string name) => Expand(Expander(name));

    internal Element ExpandDefaultSet(EnvironmentVariableTarget target) =>
        Expand(Session.Find<Element>(By.AccessibilityId(target switch
        {
            EnvironmentVariableTarget.User => "UserVariablesExpander",
            EnvironmentVariableTarget.Machine => "SystemVariablesExpander",
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        })));

    private Element Expand(Element expander)
    {
        var header = Child<Element>(expander, "Microsoft.UI.Xaml.Controls.Expander");
        if (header.GetProperty("ExpandCollapseState") != "Expanded")
        {
            Step($"Expanding {expander.Name}");
            header.Invoke(msPostAction: 0);
            Assert.IsTrue(header.WaitForProperty("ExpandCollapseState", "Expanded", 10_000), $"'{expander.Name}' did not expand.");
        }

        return expander;
    }

    internal Element VariableCard(Element parent, string name)
    {
        var match = WaitForDescendant(parent, node => Property(node, "className") == "SettingsCard" &&
            Nodes(node).Any(child => Property(child, "type") == "Text" && Property(child, "name") == name),
            $"variable card for {name}");
        return Resolve<Element>(match);
    }

    internal void SaveDialog()
    {
        var save = Session.Find<Button>(By.AccessibilityId("PrimaryButton"));
        Assert.IsTrue(save.IsEnabled, "The dialog Save button should be enabled.");
        Step("Saving dialog");
        save.Invoke(msPostAction: 0);
        Assert.IsTrue(save.WaitForGone(10_000), "The saved dialog did not close.");
    }

    internal void AddUserVariableAndVerify(string name, string value)
    {
        Step($"Adding User variable {name}");
        Session.Find<Button>(By.AccessibilityId("AddDefaultVariableUserBtn")).Invoke(msPostAction: 0);
        Session.Find<TextBox>(By.AccessibilityId("DefaultVariableNameTextBox")).SetText(name);
        Session.Find<TextBox>(By.AccessibilityId("DefaultVariableValueTextBox")).SetText(value);
        SaveDialog();
        AssertUserVariable(name, value);
        AssertAppliedVariable(name, value);
    }

    internal void VariableMenu(string profile, string name, string action) => VariableMenu(Expand(profile), name, action);

    internal void VariableMenu(EnvironmentVariableTarget target, string name, string action) => VariableMenu(ExpandDefaultSet(target), name, action);

    private void VariableMenu(Element expander, string name, string action)
    {
        Step($"{action} variable {name} in {expander.Name}");
        var card = VariableCard(expander, name);
        Child<Button>(card, "Button", automationId: "VariableOptionsButton").Invoke(msPostAction: 0);
        Menu($"{action}VariableMenuItem");
    }

    // List-edit menu actions can re-realize controls that already exist, so retain their transition grace.
    private void Menu(string automationId) => Session.Find<Element>(By.AccessibilityId(automationId)).Invoke();

    internal void ConfirmRemoval()
    {
        Step("Confirming removal");
        var yes = Session.Find<Button>(By.AccessibilityId("PrimaryButton"));
        Assert.IsTrue(yes.IsEnabled, "The removal confirmation must be enabled.");
        yes.Invoke(msPostAction: 0);
        Assert.IsTrue(yes.WaitForGone(10_000), "The removal confirmation did not close.");
    }

    internal void CreateProfileAndVerify(string name, bool enabled, params (string Name, string Value)[] variables)
    {
        Step($"Creating profile {name}");
        Session.Find<Button>(By.AccessibilityId("NewProfileButton")).Invoke(msPostAction: 0);
        FillProfile(name, enabled, variables);
    }

    internal void EditProfileAndVerify(string name, params (string Name, string Value)[] variables)
    {
        ProfileMenu(name, "Edit");
        FillProfile(name, false, variables);
    }

    internal void ProfileMenu(string name, string action)
    {
        Step($"{action} profile {name}");
        var options = Child<Button>(Expander(name), "Button", automationId: "ProfileOptionsButton");
        Assert.IsFalse(string.IsNullOrWhiteSpace(options.Name), "Profile options must expose an accessible name.");
        options.Invoke(msPostAction: 0);
        Menu($"{action}ProfileMenuItem");
    }

    private void FillProfile(string name, bool enabled, (string Name, string Value)[] variables)
    {
        Session.Find<TextBox>(By.AccessibilityId("ProfileNameTextBox")).SetText(name);
        foreach (var variable in variables)
        {
            Step($"Adding {variable.Name} to profile {name}");
            Session.Find<Button>(By.AccessibilityId("AddProfileVariableButton")).Invoke(msPostAction: 0);
            Session.Find<TextBox>(By.AccessibilityId("AddNewVariableName")).SetText(variable.Name);
            Session.Find<TextBox>(By.AccessibilityId("AddNewVariableValue")).SetText(variable.Value);
            var add = Session.Find<Button>(By.AccessibilityId("ConfirmAddVariableBtn"));
            Assert.IsTrue(add.IsEnabled, $"Could not add {variable.Name} to {name}.");
            add.Invoke(msPostAction: 0);
            Assert.IsTrue(add.WaitForGone(10_000), "The Add variable flyout did not close.");
        }

        var toggle = Session.Find<ToggleSwitch>(By.AccessibilityId("ProfileEnabledToggle"));
        SetToggle(toggle, enabled);
        SaveDialog();
        AssertProfile(name, enabled, variables);
    }

    internal void SetProfileEnabledAndVerify(string name, bool enabled)
    {
        Step($"Setting profile {name} enabled={enabled}");
        SetToggle(Child<ToggleSwitch>(Expander(name), "ToggleSwitch"), enabled);
        AssertProfile(name, enabled);
    }

    internal static void SetToggle(ToggleSwitch toggle, bool enabled)
    {
        if (toggle.IsOn != enabled)
        {
            toggle.Invoke(msPostAction: 0);
        }

        Assert.IsTrue(toggle.WaitForProperty("ToggleState", enabled ? "On" : "Off", 10_000), "The toggle state did not update.");
    }

    internal void RemoveProfileAndVerify(string name)
    {
        ProfileMenu(name, "Remove");
        ConfirmRemoval();
        Wait(
            () => !ReadProfiles().EnumerateArray().Any(profile => profile.GetProperty("Name").GetString() == name),
            $"Profile {name} was not removed from profiles.json.");
        Assert.IsFalse(Session.FindAll<Element>(By.Name(name), 0).Any(element => element.ClassName == "SettingsExpander"), $"Profile {name} remains in the UI.");
    }

    internal void AssertProfile(string name, bool enabled, params (string Name, string Value)[] variables)
    {
        Wait(
            () => ReadProfiles().EnumerateArray().Any(profile =>
                profile.GetProperty("Name").GetString() == name &&
                profile.GetProperty("IsEnabled").GetBoolean() == enabled &&
                variables.All(variable => profile.GetProperty("Variables").EnumerateArray().Any(entry =>
                    entry.GetProperty("Name").GetString() == variable.Name &&
                    entry.GetProperty("Values").GetString() == variable.Value))),
            $"Profile {name}, enabled={enabled}, was not persisted with the expected variables.");
    }

    internal static JsonElement ReadProfiles()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(TestState.ProfilesPath));
        return document.RootElement.Clone();
    }

    internal static void AssertUserVariable(string name, string? value) =>
        Wait(() => TestState.ReadUserVariable(name) == value, $"User registry value {name} did not become '{value ?? "<absent>"}'.");

    internal void AssertAppliedVariable(string name, string? value)
    {
        Step($"Checking Applied variables: {name}={value ?? "<absent>"}");
        var panel = Session.Find<Element>(By.AccessibilityId("AppliedVariablesScrollViewer"));
        Wait(
            () =>
            {
                string[] text = Nodes(Inspect(panel, depth: 6)).Where(node => Property(node, "type") == "Text")
                    .Select(node => Property(node, "name")).ToArray();
                if (value is null)
                {
                    return !text.Contains(name, StringComparer.OrdinalIgnoreCase);
                }

                // The Applied-variable template exposes name/value TextBlocks in row order.
                // Windows variable names ignore casing; values must retain their exact text.
                return Enumerable.Range(0, Math.Max(0, text.Length - 1)).Any(index =>
                    text[index].Equals(name, StringComparison.OrdinalIgnoreCase) && text[index + 1] == value);
            },
            $"Applied variables did not show {name}={value ?? "<absent>"}.");
    }

    internal static void Wait(Func<bool> condition, string failure)
    {
        var result = WaitHelper.WaitForStable(
            condition,
            match => match,
            timeoutMS: 20_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 200,
            shouldRetryException: exception => exception is IOException or JsonException);
        Assert.IsTrue(result.Succeeded, $"{failure} Last transient error: {result.LastException}");
    }
}
