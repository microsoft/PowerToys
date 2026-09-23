// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static PowerAccent.UITests.PowerAccentTestHelper;

namespace PowerAccent.UITests;

[TestClass]
public sealed class PowerAccentEndToEndTests : UITestBase
{
    private static readonly IDisposable ModuleSettings = SettingsConfigHelper.PreserveModuleSettings(ModuleName);
    private static readonly IDisposable UsageInfo = PreserveUsageInfo();

    public PowerAccentEndToEndTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: [ModuleName])
    {
    }

    protected override void PrepareTestState()
    {
        AssertInputEnvironment();
        Assert.IsTrue(
            WindowControl.TryKillProcessTreeByNameAndWait(ProcessName, timeoutMS: 30_000),
            "A previous PowerToys.PowerAccent process did not exit before the next test launch.");
        PrepareDefaultState();
    }

    [ClassCleanup]
    public static void RestoreClassState() => DisposeAll(ModuleSettings, UsageInfo);

    [TestMethod]
    [TestCategory("PowerAccent")]
    public void ArrowKeysCycleAndCommitSelectedCharacter()
    {
        ReplaceSettings(this, new Settings(
            Activation: ActivationKey.LeftRightArrow,
            InputTimeMs: 200,
            SelectedLanguage: "FR"));

        using var clipboard = PreserveClipboardText();
        using var notepad = NotepadFixture.Start(this);

        var rightResult = RunTriggeredGesture(
            this,
            notepad,
            Key.A,
            Key.Right,
            toolbar =>
            {
                Assert.AreEqual("ä", GetSelectedCharacter(toolbar, FrenchACharacters), "Right should start at the right half.");

                KeyboardHelper.SendKey(Key.Right);
                Assert.AreEqual("ã", GetSelectedCharacter(toolbar, FrenchACharacters), "Right should move to the next character.");

                KeyboardHelper.SendKey(Key.Left);
                Assert.AreEqual("ä", GetSelectedCharacter(toolbar, FrenchACharacters), "Left should move back one character.");
            });
        Assert.AreEqual("ä", rightResult, "Releasing the letter should commit the selected right-arrow character.");

        var leftResult = RunTriggeredGesture(
            this,
            notepad,
            Key.A,
            Key.Left,
            toolbar =>
            {
                Assert.AreEqual("á", GetSelectedCharacter(toolbar, FrenchACharacters), "Left should start at the left half.");

                KeyboardHelper.SendKey(Key.Left);
                Assert.AreEqual("â", GetSelectedCharacter(toolbar, FrenchACharacters), "Left should move to the previous character.");
            });
        Assert.AreEqual("â", leftResult, "Releasing the letter should commit the selected left-arrow character.");
    }

    [TestMethod]
    [TestCategory("PowerAccent")]
    public void SpaceCyclesForwardAndShiftSpaceCyclesBackward()
    {
        ReplaceSettings(this, new Settings(
            Activation: ActivationKey.Space,
            InputTimeMs: 200,
            SelectedLanguage: "FR"));

        using var clipboard = PreserveClipboardText();
        using var notepad = NotepadFixture.Start(this);

        var result = RunTriggeredGesture(
            this,
            notepad,
            Key.A,
            Key.Space,
            toolbar =>
            {
                Assert.AreEqual("à", GetSelectedCharacter(toolbar, FrenchACharacters), "Space should start at the first character.");

                KeyboardHelper.SendKey(Key.Space);
                Assert.AreEqual("â", GetSelectedCharacter(toolbar, FrenchACharacters), "Space should move forward.");

                KeyboardHelper.PressKey(Key.LShift);
                try
                {
                    KeyboardHelper.SendKey(Key.Space);
                }
                finally
                {
                    KeyboardHelper.ReleaseKey(Key.LShift);
                }

                Assert.AreEqual("à", GetSelectedCharacter(toolbar, FrenchACharacters), "Shift+Space should move backward.");
            });

        Assert.AreEqual("à", result, "Releasing the letter should commit the character selected with Space.");
    }

    [TestMethod]
    [TestCategory("PowerAccent")]
    public void DisablingQuickAccentStopsActivation()
    {
        ReplaceSettings(this, new Settings(
            Activation: ActivationKey.Space,
            InputTimeMs: 200,
            SelectedLanguage: "FR"));

        using var clipboard = PreserveClipboardText();
        NavigateToSettings(this);

        RunWithCleanup(
            () =>
            {
                SetModuleEnabled(this, enabled: false);
                Assert.IsTrue(WaitForProcess(expected: false), "PowerToys.PowerAccent did not stop after disabling Quick Accent.");

                using var notepad = NotepadFixture.Start(this);
                Assert.AreEqual(
                    "a ",
                    RunUnmodifiedGesture(notepad, Key.A, Key.Space),
                    "With Quick Accent disabled, the letter and activation key should pass through unchanged.");
            },
            () =>
            {
                SetModuleEnabled(this, enabled: true);
                Assert.IsTrue(WaitForProcess(expected: true), "PowerToys.PowerAccent did not restart after re-enabling Quick Accent.");
            });
    }

    [DataTestMethod]
    [DataRow((int)ActivationKey.LeftRightArrow, "Right")]
    [DataRow((int)ActivationKey.Space, "Space")]
    [DataRow((int)ActivationKey.Both, "Right")]
    [DataRow((int)ActivationKey.Both, "Space")]
    [DataRow((int)ActivationKey.PressAndHold, "PressAndHold")]
    [TestCategory("PowerAccent")]
    public void ActivationKeySettingIsApplied(int activationKey, string triggerName)
    {
        var activation = (ActivationKey)activationKey;
        ReplaceSettings(this, new Settings(
            Activation: activation,
            InputTimeMs: 200,
            HoldDurationMs: 250,
            SelectedLanguage: "SP"));

        using var clipboard = PreserveClipboardText();
        using var notepad = NotepadFixture.Start(this);

        string result;
        if (activation == ActivationKey.PressAndHold)
        {
            result = RunPressAndHoldGesture(
                this,
                notepad,
                Key.A,
                toolbar =>
                {
                    KeyboardHelper.SendKey(Key.Right);
                    Assert.AreEqual("á", GetSelectedCharacter(toolbar, ["á"]));
                });
        }
        else
        {
            var trigger = Enum.Parse<Key>(triggerName);
            result = RunTriggeredGesture(
                this,
                notepad,
                Key.A,
                trigger,
                toolbar => Assert.AreEqual("á", GetSelectedCharacter(toolbar, ["á"])));
        }

        Assert.AreEqual("á", result, $"{activation} activation through {triggerName} did not commit the Spanish accent.");
    }

    [TestMethod]
    [TestCategory("PowerAccent")]
    public void CurrencyLanguageLimitsAvailableCharacters()
    {
        ReplaceSettings(this, new Settings(
            Activation: ActivationKey.Space,
            InputTimeMs: 200,
            SelectedLanguage: "CUR"));

        using var clipboard = PreserveClipboardText();
        using var notepad = NotepadFixture.Start(this);

        Assert.AreEqual(
            "a ",
            RunSuppressedGesture(this, notepad, Key.A, Key.Space),
            "Currency has no mapping for A, so the input should pass through and no toolbar should appear.");

        var result = RunTriggeredGesture(
            this,
            notepad,
            Key.S,
            Key.Space,
            toolbar =>
            {
                Assert.AreEqual("$", GetSelectedCharacter(toolbar, CurrencySCharacters));
                CollectionAssert.AreEqual(
                    CurrencySCharacters,
                    GetCharactersInVisualOrder(toolbar, CurrencySCharacters).ToArray(),
                    "Currency should expose only the configured S mappings in source order.");
            });
        Assert.AreEqual("$", result, "Currency should commit the first S currency character.");
    }

    [DataTestMethod]
    [DataRow("Top center", "Center", "Top")]
    [DataRow("Bottom center", "Center", "Bottom")]
    [DataRow("Left", "Left", "Center")]
    [DataRow("Right", "Right", "Center")]
    [DataRow("Top right corner", "Right", "Top")]
    [DataRow("Top left corner", "Left", "Top")]
    [DataRow("Bottom right corner", "Right", "Bottom")]
    [DataRow("Bottom left corner", "Left", "Bottom")]
    [DataRow("Center", "Center", "Center")]
    [TestCategory("PowerAccent")]
    public void ToolbarPositionSettingIsApplied(string position, string horizontalAnchor, string verticalAnchor)
    {
        ReplaceSettings(this, new Settings(
            Activation: ActivationKey.Space,
            ToolbarPosition: position,
            InputTimeMs: 200,
            SelectedLanguage: "SP"));

        using var clipboard = PreserveClipboardText();
        using var notepad = NotepadFixture.Start(this);

        var result = RunTriggeredGesture(
            this,
            notepad,
            Key.A,
            Key.Space,
            toolbar => AssertToolbarPlacement(this, toolbar, horizontalAnchor, verticalAnchor));
        Assert.AreEqual("á", result, $"The {position} toolbar did not commit the selected accent.");
    }

    [TestMethod]
    [TestCategory("PowerAccent")]
    public void InputDelayDistinguishesFalseStartFromSelection()
    {
        const int inputTimeMs = 900;
        ReplaceSettings(this, new Settings(
            Activation: ActivationKey.Space,
            InputTimeMs: inputTimeMs,
            SelectedLanguage: "FR"));

        using var clipboard = PreserveClipboardText();
        using var notepad = NotepadFixture.Start(this);

        Assert.AreEqual(
            "a ",
            RunFalseStart(
                this,
                notepad,
                Key.A,
                Key.Space,
                heldMs: 100,
                verificationMs: inputTimeMs + 300),
            "A gesture released well before the input delay should keep the base letter and Space.");

        Assert.AreEqual(
            "à",
            RunTriggeredGesture(
                this,
                notepad,
                Key.A,
                Key.Space,
                toolbar => Assert.AreEqual("à", GetSelectedCharacter(toolbar, FrenchACharacters))),
            "Holding through the input delay should commit the selected accent.");
    }

    [TestMethod]
    [TestCategory("PowerAccent")]
    public void ExcludedApplicationDoesNotActivate()
    {
        ReplaceSettings(this, new Settings(
            Activation: ActivationKey.Space,
            InputTimeMs: 200,
            SelectedLanguage: "FR",
            ExcludedApps: "notepad.exe"));

        using var clipboard = PreserveClipboardText();
        using var notepad = NotepadFixture.Start(this);

        Assert.AreEqual(
            "a ",
            RunSuppressedGesture(this, notepad, Key.A, Key.Space),
            "Quick Accent should not consume input or reveal its toolbar in an excluded Notepad window.");
    }

    [TestMethod]
    [TestCategory("PowerAccent")]
    public void SortByFrequencyMovesUsedCharacterFirst()
    {
        ReplaceSettings(this, new Settings(
            Activation: ActivationKey.Space,
            InputTimeMs: 200,
            SelectedLanguage: "FR",
            SortByUsageFrequency: true));

        using var clipboard = PreserveClipboardText();
        using var notepad = NotepadFixture.Start(this);

        var firstResult = RunTriggeredGesture(
            this,
            notepad,
            Key.A,
            Key.Space,
            toolbar =>
            {
                Assert.AreEqual("à", GetSelectedCharacter(toolbar, FrenchACharacters));
                for (var index = 1; index < FrenchACharacters.Length; index++)
                {
                    KeyboardHelper.SendKey(Key.Space);
                }

                Assert.AreEqual("æ", GetSelectedCharacter(toolbar, FrenchACharacters));
            });
        Assert.AreEqual("æ", firstResult, "The setup gesture should record usage for the last French A character.");

        var sortedResult = RunTriggeredGesture(
            this,
            notepad,
            Key.A,
            Key.Space,
            toolbar =>
            {
                Assert.AreEqual("æ", GetSelectedCharacter(toolbar, FrenchACharacters), "The most-used character should be selected first.");
                Assert.AreEqual(
                    "æ",
                    GetCharactersInVisualOrder(toolbar, FrenchACharacters)[0],
                    "The most-used character should move to the first visual position.");
            });
        Assert.AreEqual("æ", sortedResult, "The reordered first character should be committed.");
    }

    [TestMethod]
    [TestCategory("PowerAccent")]
    public void StartFromLeftUsesFirstCharacterForBothArrows()
    {
        using var clipboard = PreserveClipboardText();
        using var notepad = NotepadFixture.Start(this);

        ReplaceSettings(this, new Settings(
            Activation: ActivationKey.LeftRightArrow,
            InputTimeMs: 200,
            SelectedLanguage: "FR",
            StartSelectionFromTheLeft: false));
        Assert.AreEqual(
            "ä",
            RunTriggeredGesture(
                this,
                notepad,
                Key.A,
                Key.Right,
                toolbar => Assert.AreEqual("ä", GetSelectedCharacter(toolbar, FrenchACharacters))),
            "The default right-arrow selection should start in the right half.");

        ReplaceSettings(this, new Settings(
            Activation: ActivationKey.LeftRightArrow,
            InputTimeMs: 200,
            SelectedLanguage: "FR",
            StartSelectionFromTheLeft: true));
        Assert.AreEqual(
            "à",
            RunTriggeredGesture(
                this,
                notepad,
                Key.A,
                Key.Right,
                toolbar => Assert.AreEqual("à", GetSelectedCharacter(toolbar, FrenchACharacters))),
            "Start-from-left should make Right select the first character.");
        Assert.AreEqual(
            "à",
            RunTriggeredGesture(
                this,
                notepad,
                Key.A,
                Key.Left,
                toolbar => Assert.AreEqual("à", GetSelectedCharacter(toolbar, FrenchACharacters))),
            "Start-from-left should make Left select the first character.");
    }
}
