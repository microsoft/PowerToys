// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;
using Windows.Globalization;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;
using Button = Microsoft.PowerToys.UITest.Next.Button;
using CheckBox = Microsoft.PowerToys.UITest.Next.CheckBox;
using TextBox = Microsoft.PowerToys.UITest.Next.TextBox;
using WinClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace AdvancedPaste.UITests;

[TestClass]
[DoNotParallelize]
[TestCategory("AdvancedPaste")]
public sealed class AdvancedPasteSettingsTests : AdvancedPasteTestBase
{
    private const string EnableCard = "AdvancedPasteEnableToggleControlHeaderText";
    private const string MainShortcutCard = "AdvancedPasteUIShortcut";
    private const string MainShortcutProperty = "advanced-paste-ui-hotkey";
    private const string PreviewCard = "AdvancedPasteEnableClipboardPreview";
    private const string CloseOnBlurCard = "AdvancedPasteCloseAfterLosingFocus";
    private const string ShowAICard = "AdvancedPasteShowAIPasteSettingsCard";
    private const string ShortcutFixtureText = "PowerToys Advanced Paste offline shortcut";

    private static readonly bool[] BooleanStates = [false, true];
    private static readonly string[] ModifierNames = ["win", "ctrl", "alt", "shift"];

    private bool shortcutDialogOpened;

    [TestMethod]
    public Task ModuleToggleStopsAndRestartsAllHotkeys()
    {
        NavigateToSettings();
        var initiallyEnabled = ModuleToggle().IsOn;
        var properties = ReadProperties();
        var shortcuts = new List<(string Name, Key[] Keys)>
        {
            ("Advanced Paste", ReadShortcut(properties[MainShortcutProperty]!)),
            ("Plain text", ReadShortcut(properties["paste-as-plain-hotkey"]!)),
            ("Markdown", ReadShortcut(properties["paste-as-markdown-hotkey"]!)),
            ("JSON", ReadShortcut(properties["paste-as-json-hotkey"]!)),
        };
        foreach (var path in new[]
        {
            "image-to-text",
            "paste-as-file.paste-as-txt-file",
            "paste-as-file.paste-as-png-file",
            "paste-as-file.paste-as-html-file",
            "transcode.transcode-to-mp3",
            "transcode.transcode-to-mp4",
        })
        {
            shortcuts.Add((path, ReadShortcut(AdditionalAction(properties, path)["shortcut"]!)));
        }

        return RunAndRestoreAsync(
            () =>
            {
                Assert.IsTrue(initiallyEnabled, "The shared fixture must start with Advanced Paste enabled.");
                OpenAdvancedPaste();
                var originalProcess = WaitForStableModuleProcess();
                DismissAdvancedPaste();

                Step("Disabling Advanced Paste through Settings and waiting for the real module process to exit");
                SetModuleEnabled(false);
                foreach (var card in new[] { MainShortcutCard, "PasteAsPlainTextShortcut", "PasteAsMarkdownShortcut", "PasteAsJsonShortcut" })
                {
                    Assert.IsFalse(ShortcutButton(card).IsEnabled, $"'{card}' remained editable while Advanced Paste was disabled.");
                }

                foreach (var shortcut in shortcuts)
                {
                    Step($"Checking that the disabled module ignores the {shortcut.Name} hotkey");
                    Assert.IsNotEmpty(shortcut.Keys, $"The fixture did not configure the {shortcut.Name} hotkey.");
                    SetClipboardText(ShortcutFixtureText);
                    AssertShortcutInactive(shortcut.Keys, ShortcutFixtureText, requireStopped: true);
                }

                Step("Re-enabling Advanced Paste through Settings without restarting or reseeding the Runner");
                SetModuleEnabled(true);
                var restartedProcess = WaitForStableModuleProcess();
                Assert.AreNotEqual(originalProcess, restartedProcess, "Enabling the module did not create a new process.");
                OpenAdvancedPaste();
                Assert.AreEqual(restartedProcess, WaitForStableModuleProcess(), "The newly enabled module did not remain running.");
                DismissAdvancedPaste();

                SetClipboardText(ShortcutFixtureText);
                Target.Focus();
                SendShortcut(shortcuts[1].Keys);
                Target.AssertText(ShortcutFixtureText);
                Assert.IsFalse(IsAdvancedPasteVisible(), "The direct plain-text hotkey unexpectedly opened the Advanced Paste window.");
            },
            () => SetModuleEnabled(initiallyEnabled));
    }

