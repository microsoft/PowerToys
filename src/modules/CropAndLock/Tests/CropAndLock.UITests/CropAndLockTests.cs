// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AutomationButton = Microsoft.PowerToys.UITest.Next.Button;

namespace Microsoft.CropAndLock.UITests
{
    [TestClass]
    [DoNotParallelize]
    [TestCategory("CropAndLock")]
    public sealed class CropAndLockTests : UITestBase
    {
        private const string ModuleProcess = "PowerToys.CropAndLock";
        private const string OverlayClass = "CropAndLock.OverlayWindow";
        private const string ThumbnailClass = "CropAndLock.ThumbnailCropAndLockWindow";
        private const string ReparentClass = "CropAndLock.ReparentCropAndLockWindow";
        private const string InitialText = "iiiiiiiiiiiiiiii";
        private const string ChangedText = "wwwwwwwwwwwwwwww";

        private CropSource? source;
        private int moduleProcessId;
        private string? originalClipboard;
        private ToggleSwitch? lifecycleToggle;
        private bool originalToggleState;

        public CropAndLockTests()
            : base(PowerToysModule.PowerToysSettings, enableModules: ["CropAndLock"])
        {
        }

        protected override IReadOnlyList<string> StaleProcessNames => [.. base.StaleProcessNames, ModuleProcess];

        [ClassInitialize]
        public static void RequireNonElevatedHost(TestContext context)
        {
            Assert.IsFalse(ElevationHelper.IsCurrentProcessElevated(), "Run this suite non-elevated so Runner and the packaged source have the same integrity level.");
        }

        [TestMethod]
        public void ThumbnailWin32ShowsSelectedRegionAndLiveUpdates()
        {
            RunThumbnail(new Win32CropSource());
        }

        [TestMethod]
        public void ThumbnailPackagedAppShowsSelectedRegionAndLiveUpdates()
        {
            RunThumbnail(new PackagedCropSource());
        }

        [TestMethod]
        public void ReparentWin32SupportsInputAndRestoresSource()
        {
            RunReparent(new Win32CropSource());
        }

        [TestMethod]
        public void ReparentPackagedAppSupportsInputAndRestoresSource()
        {
            RunReparent(new PackagedCropSource());
        }

        [TestMethod]
        public void SettingsToggleStopsAndStartsModule()
        {
            SigningPrerequisites.RequireSettingsClient();
            PrepareModule();
            lifecycleToggle = Find<ToggleSwitch>(By.Name("Crop And Lock"));
            originalToggleState = lifecycleToggle.IsOn;
            var originalProcessId = moduleProcessId;

            Step("Disabling Crop And Lock through Settings; awaiting immediate process exit");
            lifecycleToggle.Invoke();
            Assert.IsTrue(lifecycleToggle.WaitForProperty("ToggleState", "Off", 5_000), "The Settings toggle did not turn off.");
            Assert.IsTrue(
                WaitHelper.WaitForStable(
                    () => NativeMethods.ProcessIds(ModuleProcess).Count,
                    count => count == 0,
                    timeoutMS: 10_000,
                    requiredConsecutiveMatches: 3).Succeeded,
                "Crop And Lock remained running after Settings disabled it. Check Runner Settings IPC authentication diagnostics.");

            Step("Enabling Crop And Lock through Settings; awaiting a new stable process");
            lifecycleToggle.Invoke();
            Assert.IsTrue(lifecycleToggle.WaitForProperty("ToggleState", "On", 5_000), "The Settings toggle did not turn on.");
            WaitForModule();
            Assert.AreNotEqual(originalProcessId, moduleProcessId, "Enabling must launch a new module process.");
        }

