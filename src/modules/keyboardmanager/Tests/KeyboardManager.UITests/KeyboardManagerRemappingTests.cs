// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.KeyboardManager.UITests;

[TestClass]
[DoNotParallelize]
public sealed class KeyboardManagerRemappingTests : KeyboardManagerTestBase
{
    private const int A = 0x41;
    private const int B = 0x42;
    private const int D = 0x44;
    private const int E = 0x45;
    private const int F = 0x46;
    private const int Q = 0x51;
    private const int U = 0x55;
    private const int V = 0x56;
    private const int W = 0x57;
    private const int LeftControl = 0xA2;
    private const int LeftAlt = 0xA4;
    private const int LeftWindows = 0x5B;
    private const int Tab = 0x09;

    private static KeyboardInputFixtureLock? inputFixtureLock;
    private static int profileGeneration;

    protected override bool ReuseScopeAcrossTests => true;

    [ClassInitialize]
    public static void InitializeClass(TestContext testContext)
    {
        _ = testContext;
        inputFixtureLock = KeyboardInputFixtureLock.Acquire();
    }

    [ClassCleanup]
    public static void CleanupClass()
    {
        inputFixtureLock?.Dispose();
        inputFixtureLock = null;
    }

    [TestCleanup]
    public Task CleanupTest() => CleanupKeyboardManagerTestAsync();

    [TestMethod("KeyboardManager.Remapping.SingleKey")]
    [TestCategory("Keyboard Manager")]
    [DataRow("A", A, "B", B)]
    [DataRow("Left Ctrl", LeftControl, "A", A)]
    [DataRow("A", A, "Left Ctrl", LeftControl)]
    [DataRow("Left Win", LeftWindows, "B", B)]
    [DataRow("B", B, "Left Win", LeftWindows)]
    public void SingleKeyRemappingMaintainsPressReleaseState(
        string sourceName,
        int sourceKey,
        string targetName,
        int targetKey)
    {
        using var input = new NotepadInputWindow();
        using var recorder = new KeyboardEventRecorder();
        ApplyProfileAndVerify(
            input,
            recorder,
            KeyboardManagerSettings.BuildProfile(
                singleKeyRemaps: new[] { KeyboardManagerSettings.SingleKeyRemap(sourceKey, targetKey) }));

        Step($"Holding {sourceName} and expecting {targetName} down");
        recorder.Clear();
        Assert.IsTrue(input.FocusInput(), "The keyboard input window did not own foreground before single-key input.");
        try
        {
            KeyboardHelper.PressKey(ToKey(sourceKey));
            Assert.IsTrue(
                recorder.WaitForSequence(
                    KeyboardManagerTestConstants.SingleKeyInjectedFlag,
                    timeoutMS: 3_000,
                    new ExpectedKeyboardEvent(targetKey, true)),
                $"{sourceName} did not generate {targetName} down. Events: {recorder.DescribeGeneratedEvents()}.");
            Assert.IsTrue(
                WaitForKeyState(targetKey, expected: true, timeoutMS: 1_000),
                $"{targetName} was not held while {sourceName} remained down.");
        }
        finally
        {
            KeyboardHelper.ReleaseKey(ToKey(sourceKey));
        }

        Assert.IsTrue(
            recorder.WaitForSequence(
                KeyboardManagerTestConstants.SingleKeyInjectedFlag,
                timeoutMS: 3_000,
                new ExpectedKeyboardEvent(targetKey, true),
                new ExpectedKeyboardEvent(targetKey, false)),
            $"{sourceName} did not release {targetName}. Events: {recorder.DescribeGeneratedEvents()}.");
        Assert.IsTrue(
            WaitForKeyState(targetKey, expected: false, timeoutMS: 1_000),
            $"{targetName} remained logically down after {sourceName} was released.");

        if (targetKey == LeftWindows)
        {
            DismissShellAndRefocus(input, $"{sourceName} to {targetName}");
        }
        else if (sourceKey == LeftWindows)
        {
            Assert.IsTrue(
                WindowControl.WaitForForeground(input.Handle, timeoutMS: 2_000, requiredConsecutiveMatches: 2),
                "Remapping the physical Windows key to a non-Windows target unexpectedly activated a Shell surface.");
        }
    }