    [TestMethod]
    public Task MainShortcutChangesLiveAndCancelPreservesTheSavedBinding()
    {
        NavigateToSettings();
        var original = ReadShortcutFromUI(MainShortcutCard);
        Key[] replacement = [Key.LWin, Key.Ctrl, Key.Alt, Key.F10];
        Key[] cancelled = [Key.LWin, Key.Ctrl, Key.Alt, Key.F11];
        var originalProcess = WaitForStableModuleProcess();

        return RunAndRestoreAsync(
            () =>
            {
                SetClipboardText(ShortcutFixtureText);
                SetShortcut(MainShortcutCard, MainShortcutProperty, replacement);
                OpenAdvancedPaste(replacement);
                DismissAdvancedPaste();
                AssertShortcutInactive(original, ShortcutFixtureText);

                Step("Editing another activation shortcut, then cancelling without saving");
                var saved = ReadProperties()[MainShortcutProperty]!.DeepClone();
                OpenShortcutDialog(MainShortcutCard);
                EnterShortcut(cancelled);
                CloseShortcutDialog("CloseButton");
                Assert.IsTrue(JsonNode.DeepEquals(saved, ReadProperties()[MainShortcutProperty]), "Cancel changed the persisted activation shortcut.");
                AssertShortcutOnUI(MainShortcutCard, replacement);
                AssertShortcutInactive(cancelled, ShortcutFixtureText);
                OpenAdvancedPaste(replacement);
                Assert.AreEqual(originalProcess, WaitForStableModuleProcess(), "Changing the shortcut restarted the module instead of updating it live.");
            },
            () => SetShortcut(MainShortcutCard, MainShortcutProperty, original));
    }

    [TestMethod]
    [DataRow("PasteAsPlainTextShortcut", "paste-as-plain-hotkey", Key.F2, false)]
    [DataRow("PasteAsMarkdownShortcut", "paste-as-markdown-hotkey", Key.F3, true)]
    [DataRow("PasteAsJsonShortcut", "paste-as-json-hotkey", Key.F4, true)]
    public Task DirectShortcutsChangeLiveAndOptionalBindingsCanBeCleared(string card, string property, Key key, bool canClear)
    {
        NavigateToSettings();
        var original = ReadShortcutFromUI(card);
        Key[] replacement = [Key.LWin, Key.Ctrl, Key.Alt, key];
        var originalProcess = WaitForStableModuleProcess();

        return RunAndRestoreAsync(
            () =>
            {
                SetShortcut(card, property, replacement);
                var expected = SetDirectShortcutFixture(property);
                Target.Focus();
                Step($"Pasting through the newly configured {property} shortcut");
                SendShortcut(replacement);
                AssertDirectShortcutResult(expected, property);
                Assert.IsFalse(IsAdvancedPasteVisible(), "A direct conversion unexpectedly opened the Advanced Paste window.");

                SetDirectShortcutFixture(property);
                var source = ReadClipboardText();
                AssertShortcutInactive(original, source);

                if (canClear)
                {
                    Step($"Clearing the optional {property} shortcut through its dialog");
                    OpenShortcutDialog(card);
                    CloseShortcutDialog("ClearBtn");
                    WaitForSetting(p => IsUnassignedShortcut(p[property]!), $"Clearing {property} did not persist an unassigned shortcut.");
                    Assert.AreEqual(ProductStrings.ConfigureShortcut, ShortcutButton(card).HelpText, "The cleared shortcut still displays an assigned chord.");
                    AssertShortcutInactive(replacement, source);
                }

                Assert.AreEqual(originalProcess, WaitForStableModuleProcess(), "Editing a direct shortcut restarted Advanced Paste.");
            },
            () => SetShortcut(card, property, original));
    }

    [TestMethod]
    public Task ResettingAnOptionalShortcutDoesNotChangeOtherBindings()
    {
        const string card = "PasteAsMarkdownShortcut";
        const string property = "paste-as-markdown-hotkey";
        NavigateToSettings();
        var original = ReadShortcutFromUI(card);
        var otherBindings = new[] { MainShortcutProperty, "paste-as-plain-hotkey", "paste-as-json-hotkey" }
            .ToDictionary(name => name, name => ReadProperties()[name]!.DeepClone());

        return RunAndRestoreAsync(
            () =>
            {
                Step("Resetting the Markdown shortcut to its unassigned product default");
                OpenShortcutDialog(card);
                CloseShortcutDialog("ResetBtn");
                WaitForSetting(p => IsUnassignedShortcut(p[property]!), "Reset did not restore the optional Markdown shortcut's unassigned default.");
                Assert.AreEqual(ProductStrings.ConfigureShortcut, ShortcutButton(card).HelpText, "Reset did not restore the shortcut control's unassigned display.");
                foreach (var binding in otherBindings)
                {
                    Assert.IsTrue(JsonNode.DeepEquals(binding.Value, ReadProperties()[binding.Key]), $"Reset changed the unrelated {binding.Key} binding.");
                }

                SetClipboardText(ShortcutFixtureText);
                AssertShortcutInactive(original, ShortcutFixtureText);
                OpenAdvancedPaste();
            },
            () => SetShortcut(card, property, original));
    }

