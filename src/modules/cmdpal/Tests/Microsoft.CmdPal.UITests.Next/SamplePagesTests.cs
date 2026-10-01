// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Management.Deployment;

namespace Microsoft.CmdPal.UITests;

/// <summary>
/// Context-menu keyboard tests that drive pages from SamplePagesExtension. The class registers the
/// extension's loose MSIX layout (shipped next to the test binaries) before Command Palette launches
/// and removes it afterwards. Registering a loose layout requires Developer Mode.
/// </summary>
[TestClass]
public class SamplePagesTests : CommandPaletteTestBase
{
    private const string PackageName = "SamplePagesExtension";
    private const string PackagePublisher = "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US";

    // com:Class Id from SamplePagesExtension's Package.appxmanifest.
    private static readonly Guid ExtensionClassId = new("6112D28D-6341-45C8-92C3-83ED55853A9F");

    private static string? registeredPackageFullName;

    [ClassInitialize]
    public static void RegisterSamplePagesExtension(TestContext context)
    {
        var packageManager = new PackageManager();
        if (FindInstalledPackage(packageManager) is not null)
        {
            // Already deployed (for example from Visual Studio); leave it as the developer set it up.
            WarmUpExtensionServer(context);
            return;
        }

        var manifestPath = Path.Combine(AppContext.BaseDirectory, PackageName, "AppxManifest.xml");
        Assert.IsTrue(
            File.Exists(manifestPath),
            $"SamplePagesExtension layout is missing at '{manifestPath}'. Build the test project, which copies it.");

        var operation = packageManager.RegisterPackageAsync(
            new Uri(manifestPath),
            null,
            DeploymentOptions.DevelopmentMode | DeploymentOptions.ForceApplicationShutdown);
        try
        {
            operation.AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Assert.Fail(
                $"Registering SamplePagesExtension failed (Developer Mode must be enabled): {ex.Message} {TryGetErrorText(operation)}");
        }

        registeredPackageFullName = FindInstalledPackage(packageManager)?.Id.FullName;
        Assert.IsNotNull(registeredPackageFullName, "SamplePagesExtension is not installed after registration.");
        context.WriteLine($"Registered {registeredPackageFullName} from {manifestPath}");

        WarmUpExtensionServer(context);
    }

    /// <summary>
    /// The first activation of a freshly registered, self-contained extension can take longer than
    /// Command Palette's 10 s extension start budget (cold .NET start plus on-access scanning), after
    /// which the palette skips it for that session. Activate the COM server once so the palette under
    /// test connects to a warm, already running server.
    /// </summary>
    private static void WarmUpExtensionServer(TestContext context)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var activation = Task.Run(() =>
        {
            var type = Type.GetTypeFromCLSID(ExtensionClassId, throwOnError: true)!;
            var instance = Activator.CreateInstance(type)!;
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(instance);
        });

        try
        {
            Assert.IsTrue(
                activation.Wait(TimeSpan.FromMinutes(2)),
                "Activating the SamplePagesExtension COM server did not finish within 2 minutes.");
        }
        catch (AggregateException ex)
        {
            Assert.Fail($"Activating the SamplePagesExtension COM server failed: {ex.InnerException?.Message}");
        }