        [TestMethod]
        public void EscapeCancelsSelectionWithoutChangingSource()
        {
            var keys = PrepareModuleAndReadShortcut(reparent: false);
            PrepareSource(new Win32CropSource());
            var beforeState = NativeMethods.ReadState(source!.Window);
            var beforeImage = CaptureStable(source.Window, source.CropBounds, "cancel-source-before");
            var overlay = Activate(keys);

            Step("Cancelling the selection with Escape");
            KeyboardHelper.SendKeys(Key.Esc);
            Assert.IsTrue(
                WaitHelper.WaitForStable(() => NativeMethods.IsWindow(overlay), exists => !exists, 5_000).Succeeded,
                "Escape did not destroy the selection overlay.");
            Assert.AreEqual(IntPtr.Zero, FindModuleWindow(ThumbnailClass), "Escape unexpectedly created a thumbnail.");
            Assert.AreEqual(IntPtr.Zero, FindModuleWindow(ReparentClass), "Escape unexpectedly reparented the source.");
            AssertRestored(beforeState);
            AssertPixels(beforeImage, source.Window, source.CropBounds, "cancel-source-after");
        }

        [TestCleanup]
        public async Task RestoreOwnedWindows()
        {
            await CaptureFailureArtifactsBeforeCleanupAsync();
            var failed = TestContext.CurrentTestOutcome is UnitTestOutcome.Failed or UnitTestOutcome.Error or UnitTestOutcome.Unknown;
            var errors = new List<Exception>();

            Clean(() =>
            {
                foreach (var window in WindowControl.EnumerateProcessWindows(NativeMethods.ProcessIds(ModuleProcess)))
                {
                    if (window.ClassName is OverlayClass or ThumbnailClass or ReparentClass)
                    {
                        Assert.IsTrue(WindowControl.TryCloseWindow(window.Hwnd.ToInt64()), $"Could not close {window.ClassName}.");
                    }
                }
            });
            Clean(() => source?.Dispose());
            Clean(() =>
            {
                if (lifecycleToggle is not null && lifecycleToggle.IsOn != originalToggleState)
                {
                    lifecycleToggle.Invoke();
                    Assert.IsTrue(lifecycleToggle.WaitForProperty("ToggleState", originalToggleState ? "On" : "Off", 5_000));
                }
            });
            Clean(() =>
            {
                if (originalClipboard is not null)
                {
                    Assert.IsTrue(
                        originalClipboard.Length == 0 ? ClipboardHelper.Clear() : ClipboardHelper.SetText(originalClipboard),
                        "Could not restore the original clipboard text.");
                }
            });
            if (errors.Count > 0 && !failed)
            {
                Assert.Fail($"Fixture cleanup failed: {string.Join(Environment.NewLine, errors.Select(error => error.Message))}");
            }

            void Clean(Action action)
            {
                try
                {
                    action();
                }
                catch (Exception error) when (error is AssertFailedException or Win32Exception or InvalidOperationException or TimeoutException or COMException)
                {
                    errors.Add(error);
                    TestContext.WriteLine($"Cleanup: {error}");
                }
            }
        }

        private void RunThumbnail(CropSource fixture)
        {
            var keys = PrepareModuleAndReadShortcut(reparent: false);
            PrepareSource(fixture);
            var beforeState = NativeMethods.ReadState(source!.Window);
            var beforeImage = CaptureStable(source.Window, source.CropBounds, "thumbnail-source-before");
            var cropped = Crop(keys, ThumbnailClass);
            AssertCropSize(cropped);
            MoveThumbnailAwayFromSource(cropped);
            AssertRestored(beforeState);
            AssertPixels(beforeImage, cropped, ClientRegion(cropped), "thumbnail-output-before");

            Step("Editing the original source while the same thumbnail remains open");
            EditSource(ChangedText, source.Window);
            var changed = CaptureStable(source.Window, source.CropBounds, "thumbnail-source-after");
            Assert.IsFalse(CropImage.Compare(beforeImage, changed).Matches, "The fixture's visible source did not change.");
            AssertPixels(changed, cropped, ClientRegion(cropped), "thumbnail-output-after");
            AssertRestored(beforeState);

            Step("Closing the thumbnail; the source must survive unchanged");
            Assert.IsTrue(WindowControl.TryCloseWindow(cropped.ToInt64()), "The thumbnail did not close.");
            AssertRestored(beforeState);
            EditSource("restored5", source.Window);
        }