    [TestMethod]
    public Task ClipboardPreviewUpdatesAndCanBeHiddenWithoutRestarting()
    {
        NavigateToSettings();
        var original = ReadBoolean("EnableClipboardPreview");
        const string first = "PowerToys Advanced Paste preview first";
        const string second = "PowerToys Advanced Paste preview second";

        return RunAndRestoreAsync(
            () =>
            {
                SetPreference(PreviewCard, "EnableClipboardPreview", true);
                SetClipboardText(first);
                var window = OpenAdvancedPaste();
                var originalProcess = WaitForStableModuleProcess();
                WaitUntil(
                    () => HasText(window, first),
                    "The enabled clipboard preview did not display the source text.",
                    shouldRetryException: AdvancedPasteUi.IsStaleElement);

                Step("Changing the clipboard while Advanced Paste remains open");
                SetClipboardText(second);
                WaitUntil(
                    () => HasText(window, second) && !HasText(window, first),
                    "The open clipboard preview did not replace its stale text.",
                    shouldRetryException: AdvancedPasteUi.IsStaleElement);

                SetPreference(PreviewCard, "EnableClipboardPreview", false);
                WaitUntil(
                    () => !HasText(window, second),
                    "Disabling clipboard preview did not hide the current clipboard content.",
                    shouldRetryException: AdvancedPasteUi.IsStaleElement);
                Assert.IsTrue(HasAction(window, ProductStrings.PasteAsPlainText), "Hiding the preview also removed the normal paste actions.");

                SetPreference(PreviewCard, "EnableClipboardPreview", true);
                WaitUntil(
                    () => HasText(window, second),
                    "Re-enabling clipboard preview did not restore the current content.",
                    shouldRetryException: AdvancedPasteUi.IsStaleElement);
                Assert.AreEqual(originalProcess, WaitForStableModuleProcess(), "Changing clipboard preview restarted the module.");
                Assert.AreEqual(second, ReadClipboardText(), "Changing preview visibility modified the clipboard.");
                Assert.AreEqual(string.Empty, Target.Text, "Changing preview visibility pasted into the destination.");
            },
            () => SetPreference(PreviewCard, "EnableClipboardPreview", original));
    }

    [TestMethod]
    public Task CloseOnLostFocusHonorsBothValuesAndKeepsTheProcessAlive()
    {
        NavigateToSettings();
        var original = ReadBoolean("CloseAfterLosingFocus");

        return RunAndRestoreAsync(
            () =>
            {
                SetClipboardText(ShortcutFixtureText);
                var originalProcess = WaitForStableModuleProcess();
                foreach (var closeOnBlur in BooleanStates)
                {
                    SetPreference(CloseOnBlurCard, "CloseAfterLosingFocus", closeOnBlur);
                    var window = OpenAdvancedPaste();
                    RequireForeground(window);
                    Step($"Moving focus to the destination with close-on-lost-focus set to {closeOnBlur}");
                    Target.Focus();
                    if (closeOnBlur)
                    {
                        WaitUntil(() => !IsAdvancedPasteVisible(), "Advanced Paste stayed open after losing focus with automatic close enabled.");
                    }
                    else
                    {
                        var closed = WaitHelper.WaitForStable(
                            IsAdvancedPasteVisible,
                            visible => !visible,
                            timeoutMS: 2_500);
                        Assert.IsFalse(closed.Succeeded, "Advanced Paste closed on lost focus even though automatic close was disabled.");
                    }

                    Assert.AreEqual(originalProcess, WaitForStableModuleProcess(), "Losing focus terminated or restarted the module process.");
                    DismissAdvancedPaste();
                }
            },
            () => SetPreference(CloseOnBlurCard, "CloseAfterLosingFocus", original));
    }

    [TestMethod]
    public Task AIInputVisibilityIsIndependentOfProviderAvailability()
    {
        NavigateToSettings();
        var originalShowAI = ReadBoolean("ShowAIPaste");
        var originalAIEnabled = ReadBoolean("IsAIEnabled");

        return RunAndRestoreAsync(
            () =>
            {
                AssertNoProviders();
                SetAIEnabled(false);
                SetPreference(ShowAICard, "ShowAIPaste", true);
                SetClipboardText(ShortcutFixtureText);
                var window = OpenAdvancedPaste();
                AssertAIUnavailable(window);

                Step("Enabling the AI setting without configuring, downloading, or invoking any provider");
                SetAIEnabled(true);
                AssertNoProviders();
                AssertAIUnavailable(window);

                SetPreference(ShowAICard, "ShowAIPaste", false);
                WaitUntil(
                    () => !window.Has(By.AccessibilityId("InputTxtBox"), 0) && !window.Has(By.AccessibilityId("SendBtn"), 0),
                    "Hiding the AI section left its prompt or submit button accessible.");
                SetPreference(ShowAICard, "ShowAIPaste", true);
                AssertAIUnavailable(window);
                SetAIEnabled(false);
                AssertAIUnavailable(window);
                Assert.AreEqual(ShortcutFixtureText, ReadClipboardText(), "AI configuration without a provider changed the clipboard.");
                Assert.AreEqual(string.Empty, Target.Text, "AI configuration without a provider pasted content.");
            },
            () =>
            {
                SetAIEnabled(originalAIEnabled);
                SetPreference(ShowAICard, "ShowAIPaste", originalShowAI);
            });
    }

