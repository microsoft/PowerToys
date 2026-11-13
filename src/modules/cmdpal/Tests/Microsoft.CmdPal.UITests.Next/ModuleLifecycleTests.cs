// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Management.Deployment;

namespace Microsoft.CmdPal.UITests;

[TestClass]
public class ModuleLifecycleTests : UITestBase
{
    private const string PackageName = "Microsoft.CommandPalette";
    private const string Publisher = "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US";
    private const string ProcessName = "Microsoft.CmdPal.UI";
    private static string? installedPackage;

    private Session settingsSession = null!;

    public ModuleLifecycleTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: ["CmdPal"])
    {
    }

    protected override IReadOnlyList<string> StaleProcessNames =>
        [.. base.StaleProcessNames, ProcessName];

    internal static void PrepareCommandPalettePackage(TestContext context)
    {
        var manager = new PackageManager();
        var runtimeDirectory = Path.Combine(
            Path.GetDirectoryName(SessionHelper.GetExecutablePath(PowerToysModule.Runner))!, "WinUI3Apps", "CmdPal");
        var packages = Directory.GetFiles(runtimeDirectory, "*.msix", SearchOption.TopDirectoryOnly);
        if (packages.Length == 0)
        {
            packages = Directory.GetFiles(runtimeDirectory, "*.msix", SearchOption.AllDirectories);
        }

        Assert.HasCount(1, packages, "The runtime must include exactly one signed Command Palette MSIX.");
        using (var archive = ZipFile.OpenRead(packages[0]))
        {
            var manifest = archive.GetEntry("AppxManifest.xml");
            Assert.IsNotNull(manifest, "The Command Palette MSIX has no manifest.");
            using var stream = manifest.Open();
            var document = XDocument.Load(stream);
            Assert.IsNotNull(document.Root);
            var versionText = document.Root.Element(document.Root.Name.Namespace + "Identity")?.Attribute("Version")?.Value;
            Assert.IsNotNull(versionText, "The Command Palette MSIX has no package version.");
            var expectedVersion = Version.Parse(versionText);
            var existing = manager.FindPackagesForUser(string.Empty, PackageName, Publisher)
                .FirstOrDefault(package => !package.IsResourcePackage);
            if (existing is not null)
            {
                var id = existing.Id;
                var installedVersion = new Version(id.Version.Major, id.Version.Minor, id.Version.Build, id.Version.Revision);
                Assert.AreEqual(
                    expectedVersion,
                    installedVersion,
                    $"The existing Command Palette package '{id.FullName}' differs from the runtime MSIX. Use a clean test profile; the suite will not replace a pre-existing package.");
                context.WriteLine($"Preserving the existing Command Palette package '{id.FullName}' from '{existing.InstalledLocation.Path}'.");
                return;
            }
        }

        context.WriteLine($"Installing Command Palette for the UI-test assembly: {packages[0]}");
        manager.AddPackageAsync(new Uri(packages[0]), null, DeploymentOptions.ForceApplicationShutdown)
            .AsTask().GetAwaiter().GetResult();
        installedPackage = manager.FindPackagesForUser(string.Empty, PackageName, Publisher)
            .Single().Id.FullName;
    }

    internal static void RestoreCommandPalettePackage()
    {
        if (installedPackage is not null)
        {
            new PackageManager().RemovePackageAsync(installedPackage).AsTask().GetAwaiter().GetResult();
            installedPackage = null;
        }
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void SettingsToggleControlsProcessAndActivationShortcut()
    {
        settingsSession = WaitForSettingsContent();
        if (!settingsSession.Has(By.AccessibilityId("CmdPalNavItem"), timeoutMS: 500))
        {
            settingsSession.Find<NavigationViewItem>(By.AccessibilityId("SystemToolsNavItem")).Click();
        }

        settingsSession.Find<NavigationViewItem>(By.AccessibilityId("CmdPalNavItem")).Click();
        var toggle = settingsSession.Find<ToggleSwitch>("Command Palette");
        var initiallyEnabled = toggle.IsOn;
        try
        {
            SetModuleEnabled(toggle, true);
            ActivateWithShortcut();
            SetModuleEnabled(toggle, false);

            Step("Sending the activation shortcut while the module is disabled");
            WindowHelper.MinimizeWindow(new IntPtr(settingsSession.WindowHandle));
            KeyboardHelper.SendKeys(Key.LWin, Key.Alt, Key.Space);
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < TimeSpan.FromSeconds(3))
            {
                Assert.IsFalse(IsProcessRunning(), "Disabled Command Palette restarted after its shortcut.");
                Assert.IsFalse(IsPaletteVisible(), "Disabled Command Palette appeared after its shortcut.");
                Thread.Sleep(100);
            }

            SetModuleEnabled(toggle, true);
            ActivateWithShortcut();
        }
        finally
        {
            SetModuleEnabled(toggle, initiallyEnabled);
        }
    }

    private static Session WaitForSettingsContent()
    {
        var processName = SessionHelper.GetProcessName(PowerToysModule.PowerToysSettings);
        long maximizedWindow = 0;
        var ready = WaitHelper.WaitForStable(
            observe: () =>
            {
                var settings = WindowsFinder.WaitForWindow(
                    window => window.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase) &&
                        window.Title == "PowerToys Settings",
                    attributeAs: PowerToysModule.PowerToysSettings,
                    timeoutMS: 1_000);
                if (settings is not null && settings.WindowHandle != maximizedWindow)
                {
                    WindowHelper.MaximizeWindow(new IntPtr(settings.WindowHandle));
                    maximizedWindow = settings.WindowHandle;
                }

                return settings;
            },
            isMatch: settings => settings is not null &&
                (settings.Has(By.AccessibilityId("SystemToolsNavItem"), timeoutMS: 0) ||
                 settings.Has(By.AccessibilityId("CmdPalNavItem"), timeoutMS: 0)),
            timeoutMS: 60_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 200,
            shouldRetryException: exception => exception is AssertFailedException &&
                exception.Message.Contains("Window HWND", StringComparison.Ordinal) &&
                exception.Message.Contains("AppNotFoundException", StringComparison.Ordinal));
        Assert.IsTrue(ready.Succeeded, $"PowerToys Settings did not expose its navigation content. {ready.LastException?.Message}");
        Assert.IsNotNull(ready.LastObservation);
        return ready.LastObservation;
    }

    private void SetModuleEnabled(ToggleSwitch toggle, bool enabled)
    {
        Step($"Setting the PowerToys Command Palette toggle to {enabled}");
        settingsSession.EnsureForeground();
        if (toggle.IsOn != enabled)
        {
            toggle.Invoke(msPostAction: 0);
        }

        Assert.IsTrue(toggle.WaitForProperty("ToggleState", enabled ? "On" : "Off"));
        var state = WaitHelper.WaitForStable(
            observe: IsProcessRunning,
            isMatch: running => running == enabled,
            timeoutMS: 60_000,
            requiredConsecutiveMatches: 3);
        Assert.IsTrue(state.Succeeded, $"The Command Palette process did not become enabled={enabled}.");
    }

    private void ActivateWithShortcut()
    {
        Step("Activating runner-launched Command Palette with Win+Alt+Space");
        var initialized = WaitHelper.WaitForStable(
            observe: () => WindowsFinder.ListByApp(ProcessName)
                .FirstOrDefault(window => window.Title == "Command Palette"),
            isMatch: window => window is not null,
            timeoutMS: 150_000,
            requiredConsecutiveMatches: 2);
        Assert.IsTrue(initialized.Succeeded, "The runner-launched Command Palette did not initialize its native window.");
        Assert.IsNotNull(initialized.LastObservation);
        Step($"Initialized CmdPal HWND={initialized.LastObservation.Hwnd}");
        WindowHelper.MinimizeWindow(new IntPtr(settingsSession.WindowHandle));
        if (!IsPaletteVisible())
        {
            KeyboardHelper.SendKeys(Key.Esc);
        }

        var deadline = Stopwatch.StartNew();
        while (!IsPaletteVisible() && deadline.Elapsed < TimeSpan.FromSeconds(30))
        {
            KeyboardHelper.SendKeys(Key.LWin, Key.Alt, Key.Space);
            var visible = WaitHelper.WaitForStable(
                observe: IsPaletteVisible,
                isMatch: appeared => appeared,
                timeoutMS: 5_000,
                requiredConsecutiveMatches: 1);
            if (visible.Succeeded)
            {
                break;
            }
        }

        Step($"Foreground: {WindowControl.GetForegroundWindowInfo()}");
        Assert.IsTrue(IsPaletteVisible(), "The activation shortcut did not show Command Palette.");
        var palette = Session.FromProcess(ProcessName, PowerToysModule.CommandPalette);
        Assert.IsNotNull(palette.Find<TextBox>(By.AccessibilityId("MainSearchBox"), timeoutMS: 20_000));
    }

    private static bool IsProcessRunning()
    {
        var processes = Process.GetProcessesByName(ProcessName);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static bool IsPaletteVisible() =>
        WindowsFinder.ListByApp(ProcessName).Any(
            window => window.Title == "Command Palette" &&
                      !WindowHelper.IsWindowCloaked(new IntPtr(window.Hwnd)));

    private void Step(string message) =>
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
}