        private void RunReparent(CropSource fixture)
        {
            var keys = PrepareModuleAndReadShortcut(reparent: true);
            PrepareSource(fixture);
            var beforeState = NativeMethods.ReadState(source!.Window);
            var beforeImage = CaptureStable(source.Window, source.CropBounds, "reparent-source-before");
            var cropped = Crop(keys, ReparentClass);
            AssertCropSize(cropped);

            Step("Checking the source HWND is a child of the product's clipping window");
            var reparented = WaitHelper.WaitForStable(
                () => NativeMethods.ReadState(source.Window),
                state => state.Parent != IntPtr.Zero &&
                    NativeMethods.ClassName(state.Parent) == "CropAndLock.ChildWindow" &&
                    NativeMethods.GetParent(state.Parent) == cropped &&
                    (state.Style & NativeMethods.ChildStyle) != 0,
                timeoutMS: 10_000,
                requiredConsecutiveMatches: 3);
            Assert.IsTrue(reparented.Succeeded, $"Source was not reparented into the crop: {reparented.LastObservation}.");
            Assert.AreEqual(beforeState.Style | NativeMethods.ChildStyle, reparented.LastObservation.Style, "Reparent changed unrelated source window styles.");
            Assert.AreEqual(beforeState.Bounds.Size, reparented.LastObservation.Bounds.Size, "Reparent resized the source instead of clipping it.");
            AssertPixels(beforeImage, cropped, ClientRegion(cropped), "reparent-output-before");

            Step("Typing through the cropped source and verifying its text via Copy");
            EditSource(ChangedText, cropped);
            var changed = CaptureStable(cropped, ClientRegion(cropped), "reparent-output-after");
            Assert.IsFalse(CropImage.Compare(beforeImage, changed).Matches, "Input did not change the visible reparented content.");

            Step("Closing the crop; verifying original parent, styles, geometry, pixels and input");
            Assert.IsTrue(WindowControl.TryCloseWindow(cropped.ToInt64()), "The reparent window did not close.");
            AssertRestored(beforeState);
            AssertPixels(changed, source.Window, source.CropBounds, "reparent-restored-source");
            EditSource("restored5", source.Window);
        }

        private void PrepareModule()
        {
            Step("Navigating to Crop And Lock Settings");
            if (!Session.Has(By.AccessibilityId("CropAndLockNavItem"), timeoutMS: 500))
            {
                Find<NavigationViewItem>(By.AccessibilityId("WindowingAndLayoutsNavItem")).Click();
            }

            Find<NavigationViewItem>(By.AccessibilityId("CropAndLockNavItem")).Click();
            Assert.IsTrue(Find<ToggleSwitch>(By.Name("Crop And Lock")).IsOn, "The isolated Crop And Lock module must start enabled.");
            WaitForModule();
        }

        private Key[] PrepareModuleAndReadShortcut(bool reparent)
        {
            PrepareModule();
            var cardId = reparent ? "CropAndLockReparentActivationShortcut" : "CropAndLockThumbnailActivationShortcut";
            Step($"Reading the live shortcut from {cardId}");
            var card = Find<Element>(By.AccessibilityId(cardId));
            var bounds = new Rectangle(card.X, card.Y, card.Width, card.Height);

            // Element.Find currently searches the owning session, not the subtree. Resolve the
            // EditButton inside this named card, rather than silently reading the first mode's chord.
            var buttons = Session.FindAll<AutomationButton>(By.AccessibilityId("EditButton"))
                .Where(button => button.Width > 0 && button.Height > 0 &&
                    bounds.Contains(new Point(button.X + (button.Width / 2), button.Y + (button.Height / 2))))
                .ToArray();
            Assert.HasCount(1, buttons, $"Expected exactly one shortcut EditButton inside {cardId}.");
            var shortcut = WaitHelper.WaitForStable(
                () => buttons[0].HelpText,
                text =>
                {
                    var keys = ParseShortcut(text);
                    Assert.IsTrue(
                        string.IsNullOrWhiteSpace(text) || keys.Length > 0,
                        $"Shortcut '{text}' could not be parsed; this suite requires an enabled shortcut in the English Settings UI.");
                    return keys.Any(key => key is not (Key.LWin or Key.Ctrl or Key.Shift or Key.Alt));
                },
                timeoutMS: 5_000,
                requiredConsecutiveMatches: 2);
            Assert.IsTrue(shortcut.Succeeded, $"{cardId} exposed no usable activation shortcut: '{shortcut.LastObservation}'.");
            Step($"Live {cardId} shortcut: {shortcut.LastObservation}");
            return ParseShortcut(shortcut.LastObservation);
        }