    [TestMethod("KeyboardManager.Remapping.Disable")]
    [TestCategory("Keyboard Manager")]
    [DataRow("A", A)]
    [DataRow("Left Win", LeftWindows)]
    public void DisabledKeyIsSuppressedWithoutOpeningShell(string sourceName, int sourceKey)
    {
        using var input = new NotepadInputWindow();
        using var recorder = new KeyboardEventRecorder();
        ApplyProfileAndVerify(
            input,
            recorder,
            KeyboardManagerSettings.BuildProfile(
                singleKeyRemaps: new[]
                {
                    KeyboardManagerSettings.SingleKeyRemap(sourceKey, KeyboardManagerTestConstants.DisabledKey),
                }));

        recorder.Clear();
        Assert.IsTrue(input.FocusInput(), "The keyboard input window did not own foreground before disabled-key input.");
        try
        {
            Step($"Holding disabled key {sourceName}");
            KeyboardHelper.PressKey(ToKey(sourceKey));
            Assert.IsTrue(
                WaitForKeyState(sourceKey, expected: false, timeoutMS: 1_000),
                $"Disabled key {sourceName} reached the system key state instead of being suppressed.");
        }
        finally
        {
            KeyboardHelper.ReleaseKey(ToKey(sourceKey));
        }

        Assert.AreEqual(string.Empty, input.Text, $"Disabled key {sourceName} produced text.");
        Assert.IsTrue(
            WindowControl.WaitForForeground(input.Handle, timeoutMS: 2_000, requiredConsecutiveMatches: 2),
            "A disabled Windows key opened a Shell surface or moved foreground.");
    }

    [TestMethod("KeyboardManager.Remapping.KeyToShortcut")]
    [TestCategory("Keyboard Manager")]
    [DataRow("A", A, "Ctrl+V", LeftControl, V)]
    [DataRow("B", B, "Win+A", LeftWindows, A)]
    public void KeyToShortcutProducesAndReleasesCompleteChord(
        string sourceName,
        int sourceKey,
        string targetName,
        int targetModifier,
        int targetAction)
    {
        using var input = new NotepadInputWindow();
        using var recorder = new KeyboardEventRecorder();
        ApplyProfileAndVerify(
            input,
            recorder,
            KeyboardManagerSettings.BuildProfile(
                singleKeyRemaps: new[]
                {
                    KeyboardManagerSettings.SingleKeyRemap(sourceKey, targetModifier, targetAction),
                }));

        recorder.Clear();
        Assert.IsTrue(input.FocusInput(), "The keyboard input window did not own foreground before key-to-shortcut input.");
        try
        {
            Step($"Holding {sourceName} and expecting {targetName}");
            KeyboardHelper.PressKey(ToKey(sourceKey));
            Assert.IsTrue(
                recorder.WaitForSequence(
                    KeyboardManagerTestConstants.SingleKeyInjectedFlag,
                    timeoutMS: 3_000,
                    new ExpectedKeyboardEvent(targetModifier, true),
                    new ExpectedKeyboardEvent(targetAction, true)),
                $"{sourceName} did not generate {targetName}. Events: {recorder.DescribeGeneratedEvents()}.");
        }
        finally
        {
            KeyboardHelper.ReleaseKey(ToKey(sourceKey));
        }

        Assert.IsTrue(
            recorder.WaitForSequence(
                KeyboardManagerTestConstants.SingleKeyInjectedFlag,
                timeoutMS: 3_000,
                new ExpectedKeyboardEvent(targetModifier, true),
                new ExpectedKeyboardEvent(targetAction, true),
                new ExpectedKeyboardEvent(targetAction, false),
                new ExpectedKeyboardEvent(targetModifier, false)),
            $"{targetName} was not fully released. Events: {recorder.DescribeGeneratedEvents()}.");
        Assert.IsTrue(WaitForKeyState(targetModifier, expected: false, timeoutMS: 1_000), $"{targetName}'s modifier remained down.");
        Assert.IsTrue(WaitForKeyState(targetAction, expected: false, timeoutMS: 1_000), $"{targetName}'s action key remained down.");
        if (targetModifier == LeftWindows)
        {
            DismissShellAndRefocus(input, $"{sourceName} to {targetName}");
        }
        else
        {
            KeyboardHelper.SendKey(Key.Esc);
        }
    }