    [TestMethod]
    public Task DisablingCustomPreviewAutomaticallyPastesOcr()
    {
        const string card = "AdvancedPasteShowCustomPreviewSettingsCard";
        NavigateToSettings();
        var original = Preference(card).IsChecked;

        return RunAndRestoreAsync(
            async () =>
            {
                Assert.IsTrue(
                    OcrEngine.IsLanguageSupported(new Language("en-US")),
                    "BLOCKED: the built-in Windows en-US OCR language is required; no external model or provider is used.");
                AssertNoProviders();
                SetPreference(card, "ShowCustomPreview", true);
                var source = Path.Combine(TestDirectory, "settings-ocr.png");
                Step("Creating a deterministic image for the Settings-driven offline OCR scenario");
                ClipboardFixtures.CreateImage(source, withText: true);
                await SetBitmapClipboard(source);
                var window = OpenAdvancedPaste();
                var process = WaitForStableModuleProcess();

                SetPreference(card, "ShowCustomPreview", false);
                Step("Reopening Advanced Paste from the destination after the Settings interaction");
                DismissAdvancedPaste();
                window = OpenAdvancedPaste();
                Step("Invoking Image to text after disabling custom preview, without accepting any preview");
                SelectAction(window, ProductStrings.ImageToText);
                WaitUntil(
                    () => Target.Text == ClipboardFixtures.OcrText,
                    "Image to text did not paste automatically after Settings disabled custom preview.",
                    timeoutMS: 30_000);
                Assert.AreEqual(ClipboardFixtures.OcrText, ReadClipboardText(), "Automatic OCR did not put the recognized text on the clipboard.");
                WaitUntil(
                    () => !IsAdvancedPasteVisible(),
                    "Disabling custom preview left an OCR preview or Advanced Paste window open.");
                Assert.AreEqual(process, WaitForStableModuleProcess(), "Changing custom preview restarted Advanced Paste rather than applying the setting live.");
                AssertNoProviders();
            },
            () => SetPreference(card, "ShowCustomPreview", original));
    }

    [TestMethod]
    [DataRow("AdvancedPasteShowCustomPreviewSettingsCard", "ShowCustomPreview")]
    [DataRow("AdvancedPasteAutoCopySelectionCustomAction", "AutoCopySelectionForCustomActionHotkey")]
    public Task OfflinePreferencesPersistThroughSettingsNavigation(string card, string property)
    {
        NavigateToSettings();
        var original = Preference(card).IsChecked;

        return RunAndRestoreAsync(
            () =>
            {
                foreach (var value in new[] { !original, original })
                {
                    SetPreference(card, property, value);
                    Step($"Navigating away and back to verify persisted {property}={value}");
                    NavigateToGeneralSettings();
                    NavigateToSettings();
                    Assert.AreEqual(value, Preference(card).IsChecked, $"The reloaded Settings page did not retain {property}.");
                    Assert.AreEqual(value, ReadBoolean(property), $"The persisted {property} value changed during navigation.");
                    AssertNoProviders();
                }
            },
            () => SetPreference(card, property, original));
    }

    [TestMethod]
    [DataRow("ImageToText", "image-to-text", "image", ProductStrings.ImageToText)]
    [DataRow("PasteAsTxtFile", "paste-as-file.paste-as-txt-file", "text", ProductStrings.PasteAsTxtFile)]
    [DataRow("PasteAsPngFile", "paste-as-file.paste-as-png-file", "image", ProductStrings.PasteAsPngFile)]
    [DataRow("PasteAsHtmlFile", "paste-as-file.paste-as-html-file", "html", ProductStrings.PasteAsHtmlFile)]
    [DataRow("TranscodeToMp3", "transcode.transcode-to-mp3", "video", ProductStrings.TranscodeToMp3)]
    [DataRow("TranscodeToMp4", "transcode.transcode-to-mp4", "video", ProductStrings.TranscodeToMp4)]
    public Task OfflineActionVisibilityChangesLive(string card, string propertyPath, string fixture, string action)
    {
        NavigateToSettings();
        var original = AdditionalAction(ReadProperties(), propertyPath)["isShown"]!.GetValue<bool>();
        return RunAndRestoreAsync(
            async () =>
            {
                await SetActionClipboardAsync(fixture);
                SetActionShown(card, propertyPath, true);
                var window = OpenAdvancedPaste();
                var process = WaitForStableModuleProcess();
                WaitForActions(window, [action], visible: true);
                SetActionShown(card, propertyPath, false);
                WaitForActions(window, [action], visible: false);
                SetActionShown(card, propertyPath, true);
                WaitForActions(window, [action], visible: true);
                Assert.AreEqual(process, WaitForStableModuleProcess(), "Changing an action's visibility restarted Advanced Paste.");
                Assert.AreEqual(string.Empty, Target.Text, "Showing or hiding an action unexpectedly pasted content.");
            },
            () => SetActionShown(card, propertyPath, original));
    }