        private static Key[] ParseShortcut(string? text)
        {
            var tokens = (text ?? string.Empty).Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var keys = tokens.Select(ParseKey).ToArray();
            return keys.All(key => key.HasValue) ? keys.Select(key => key!.Value).ToArray() : [];
        }

        private static Key? ParseKey(string text)
        {
            return text.ToLowerInvariant() switch
            {
                "win" or "windows" => Key.LWin,
                "ctrl" or "control" => Key.Ctrl,
                "shift" => Key.Shift,
                "alt" => Key.Alt,
                _ when text.Length == 1 && char.IsAsciiDigit(text[0]) => Enum.Parse<Key>($"Num{text}"),
                _ when char.IsAsciiLetter(text[0]) && Enum.TryParse<Key>(text, true, out var key) => key,
                _ => null,
            };
        }

        private void WaitForModule()
        {
            var previous = 0;
            var stable = WaitHelper.WaitForStable(
                () =>
                {
                    var processes = NativeMethods.ProcessIds(ModuleProcess);
                    var current = processes.Count == 1 ? processes[0] : 0;
                    var same = current != 0 && current == previous;
                    previous = current;
                    return same;
                },
                value => value,
                timeoutMS: 15_000,
                requiredConsecutiveMatches: 3);
            Assert.IsTrue(stable.Succeeded, "Exactly one stable PowerToys.CropAndLock process must be running.");
            moduleProcessId = previous;
            Assert.IsFalse(ElevationHelper.IsProcessElevated(moduleProcessId) ?? true, "Crop And Lock must match the non-elevated packaged source.");
        }

        private void PrepareSource(CropSource fixture)
        {
            source = fixture;
            originalClipboard = ClipboardHelper.GetText();
            Step($"Opening {fixture.GetType().Name}");
            source.Open(TestContext);
            Assert.IsTrue(NativeMethods.IsWindow(source.Window), "The fixture did not create a source HWND.");
            Assert.AreEqual(IntPtr.Zero, NativeMethods.GetParent(source.Window), "The source must start as a top-level window.");
            Assert.IsTrue(
                new Rectangle(Point.Empty, NativeMethods.ClientBounds(source.Window).Size).Contains(source.CropBounds),
                "The crop must lie entirely inside the source client area.");
            EditSource(InitialText, source.Window);
        }

        private IntPtr Activate(Key[] keys)
        {
            Assert.IsNotNull(source);
            Step("Focusing the source and activating the real Crop And Lock shortcut");
            IntPtr overlay = IntPtr.Zero;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                overlay = FindModuleWindow(OverlayClass);
                if (overlay != IntPtr.Zero)
                {
                    break;
                }

                Assert.IsTrue(
                    WindowControl.WaitForForeground(source.Window, timeoutMS: 8_000, requiredConsecutiveMatches: 3),
                    $"The Runner must capture this exact source HWND 0x{source.Window:X}; actual foreground: {WindowControl.GetForegroundWindowInfo()}");
                KeyboardHelper.SendKeys(keys);
                var appeared = WaitHelper.WaitForStable(
                    () => FindModuleWindow(OverlayClass),
                    window => window != IntPtr.Zero,
                    timeoutMS: 8_000);
                overlay = appeared.LastObservation;
                if (appeared.Succeeded)
                {
                    break; // Never resend after even an initializing/hidden overlay HWND exists.
                }
            }

            Assert.AreNotEqual(IntPtr.Zero, overlay, $"The shortcut did not create {OverlayClass}. Foreground: {WindowControl.GetForegroundWindowInfo()}");
            RequireForeground(overlay);
            return overlay;
        }