    [TestMethod("KeyboardManager.Remapping.ShortcutToShortcut")]
    [TestCategory("Keyboard Manager")]
    [DataRow("Ctrl+A to Ctrl+V", LeftControl, A, LeftControl, V, false)]
    [DataRow("Ctrl+A to Ctrl+V, modifier first", LeftControl, A, LeftControl, V, true)]
    [DataRow("Win+A to Ctrl+V", LeftWindows, A, LeftControl, V, false)]
    [DataRow("Win+A to Ctrl+V, modifier first", LeftWindows, A, LeftControl, V, true)]
    [DataRow("Ctrl+V to Win+A", LeftControl, V, LeftWindows, A, false)]
    [DataRow("Ctrl+V to Win+A, modifier first", LeftControl, V, LeftWindows, A, true)]
    [DataRow("Win+A to Win+F", LeftWindows, A, LeftWindows, F, false)]
    [DataRow("Win+A to Win+F, modifier first", LeftWindows, A, LeftWindows, F, true)]
    public void ShortcutToShortcutReleasesTargetsForEitherSourceReleaseOrder(
        string scenario,
        int sourceModifier,
        int sourceAction,
        int targetModifier,
        int targetAction,
        bool releaseModifierFirst)
    {
        using var input = new NotepadInputWindow();
        using var recorder = new KeyboardEventRecorder();
        ApplyProfileAndVerify(
            input,
            recorder,
            KeyboardManagerSettings.BuildProfile(
                shortcutRemaps: new[]
                {
                    new ShortcutRemap(new[] { sourceModifier, sourceAction }, new[] { targetModifier, targetAction }),
                }));

        recorder.Clear();
        Assert.IsTrue(input.FocusInput(), "The keyboard input window did not own foreground before shortcut input.");
        var expectedKeyDown = sourceModifier == targetModifier
            ? new[]
            {
                new ExpectedKeyboardEvent(targetAction, true),
            }
            : new[]
            {
                new ExpectedKeyboardEvent(targetModifier, true),
                new ExpectedKeyboardEvent(targetAction, true),
            };
        SendSourceShortcut(sourceModifier, sourceAction, releaseModifierFirst, () =>
        {
            Assert.IsTrue(
                recorder.WaitForSequence(
                    KeyboardManagerTestConstants.ShortcutInjectedFlag,
                    timeoutMS: 3_000,
                    expectedKeyDown),
                $"{scenario} did not generate its target chord. Events: {recorder.DescribeGeneratedEvents()}.");
        });

        var expectedCompleteSequence = sourceModifier == targetModifier
            ? new[]
            {
                new ExpectedKeyboardEvent(targetAction, true),
                new ExpectedKeyboardEvent(targetAction, false),
                new ExpectedKeyboardEvent(targetModifier, false),
            }
            : new[]
            {
                new ExpectedKeyboardEvent(targetModifier, true),
                new ExpectedKeyboardEvent(targetAction, true),
                new ExpectedKeyboardEvent(targetAction, false),
                new ExpectedKeyboardEvent(targetModifier, false),
            };
        Assert.IsTrue(
            recorder.WaitForSequence(
                KeyboardManagerTestConstants.ShortcutInjectedFlag,
                timeoutMS: 3_000,
                expectedCompleteSequence),
            $"{scenario} did not fully release its target chord. Events: {recorder.DescribeGeneratedEvents()}.");
        Assert.IsTrue(WaitForKeyState(targetModifier, expected: false, timeoutMS: 1_000), $"{scenario} left its target modifier down.");
        Assert.IsTrue(WaitForKeyState(targetAction, expected: false, timeoutMS: 1_000), $"{scenario} left its target action key down.");
        if (targetModifier == LeftWindows)
        {
            DismissShellAndRefocus(input, scenario);
        }
        else
        {
            KeyboardHelper.SendKey(Key.Esc);
        }
    }