    [TestMethod]
    [DataRow("PasteAsFile", "paste-as-file", "mixed")]
    [DataRow("Transcode", "transcode", "video")]
    public Task OfflineActionGroupsHideAndRestoreAllChildren(string card, string propertyPath, string fixture)
    {
        NavigateToSettings();
        var original = AdditionalAction(ReadProperties(), propertyPath)["isShown"]!.GetValue<bool>();
        string[] actions = card == "PasteAsFile"
            ? [ProductStrings.PasteAsTxtFile, ProductStrings.PasteAsPngFile, ProductStrings.PasteAsHtmlFile]
            : [ProductStrings.TranscodeToMp3, ProductStrings.TranscodeToMp4];
        var originalChildren = AdditionalAction(ReadProperties(), propertyPath).DeepClone();

        return RunAndRestoreAsync(
            async () =>
            {
                await SetActionClipboardAsync(fixture);
                SetActionShown(card, propertyPath, true);
                var window = OpenAdvancedPaste();
                var process = WaitForStableModuleProcess();
                WaitForActions(window, actions, visible: true);
                SetActionShown(card, propertyPath, false);
                WaitForActions(window, actions, visible: false);
                SetActionShown(card, propertyPath, true);
                WaitForActions(window, actions, visible: true);
                foreach (var child in originalChildren.AsObject().Where(pair => pair.Key != "isShown"))
                {
                    Assert.IsTrue(
                        JsonNode.DeepEquals(child.Value, AdditionalAction(ReadProperties(), propertyPath)[child.Key]),
                        $"Toggling the {card} group changed child configuration '{child.Key}'.");
                }

                Assert.AreEqual(process, WaitForStableModuleProcess(), "Changing a group restarted Advanced Paste.");
            },
            () => SetActionShown(card, propertyPath, original));
    }

    private Task RunAndRestoreAsync(Action scenario, Action restore) =>
        RunAndRestoreAsync(
            () =>
            {
                scenario();
                return Task.CompletedTask;
            },
            restore);

    private async Task RunAndRestoreAsync(Func<Task> scenario, Action restore)
    {
        try
        {
            await scenario();
        }
        catch
        {
            await CaptureFailureArtifactsAsync();
            throw;
        }
        finally
        {
            Step("Restoring this scenario's Settings UI changes");
            if (shortcutDialogOpened && Session.Has(By.AccessibilityId("ResetBtn"), 0))
            {
                CloseShortcutDialog("CloseButton");
            }

            DismissAdvancedPaste();
            restore();
        }
    }

    private ToggleSwitch ModuleToggle() => AdvancedPasteUi.CardControl<ToggleSwitch>(Session, EnableCard, "Button", className: "ToggleSwitch");

    private CheckBox Preference(string card) => AdvancedPasteUi.CardControl<CheckBox>(Session, card, "CheckBox");

    private Button ShortcutButton(string card) => AdvancedPasteUi.CardControl<Button>(Session, card, "Button", automationId: "EditButton");

    private static bool ReadBoolean(string property) => ReadProperties()[property]!["value"]!.GetValue<bool>();

    private void SetModuleEnabled(bool enabled)
    {
        Step($"Setting module enabled={enabled} through Settings");
        var toggle = ModuleToggle();
        if (toggle.IsOn != enabled)
        {
            toggle.Invoke(msPostAction: 0);
        }

        Assert.IsTrue(ModuleToggle().WaitForProperty("ToggleState", enabled ? "On" : "Off", 10_000), "The module enable switch did not reach the requested state.");
        if (enabled)
        {
            WaitForStableModuleProcess();
        }
        else
        {
            WaitUntil(() => GetModuleProcessIds().Length == 0, "Disabling Advanced Paste in Settings did not stop its process. Verify authenticated Settings-to-Runner IPC.");
            Assert.IsFalse(IsAdvancedPasteVisible(), "The disabled module left an Advanced Paste window open.");
        }
    }