        private IntPtr Crop(Key[] keys, string resultClass)
        {
            var selection = source!.ScreenCrop();
            var overlay = Activate(keys);
            Assert.IsTrue(WindowControl.IsPointOwnedByWindow(overlay, selection.Left, selection.Top), "The selection start is not on the active overlay.");
            Step($"Dragging the source client selection {selection}");
            MouseHelper.Drag(selection.Left, selection.Top, selection.Right, selection.Bottom);

            var created = WaitHelper.WaitForStable(
                () => FindModuleWindow(resultClass),
                window => window != IntPtr.Zero && !WindowHelper.IsWindowCloaked(window),
                timeoutMS: 15_000,
                requiredConsecutiveMatches: 3);
            Assert.IsTrue(created.Succeeded, $"The drag did not create {resultClass}. Windows: {string.Join(", ", ModuleWindows())}");
            Assert.IsTrue(
                WaitHelper.WaitForStable(() => NativeMethods.IsWindow(overlay), exists => !exists, 5_000).Succeeded,
                "The selection overlay remained after completing the crop.");
            return created.LastObservation;
        }

        private void AssertCropSize(IntPtr cropped)
        {
            var bounds = WaitHelper.WaitForStable(
                () => NativeMethods.ClientBounds(cropped).Size,
                size => size == source!.CropBounds.Size,
                timeoutMS: 5_000,
                requiredConsecutiveMatches: 3);
            Assert.IsTrue(bounds.Succeeded, $"Crop client size {bounds.LastObservation} did not match selected size {source!.CropBounds.Size}.");
            Assert.AreNotEqual(0L, NativeMethods.ReadState(cropped).ExtendedStyle & NativeMethods.TopmostStyle, "The crop must be always on top.");
        }

        private void MoveThumbnailAwayFromSource(IntPtr cropped)
        {
            var monitor = MonitorInfo.GetFromWindow(source!.Window);
            var size = NativeMethods.ReadState(cropped).Bounds.Size;
            WindowHelper.MoveWindow(cropped, monitor.WorkRight - size.Width - 24, monitor.WorkBottom - size.Height - 24);
            Assert.IsFalse(NativeMethods.ReadState(cropped).Bounds.IntersectsWith(source.ScreenCrop()), "The thumbnail would occlude its source during the live-update assertion.");
        }

        private void EditSource(string text, IntPtr inputRoot)
        {
            var typedKeys = text.Select(character => ParseKey(character.ToString())).ToArray();
            Assert.IsTrue(typedKeys.All(key => key.HasValue), $"Unsupported input character in '{text}'.");
            Step($"Waiting for the source input to own its screen point through HWND 0x{inputRoot:X}");
            RequireForeground(inputRoot);
            var dismissedShellOverlay = false;
            var ready = WaitHelper.WaitForStable(
                () =>
                {
                    var input = source!.ScreenInput();
                    return (Point: input, Root: NativeMethods.RootAtPoint(input));
                },
                state => state.Root == inputRoot,
                timeoutMS: 8_000,
                requiredConsecutiveMatches: 3,
                recover: state =>
                {
                    if (!dismissedShellOverlay && NativeMethods.ClassName(state.Root) == "Shell_LightDismissOverlay")
                    {
                        Step($"Dismissing the Shell outside-click overlay 0x{state.Root:X} before source input");
                        MouseHelper.LeftClickAt(state.Point.X, state.Point.Y);
                        dismissedShellOverlay = true;
                    }
                    else
                    {
                        WindowControl.TryBringToForeground(inputRoot);
                    }
                });
            var ownershipDiagnostic =
                $"The source input is occluded or outside the crop. Point={ready.LastObservation.Point}, expected=0x{inputRoot:X}, " +
                $"actual=0x{ready.LastObservation.Root:X} ({NativeMethods.ClassName(ready.LastObservation.Root)}), foreground={WindowControl.GetForegroundWindowInfo()}.";
            Assert.IsTrue(ready.Succeeded, ownershipDiagnostic);
            var point = ready.LastObservation.Point;
            Step($"Sending real input at {point} through HWND 0x{inputRoot:X}");
            MouseHelper.LeftClickAt(point.X, point.Y);
            KeyboardHelper.SendKeys(Key.Ctrl, Key.A);
            KeyboardHelper.SendKeys(typedKeys.Select(key => key!.Value).ToArray());
            Assert.IsTrue(ClipboardHelper.SetText("crop-and-lock-copy-sentinel"), "Could not seed the Copy assertion's clipboard sentinel.");
            KeyboardHelper.SendKeys(Key.Ctrl, Key.A);
            KeyboardHelper.SendKeys(Key.Ctrl, Key.C);
            var copied = WaitHelper.WaitForStable(
                ClipboardHelper.GetText,
                value => value == text,
                timeoutMS: 5_000,
                requiredConsecutiveMatches: 2);
            Assert.IsTrue(copied.Succeeded, $"The source did not accept input through the crop. Copied '{copied.LastObservation}', expected '{text}'.");
            KeyboardHelper.SendKeys(Key.Right);
        }