    [TestMethod("KeyboardManager.Remapping.ShortcutToKey")]
    [TestCategory("Keyboard Manager")]
    [DataRow("Ctrl+A to B", LeftControl, A, B, false)]
    [DataRow("Ctrl+A to B, modifier first", LeftControl, A, B, true)]
    [DataRow("Ctrl+A to Win", LeftControl, A, LeftWindows, false)]
    [DataRow("Ctrl+A to Win, modifier first", LeftControl, A, LeftWindows, true)]
    [DataRow("Win+A to B", LeftWindows, A, B, false)]
    [DataRow("Win+A to B, modifier first", LeftWindows, A, B, true)]
    public void ShortcutToKeyReleasesTargetForEitherSourceReleaseOrder(
        string scenario,
        int sourceModifier,
        int sourceAction,
        int targetKey,
        bool releaseModifierFirst)
    {
        using var input = new NotepadInputWindow();
        using var recorder = new KeyboardEventRecorder();
        ApplyProfileAndVerify(
            input,
            recorder,
            KeyboardManagerSettings.BuildProfile(
                shortcutRemaps: new[]
                {
                    new ShortcutRemap(new[] { sourceModifier, sourceAction }, new[] { targetKey }),
                }));

        recorder.Clear();
        Assert.IsTrue(input.FocusInput(), "The keyboard input window did not own foreground before shortcut input.");
        SendSourceShortcut(sourceModifier, sourceAction, releaseModifierFirst, () =>
        {
            Assert.IsTrue(
                recorder.WaitForSequence(
                    KeyboardManagerTestConstants.ShortcutInjectedFlag,
                    timeoutMS: 3_000,
                    new ExpectedKeyboardEvent(targetKey, true)),
                $"{scenario} did not generate its target key. Events: {recorder.DescribeGeneratedEvents()}.");
        });

        Assert.IsTrue(
            recorder.WaitForSequence(
                KeyboardManagerTestConstants.ShortcutInjectedFlag,
                timeoutMS: 3_000,
                new ExpectedKeyboardEvent(targetKey, true),
                new ExpectedKeyboardEvent(targetKey, false)),
            $"{scenario} did not release its target key. Events: {recorder.DescribeGeneratedEvents()}.");
        Assert.IsTrue(WaitForKeyState(targetKey, expected: false, timeoutMS: 1_000), $"{scenario} left its target key down.");

        if (targetKey == LeftWindows)
        {
            DismissShellAndRefocus(input, scenario);
        }
    }

    [TestMethod("KeyboardManager.Remapping.AppSpecific")]
    [TestCategory("Keyboard Manager")]
    [DataRow(true)]
    [DataRow(false)]
    public void AppSpecificShortcutAppliesOnlyToMatchingForegroundProcess(bool matchingTarget)
    {
        using var input = new NotepadInputWindow();
        using var recorder = new KeyboardEventRecorder();
        Assert.IsTrue(input.FocusInput(), "Notepad did not own foreground before resolving its app-specific process name.");
        Step("Positive control: direct B input should persist in Notepad");
        KeyboardHelper.SendKeys(Key.B);
        Assert.IsTrue(input.WaitForText("b", timeoutMS: 3_000), "Direct B input did not persist in the Notepad test file.");
        Assert.IsTrue(input.FocusInput(), "Notepad did not regain foreground before clearing the positive control.");
        SendSourceShortcut(LeftControl, A, releaseModifierFirst: false);
        KeyboardHelper.SendKeys(Key.Backspace);
        Assert.IsTrue(input.WaitForText(string.Empty, timeoutMS: 3_000), "Notepad did not clear the positive-control text.");

        var foregroundProcess = WindowControl.GetForegroundWindowInfo().ProcessName;
        Assert.IsFalse(string.IsNullOrWhiteSpace(foregroundProcess), "The focused Notepad window had no observable process name.");
        var foregroundExecutable = Path.HasExtension(foregroundProcess) ? foregroundProcess : $"{foregroundProcess}.exe";
        var targetApp = matchingTarget ? foregroundExecutable : $"not-{foregroundExecutable}";
        Step($"App-specific target: '{targetApp}' for focused process '{foregroundProcess}'");
        ApplyProfileAndVerify(
            input,
            recorder,
            KeyboardManagerSettings.BuildProfile(
                shortcutRemaps: new[]
                {
                    new ShortcutRemap(new[] { LeftControl, U }, new[] { B }, targetApp),
                }));

        recorder.Clear();
        Assert.IsTrue(input.FocusInput(), "The keyboard input window did not own foreground before app-specific input.");
        SendSourceShortcut(LeftControl, U, releaseModifierFirst: false);

        var generated = recorder.WaitForSequence(
            KeyboardManagerTestConstants.ShortcutInjectedFlag,
            timeoutMS: matchingTarget ? 3_000 : 1_000,
            new ExpectedKeyboardEvent(B, true),
            new ExpectedKeyboardEvent(B, false));
        Assert.AreEqual(
            matchingTarget,
            generated,
            $"App-specific remap targeting '{targetApp}' had the wrong scope. Events: {recorder.DescribeGeneratedEvents()}.");
        Assert.AreEqual(matchingTarget ? "b" : string.Empty, input.Text, "The app-specific remap produced the wrong Notepad file content.");
    }