    private static int WaitForStableModuleProcess()
    {
        int? previous = null;
        var result = WaitHelper.WaitForStable(
            GetModuleProcessIds,
            ids =>
            {
                var current = ids is { Length: 1 } ? ids[0] : (int?)null;
                var stable = current.HasValue && current == previous;
                previous = current;
                return stable;
            },
            timeoutMS: 30_000,
            requiredConsecutiveMatches: 3,
            pollIntervalMS: 250);
        Assert.IsTrue(result.Succeeded, $"Advanced Paste did not reach one stable running process. Last process IDs: {string.Join(", ", result.LastObservation ?? [])}.");
        return result.LastObservation![0];
    }

    private void SetPreference(string card, string property, bool value)
    {
        Step($"Setting {property}={value} through its Settings checkbox");
        var checkbox = Preference(card);
        Assert.IsTrue(checkbox.IsEnabled, $"The {property} Settings checkbox is unexpectedly disabled.");
        if (checkbox.IsChecked != value)
        {
            checkbox.Invoke(msPostAction: 0);
        }

        Assert.IsTrue(Preference(card).WaitForProperty("ToggleState", value ? "On" : "Off", 10_000), $"The {property} checkbox did not change.");
        WaitForSetting(p => p[property]!["value"]!.GetValue<bool>() == value, $"Settings did not persist {property}={value}.");
    }

    private void SetAIEnabled(bool enabled)
    {
        Step($"Setting IsAIEnabled={enabled} without configuring a provider");
        var toggle = Session.Find<ToggleSwitch>(By.AccessibilityId("AdvancedPaste_EnableAIToggle"));
        Assert.IsTrue(toggle.IsEnabled, "BLOCKED: policy or module state prevents exercising the AI Settings toggle.");
        if (toggle.IsOn != enabled)
        {
            toggle.Invoke(msPostAction: 0);
        }

        Assert.IsTrue(
            Session.Find<ToggleSwitch>(By.AccessibilityId("AdvancedPaste_EnableAIToggle")).WaitForProperty("ToggleState", enabled ? "On" : "Off", 10_000),
            "The AI toggle did not reach the requested state.");
        WaitForSetting(p => p["IsAIEnabled"]!["value"]!.GetValue<bool>() == enabled, "The AI enabled setting was not persisted.");
    }

    private static void AssertNoProviders()
    {
        Assert.HasCount(0, ReadProperties()["paste-ai-configuration"]!["providers"]!.AsArray(), "Offline Settings tests must not configure any AI provider.");
    }

    private static void AssertAIUnavailable(Session window)
    {
        var prompt = window.Find<TextBox>(By.AccessibilityId("InputTxtBox"), 15_000);
        Assert.IsFalse(prompt.IsEnabled, "The AI prompt is enabled even though no provider is configured.");

        // The prompt's GetValue falls back to its placeholder; it is not a query-state oracle.
        Assert.IsFalse(window.Has<Button>(By.AccessibilityId("SendBtn"), 0), "The empty, disabled AI prompt exposed a submit button.");
    }

    private void OpenShortcutDialog(string card)
    {
        Step($"Opening the shortcut editor for {card}");
        shortcutDialogOpened = true;
        ShortcutButton(card).Invoke(msPostAction: 0);
        Session.Find(By.AccessibilityId("ResetBtn"), 10_000);
    }

    private void EnterShortcut(Key[] keys)
    {
        Step($"Entering shortcut {string.Join(" + ", keys)}");
        RequireForeground(Session);

        // The shortcut hook ignores keyboard input when a ContentDialog Button has focus.
        // Its Reset hyperlink is focusable without invoking it, and remains inside the editor.
        var captureFocus = Session.Find(By.AccessibilityId("ResetBtn"));
        captureFocus.Focus();
        Assert.IsTrue(captureFocus.WaitForProperty("HasKeyboardFocus", "true", 5_000), "The shortcut editor did not receive keyboard focus.");
        SendShortcut(keys);
        WaitUntil(() => Session.Find<Button>(By.AccessibilityId("PrimaryButton")).IsEnabled, "The shortcut editor did not accept a valid chord.");
    }

    private void CloseShortcutDialog(string buttonId)
    {
        Step($"Invoking {buttonId} in the shortcut dialog");
        Session.Find(By.AccessibilityId(buttonId)).Invoke(msPostAction: 0);
        WaitUntil(() => !Session.Has(By.AccessibilityId("ResetBtn"), 0), "The shortcut dialog did not close.");
        shortcutDialogOpened = false;
    }

    private void SetShortcut(string card, string property, Key[] keys)
    {
        OpenShortcutDialog(card);
        if (keys.Length == 0)
        {
            CloseShortcutDialog("ClearBtn");
        }
        else
        {
            EnterShortcut(keys);
            CloseShortcutDialog("PrimaryButton");
        }

        WaitForSetting(
            p => keys.Length == 0 ? IsUnassignedShortcut(p[property]!) : ReadShortcut(p[property]!).Order().SequenceEqual(keys.Order()),
            $"Settings did not persist the requested {property} chord.");
        if (keys.Length > 0)
        {
            AssertShortcutOnUI(card, keys);
        }
    }

