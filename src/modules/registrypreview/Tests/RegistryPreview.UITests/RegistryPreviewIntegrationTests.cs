// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.RegistryPreview.UITests;

/// <summary>Settings, Explorer context-menu, keyboard-shortcut, and file-association scenarios.</summary>
/// <remarks>
/// Covers checklist items 6, 8, 9 and the implemented OpenWith registration portion of item 11 in
/// issue #40675. The true-default-handler assertion in item 11 is stale: Windows owns UserChoice,
/// while the product setting only registers PowerToys.RegistryPreview in OpenWithProgIDs.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class RegistryPreviewIntegrationTests : RegistryPreviewTestBase
{
    private const string DefaultAppToggleName = "Make Registry Preview the default app for .reg files";

    private Session? settingsToRestore;
    private bool? moduleEnabledToRestore;
    private bool? defaultAppEnabledToRestore;

    [TestCleanup]
    public async Task RestoreIntegrationState()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync(TimeSpan.FromSeconds(2));
        KeyboardHelper.SendKeys(Key.Esc);
        CloseRegistryPreviewWindows();
        CloseExplorerFileWindows();

        var restorationFailures = new List<Exception>();
        if (defaultAppEnabledToRestore.HasValue && settingsToRestore is not null)
        {
            try
            {
                var toggle = FindExact<ToggleSwitch>(settingsToRestore, DefaultAppToggleName, ActionTimeoutMS);
                Assert.IsNotNull(toggle, "The default-app switch was unavailable during state restoration.");
                SetDefaultApp(toggle!, settingsToRestore, defaultAppEnabledToRestore.Value);
            }
            catch (Exception ex)
            {
                restorationFailures.Add(ex);
            }
        }

        if (moduleEnabledToRestore.HasValue && settingsToRestore is not null)
        {
            try
            {
                SetModuleEnabled(settingsToRestore, moduleEnabledToRestore.Value);
            }
            catch (Exception ex)
            {
                restorationFailures.Add(ex);
            }
        }

        if (restorationFailures.Count > 0)
        {
            throw new AggregateException("Registry Preview state restoration failed.", restorationFailures);
        }
    }

    [TestMethod("RegistryPreview.Integration.ContextMenuEnabledState")]
    [TestCategory("RegistryPreview")]
    public void ContextMenuTracksEnabledStateAndLaunchesPreview()
    {
        var folder = CreateTestFolder();
        var keyPath = CreateIsolatedRegistryKeyPath();
        var fixture = CreateRegFixture(folder, "context-menu.reg", keyPath);
        var settings = NavigateToRegistryPreviewSettings();
        RememberModuleState(settings);

        Step("Disabling Registry Preview and checking the classic context menu");
        SetModuleEnabled(settings, false);
        var explorer = OpenExplorer(folder);
        var menu = OpenClassicContextMenu(explorer, fixture);
        Assert.IsNotNull(
            FindExact<Element>(menu, "Edit", timeoutMS: 2_000),
            "Explorer's classic context menu was not enumerable; the negative Preview assertion would be vacuous.");
        Assert.IsNull(
            FindExact<Element>(menu, ContextMenuCaption, timeoutMS: 2_000),
            $"Explorer still offered '{ContextMenuCaption}' while Registry Preview was disabled.");
        KeyboardHelper.SendKeys(Key.Esc);

        Step("Enabling Registry Preview and invoking its classic context-menu command");
        SetModuleEnabled(settings, true);
        explorer = OpenExplorer(folder);
        var preview = WaitForClassicContextMenuItem(explorer, fixture, ContextMenuCaption, timeoutMS: WindowTimeoutMS);
        Assert.IsNotNull(preview, $"Explorer did not offer '{ContextMenuCaption}' after Registry Preview was enabled.");
        preview!.Invoke(msPostAction: 300);

        var registryPreview = WindowsFinder.WaitForWindowByApp(
            RegistryPreviewProcessName,
            candidate => candidate.Width > 0 && candidate.Height > 0,
            timeoutMS: WindowTimeoutMS);
        Assert.IsNotNull(registryPreview, "The Explorer Preview command did not launch Registry Preview.");
        var editor = Session.FromProcess(RegistryPreviewProcessName, PowerToysModule.RegistryPreview, WindowTimeoutMS);
        AssertExactElement(editor, Path.GetFileName(keyPath), "Context-menu-launched visual-tree key");
    }

    [TestMethod("RegistryPreview.Integration.SettingsLaunchAndOpenShortcut")]
    [TestCategory("RegistryPreview")]
    public void SettingsLaunchAndOpenShortcutWorkWhileEnabled()
    {
        var folder = CreateTestFolder();
        var keyPath = CreateIsolatedRegistryKeyPath();
        var fixture = CreateRegFixture(folder, "settings-shortcut.reg", keyPath);
        var settings = NavigateToRegistryPreviewSettings();
        RememberModuleState(settings);

        SetModuleEnabled(settings, true);
        CloseRegistryPreviewWindows();

        Step("Launching Registry Preview from its Settings page");
        var registryPreview = LaunchRegistryPreviewFromControl(
            settings,
            By.AccessibilityId("RegistryPreviewLaunchButtonControl"),
            "Settings");

        Step("Opening a registry file with the Ctrl+O keyboard shortcut");
        TryBringRegistryPreviewForward();
        KeyboardHelper.SendKeys(Key.Ctrl, Key.O);
        CompleteFileDialogWithPath(fixture);
        AssertExactElement(registryPreview, Path.GetFileName(keyPath), "Ctrl+O-opened visual-tree key");
    }

    [TestMethod("RegistryPreview.Integration.DefaultRegAppRegistration")]
    [TestCategory("RegistryPreview")]
    public void DefaultAppSettingRegistersTheHandlerAndRestoresRegedit()
    {
        var settings = NavigateToRegistryPreviewSettings();
        RememberModuleState(settings);
        SetModuleEnabled(settings, true);

        var toggle = FindExact<ToggleSwitch>(settings, DefaultAppToggleName, ActionTimeoutMS);
        Assert.IsNotNull(toggle, "The Registry Preview default-app switch was not exposed.");
        defaultAppEnabledToRestore = toggle!.IsOn;

        SetDefaultApp(toggle, settings, false);
        var disabledExecutable = QueryRegFileExecutable();
        Assert.IsTrue(
            string.Equals(Path.GetFileName(disabledExecutable), "regedit.exe", StringComparison.OrdinalIgnoreCase),
            $"Disabling Registry Preview did not restore the system .reg handler: '{disabledExecutable ?? "<none>"}'.");

        SetDefaultApp(toggle, settings, true);
        var registeredCommand = QueryRegistryPreviewOpenCommand();
        Assert.IsNotNull(registeredCommand, "The Registry Preview ProgID did not register an open command.");
        StringAssert.Contains(registeredCommand!, "PowerToys.RegistryPreview.exe", "The registered .reg open command targets the wrong executable.");
        StringAssert.Contains(registeredCommand, "%1", "The registered .reg open command does not forward the selected file.");

        SetDefaultApp(toggle, settings, false);
        Assert.IsTrue(
            settings.WaitFor(
                () => string.Equals(Path.GetFileName(QueryRegFileExecutable()), "regedit.exe", StringComparison.OrdinalIgnoreCase),
                ActionTimeoutMS,
                pollIntervalMS: 250),
            $"Disabling the setting did not restore regedit.exe: '{QueryRegFileExecutable() ?? "<none>"}'.");

        TestContext.WriteLine(
            "Checklist item 11's 'becomes default' wording exceeds the implemented contract: " +
            "the setting registers PowerToys.RegistryPreview in OpenWithProgIDs but Windows owns UserChoice.");
    }

    private static void SetDefaultApp(ToggleSwitch toggle, Session settings, bool enabled)
    {
        toggle.Toggle(enabled);
        Assert.IsTrue(
            toggle.WaitForProperty("ToggleState", enabled ? "On" : "Off", timeoutMS: 5_000),
            $"The default-app switch did not settle to {(enabled ? "On" : "Off")}.");
        Assert.IsTrue(
            settings.WaitFor(() => IsDefaultAppRegistrationPresent() == enabled, ActionTimeoutMS, pollIntervalMS: 250),
            $"The PowerToys.RegistryPreview OpenWith registration did not become {(enabled ? "present" : "absent")}.");
    }

    private void RememberModuleState(Session settings)
    {
        settingsToRestore = settings;
        moduleEnabledToRestore ??= FindModuleToggle(settings).IsOn;
    }
}