    [TestMethod("KeyboardManager.Remapping.HeldModifierSwitch")]
    [TestCategory("Keyboard Manager")]
    [DataRow("Ctrl", LeftControl)]
    [DataRow("Win", LeftWindows)]
    public void SwitchingRemapsWhileModifierIsHeldPreservesTargetSequence(string modifierName, int sourceModifier)
    {
        using var input = new NotepadInputWindow("replace me");
        using var recorder = new KeyboardEventRecorder();
        ApplyProfileAndVerify(
            input,
            recorder,
            KeyboardManagerSettings.BuildProfile(
                shortcutRemaps: new[]
                {
                    new ShortcutRemap(new[] { sourceModifier, D }, new[] { LeftControl, A }),
                    new ShortcutRemap(new[] { sourceModifier, E }, new[] { LeftControl, V }),
                }));

        var originalClipboard = ClipboardHelper.GetText();
        try
        {
            ClipboardHelper.SetText("pasted");
            recorder.Clear();
            Assert.IsTrue(input.FocusInput(), "The keyboard input window did not own foreground before held-modifier input.");

            Step($"Holding {modifierName}, then pressing D followed by E");
            KeyboardHelper.PressKey(ToKey(sourceModifier));
            Thread.Sleep(100);
            SendHeldAction(D);
            Thread.Sleep(150);
            SendHeldAction(E);
            KeyboardHelper.ReleaseKey(ToKey(sourceModifier));

            var expectedSequence = sourceModifier == LeftControl
                ? new[]
                {
                    new ExpectedKeyboardEvent(A, true),
                    new ExpectedKeyboardEvent(A, false),
                    new ExpectedKeyboardEvent(V, true),
                    new ExpectedKeyboardEvent(V, false),
                    new ExpectedKeyboardEvent(LeftControl, false),
                }
                : new[]
                {
                    new ExpectedKeyboardEvent(LeftControl, true),
                    new ExpectedKeyboardEvent(A, true),
                    new ExpectedKeyboardEvent(A, false),
                    new ExpectedKeyboardEvent(V, true),
                    new ExpectedKeyboardEvent(V, false),
                    new ExpectedKeyboardEvent(LeftControl, false),
                };
            Assert.IsTrue(
                recorder.WaitForSequence(
                    KeyboardManagerTestConstants.ShortcutInjectedFlag,
                    timeoutMS: 5_000,
                    expectedSequence),
                $"Held-{modifierName} remap switching produced the wrong target sequence. Events: {recorder.DescribeGeneratedEvents()}.");
            if (sourceModifier == LeftControl)
            {
                Assert.IsTrue(input.WaitForText("pasted", timeoutMS: 3_000), "Held-Ctrl remaps did not select all and paste over the input text.");
            }
            else
            {
                Assert.IsTrue(input.WaitForText("pasted", timeoutMS: 3_000), "Held-Win remaps did not select all and paste over the Notepad text.");
            }

            Assert.IsTrue(WaitForKeyState(LeftControl, expected: false, timeoutMS: 1_000), "Target Ctrl remained down after the sequence.");
        }
        finally
        {
            KeyboardHelper.ReleaseKey(ToKey(sourceModifier));
            ClipboardHelper.SetText(originalClipboard);
        }
    }