    private Key[] ReadShortcutFromUI(string card)
    {
        var text = ShortcutButton(card).HelpText;
        var keys = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.ToUpperInvariant() switch
            {
                "WIN" or "WINDOWS" => Key.LWin,
                "CTRL" or "CONTROL" => Key.Ctrl,
                "ALT" => Key.Alt,
                "SHIFT" => Key.Shift,
                _ => Enum.TryParse<Key>(part, ignoreCase: true, out var key)
                    ? key
                    : throw new AssertFailedException($"Unrecognized shortcut component '{part}' in {card}."),
            })
            .ToArray();
        Assert.IsTrue(keys.Length >= 2, $"The {card} accessible shortcut is not a valid chord.");
        return keys;
    }

    private void AssertShortcutOnUI(string card, Key[] expected) =>
        CollectionAssert.AreEquivalent(expected, ReadShortcutFromUI(card), $"The {card} shortcut display does not match the saved binding.");

    private static bool IsUnassignedShortcut(JsonNode shortcut) =>
        shortcut["code"]!.GetValue<int>() == 0 &&
        ModifierNames.All(name => !shortcut[name]!.GetValue<bool>());

    private static Key[] ReadShortcut(JsonNode shortcut)
    {
        var code = shortcut["code"]!.GetValue<int>();
        if (code == 0)
        {
            return [];
        }

        var keys = new List<Key>();
        foreach (var (name, key) in new[] { ("win", Key.LWin), ("ctrl", Key.Ctrl), ("alt", Key.Alt), ("shift", Key.Shift) })
        {
            if (shortcut[name]!.GetValue<bool>())
            {
                keys.Add(key);
            }
        }

        keys.Add((Key)code);
        return keys.ToArray();
    }

    private void AssertShortcutInactive(Key[] shortcut, string source, bool requireStopped = false)
    {
        Target.Clear();
        Target.Focus();
        var formats = ReadClipboardFormats();
        Step($"Verifying that {string.Join(" + ", shortcut)} has no UI, paste, or clipboard effect");
        SendShortcut(shortcut);
        var unexpectedEffect = WaitHelper.WaitForStable(
            () => (
                Visible: IsAdvancedPasteVisible(),
                TargetText: Target.Text,
                ClipboardText: ReadClipboardText(),
                Formats: ReadClipboardFormats(),
                ProcessIds: GetModuleProcessIds()),
            state => state.Visible || state.TargetText.Length != 0 ||
                state.ClipboardText != source || !state.Formats.SequenceEqual(formats) ||
                (requireStopped && state.ProcessIds.Length != 0),
            timeoutMS: 2_500);
        var observed = unexpectedEffect.LastObservation;
        Assert.IsFalse(
            unexpectedEffect.Succeeded,
            $"An inactive shortcut had an effect: visible={observed.Visible}; processes={string.Join(", ", observed.ProcessIds)}; " +
            $"pasted characters={observed.TargetText.Length}; clipboard matches={observed.ClipboardText == source}; " +
            $"formats before=[{string.Join(", ", formats)}], after=[{string.Join(", ", observed.Formats)}].");
        Assert.AreEqual(source, ReadClipboardText(), "An inactive shortcut changed the text clipboard.");
        Assert.AreEqual(string.Empty, Target.Text, "An inactive shortcut pasted into the destination.");
    }

    private string SetDirectShortcutFixture(string property)
    {
        Target.Clear();
        if (property == "paste-as-plain-hotkey")
        {
            SetHtmlClipboard($"<p><b>{ShortcutFixtureText}</b></p>", ShortcutFixtureText);
            return ShortcutFixtureText;
        }

        var markdown = property == "paste-as-markdown-hotkey";
        SetClipboardText(File.ReadAllText(FixturePath(markdown ? "PasteAsMarkdownFile.html" : "PasteAsJsonFile.xml")));
        return File.ReadAllText(FixturePath(markdown ? "PasteAsMarkdownResultFile.txt" : "PasteAsJsonResultFile.txt"));
    }

    private void AssertDirectShortcutResult(string expected, string property)
    {
        if (property == "paste-as-json-hotkey")
        {
            var expectedJson = JsonNode.Parse(expected);
            WaitUntil(
                () => JsonNode.DeepEquals(expectedJson, JsonNode.Parse(Target.Text)),
                "The newly configured JSON shortcut did not paste the expected offline XML conversion.",
                shouldRetryException: exception => exception is JsonException);
            Assert.IsTrue(
                JsonNode.DeepEquals(expectedJson, JsonNode.Parse(ReadClipboardText())),
                "The JSON clipboard does not match the pasted result.");
            return;
        }

        var markdown = property == "paste-as-markdown-hotkey";
        var normalized = markdown ? NormalizeMarkdown(expected) : expected;
        WaitUntil(
            () => (markdown ? NormalizeMarkdown(Target.Text) : Target.Text) == normalized,
            $"The newly configured {property} shortcut did not paste its expected offline conversion.");
        var clipboard = ReadClipboardText();
        Assert.AreEqual(normalized, markdown ? NormalizeMarkdown(clipboard) : clipboard, "The converted clipboard does not match the pasted result.");
        if (property == "paste-as-plain-hotkey")
        {
            Assert.IsFalse(Target.IsBold(0, ShortcutFixtureText.Length), "The direct plain-text shortcut retained the source HTML formatting.");
        }
    }

    private static string NormalizeMarkdown(string text) =>
        string.Join("\n", text.ReplaceLineEndings("\n").Split('\n').Select(line => string.IsNullOrWhiteSpace(line) ? string.Empty : line)).TrimEnd();

    private static void RequireForeground(Session window) =>
        TestWindow.SelectFromTaskbar(new IntPtr(window.WindowHandle), window.ProcessName);

    private static bool HasText(Session window, string text) =>
        window.FindAll<TextBlock>(By.Name(text), 0).Any(element => element.Name == text);

    private static bool HasAction(Session window, string name) =>
        window.FindAll<Element>(By.Name(name), 0).Any(element =>
            element.ControlType.Equals("ListItem", StringComparison.OrdinalIgnoreCase) &&
            element.Name.StartsWith(name + " (", StringComparison.Ordinal));

    private void WaitForActions(Session window, string[] names, bool visible)
    {
        Step($"Waiting for offline actions to be {(visible ? "shown" : "hidden")}: {string.Join(", ", names)}");
        WaitUntil(
            () => names.All(name => HasAction(window, name) == visible),
            "The running Advanced Paste action list did not reflect its Settings visibility.",
            shouldRetryException: AdvancedPasteUi.IsStaleElement);
        Assert.IsTrue(IsAdvancedPasteVisible(), "Changing action visibility unexpectedly closed the Advanced Paste window.");
    }

    private static JsonNode AdditionalAction(JsonObject properties, string path)
    {
        var action = properties["additional-actions"]!;
        foreach (var name in path.Split('.'))
        {
            action = action[name]!;
        }

        return action;
    }

    private void SetActionShown(string card, string propertyPath, bool shown)
    {
        Step($"Setting {propertyPath}.isShown={shown} through Settings");
        var toggle = AdvancedPasteUi.CardControl<ToggleSwitch>(Session, card, "Button", className: "ToggleSwitch");
        Assert.IsTrue(toggle.IsEnabled, $"The {card} visibility switch is not enabled.");
        if (toggle.IsOn != shown)
        {
            toggle.Invoke(msPostAction: 0);
        }

        Assert.IsTrue(
            AdvancedPasteUi.CardControl<ToggleSwitch>(Session, card, "Button", className: "ToggleSwitch").WaitForProperty("ToggleState", shown ? "On" : "Off", 10_000),
            $"The {card} visibility switch did not change.");
        WaitForSetting(p => AdditionalAction(p, propertyPath)["isShown"]!.GetValue<bool>() == shown, $"The {propertyPath} visibility was not persisted.");
    }

    private async Task SetActionClipboardAsync(string fixture)
    {
        if (fixture == "video")
        {
            var path = Path.Combine(TestDirectory, "settings-video.mp4");
            await ClipboardFixtures.CreateVideoAsync(path);
            SetFileClipboard(path);
        }
        else if (fixture is "image" or "mixed")
        {
            var path = Path.Combine(TestDirectory, "settings-image.png");
            ClipboardFixtures.CreateImage(path);
            if (fixture == "image")
            {
                await SetBitmapClipboard(path);
            }
            else
            {
                var package = new DataPackage();
                package.SetText(ShortcutFixtureText);
                package.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat($"<p>{ShortcutFixtureText}</p>"));
                package.SetBitmap(RandomAccessStreamReference.CreateFromFile(await StorageFile.GetFileFromPathAsync(path)));
                SetClipboard(package);
                foreach (var format in new[] { StandardDataFormats.Text, StandardDataFormats.Html, StandardDataFormats.Bitmap })
                {
                    Assert.IsTrue(WinClipboard.GetContent().Contains(format), $"The combined clipboard fixture is missing {format}.");
                }
            }
        }
        else if (fixture == "html")
        {
            SetHtmlClipboard($"<p>{ShortcutFixtureText}</p>", ShortcutFixtureText);
        }
        else
        {
            Assert.AreEqual("text", fixture, "Unknown offline action clipboard fixture.");
            SetClipboardText(ShortcutFixtureText);
        }
    }
}
