// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ComboBox = Microsoft.PowerToys.UITest.Next.ComboBox;
using TextBox = Microsoft.PowerToys.UITest.Next.TextBox;

namespace Microsoft.GrabAndMove.UITests;

[TestClass]
[DoNotParallelize]
[TestCategory("GrabAndMove")]
public sealed class GrabAndMoveTests : UITestBase
{
    private const string ModuleName = "GrabAndMove";
    private const string ModuleProcess = "PowerToys.GrabAndMove";
    private const string OverlayClass = "GrabAndMove_Overlay";
    private const int DeltaX = 96;
    private const int DeltaY = 64;
    private const string BaselineSettings = """
        {
          "name": "GrabAndMove",
          "version": "1.0",
          "properties": {
            "modifierKey": { "value": 0 },
            "shouldAbsorbAlt": { "value": true },
            "showGeometry": { "value": false },
            "useAltResize": { "value": true },
            "doNotActivateOnGameMode": { "value": false },
            "excluded_apps": { "value": "" },
            "excluded_apps_for_modifier": { "value": "" }
          }
        }
        """;

    private static readonly string SettingsPath = Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, ModuleName, "settings.json");
    private static IDisposable? originalSettings;
    private static UmdfHidInput? hidInput;
    private static Point originalPointer;
    private TestWindow? fixture;

    public GrabAndMoveTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: [ModuleName])
    {
    }

    protected override IReadOnlyList<string> StaleProcessNames => [.. base.StaleProcessNames, ModuleProcess];

    [ClassInitialize]
    public static void PreserveSettings(TestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Assert.IsFalse(ElevationHelper.IsCurrentProcessElevated(), "Run Grab And Move tests in a standard-user desktop.");
        var (x, y) = MouseHelper.GetMousePosition();
        originalPointer = new Point(x, y);
        hidInput = UmdfHidInput.Activate();
        context.WriteLine($"Real HID input: {hidInput.DevicePath}");
        StopProduct();
        originalSettings = SettingsConfigHelper.PreserveModuleSettings(ModuleName);
    }

    [ClassCleanup]
    public static void RestoreSettings()
    {
        try
        {
            StopProduct();
        }
        finally
        {
            try
            {
                originalSettings?.Dispose();
            }
            finally
            {
                originalSettings = null;
                try
                {
                    hidInput?.Dispose();
                }
                finally
                {
                    hidInput = null;
                    MouseHelper.MoveTo(originalPointer.X, originalPointer.Y);
                }
            }
        }
    }

    protected override void PrepareTestState()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, BaselineSettings);
    }

    [TestInitialize]
    public void OpenSettingsAndFixture()
    {
        Step("Navigating to Grab And Move settings");
        if (!Session.Has(By.AccessibilityId("GrabAndMoveNavItem"), 500))
        {
            Find<NavigationViewItem>(By.AccessibilityId("WindowingAndLayoutsNavItem")).Click(msPostAction: 0);
        }

        Find<NavigationViewItem>(By.AccessibilityId("GrabAndMoveNavItem")).Click(msPostAction: 0);
        Assert.IsTrue(EnableToggle().IsOn, "Grab And Move must start enabled.");
        WaitForModule(expected: true);

        Step("Opening a standard-user resizable window with an inert client area");
        fixture = new TestWindow();
        FocusFixture();
    }

    [TestMethod]
    public void AltLeftDragMovesWindowToExpectedPosition()
    {
        DragAndAssert(Key.Alt, resize: false);
    }

    [TestMethod]
    public void AltRightDragResizesWindowToExpectedDimensions()
    {
        DragAndAssert(Key.Alt, resize: true);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GeometryOverlayAppearsOnlyDuringGesture(bool resize)
    {
        Assert.IsFalse(IsGeometryVisible(), "Geometry must be hidden before a gesture.");
        DragAndAssert(Key.Alt, resize, duringDrag: () => AssertGeometryVisible(expected: false));

        Step("Enabling Show window geometry through Settings");
        SetToggle(Find<ToggleSwitch>(By.Name("Show window geometry")), true);
        WaitForSetting("showGeometry", true);
        DragAndAssert(Key.Alt, resize, duringDrag: () =>
        {
            AssertGeometryVisible(expected: true);
            var path = Path.Combine(TestContext.TestRunResultsDirectory!, $"geometry-{resize}.png");
            var screen = System.Windows.Forms.SystemInformation.PrimaryMonitorSize;
            using var image = new Bitmap(screen.Width, screen.Height);
            using (var graphics = Graphics.FromImage(image))
            {
                graphics.CopyFromScreen(Point.Empty, Point.Empty, screen);
            }

            image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            TestContext.AddResultFile(path);
        });
        AssertGeometryVisible(expected: false);
    }

    [TestMethod]
    public void ChangingModifierToWinUpdatesMoveAndResizeWithoutRestart()
    {
        DragAndAssert(Key.Alt, resize: false);
        DragAndAssert(Key.Alt, resize: true);
        var processId = ModuleProcessId();

        Step("Changing the activation modifier from Alt to Windows key through Settings");
        var settings = new IntPtr(Session.WindowHandle);
        fixture!.TransferForegroundTo(settings);
        var foreground = WaitHelper.WaitForStable(
            WindowControl.GetForegroundWindowHandle,
            hwnd => hwnd == settings,
            5_000,
            requiredConsecutiveMatches: 3,
            recover: _ => fixture.TransferForegroundTo(settings));
        Assert.IsTrue(foreground.Succeeded, ForegroundDiagnostic());
        var modifier = Find<ComboBox>(By.Name("Activation modifier key"));
        modifier.Focus();
        KeyboardHelper.SendKeys(Key.End);
        KeyboardHelper.SendKeys(Key.Enter);
        WaitForSetting("modifierKey", 1);

        DragAndAssert(Key.LWin, resize: false);
        DragAndAssert(Key.LWin, resize: true);
        DragAndAssert(Key.Alt, resize: false, expectedChange: false);
        DragAndAssert(Key.Alt, resize: true, expectedChange: false);
        Assert.AreEqual(processId, ModuleProcessId(), "Changing the modifier must hot-reload without restarting the module.");
    }

    [TestMethod]
    public void ExcludedApplicationCannotBeMovedOrResized()
    {
        DragAndAssert(Key.Alt, resize: false);
        DragAndAssert(Key.Alt, resize: true);
        var processId = ModuleProcessId();
        var executable = Path.GetFileName(Environment.ProcessPath)
            ?? throw new InvalidOperationException("The fixture executable name is unavailable.");

        Step($"Excluding the fixture application '{executable}' through Settings");
        var excluded = Find<TextBox>(By.AccessibilityId("TextBox_GrabAndMove_ExcludedApps"));
        excluded.SetText(executable);
        WaitForSetting("excluded_apps", executable);
        WaitForUnacknowledgedReload();
        DragAndAssert(Key.Alt, resize: false, expectedChange: false);
        DragAndAssert(Key.Alt, resize: true, expectedChange: false);

        Step("Removing the application exclusion and verifying the same window works again");
        excluded.SetText(string.Empty);
        WaitForSetting("excluded_apps", string.Empty);
        DragAndAssert(Key.Alt, resize: false);
        DragAndAssert(Key.Alt, resize: true);
        Assert.AreEqual(processId, ModuleProcessId(), "Exclusion changes must apply without restarting the module.");
    }

    [TestMethod]
    public void DisablingModuleStopsModifierMoveAndResize()
    {
        DragAndAssert(Key.Alt, resize: false);
        DragAndAssert(Key.Alt, resize: true);

        Step("Disabling Grab And Move through Settings and awaiting process exit");
        SetToggle(EnableToggle(), false);
        WaitForModule(expected: false);
        DragAndAssert(Key.Alt, resize: false, expectedChange: false);
        DragAndAssert(Key.Alt, resize: true, expectedChange: false);

        Step("Re-enabling Grab And Move through Settings");
        SetToggle(EnableToggle(), true);
        WaitForModule(expected: true);
        DragAndAssert(Key.Alt, resize: false);
        DragAndAssert(Key.Alt, resize: true);
    }

    [TestCleanup]
    public async Task CloseFixture()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();
        try
        {
            MouseHelper.LeftUp();
            MouseHelper.RightUp();
            KeyboardHelper.ReleaseKey(Key.Alt);
            KeyboardHelper.ReleaseKey(Key.LWin);
        }
        finally
        {
            fixture?.Dispose();
            fixture = null;
        }
    }

    private static void StopProduct()
    {
        foreach (var name in new[] { "PowerToys", "PowerToys.Settings", ModuleProcess })
        {
            if (!WindowControl.TryKillProcessTreeByNameAndWait(name, 10_000))
            {
                throw new InvalidOperationException($"Could not stop '{name}' before restoring module settings.");
            }
        }
    }

    private static int ModuleProcessId()
    {
        var processes = Process.GetProcessesByName(ModuleProcess);
        try
        {
            Assert.HasCount(1, processes, $"Expected exactly one {ModuleProcess} process.");
            return processes[0].Id;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static void WaitForModule(bool expected)
    {
        var result = WaitHelper.WaitForStable(
            () =>
            {
                var processes = Process.GetProcessesByName(ModuleProcess);
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
            count => count == (expected ? 1 : 0),
            timeoutMS: 15_000,
            requiredConsecutiveMatches: 3);
        Assert.IsTrue(result.Succeeded, $"Expected {ModuleProcess} running={expected}; last process count={result.LastObservation}. Check Runner Settings IPC logs.");
    }

    private bool IsGeometryVisible()
    {
        var bounds = fixture!.Bounds;
        return WindowControl.EnumerateProcessWindows([ModuleProcessId()]).Any(window =>
            window.ClassName == OverlayClass && window.IsVisible &&
            window.Width < bounds.Width && window.Height < bounds.Height);
    }

    private void AssertGeometryVisible(bool expected)
    {
        var result = WaitHelper.WaitForStable(IsGeometryVisible, visible => visible == expected, 5_000, requiredConsecutiveMatches: 3);
        Assert.IsTrue(result.Succeeded, $"Expected geometry overlay visible={expected}; last visible={result.LastObservation}.");
    }

    private static void SetToggle(ToggleSwitch toggle, bool enabled)
    {
        if (toggle.IsOn != enabled)
        {
            toggle.Invoke(msPostAction: 0);
        }

        Assert.IsTrue(toggle.WaitForProperty("ToggleState", enabled ? "On" : "Off", 5_000), $"The Settings switch did not become {enabled}.");
    }

    private static void WaitForSetting<T>(string property, T expected)
    {
        var result = WaitHelper.WaitForStable(
            () =>
            {
                using var stream = new FileStream(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var settings = JsonDocument.Parse(stream);
                return settings.RootElement.GetProperty("properties").GetProperty(property).GetProperty("value").Deserialize<T>();
            },
            value => EqualityComparer<T>.Default.Equals(expected, value),
            10_000,
            requiredConsecutiveMatches: 3,
            shouldRetryException: error => error is IOException or JsonException);
        Assert.IsTrue(result.Succeeded, $"Runner did not persist {property}={expected}; last value={result.LastObservation}, error={result.LastException}.");
    }

    private static void WaitForUnacknowledgedReload()
    {
        // Persistence precedes delivery of the reload event, which has no acknowledgement.
        // Negative assertions need this bounded settle so old settings cannot produce a false pass.
        Thread.Sleep(3_000);
    }

    private static string ForegroundDiagnostic()
    {
        var foreground = WindowControl.GetForegroundWindowInfo();
        return $"Foreground HWND={foreground.Hwnd}, process='{foreground.ProcessName}', title='{foreground.Title}'.";
    }

    private ToggleSwitch EnableToggle() => Find<ToggleSwitch>(By.Name("Grab And Move"));

    private void FocusFixture()
    {
        Assert.IsNotNull(fixture);
        Step($"Focusing fixture HWND={fixture.Handle}");
        fixture.Focus();
    }

    private void DragAndAssert(Key modifier, bool resize, bool expectedChange = true, Action? duringDrag = null)
    {
        Assert.IsNotNull(fixture);
        FocusFixture();
        var before = fixture.Bounds;
        var start = resize
            ? new Point(before.Right - 60, before.Bottom - 60)
            : new Point(before.Left + (before.Width / 2), before.Top + (before.Height / 2));
        var expected = expectedChange
            ? resize
                ? new Rectangle(before.X, before.Y, before.Width + DeltaX, before.Height + DeltaY)
                : new Rectangle(before.X + DeltaX, before.Y + DeltaY, before.Width, before.Height)
            : before;
        Step($"{modifier}+{(resize ? "right" : "left")} drag from {start}; before={before}, expected={expected}");
        MouseHelper.MoveTo(start.X, start.Y);
        var cursor = WaitHelper.WaitForStable(
            MouseHelper.GetMousePosition,
            position => position == (start.X, start.Y),
            5_000,
            requiredConsecutiveMatches: 3);
        Assert.IsTrue(cursor.Succeeded, $"The gesture cursor did not settle at {start}: {cursor.LastObservation}.");
        try
        {
            KeyboardHelper.PressKey(modifier);
            if (resize)
            {
                MouseHelper.RightDown();
            }
            else
            {
                MouseHelper.LeftDown();
            }

            for (var step = 1; step <= 16; step++)
            {
                MouseHelper.MoveTo(start.X + (DeltaX * step / 16), start.Y + (DeltaY * step / 16));
                Thread.Sleep(30);
            }

            var endpoint = WaitHelper.WaitForStable(
                MouseHelper.GetMousePosition,
                position => position == (start.X + DeltaX, start.Y + DeltaY),
                5_000,
                requiredConsecutiveMatches: 3);
            Assert.IsTrue(endpoint.Succeeded, $"The gesture cursor did not reach its endpoint: {endpoint.LastObservation}.");

            duringDrag?.Invoke();
        }
        finally
        {
            if (resize)
            {
                MouseHelper.RightUp();
            }
            else
            {
                MouseHelper.LeftUp();
            }

            KeyboardHelper.ReleaseKey(modifier);
        }

        if (expectedChange)
        {
            AssertBounds(expected);
        }
        else
        {
            var observation = Stopwatch.StartNew();
            do
            {
                Assert.AreEqual(expected, fixture.Bounds, "A blocked modifier gesture changed the fixture geometry.");
                Thread.Sleep(100);
            }
            while (observation.ElapsedMilliseconds < 2_000);
        }
    }

    private void AssertBounds(Rectangle expected)
    {
        var result = WaitHelper.WaitForStable(() => fixture!.Bounds, bounds => bounds == expected, 5_000, requiredConsecutiveMatches: 3);
        Assert.IsTrue(result.Succeeded, $"Expected exact physical bounds {expected}; last bounds={result.LastObservation}. {ForegroundDiagnostic()}");
    }

    private void Step(string message) => TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
}