    [TestMethod("KeyboardManager.Remapping.AltTabTarget")]
    [TestCategory("Keyboard Manager")]
    public void AltTabTargetChangesForegroundAndReleasesModifier()
    {
        using var recorder = new KeyboardEventRecorder();
        using var input = new NotepadInputWindow();
        ApplyProfileAndVerify(
            input,
            recorder,
            KeyboardManagerSettings.BuildProfile(
                shortcutRemaps: new[]
                {
                    new ShortcutRemap(new[] { LeftControl, Q }, new[] { LeftAlt, Tab }),
                }));
        recorder.Clear();
        Assert.IsTrue(input.FocusInput(), "Notepad did not own foreground before Alt+Tab.");
        SendSourceShortcut(LeftControl, Q, releaseModifierFirst: false);
        Assert.IsTrue(
            recorder.WaitForSequence(
                KeyboardManagerTestConstants.ShortcutInjectedFlag,
                timeoutMS: 3_000,
                new ExpectedKeyboardEvent(LeftAlt, true),
                new ExpectedKeyboardEvent(Tab, true),
                new ExpectedKeyboardEvent(Tab, false),
                new ExpectedKeyboardEvent(LeftAlt, false)),
            $"Alt+Tab target did not complete. Events: {recorder.DescribeGeneratedEvents()}.");
        Assert.AreNotEqual(
            input.Handle,
            WindowControl.GetForegroundWindowInfo().Hwnd,
            "Alt+Tab did not move foreground away from Notepad.");
        Assert.IsTrue(WaitForKeyState(LeftAlt, expected: false, timeoutMS: 1_000), "Alt remained down after Alt+Tab.");
    }

    [TestMethod("KeyboardManager.Remapping.AltF4Target")]
    [TestCategory("Keyboard Manager")]
    public void AltF4TargetClosesNotepadAndReleasesModifier()
    {
        using (var controlInput = new CalculatorInputWindow())
        {
            Assert.IsTrue(controlInput.FocusInput(), "Calculator did not own foreground before the direct Alt+F4 control.");
            using var controlLifecycle = new WindowShowWatcher(controlInput.ClassName, controlInput.Handle.ToInt64());
            SendSourceShortcut(LeftAlt, KeyCode(Key.F4), releaseModifierFirst: false);
            Assert.IsTrue(
                controlLifecycle.WaitForHidden(timeoutMs: 10_000),
                $"The harness's direct Alt+F4 control did not hide or destroy Calculator; remapped Alt+F4 cannot be evaluated on this desktop. Events: {string.Join(", ", controlLifecycle.Events)}.");
        }

        using var recorder = new KeyboardEventRecorder();
        using var input = new CalculatorInputWindow();
        ApplyProfileAndVerify(
            input,
            recorder,
            KeyboardManagerSettings.BuildProfile(
                shortcutRemaps: new[]
                {
                    new ShortcutRemap(new[] { LeftControl, W }, new[] { LeftAlt, KeyCode(Key.F4) }),
                }));
        recorder.Clear();
        Assert.IsTrue(input.FocusInput(), "Calculator did not own foreground before Alt+F4.");
        using var inputLifecycle = new WindowShowWatcher(input.ClassName, input.Handle.ToInt64());
        SendSourceShortcut(LeftControl, W, releaseModifierFirst: false);
        Assert.IsTrue(
            recorder.WaitForSequence(
                KeyboardManagerTestConstants.ShortcutInjectedFlag,
                timeoutMS: 3_000,
                new ExpectedKeyboardEvent(LeftAlt, true),
                new ExpectedKeyboardEvent(KeyCode(Key.F4), true),
                new ExpectedKeyboardEvent(KeyCode(Key.F4), false),
                new ExpectedKeyboardEvent(LeftAlt, false)),
            $"Alt+F4 target did not complete. Events: {recorder.DescribeGeneratedEvents()}.");
        Assert.IsTrue(
            inputLifecycle.WaitForHidden(timeoutMs: 10_000),
            $"Alt+F4 did not hide or destroy the tracked Notepad window. Events: {string.Join(", ", inputLifecycle.Events)}.");
        Assert.IsTrue(WaitForKeyState(LeftAlt, expected: false, timeoutMS: 1_000), "Alt remained down after Alt+F4.");
    }

