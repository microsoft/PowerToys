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
    private const string ClassicMenuClass = "#32768";
    private const string ModernMenuClass = "Microsoft.UI.Content.PopupWindowSiteBridge";
    private const string HandlerKey = @"Software\Classes\Directory\background\ShellEx\ContextMenuHandlers\NewPlusShellExtensionWin10";
    private const int TimeoutMS = 30_000;

    private static readonly string SuiteRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        $"NewPlusUITests-{Guid.NewGuid():N}");

    private static IDisposable? moduleSettingsSnapshot;
    private static bool explorerRestarted;
    private string caseFolder = string.Empty;
    private string outputFolder = string.Empty;
    private Session settings = null!;

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
            root["version"] = "0.0.1";
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
        DismissMenus();
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
        DismissMenus();
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
        Assert.IsTrue(registration.Succeeded, $"Runner did not {(enabled ? "register" : "unregister")} New+'s handler. Last value: {registration.LastObservation}");
    }

    private void SetDisplayOption(string caption, string property, bool enabled)
    {
        DismissMenus();
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
            observe: () => JsonNode.Parse(File.ReadAllText(path)),
            isMatch: predicate,
            timeoutMS: TimeoutMS,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 200,
            shouldRetryException: exception => exception is IOException or JsonException);
        Assert.IsTrue(result.Succeeded, $"Settings did not persist {description}. Last JSON: {result.LastObservation}; error: {result.LastException}");
    }

    private string ChooseNewTemplateFolder()
    {
        DismissMenus();
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
            var registered = WaitHelper.WaitForStable(
                observe: () => new Windows.Management.Deployment.PackageManager().FindPackagesForUser(string.Empty)
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
        foreach (var process in Process.GetProcessesByName("explorer"))
        {
            using (process)
            {
                process.Kill();
                Assert.IsTrue(process.WaitForExit(10_000), $"Explorer PID {process.Id} did not exit.");
            }
        }

        var shell = WindowsFinder.WaitForWindow(window => window.ClassName == "Shell_TrayWnd", timeoutMS: TimeoutMS);
        Assert.IsNotNull(shell, "Explorer did not restart its taskbar after handler registration.");
        explorerRestarted = true;
    }

    private Session OpenExplorer()
    {
        CloseExplorerWindows();
        Step($"Opening Explorer at '{outputFolder}'");
        using var process = Process.Start(new ProcessStartInfo("explorer.exe", $"/n,\"{outputFolder}\"") { UseShellExecute = true });
        var explorer = WindowsFinder.WaitForWindowByApp("explorer", IsFileWindow, timeoutMS: TimeoutMS);
        Assert.IsNotNull(explorer, "Explorer did not open the output folder.");
        WindowHelper.MaximizeWindow(new IntPtr(explorer.WindowHandle));
        return explorer;
    }

    private Session OpenRootMenu(Session explorer, bool expectNewPlus)
    {
        var deadline = Stopwatch.StartNew();
        do
        {
            DismissMenus();
            Assert.IsTrue(
                WindowControl.WaitForForeground(new IntPtr(explorer.WindowHandle), TimeoutMS, requiredConsecutiveMatches: 3),
                $"Explorer did not own foreground: {WindowControl.GetForegroundWindowInfo()}");
            Step("Opening the Explorer folder-background context menu");
            var view = explorer.FindAll<Element>(By.Name("Items View"), TimeoutMS)
                .FirstOrDefault(element => element.ClassName == "UIItemsView" && element.Width > 0 && element.Height > 0);
            Assert.IsNotNull(view, "Explorer did not expose its folder Items View.");

            // The fixtures occupy the top of the view. Click the lower empty area so this is the
            // folder-background menu, not the selected-file menu used by PowerRename.
            MouseHelper.MoveTo(view.X + (view.Width / 2), view.Y + view.Height - 40);
            MouseHelper.RightClick();
            var menu = WindowsFinder.WaitForWindowByApp(
                "explorer",
                window => window.ClassName == (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) ? ModernMenuClass : ClassicMenuClass),
                timeoutMS: 25_000);
            if (menu is not null && FindMenuItem(menu, expectNewPlus ? "New+" : "View") is not null)
            {
                return menu;
            }
        }
        while (deadline.ElapsedMilliseconds < 90_000);

        Assert.Fail($"Explorer did not open its background menu{(expectNewPlus ? " with New+" : string.Empty)}. Foreground: {WindowControl.GetForegroundWindowInfo()}");
        return null!;
    }

    private Session OpenTemplateMenu(Session explorer)
    {
        var root = OpenRootMenu(explorer, expectNewPlus: true);
        var item = FindMenuItem(root, "New+");
        Assert.IsNotNull(item, "New+ was missing from the open context menu.");
        Step("Expanding the New+ template submenu");
        item.Invoke(msPostAction: 0);
        var submenu = WindowsFinder.WaitForWindowByApp(
            "explorer",
            window => window.Hwnd != root.WindowHandle && (window.ClassName == ClassicMenuClass || window.ClassName == ModernMenuClass),
            timeoutMS: TimeoutMS);
        Assert.IsNotNull(submenu, "The New+ template submenu did not open.");
        Assert.IsNotNull(FindMenuItem(submenu, OpenTemplatesName), "The New+ submenu did not expose Open templates.");
        return submenu;
    }

    private void AssertRootMenu(Session explorer, bool expected)
    {
        var menu = OpenRootMenu(explorer, expectNewPlus: expected);
        var observed = WaitHelper.WaitForStable(
            observe: () => ReadMenuNames(menu),
            isMatch: names => names is not null && names.Contains("View") && names.Contains("New+") == expected,
            timeoutMS: TimeoutMS,
            requiredConsecutiveMatches: expected ? 2 : 4,
            pollIntervalMS: 250);
        Assert.IsTrue(observed.Succeeded, $"New+ menu presence should be {expected}; menu: {string.Join(", ", observed.LastObservation ?? [])}");
        AttachMenuEvidence("enabled-" + expected);
        DismissMenus();
    }

    private void AssertTemplateMenu(Session explorer, string[] expected)
    {
        var menu = OpenTemplateMenu(explorer);
        var expectedNames = expected.Append(OpenTemplatesName).ToHashSet(StringComparer.Ordinal);
        var observed = WaitHelper.WaitForStable(
            observe: () => ReadMenuNames(menu),
            isMatch: names => names is not null && names.Count == expectedNames.Count && expectedNames.SetEquals(names),
            timeoutMS: TimeoutMS,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 250);
        Assert.IsTrue(observed.Succeeded, $"Expected templates [{string.Join(", ", expectedNames)}]; actual menu: [{string.Join(", ", observed.LastObservation ?? [])}].");
        AttachMenuEvidence("templates");
        DismissMenus();
    }

    private void InvokeTemplate(Session explorer, string caption)
    {
        var menu = OpenTemplateMenu(explorer);
        var item = FindMenuItem(menu, caption);
        Assert.IsNotNull(item, $"Template '{caption}' was not visible in the New+ submenu.");
        Step($"Invoking template '{caption}'");
        item.Invoke(msPostAction: 0);
        Assert.IsTrue(
            explorer.WaitFor(
                () => !WindowsFinder.ListByApp("explorer").Any(window => window.ClassName == ClassicMenuClass || window.ClassName == ModernMenuClass),
                TimeoutMS),
            "The context menu did not close after creating the template.");
    }

    private static Element? FindMenuItem(Session menu, string name) =>
        menu.FindAll<Element>(By.Name(name), timeoutMS: 3_000)
            .FirstOrDefault(item => item.Name == name && item.ControlType == "MenuItem" && item.Width > 0 && item.Height > 0);

    private static IReadOnlyList<string> ReadMenuNames(Session menu)
    {
        var names = new List<string>();
        CollectMenuNames(menu.Inspect(depth: 12, hideOffscreen: true), names);
        return names;
    }

    private static void CollectMenuNames(JsonElement node, List<string> names)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("type", out var type) && type.GetString() == "MenuItem" &&
                node.TryGetProperty("name", out var name) && !string.IsNullOrEmpty(name.GetString()))
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

    private void AttachMenuEvidence(string label)
    {
        // MSTest removes its deployment directory after a green run, even for attached files.
        var resultsRoot = Directory.GetParent(TestContext.TestRunDirectory!)!.FullName;
        var evidence = Directory.CreateDirectory(Path.Combine(resultsRoot, "NewPlusEvidence")).FullName;
        var path = Path.Combine(evidence, $"newplus-{label}-{Guid.NewGuid():N}.png");
        Assert.IsTrue(ScreenCapture.TryCaptureDesktop(path), "Could not capture the visible New+ menu.");
        TestContext.AddResultFile(path);
    }

    private static void AssertCopiedTree(string source, string destination)
    {
        var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
        var directories = Directory.GetDirectories(source, "*", SearchOption.AllDirectories);
        var copied = WaitHelper.WaitForStable(
            observe: () => Directory.Exists(destination) &&
                directories.All(path => Directory.Exists(Path.Combine(destination, Path.GetRelativePath(source, path)))) &&
                files.All(path => File.Exists(Path.Combine(destination, Path.GetRelativePath(source, path)))),
            isMatch: value => value,
            timeoutMS: TimeoutMS,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 200);
        Assert.IsTrue(copied.Succeeded, $"New+ did not copy the complete template tree from '{source}' to '{destination}'.");
        CollectionAssert.AreEquivalent(
            files.Select(path => Path.GetRelativePath(source, path)).ToArray(),
            Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(destination, path)).ToArray(),
            "The created file inventory differs from the template.");
        CollectionAssert.AreEquivalent(
            directories.Select(path => Path.GetRelativePath(source, path)).ToArray(),
            Directory.GetDirectories(destination, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(destination, path)).ToArray(),
            "The created directory inventory differs from the template.");
        foreach (var file in files)
        {
            AssertFileContents(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
        }
    }

    private static void AssertFileContents(string source, string destination)
    {
        var expected = File.ReadAllBytes(source);
        var copied = WaitHelper.WaitForStable(
            observe: () => File.Exists(destination) ? File.ReadAllBytes(destination) : null,
            isMatch: bytes => bytes is not null && expected.SequenceEqual(bytes),
            timeoutMS: TimeoutMS,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 200,
            shouldRetryException: exception => exception is IOException);
        Assert.IsTrue(copied.Succeeded, $"New+ did not create '{destination}' with the exact contents of '{source}'.");
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

    private static bool IsFileWindow(WindowsFinder.WindowInfo window) => window.ClassName == "CabinetWClass";

    private static void CloseExplorerWindows() =>
        Assert.IsTrue(WindowControl.TryCloseByApp("explorer", IsFileWindow, timeoutMS: 10_000), "Explorer file windows did not close.");

    private static void DismissMenus()
    {
        KeyboardHelper.SendKeys(Key.Esc);
        KeyboardHelper.SendKeys(Key.Esc);
    }

    private void Step(string message) => TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
}