        context.WriteLine($"Warmed up SamplePagesExtension COM server in {stopwatch.ElapsedMilliseconds} ms");
    }

    [ClassCleanup]
    public static void RemoveSamplePagesExtension()
    {
        if (registeredPackageFullName is null)
        {
            return;
        }

        try
        {
            new PackageManager().RemovePackageAsync(registeredPackageFullName).AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to remove {registeredPackageFullName}: {ex.Message}");
        }

        registeredPackageFullName = null;
    }

    private static Windows.ApplicationModel.Package? FindInstalledPackage(PackageManager packageManager) =>
        packageManager.FindPackagesForUser(string.Empty, PackageName, PackagePublisher).FirstOrDefault();

    private static string TryGetErrorText(Windows.Foundation.IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress> operation)
    {
        try
        {
            return operation.GetResults()?.ErrorText ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Search for the "Sample Pages" command and wait until it is the selected result. Command Palette
    /// loads extensions asynchronously after startup, and name search is a substring match that the
    /// "Sample Pages Extension" app entry also satisfies, so require the exact command title.
    /// </summary>
    private void SearchForSamplePages()
    {
        SetSearchBox("Sample Pages");
        var deadline = DateTime.UtcNow.AddSeconds(30);
        var retyped = false;
        while (DateTime.UtcNow < deadline)
        {
            var item = FindExactItem("Sample Pages", timeoutMS: 1000);
            if (item is not null && item.Selected)
            {
                return;
            }

            if (!retyped && DateTime.UtcNow > deadline.AddSeconds(-15))
            {
                // Re-run the query once in case the provider finished loading after the first filter.
                SetSearchBox("Sample Pages");
                retyped = true;
            }

            Thread.Sleep(500);
        }

        Assert.Fail("Command Palette did not show the registered SamplePagesExtension's 'Sample Pages' command as the selected result.");
    }

    private NavigationViewItem? FindExactItem(string name, int timeoutMS = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMS);
        do
        {
            var match = CommandPaletteSession
                .FindAll<NavigationViewItem>(By.Name(name), timeoutMS: 0)
                .FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));
            if (match is not null)
            {
                return match;
            }

            Thread.Sleep(250);
        }
        while (DateTime.UtcNow < deadline);

        return null;
    }

    private void AssertOnRootPage(string message)
    {
        Assert.IsNotNull(FindExactItem("Sample Pages"), message);
        Assert.IsFalse(
            CommandPaletteSession.Has<NavigationViewItem>(By.Name("This is a basic item in the list"), timeoutMS: 0),
            $"{message} The list sample page is still shown.");
    }

    /// <summary>
    /// The context filter box is <c>AccessibilityView="Raw"</c>, so it is not addressable in the control
    /// view; read the focused element directly instead.
    /// </summary>
    private (string Type, string AutomationId) GetFocusedElement()
    {
        try
        {
            var result = CommandPaletteSession.GetFocused();
            if (result.TryGetProperty("element", out var element) && element.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                var type = element.TryGetProperty("type", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                var id = element.TryGetProperty("automationId", out var a) ? a.GetString() ?? string.Empty : string.Empty;
                return (type, id);
            }
        }
        catch (AssertFailedException)
        {
            // Focus may be transitioning; callers poll.
        }

        return (string.Empty, string.Empty);
    }

    private void MoveFocusFromContextFilterToList()
    {
        Step("Moving focus from the context filter to the result list");
        Step($"Focused before Shift+Tab: {GetFocusedElement()}");
        KeyboardHelper.SendKeys(Key.Shift, Key.Tab);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        (string Type, string AutomationId) focused;
        do
        {
            focused = GetFocusedElement();
            if (focused.AutomationId != "ContextFilterBox" && focused.Type is "ListItem" or "List")
            {
                return;
            }

            Thread.Sleep(200);
        }
        while (DateTime.UtcNow < deadline);

        Assert.Fail($"Shift+Tab must move focus from ContextFilterBox to the context list; focused element is {focused}.");
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    [TestCategory("SamplePagesExtension")]
    [DataRow("closed", true)]
    [DataRow("filter", false)]
    [DataRow("list", false)]
    public void ContextShortcut_NavigatesOnce(string menuFocus, bool useEscape)
    {
        SearchForSamplePages();

        if (menuFocus != "closed")
        {
            Step("Opening the result context menu with Ctrl+K");
            KeyboardHelper.SendKeys(Key.Ctrl, Key.K);
            Assert.IsNotNull(CommandPaletteSession.Find<NavigationViewItem>("Open list sample with shortcut"));

            if (menuFocus == "list")
            {
                MoveFocusFromContextFilterToList();
            }
        }

        Step("Invoking the first result with Ctrl+1");
        KeyboardHelper.SendKeys(Key.Ctrl, Key.Num1);
        Assert.IsNotNull(CommandPaletteSession.Find<NavigationViewItem>("This is a basic item in the list"));

        if (useEscape)
        {
            Step("Returning to the root page with Escape");
            KeyboardHelper.SendKeys(Key.Esc);
        }
        else
        {
            Step("Returning to the root page with Back");
            CommandPaletteSession.Find<Button>("Back").Click();
        }

        AssertOnRootPage("One Back or Escape must return to the root page.");
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    [TestCategory("SamplePagesExtension")]
    [DataRow(false)]
    [DataRow(true)]
    public void ContextShortcut_OpensSubmenuBeforeInvokingChild(bool openContextMenu)
    {
        SearchForSamplePages();

        if (openContextMenu)
        {
            Step("Opening the result context menu with Ctrl+K");
            KeyboardHelper.SendKeys(Key.Ctrl, Key.K);
            Assert.IsNotNull(CommandPaletteSession.Find<NavigationViewItem>("Shortcut submenu"));
        }

        Step("Opening the submenu with Ctrl+2");
        KeyboardHelper.SendKeys(Key.Ctrl, Key.Num2);
        Assert.IsNotNull(CommandPaletteSession.Find<NavigationViewItem>("Open nested list sample"));

        Step("Invoking the first child with Ctrl+1");
        KeyboardHelper.SendKeys(Key.Ctrl, Key.Num1);
        Assert.IsNotNull(CommandPaletteSession.Find<NavigationViewItem>("Details with rich content (Small)"));

        CommandPaletteSession.Find<Button>("Back").Click();
        Assert.IsNotNull(FindExactItem("Sample Pages"));
    }

    /// <summary>
    /// A shortcut that opens a submenu directly still builds the parent menu beneath it, so the first
    /// Escape returns to the parent menu and the second closes the flyout (#50847).
    /// </summary>
    [TestMethod]
    [TestCategory("CmdPal")]
    [TestCategory("SamplePagesExtension")]
    public void ContextSubmenuEscape_ClosesFlyoutAndRestoresFocus()
    {
        SearchForSamplePages();

        Step("Opening the nested list with Ctrl+2");
        KeyboardHelper.SendKeys(Key.Ctrl, Key.Num2);
        Assert.IsNotNull(CommandPaletteSession.Find<NavigationViewItem>("Open nested list sample"));

        Step("Returning to the parent context menu with Escape");
        KeyboardHelper.SendKeys(Key.Esc);
        Assert.IsNotNull(
            CommandPaletteSession.Find<NavigationViewItem>("Shortcut submenu"),
            "The first Escape must return to the parent context menu.");
        Assert.IsFalse(CommandPaletteSession.Has<NavigationViewItem>(By.Name("Open nested list sample"), timeoutMS: 0));

        Step("Closing the context menu with Escape");
        KeyboardHelper.SendKeys(Key.Esc);
        Assert.IsTrue(
            WaitUntil(() => !CommandPaletteSession.Has<NavigationViewItem>(By.Name("Shortcut submenu"), timeoutMS: 0)),
            "The second Escape must close the context menu.");
        Assert.IsTrue(
            WaitUntil(() => bool.Parse(CommandPaletteSession.Find<TextBox>(
                By.AccessibilityId("MainSearchBox")).GetProperty("HasKeyboardFocus"))),
            "Closing the context menu must restore focus to the main search box.");
    }

    private static bool WaitUntil(Func<bool> condition, int timeoutMS = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMS);
        do
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(200);
        }
        while (DateTime.UtcNow < deadline);

        return false;
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    [TestCategory("SamplePagesExtension")]
    [DataRow(false)]
    [DataRow(true)]
    public void ContextSecondaryShortcut_InvokesSecondaryInsteadOfHighlightedPrimary(bool focusList)
    {
        SearchForSamplePages();

        Step("Opening the result context menu with Ctrl+K");
        KeyboardHelper.SendKeys(Key.Ctrl, Key.K);
        Assert.IsNotNull(CommandPaletteSession.Find<NavigationViewItem>("Open list sample with shortcut"));

        if (focusList)
        {
            MoveFocusFromContextFilterToList();
        }

        Step("Invoking the secondary command with Ctrl+Enter");
        KeyboardHelper.SendKeys(Key.Ctrl, Key.Enter);
        Assert.IsNotNull(CommandPaletteSession.Find<NavigationViewItem>("This is a basic item in the list"));
        CommandPaletteSession.Find<Button>("Back").Click();
        AssertOnRootPage("Back must return to the root page.");
    }
}
