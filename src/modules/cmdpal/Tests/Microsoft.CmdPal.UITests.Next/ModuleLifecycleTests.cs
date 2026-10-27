// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
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

    public ModuleLifecycleTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: ["CmdPal"])
    {
    }

    protected override IReadOnlyList<string> StaleProcessNames =>
        [.. base.StaleProcessNames, ProcessName];

    [ClassInitialize]
    public static void PrepareCommandPalettePackage(TestContext context)
    {
        var manager = new PackageManager();
        if (manager.FindPackagesForUser(string.Empty, PackageName, Publisher).Any())
        {
            context.WriteLine("Preserving the Command Palette package already installed for the test user.");
            return;
        }

        var runtimeDirectory = Path.GetDirectoryName(
            SessionHelper.GetExecutablePath(PowerToysModule.CommandPalette))!;
        var packages = Directory.GetFiles(runtimeDirectory, "*.msix", SearchOption.TopDirectoryOnly);
        if (packages.Length == 0)
        {
            packages = Directory.GetFiles(runtimeDirectory, "*.msix", SearchOption.AllDirectories);
        }

        Assert.HasCount(1, packages, "The runtime must include exactly one signed Command Palette MSIX.");
        context.WriteLine($"Installing Command Palette for the runner lifecycle test: {packages[0]}");
        manager.AddPackageAsync(new Uri(packages[0]), null, DeploymentOptions.ForceApplicationShutdown)
            .AsTask().GetAwaiter().GetResult();
        installedPackage = manager.FindPackagesForUser(string.Empty, PackageName, Publisher)
            .Single().Id.FullName;
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void RestoreCommandPalettePackage()
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
        if (!Session.Has(By.AccessibilityId("CmdPalNavItem"), timeoutMS: 500))
        {
            Session.Find<NavigationViewItem>(By.AccessibilityId("SystemToolsNavItem")).Click();
        }

        Session.Find<NavigationViewItem>(By.AccessibilityId("CmdPalNavItem")).Click();
        var toggle = Session.Find<ToggleSwitch>("Command Palette");
        var initiallyEnabled = toggle.IsOn;
        try
        {
            SetModuleEnabled(toggle, true);
            ActivateWithShortcut();
            SetModuleEnabled(toggle, false);

            Step("Sending the activation shortcut while the module is disabled");
            WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
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

    private void SetModuleEnabled(ToggleSwitch toggle, bool enabled)
    {
        Step($"Setting the PowerToys Command Palette toggle to {enabled}");
        Session.EnsureForeground();
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
        WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
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