        private static void RequireForeground(IntPtr root)
        {
            var ready = WaitHelper.WaitForStable(
                WindowControl.GetForegroundWindowHandle,
                foreground => foreground == root || NativeMethods.Root(foreground) == root,
                timeoutMS: 8_000,
                requiredConsecutiveMatches: 3,
                recover: _ => WindowControl.TryBringToForeground(root));
            Assert.IsTrue(ready.Succeeded, $"Expected foreground HWND 0x{root:X} or its child; actual: {WindowControl.GetForegroundWindowInfo()}");
        }

        private void AssertRestored(NativeMethods.WindowState expected)
        {
            Assert.IsTrue(NativeMethods.IsWindow(source!.Window), "Closing the crop destroyed the source HWND.");
            var restored = WaitHelper.WaitForStable(
                () => NativeMethods.ReadState(source.Window),
                state => state == expected,
                timeoutMS: 10_000,
                requiredConsecutiveMatches: 3);
            Assert.IsTrue(restored.Succeeded, $"Source parent/style/extended style/geometry changed. Expected {expected}; actual {restored.LastObservation}.");
        }

        private CropImage CaptureStable(IntPtr window, Rectangle region, string name)
        {
            Step($"Waiting for stable composed pixels: {name}");
            var path = ArtifactPath(name);
            CropImage? last = null;
            var stable = WaitHelper.WaitForStable(
                () =>
                {
                    var current = CropImage.Capture(window, region, path);
                    var comparison = last is null ? default : CropImage.Compare(last, current);
                    last = current;
                    return comparison;
                },
                comparison => comparison.Matches,
                timeoutMS: 15_000,
                requiredConsecutiveMatches: 3,
                pollIntervalMS: 200);
            TestContext.AddResultFile(path);
            Assert.IsTrue(stable.Succeeded, $"The source/crop did not render stable nonblank content: {name}: {stable.LastObservation}.");
            return last!;
        }

        private void AssertPixels(CropImage expected, IntPtr window, Rectangle region, string name)
        {
            Step($"Comparing selected-source pixels to composed output: {name}");
            var path = ArtifactPath(name);
            var matched = WaitHelper.WaitForStable(
                () => CropImage.Compare(expected, CropImage.Capture(window, region, path)),
                comparison => comparison.Matches,
                timeoutMS: 15_000,
                requiredConsecutiveMatches: 3,
                pollIntervalMS: 200);
            TestContext.AddResultFile(path);
            Assert.IsTrue(matched.Succeeded, $"The crop does not show the selected source pixels: {name}: {matched.LastObservation}.");
        }

        private string ArtifactPath(string name)
        {
            var directory = Path.Combine(
                TestContext.TestResultsDirectory ?? Path.Combine(Environment.CurrentDirectory, "TestResults"),
                "CropAndLock",
                TestContext.TestName ?? nameof(CropAndLockTests));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, $"{name}.png");
        }

        private static Rectangle ClientRegion(IntPtr window) => new(Point.Empty, NativeMethods.ClientBounds(window).Size);

        private IReadOnlyList<WindowControl.ProcessWindow> ModuleWindows()
        {
            var current = NativeMethods.ProcessIds(ModuleProcess);
            Assert.HasCount(1, current, $"Expected one {ModuleProcess}; observed: {string.Join(", ", current)}.");
            Assert.AreEqual(moduleProcessId, current[0], "Crop And Lock exited or restarted during the scenario.");
            return WindowControl.EnumerateProcessWindows([moduleProcessId]);
        }

        private IntPtr FindModuleWindow(string className) =>
            ModuleWindows().FirstOrDefault(window => window.ClassName == className).Hwnd;

        private void Step(string message) => TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
    }
}
