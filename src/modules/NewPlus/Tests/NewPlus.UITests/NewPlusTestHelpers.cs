// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace Microsoft.PowerToys.NewPlus.UITests;

public sealed partial class NewPlusTests
{
    private const string ModuleName = "NewPlus";
    private const string HideExtensionName = "Hide the file extension in template names";
    private const string HideDigitsName = "Hide leading digits, spaces, and dots in template filenames";
    private const string OpenTemplatesName = "Open templates";
    private const string HandlerKey = @"Software\Classes\Directory\background\ShellEx\ContextMenuHandlers\NewPlusShellExtensionWin10";
    private const int TimeoutMS = 30_000;
    private const int MenuOpenTimeoutMS = 90_000;
    private const int MaxMenuEvidencePerTest = 2;

    private static readonly string SuiteRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        $"NewPlusUITests-{Guid.NewGuid():N}");

    private static IDisposable? moduleSettingsSnapshot;
    private static bool explorerRestarted;
    private string caseFolder = string.Empty;
    private string outputFolder = string.Empty;
    private Session settings = null!;
    private int menuEvidenceCaptures;

    public NewPlusTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: [ModuleName])
    {
    }

    protected override bool ReuseScopeAcrossTests => true;

    protected override void PrepareTestState()
    {
        moduleSettingsSnapshot ??= SettingsConfigHelper.PreserveModuleSettings(ModuleName);
        var initialTemplates = Directory.CreateDirectory(Path.Combine(SuiteRoot, "InitialTemplates")).FullName;
        SettingsConfigHelper.UpdateModuleSettings(ModuleName, "{}", root =>
        {
            root["name"] = ModuleName;
            root["version"] = "1.0";
            root["properties"] = new JsonObject
            {
                ["TemplateLocation"] = new JsonObject { ["value"] = initialTemplates },
                ["HideFileExtension"] = new JsonObject { ["value"] = false },
                ["HideStartingDigits"] = new JsonObject { ["value"] = false },
                ["ReplaceVariables"] = new JsonObject { ["value"] = false },
                ["BuiltInNewHidePreference"] = new JsonObject { ["value"] = false },
            };
        });
    }

    [TestInitialize]
    public void PrepareNewPlusTest()
    {
        menuEvidenceCaptures = 0;
        Step("Navigating to New+ Settings");
        settings = Session.FromProcess("PowerToys.Settings", PowerToysModule.PowerToysSettings);
        if (!settings.Has(By.AccessibilityId("NewPlusNavItem"), timeoutMS: 500))
        {
            settings.Find<NavigationViewItem>(By.AccessibilityId("FileManagementNavItem")).Click(msPostAction: 0);
        }

        settings.Find<NavigationViewItem>(By.AccessibilityId("NewPlusNavItem"), TimeoutMS).Click(msPostAction: 0);
        SetModuleEnabled(true);
        SetDisplayOption(HideExtensionName, "HideFileExtension", false);
        SetDisplayOption(HideDigitsName, "HideStartingDigits", false);
        EnsureShellRegistration();
        CloseExplorerWindows();
        caseFolder = Directory.CreateDirectory(Path.Combine(SuiteRoot, $"Case-{Guid.NewGuid():N}")).FullName;
        outputFolder = Directory.CreateDirectory(Path.Combine(caseFolder, "Output")).FullName;
    }

    [TestCleanup]
    public async Task CleanupNewPlusTest()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync(TimeSpan.FromSeconds(2));
        ShellMenu.Dismiss();
        var picker = WindowsFinder.WaitForWindowByApp(
            "PowerToys.Settings", window => window.ClassName == "#32770", timeoutMS: 500);
        if (picker is not null)
        {
            picker.Find<Button>(By.Name("Cancel")).Invoke(msPostAction: 0);
        }

        CloseExplorerWindows();
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void RestoreNewPlusState()
    {
        // Stop the shared Runner before restoring bytes; otherwise shutdown can overwrite them.
        // The inherited ClassCleanup also calls this; the idempotent call here guarantees ordering.
        StopSharedScope();
        try
        {
            moduleSettingsSnapshot?.Dispose();
        }
        finally
        {
            moduleSettingsSnapshot = null;
            if (Directory.Exists(SuiteRoot))
            {
                Directory.Delete(SuiteRoot, recursive: true);
            }
        }
    }

    private void SetModuleEnabled(bool enabled)
    {
        ShellMenu.Dismiss();
        Step($"Setting New+ enabled={enabled} through Settings");
        SetToggle(FindToggle("New+"), enabled);
        WaitForJsonValue(
            Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "settings.json"),
            root => root?["enabled"]?[ModuleName]?.GetValue<bool>() == enabled,
            $"enabled.NewPlus={enabled}");

        var registration = WaitHelper.WaitForStable(
            observe: () =>
            {
                using var key = Registry.CurrentUser.OpenSubKey(HandlerKey);
                return key?.GetValue(null) as string;
            },
            isMatch: value => enabled ? value == "{FF90D477-E32A-4BE8-8CC5-A502A97F5401}" : value is null,
            timeoutMS: TimeoutMS,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 250);
        Assert.IsTrue(
            registration.Succeeded,
            $"Runner did not {(enabled ? "register" : "unregister")} New+'s handler. Last value: {registration.LastObservation}. " +
            "Classic-handler registration requires a Release (NDEBUG) product runtime or ENABLE_REGISTRATION.");
    }

    private void SetDisplayOption(string caption, string property, bool enabled)
    {
        ShellMenu.Dismiss();
        Step($"Setting {property}={enabled} through Settings");
        SetToggle(FindToggle(caption), enabled);
        WaitForJsonValue(ModuleSettingsPath, root => root?["properties"]?[property]?["value"]?.GetValue<bool>() == enabled, $"{property}={enabled}");
    }

    private ToggleSwitch FindToggle(string caption)
    {
        var matches = settings.FindAll<ToggleSwitch>(By.Name(caption), TimeoutMS);
        var toggle = matches.SingleOrDefault(item => item.Name == caption);
        Assert.IsNotNull(toggle, $"Settings did not expose the '{caption}' toggle.");
        return toggle;
    }

    private static void SetToggle(ToggleSwitch toggle, bool enabled)
    {
        if (toggle.IsOn != enabled)
        {
            toggle.Invoke(msPostAction: 0);
        }

        Assert.IsTrue(toggle.WaitForProperty("ToggleState", enabled ? "On" : "Off", TimeoutMS), $"Toggle did not become {enabled}.");
    }

    private static string ModuleSettingsPath => Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, ModuleName, "settings.json");

    private static void WaitForJsonValue(string path, Func<JsonNode?, bool> predicate, string description)
    {
        var result = WaitHelper.WaitForStable(
            observe: () =>
            {
                // Observing settings must not deny the Runner's concurrent save or replacement.
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                return JsonNode.Parse(stream);
            },
            isMatch: predicate,
            timeoutMS: TimeoutMS,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 200,
            shouldRetryException: exception => exception is IOException or JsonException);
        Assert.IsTrue(result.Succeeded, $"Settings did not persist {description}. Last JSON: {result.LastObservation}; error: {result.LastException}");
    }

    private string ChooseNewTemplateFolder()
    {
        ShellMenu.Dismiss();
        Step($"Creating and selecting a template folder under '{caseFolder}' with the Settings folder picker");

        // SHBrowseForFolder blocks its caller until dismissed, including a synchronous UIA Invoke.
        settings.Find<Element>(By.AccessibilityId("NewPlusTemplatesLocation"))
            .Find<Button>(By.Name("Change")).Click(msPostAction: 0);
        var picker = WindowsFinder.WaitForWindowByApp(
            "PowerToys.Settings", window => window.ClassName == "#32770", timeoutMS: TimeoutMS);
        Assert.IsNotNull(picker, "The template folder picker did not open.");

        SelectFolderTreeItem(picker, Path.GetFileName(SuiteRoot), expand: true);
        SelectFolderTreeItem(picker, Path.GetFileName(caseFolder), expand: false);
        var before = Directory.GetDirectories(caseFolder).ToHashSet(StringComparer.OrdinalIgnoreCase);
        picker.Find<Button>(By.Name("Make New Folder")).Invoke(msPostAction: 0);
        var created = WaitHelper.WaitForStable(
            observe: () => Directory.GetDirectories(caseFolder).Where(path => !before.Contains(path)).ToArray(),
            isMatch: paths => paths?.Length == 1,
            timeoutMS: TimeoutMS,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 200);
        Assert.IsTrue(created.Succeeded, "Make New Folder did not create exactly one folder beneath the selected test directory.");
        var templates = created.LastObservation!.Single();
        Assert.AreEqual(0, Directory.GetFileSystemEntries(templates).Length, "The newly created folder is not empty.");

        // Finish the tree's inline rename before accepting the selected folder.
        KeyboardHelper.SendKeys(Key.Enter);
        picker.Find<Button>(By.Name("OK")).Invoke(msPostAction: 0);
        Assert.IsTrue(
            settings.WaitFor(
                () => !WindowsFinder.ListByApp("PowerToys.Settings").Any(window => window.ClassName == "#32770"),
                TimeoutMS),
            "The folder picker did not close.");
        WaitForJsonValue(
            ModuleSettingsPath,
            root => string.Equals(root?["properties"]?["TemplateLocation"]?["value"]?.GetValue<string>(), templates, StringComparison.OrdinalIgnoreCase),
            $"TemplateLocation='{templates}'");
        Assert.IsTrue(settings.Has(By.Name(templates), TimeoutMS), "Settings did not display the selected template path.");
        return templates;
    }

    private static void SelectFolderTreeItem(Session picker, string name, bool expand)
    {
        var item = picker.FindAll<Element>(By.Name(name), TimeoutMS)
            .SingleOrDefault(element => element.Name == name && element.ControlType == "TreeItem");
        Assert.IsNotNull(item, $"The folder picker did not list '{name}'.");
        item.ScrollIntoView();
        item.Click(msPostAction: 0);
        Assert.IsTrue(item.WaitForProperty("IsSelected", "true", TimeoutMS), $"The folder picker did not select '{name}'.");
        if (expand)
        {
            KeyboardHelper.SendKeys(Key.Right);
        }
    }

    private void EnsureShellRegistration()
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            var packageManager = new Windows.Management.Deployment.PackageManager();
            var registered = WaitHelper.WaitForStable(
                observe: () => packageManager.FindPackagesForUser(string.Empty)
                    .Any(package => package.Id.Name == "Microsoft.PowerToys.NewPlusContextMenu"),
                isMatch: value => value,
                timeoutMS: TimeoutMS,
                requiredConsecutiveMatches: 2,
                pollIntervalMS: 250);
            Assert.IsTrue(registered.Succeeded, "NewPlusPackage.msix did not register. Windows 11 requires the signed, trusted tier-1 package; a classic-menu fallback would not test New+.");
        }

        if (explorerRestarted)
        {
            return;
        }

        Step("Restarting Explorer once after New+ registration");
        Assert.IsTrue(
            ExplorerControl.RestartShell(TimeoutMS, Step),
            "Explorer did not restart its taskbar in a fresh process after handler registration.");
        explorerRestarted = true;
    }

    private Session OpenExplorer()
    {
        CloseExplorerWindows();
        Step($"Opening Explorer at '{outputFolder}'");

        var explorer = ExplorerControl.OpenFolder(outputFolder, TimeoutMS);
        Assert.IsNotNull(explorer, "Explorer did not open the output folder.");
        WindowHelper.MaximizeWindow(new IntPtr(explorer.WindowHandle));
        return explorer;
    }

    private Session OpenRootMenu(Session explorer, bool expectNewPlus)
    {
        var deadline = Stopwatch.StartNew();
        int RemainingTimeout(int maximumMS) =>
            Math.Min(maximumMS, Math.Max(0, MenuOpenTimeoutMS - (int)deadline.ElapsedMilliseconds));

        while (deadline.ElapsedMilliseconds < MenuOpenTimeoutMS)
        {
            ShellMenu.Dismiss();
            var foregroundTimeout = RemainingTimeout(TimeoutMS);
            if (foregroundTimeout == 0)
            {
                break;
            }

            Assert.IsTrue(
                WindowControl.WaitForForeground(new IntPtr(explorer.WindowHandle), foregroundTimeout, requiredConsecutiveMatches: 3),
                $"Explorer did not own foreground: {WindowControl.GetForegroundWindowInfo()}");
            var viewTimeout = RemainingTimeout(TimeoutMS);
            if (viewTimeout == 0)
            {
                break;
            }

            Step("Opening the Explorer folder-background context menu");
            var view = explorer.FindAll<Element>(By.Name("Items View"), viewTimeout)
                .FirstOrDefault(element => element.ClassName == "UIItemsView" && element.Width > 0 && element.Height > 0);
            Assert.IsNotNull(view, "Explorer did not expose its folder Items View.");
            var menuTimeout = RemainingTimeout(25_000);
            if (menuTimeout == 0)
            {
                break;
            }

            // The fixtures occupy the top of the view. Click the lower empty area so this is the
            // folder-background menu, not the selected-file menu used by PowerRename.
            MouseHelper.MoveTo(view.X + (view.Width / 2), view.Y + view.Height - 40);
            MouseHelper.RightClick();
            var menu = ShellMenu.WaitForWindow(
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) ? ShellMenu.ModernWindowClassName : ShellMenu.ClassicWindowClassName,
                timeoutMS: menuTimeout,
                processNameOrId: ExplorerControl.ProcessName);
            var itemTimeout = RemainingTimeout(3_000);
            if (itemTimeout == 0)
            {
                break;
            }

            if (menu is not null && ShellMenu.FindVisibleMenuItem(menu, expectNewPlus ? "New+" : "View", itemTimeout, StringComparison.Ordinal) is not null)
            {
                return menu;
            }
        }

        Assert.Fail(
            $"Explorer did not open its background menu{(expectNewPlus ? " with New+" : string.Empty)} " +
            $"within the {MenuOpenTimeoutMS}ms retry budget. Foreground: {WindowControl.GetForegroundWindowInfo()}");
        return null!;
    }

    private Session OpenTemplateMenu(Session explorer)
    {
        var root = OpenRootMenu(explorer, expectNewPlus: true);
        var item = ShellMenu.FindVisibleMenuItem(root, "New+", comparison: StringComparison.Ordinal);
        Assert.IsNotNull(item, "New+ was missing from the open context menu.");
        Step("Expanding the New+ template submenu");
        item.Invoke(msPostAction: 0);

        var submenu = ShellMenu.WaitForSubmenu(
            root,
            OpenTemplatesName,
            timeoutMS: TimeoutMS,
            requiredConsecutiveMatches: 2,
            comparison: StringComparison.Ordinal);
        Assert.IsNotNull(submenu, "The New+ submenu did not expose a stable, visible Open templates item.");
        return submenu;
    }

    private void AssertRootMenu(Session explorer, bool expected)
    {
        var menu = OpenRootMenu(explorer, expectNewPlus: expected);

        // Absence needs more stable samples: an enumerating menu can temporarily omit New+.
        var observed = WaitHelper.WaitForStable(
            observe: () => ShellMenu.ReadMenuNames(menu),
            isMatch: names => names is not null && names.Contains("View") && names.Contains("New+") == expected,
            timeoutMS: TimeoutMS,
            requiredConsecutiveMatches: expected ? 2 : 4,
            pollIntervalMS: 250);
        Assert.IsTrue(observed.Succeeded, $"New+ menu presence should be {expected}; menu: {string.Join(", ", observed.LastObservation ?? [])}");
        AttachMenuEvidence("enabled-" + expected);
        ShellMenu.Dismiss();
    }

    private void AssertTemplateMenu(Session explorer, string[] expected)
    {
        var menu = OpenTemplateMenu(explorer);
        var expectedNames = expected.Append(OpenTemplatesName).ToHashSet(StringComparer.Ordinal);
        var observed = WaitHelper.WaitForStable(
            observe: () => ShellMenu.ReadMenuNames(menu),
            isMatch: names => names is not null && names.Count == expectedNames.Count && expectedNames.SetEquals(names),
            timeoutMS: TimeoutMS,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 250);
        Assert.IsTrue(observed.Succeeded, $"Expected templates [{string.Join(", ", expectedNames)}]; actual menu: [{string.Join(", ", observed.LastObservation ?? [])}].");
        AttachMenuEvidence("templates");
        ShellMenu.Dismiss();
    }

    private void InvokeTemplate(Session explorer, string caption)
    {
        var menu = OpenTemplateMenu(explorer);
        var item = ShellMenu.FindVisibleMenuItem(menu, caption, comparison: StringComparison.Ordinal);
        Assert.IsNotNull(item, $"Template '{caption}' was not visible in the New+ submenu.");
        Step($"Invoking template '{caption}'");
        item.Invoke(msPostAction: 0);
        Assert.IsTrue(
            explorer.WaitFor(
                () => !WindowsFinder.ListByApp(ExplorerControl.ProcessName).Any(ShellMenu.IsMenuWindow),
                TimeoutMS),
            "The context menu did not close after creating the template.");
    }

    private void AttachMenuEvidence(string label)
    {
        if (menuEvidenceCaptures >= MaxMenuEvidencePerTest)
        {
            Step($"Skipping supplementary menu capture: the {MaxMenuEvidencePerTest}-image per-test budget is exhausted.");
            return;
        }

        menuEvidenceCaptures++;
        try
        {
            // MSTest removes its deployment directory after a green run, even for attached files.
            var runDirectory = TestContext.TestRunDirectory;
            var resultsRoot = string.IsNullOrEmpty(runDirectory) ? null : Directory.GetParent(runDirectory)?.FullName;
            if (resultsRoot is null)
            {
                resultsRoot = Path.GetTempPath();
                Step($"No test-run parent directory is available; writing menu evidence under '{resultsRoot}'.");
            }

            var evidence = Directory.CreateDirectory(Path.Combine(resultsRoot, "NewPlusEvidence")).FullName;
            var path = Path.Combine(evidence, $"newplus-{label}-{Guid.NewGuid():N}.png");
            if (!ScreenCapture.TryCaptureDesktop(path))
            {
                Step($"Supplementary menu capture was unavailable for '{label}'.");
                return;
            }

            TestContext.AddResultFile(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Step($"Could not attach supplementary menu evidence for '{label}': {ex.Message}");
        }
    }

    private static string GetSettingsExecutable()
    {
        var processes = Process.GetProcessesByName("PowerToys.Settings");
        try
        {
            Assert.AreEqual(1, processes.Length, "Expected exactly one Settings process.");
            return processes[0].MainModule!.FileName;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static void CloseExplorerWindows() =>
        Assert.IsTrue(ExplorerControl.CloseFileWindows(), "Explorer file windows did not close.");

    private void Step(string message) => TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
}