    private void ApplyProfileAndVerify(
        IKeyboardInputWindow input,
        KeyboardEventRecorder recorder,
        System.Text.Json.Nodes.JsonObject profile)
    {
        var generation = Interlocked.Increment(ref profileGeneration) - 1;
        var sourceIndex = generation % KeyboardManagerTestConstants.LoadProbeKeyCount;
        var targetOffset = 1 + ((generation / KeyboardManagerTestConstants.LoadProbeKeyCount) % (KeyboardManagerTestConstants.LoadProbeKeyCount - 1));
        var probeSource = KeyboardManagerTestConstants.LoadProbeBaseKey + sourceIndex;
        var probeTarget = KeyboardManagerTestConstants.LoadProbeBaseKey + ((sourceIndex + targetOffset) % KeyboardManagerTestConstants.LoadProbeKeyCount);
        SetLoadProbe(profile, probeSource, probeTarget);
        Step("Writing the Keyboard Manager profile and signaling live reload");
        KeyboardManagerSettings.ApplyProfile(profile);
        Assert.IsTrue(input.FocusInput(), "The keyboard input window did not own foreground for the profile-load probe.");

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            recorder.Clear();
            Step($"Profile-load probe {attempt}/5: 0x{probeSource:X2} should generate 0x{probeTarget:X2}");
            KeyboardHelper.SendKey(ToKey(probeSource));
            if (recorder.WaitForSequence(
                KeyboardManagerTestConstants.SingleKeyInjectedFlag,
                timeoutMS: 1_500,
                new ExpectedKeyboardEvent(probeTarget, true),
                new ExpectedKeyboardEvent(probeTarget, false)))
            {
                recorder.Clear();
                return;
            }

            KeyboardManagerSettings.SignalSettingsChanged();
        }

        Assert.Fail($"Keyboard Manager did not load the profile after five event signals. Events: {recorder.DescribeGeneratedEvents()}.");
    }

    private static void SetLoadProbe(System.Text.Json.Nodes.JsonObject profile, int probeSource, int probeTarget)
    {
        var mappings = profile["remapKeys"]?["inProcess"] as System.Text.Json.Nodes.JsonArray;
        var probe = mappings?
            .OfType<System.Text.Json.Nodes.JsonObject>()
            .FirstOrDefault(mapping =>
                mapping["originalKeys"]?.GetValue<string>() == KeyboardManagerTestConstants.LoadProbeSourceKey.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.IsNotNull(probe, "The generated Keyboard Manager profile did not contain its load probe mapping.");
        probe!["originalKeys"] = probeSource.ToString(System.Globalization.CultureInfo.InvariantCulture);
        probe!["newRemapKeys"] = probeTarget.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void SendSourceShortcut(
        int modifier,
        int action,
        bool releaseModifierFirst,
        Action? afterKeyDown = null)
    {
        var modifierKey = ToKey(modifier);
        var actionKey = ToKey(action);
        try
        {
            KeyboardHelper.PressKey(modifierKey);
            Thread.Sleep(100);
            KeyboardHelper.PressKey(actionKey);
            afterKeyDown?.Invoke();
            Thread.Sleep(100);

            if (releaseModifierFirst)
            {
                KeyboardHelper.ReleaseKey(modifierKey);
                Thread.Sleep(50);
                KeyboardHelper.ReleaseKey(actionKey);
            }
            else
            {
                KeyboardHelper.ReleaseKey(actionKey);
                Thread.Sleep(50);
                KeyboardHelper.ReleaseKey(modifierKey);
            }
        }
        finally
        {
            KeyboardHelper.ReleaseKey(actionKey);
            KeyboardHelper.ReleaseKey(modifierKey);
        }
    }

    private static bool WaitForKeyState(int virtualKey, bool expected, int timeoutMS)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMS);
        do
        {
            if (KeyboardHelper.IsKeyDown(ToKey(virtualKey)) == expected)
            {
                return true;
            }

            Thread.Sleep(20);
        }
        while (DateTime.UtcNow < deadline);

        return KeyboardHelper.IsKeyDown(ToKey(virtualKey)) == expected;
    }

    private static void DismissShellAndRefocus(NotepadInputWindow input, string scenario)
    {
        KeyboardHelper.SendKey(Key.Esc);
        if (!input.FocusInput(timeoutMS: 5_000))
        {
            Console.WriteLine($"{scenario}: Shell dismissal succeeded, but the disposable input fixture did not regain foreground.");
        }
    }

    private static void SendHeldAction(int action) => KeyboardHelper.SendKey(ToKey(action));

    private static Key ToKey(int virtualKey) => (Key)checked((byte)virtualKey);

    private static int KeyCode(Key key) => (byte)key;
}
