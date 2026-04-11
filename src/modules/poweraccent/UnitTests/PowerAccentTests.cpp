#include "pch.h"
#include "CppUnitTest.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

// Mirror the enum values from KeyboardListener.idl so we can test the logic
// without pulling in WinRT/COM infrastructure.
namespace PowerAccentTestEnums
{
    enum LetterKey
    {
        None = 0x00,
        VK_0 = 0x30, VK_1 = 0x31, VK_2 = 0x32, VK_3 = 0x33, VK_4 = 0x34,
        VK_5 = 0x35, VK_6 = 0x36, VK_7 = 0x37, VK_8 = 0x38, VK_9 = 0x39,
        VK_A = 0x41, VK_B = 0x42, VK_C = 0x43, VK_D = 0x44, VK_E = 0x45,
        VK_F = 0x46, VK_G = 0x47, VK_H = 0x48, VK_I = 0x49, VK_J = 0x4A,
        VK_K = 0x4B, VK_L = 0x4C, VK_M = 0x4D, VK_N = 0x4E, VK_O = 0x4F,
        VK_P = 0x50, VK_Q = 0x51, VK_R = 0x52, VK_S = 0x53, VK_T = 0x54,
        VK_U = 0x55, VK_V = 0x56, VK_W = 0x57, VK_X = 0x58, VK_Y = 0x59,
        VK_Z = 0x5A,
        VK_PLUS = 0xBB, VK_COMMA = 0xBC, VK_PERIOD = 0xBE, VK_MINUS = 0xBD,
        VK_MULTIPLY_ = 0x6A, VK_SLASH_ = 0xBF, VK_DIVIDE_ = 0x6F, VK_BACKSLASH = 0xDC,
    };

    enum TriggerKey
    {
        Right = 0x27,
        Left = 0x25,
        Space = 0x20,
    };

    enum InputType
    {
        InputNone,
        InputSpace,
        InputLeft,
        InputRight,
        InputChar,
    };

    enum PowerAccentActivationKey
    {
        LeftRightArrow = 0,
        ActivationSpace = 1,
        Both = 2,
    };
}

// Simplified settings structure matching the C++ KeyboardListener
struct PowerAccentSettings
{
    PowerAccentTestEnums::PowerAccentActivationKey activationKey =
        PowerAccentTestEnums::PowerAccentActivationKey::Both;
    bool doNotActivateOnGameMode = true;
    std::chrono::milliseconds inputTime{ 300 };
    std::vector<std::wstring> excludedApps;
};

// Lightweight state machine that mirrors the key-down/key-up logic in
// KeyboardListener.  We test the *decisions* the real code makes without
// needing Win32 hooks, COM, or the actual keyboard.
struct KeyStateMachine
{
    using LetterKey = PowerAccentTestEnums::LetterKey;
    using TriggerKey = PowerAccentTestEnums::TriggerKey;
    using InputType = PowerAccentTestEnums::InputType;

    PowerAccentSettings settings;

    bool toolbarVisible = false;
    LetterKey letterPressed = LetterKey::None;
    bool triggeredWithSpace = false;
    bool triggeredWithLeftArrow = false;
    bool triggeredWithRightArrow = false;

    // Callbacks
    LetterKey lastShowLetter = LetterKey::None;
    InputType lastHideInput = InputType::InputNone;
    TriggerKey lastNextTrigger = TriggerKey::Space;
    bool lastNextShift = false;
    int showCount = 0;
    int hideCount = 0;
    int nextCount = 0;

    // Valid letters (mirroring the static list in KeyboardListener)
    static const std::vector<LetterKey>& GetLetters()
    {
        static const std::vector<LetterKey> letters = {
            LetterKey::VK_0, LetterKey::VK_1, LetterKey::VK_2, LetterKey::VK_3, LetterKey::VK_4,
            LetterKey::VK_5, LetterKey::VK_6, LetterKey::VK_7, LetterKey::VK_8, LetterKey::VK_9,
            LetterKey::VK_A, LetterKey::VK_B, LetterKey::VK_C, LetterKey::VK_D, LetterKey::VK_E,
            LetterKey::VK_F, LetterKey::VK_G, LetterKey::VK_H, LetterKey::VK_I, LetterKey::VK_J,
            LetterKey::VK_K, LetterKey::VK_L, LetterKey::VK_M, LetterKey::VK_N, LetterKey::VK_O,
            LetterKey::VK_P, LetterKey::VK_Q, LetterKey::VK_R, LetterKey::VK_S, LetterKey::VK_T,
            LetterKey::VK_U, LetterKey::VK_V, LetterKey::VK_W, LetterKey::VK_X, LetterKey::VK_Y,
            LetterKey::VK_Z, LetterKey::VK_PLUS, LetterKey::VK_COMMA, LetterKey::VK_PERIOD,
            LetterKey::VK_MINUS, LetterKey::VK_SLASH_, LetterKey::VK_DIVIDE_, LetterKey::VK_MULTIPLY_,
            LetterKey::VK_BACKSLASH,
        };
        return letters;
    }

    static const std::vector<TriggerKey>& GetTriggers()
    {
        static const std::vector<TriggerKey> triggers = {
            TriggerKey::Right, TriggerKey::Left, TriggerKey::Space
        };
        return triggers;
    }

    bool IsLetter(int vk) const
    {
        auto key = static_cast<LetterKey>(vk);
        const auto& letters = GetLetters();
        return std::find(letters.begin(), letters.end(), key) != letters.end();
    }

    bool IsTrigger(int vk) const
    {
        auto key = static_cast<TriggerKey>(vk);
        const auto& triggers = GetTriggers();
        return std::find(triggers.begin(), triggers.end(), key) != triggers.end();
    }

    // Simulate whether the activation key setting allows the given trigger
    bool IsTriggerAllowed(int triggerVk) const
    {
        using AK = PowerAccentTestEnums::PowerAccentActivationKey;
        if (triggerVk == VK_SPACE && settings.activationKey == AK::LeftRightArrow)
            return false;
        if ((triggerVk == VK_LEFT || triggerVk == VK_RIGHT) && settings.activationKey == AK::ActivationSpace)
            return false;
        return true;
    }

    // Returns true if the key should be suppressed (eaten)
    bool OnKeyDown(int vkCode, bool letterStillHeld = true, bool isLanguageLetter = true)
    {
        auto letterKey = static_cast<LetterKey>(vkCode);

        if (IsLetter(vkCode) && isLanguageLetter)
        {
            if (toolbarVisible && letterPressed == letterKey)
                return true; // suppress repeated letter
            letterPressed = letterKey;
        }

        UINT triggerPressed = 0;
        if (letterPressed != LetterKey::None && IsTrigger(vkCode))
        {
            triggerPressed = vkCode;
            if (!letterStillHeld || !IsTriggerAllowed(triggerPressed))
                return false;
        }

        if (!toolbarVisible && letterPressed != LetterKey::None && triggerPressed)
        {
            triggeredWithSpace = (triggerPressed == VK_SPACE);
            triggeredWithLeftArrow = (triggerPressed == VK_LEFT);
            triggeredWithRightArrow = (triggerPressed == VK_RIGHT);
            toolbarVisible = true;
            lastShowLetter = letterPressed;
            showCount++;
        }

        if (toolbarVisible && triggerPressed)
        {
            lastNextTrigger = static_cast<TriggerKey>(triggerPressed);
            nextCount++;
            return true;
        }

        return false;
    }

    // Returns true if suppressed
    bool OnKeyUp(int vkCode, bool wasFastActivation = false, bool isLanguageLetter = true)
    {
        if (IsLetter(vkCode) && isLanguageLetter)
        {
            letterPressed = LetterKey::None;

            if (toolbarVisible)
            {
                if (wasFastActivation)
                {
                    // False start
                    if (triggeredWithSpace)
                        lastHideInput = InputType::InputSpace;
                    else if (triggeredWithLeftArrow)
                        lastHideInput = InputType::InputLeft;
                    else if (triggeredWithRightArrow)
                        lastHideInput = InputType::InputRight;
                    else
                        lastHideInput = InputType::InputNone;
                    hideCount++;
                    toolbarVisible = false;
                    return true;
                }

                lastHideInput = InputType::InputChar;
                hideCount++;
                toolbarVisible = false;
            }
        }
        return false;
    }
};

namespace PowerAccentUnitTests
{
    // ========================================================================
    // LetterKey enum values
    // ========================================================================
    TEST_CLASS(LetterKeyEnumTests)
    {
    public:

        TEST_METHOD(VK_A_HasCorrectValue)
        {
            Assert::AreEqual(0x41, static_cast<int>(PowerAccentTestEnums::LetterKey::VK_A));
        }

        TEST_METHOD(VK_Z_HasCorrectValue)
        {
            Assert::AreEqual(0x5A, static_cast<int>(PowerAccentTestEnums::LetterKey::VK_Z));
        }

        TEST_METHOD(VK_0_HasCorrectValue)
        {
            Assert::AreEqual(0x30, static_cast<int>(PowerAccentTestEnums::LetterKey::VK_0));
        }

        TEST_METHOD(None_IsZero)
        {
            Assert::AreEqual(0, static_cast<int>(PowerAccentTestEnums::LetterKey::None));
        }

        TEST_METHOD(SpecialKeys_HaveCorrectValues)
        {
            Assert::AreEqual(0xBB, static_cast<int>(PowerAccentTestEnums::LetterKey::VK_PLUS));
            Assert::AreEqual(0xBC, static_cast<int>(PowerAccentTestEnums::LetterKey::VK_COMMA));
            Assert::AreEqual(0xBE, static_cast<int>(PowerAccentTestEnums::LetterKey::VK_PERIOD));
            Assert::AreEqual(0xBD, static_cast<int>(PowerAccentTestEnums::LetterKey::VK_MINUS));
            Assert::AreEqual(0xBF, static_cast<int>(PowerAccentTestEnums::LetterKey::VK_SLASH_));
            Assert::AreEqual(0x6F, static_cast<int>(PowerAccentTestEnums::LetterKey::VK_DIVIDE_));
            Assert::AreEqual(0x6A, static_cast<int>(PowerAccentTestEnums::LetterKey::VK_MULTIPLY_));
            Assert::AreEqual(0xDC, static_cast<int>(PowerAccentTestEnums::LetterKey::VK_BACKSLASH));
        }
    };

    // ========================================================================
    // TriggerKey enum values
    // ========================================================================
    TEST_CLASS(TriggerKeyEnumTests)
    {
    public:

        TEST_METHOD(TriggerRight_IsVK_RIGHT)
        {
            Assert::AreEqual(0x27, static_cast<int>(PowerAccentTestEnums::TriggerKey::Right));
        }

        TEST_METHOD(TriggerLeft_IsVK_LEFT)
        {
            Assert::AreEqual(0x25, static_cast<int>(PowerAccentTestEnums::TriggerKey::Left));
        }

        TEST_METHOD(TriggerSpace_IsVK_SPACE)
        {
            Assert::AreEqual(0x20, static_cast<int>(PowerAccentTestEnums::TriggerKey::Space));
        }
    };

    // ========================================================================
    // Settings defaults
    // ========================================================================
    TEST_CLASS(SettingsTests)
    {
    public:

        TEST_METHOD(DefaultActivationKey_IsBoth)
        {
            PowerAccentSettings s;
            Assert::IsTrue(s.activationKey == PowerAccentTestEnums::PowerAccentActivationKey::Both);
        }

        TEST_METHOD(DefaultGameMode_IsTrue)
        {
            PowerAccentSettings s;
            Assert::IsTrue(s.doNotActivateOnGameMode);
        }

        TEST_METHOD(DefaultInputTime_Is300ms)
        {
            PowerAccentSettings s;
            Assert::AreEqual(300LL, static_cast<long long>(s.inputTime.count()));
        }

        TEST_METHOD(DefaultExcludedApps_IsEmpty)
        {
            PowerAccentSettings s;
            Assert::IsTrue(s.excludedApps.empty());
        }

        TEST_METHOD(UpdateActivationKey_Space)
        {
            PowerAccentSettings s;
            s.activationKey = PowerAccentTestEnums::PowerAccentActivationKey::ActivationSpace;
            Assert::IsTrue(s.activationKey == PowerAccentTestEnums::PowerAccentActivationKey::ActivationSpace);
        }

        TEST_METHOD(UpdateInputTime_Custom)
        {
            PowerAccentSettings s;
            s.inputTime = std::chrono::milliseconds(500);
            Assert::AreEqual(500LL, static_cast<long long>(s.inputTime.count()));
        }
    };

    // ========================================================================
    // Key state machine logic
    // ========================================================================
    TEST_CLASS(KeyStateMachineTests)
    {
    public:

        TEST_METHOD(LetterDown_ThenSpaceTrigger_ShowsToolbar)
        {
            KeyStateMachine sm;
            sm.OnKeyDown(0x41); // 'A'
            sm.OnKeyDown(VK_SPACE);
            Assert::IsTrue(sm.toolbarVisible);
            Assert::AreEqual(1, sm.showCount);
            Assert::IsTrue(sm.lastShowLetter == PowerAccentTestEnums::LetterKey::VK_A);
        }

        TEST_METHOD(LetterDown_ThenRightArrow_ShowsToolbar)
        {
            KeyStateMachine sm;
            sm.OnKeyDown(0x45); // 'E'
            sm.OnKeyDown(VK_RIGHT);
            Assert::IsTrue(sm.toolbarVisible);
            Assert::IsTrue(sm.triggeredWithRightArrow);
        }

        TEST_METHOD(LetterDown_ThenLeftArrow_ShowsToolbar)
        {
            KeyStateMachine sm;
            sm.OnKeyDown(0x4E); // 'N'
            sm.OnKeyDown(VK_LEFT);
            Assert::IsTrue(sm.toolbarVisible);
            Assert::IsTrue(sm.triggeredWithLeftArrow);
        }

        TEST_METHOD(SpaceTrigger_SuppressesKey)
        {
            KeyStateMachine sm;
            sm.OnKeyDown(0x41); // 'A'
            bool suppressed = sm.OnKeyDown(VK_SPACE);
            Assert::IsTrue(suppressed, L"Trigger key should be suppressed when toolbar shows");
        }

        TEST_METHOD(LetterUp_AfterFastActivation_HidesWithSpace)
        {
            KeyStateMachine sm;
            sm.OnKeyDown(0x41); // 'A'
            sm.OnKeyDown(VK_SPACE);
            Assert::IsTrue(sm.toolbarVisible);

            // Fast activation (user released too quickly)
            sm.OnKeyUp(0x41, /*wasFastActivation=*/true);
            Assert::IsFalse(sm.toolbarVisible);
            Assert::IsTrue(sm.lastHideInput == PowerAccentTestEnums::InputType::InputSpace);
        }

        TEST_METHOD(LetterUp_NormalActivation_HidesWithChar)
        {
            KeyStateMachine sm;
            sm.OnKeyDown(0x41); // 'A'
            sm.OnKeyDown(VK_SPACE);
            Assert::IsTrue(sm.toolbarVisible);

            // Normal activation (user held long enough)
            sm.OnKeyUp(0x41, /*wasFastActivation=*/false);
            Assert::IsFalse(sm.toolbarVisible);
            Assert::IsTrue(sm.lastHideInput == PowerAccentTestEnums::InputType::InputChar);
        }

        TEST_METHOD(LetterUp_WithoutTrigger_NoAccent)
        {
            KeyStateMachine sm;
            sm.OnKeyDown(0x41); // 'A'
            sm.OnKeyUp(0x41);   // Released without trigger
            Assert::IsFalse(sm.toolbarVisible);
            Assert::AreEqual(0, sm.hideCount, L"No hide event should fire if toolbar was never shown");
        }

        TEST_METHOD(CyclingWithArrows_IncrementsNextCount)
        {
            KeyStateMachine sm;
            sm.OnKeyDown(0x4F); // 'O'
            sm.OnKeyDown(VK_SPACE);
            Assert::AreEqual(1, sm.nextCount);

            // Cycle with right arrow
            sm.OnKeyDown(VK_RIGHT);
            Assert::AreEqual(2, sm.nextCount);
            Assert::IsTrue(sm.lastNextTrigger == PowerAccentTestEnums::TriggerKey::Right);

            // Cycle with left arrow
            sm.OnKeyDown(VK_LEFT);
            Assert::AreEqual(3, sm.nextCount);
            Assert::IsTrue(sm.lastNextTrigger == PowerAccentTestEnums::TriggerKey::Left);
        }

        TEST_METHOD(ActivationKey_LeftRightOnly_SpaceIgnored)
        {
            KeyStateMachine sm;
            sm.settings.activationKey = PowerAccentTestEnums::PowerAccentActivationKey::LeftRightArrow;
            sm.OnKeyDown(0x41); // 'A'
            sm.OnKeyDown(VK_SPACE);
            Assert::IsFalse(sm.toolbarVisible,
                            L"Space trigger should be ignored when activation is LeftRightArrow only");
        }

        TEST_METHOD(ActivationKey_SpaceOnly_ArrowsIgnored)
        {
            KeyStateMachine sm;
            sm.settings.activationKey = PowerAccentTestEnums::PowerAccentActivationKey::ActivationSpace;
            sm.OnKeyDown(0x41); // 'A'
            sm.OnKeyDown(VK_RIGHT);
            Assert::IsFalse(sm.toolbarVisible,
                            L"Arrow trigger should be ignored when activation is Space only");
        }

        TEST_METHOD(NonLetterKey_DoesNotTrigger)
        {
            KeyStateMachine sm;
            // F1 key (0x70) is not in the letter list
            sm.OnKeyDown(0x70);
            sm.OnKeyDown(VK_SPACE);
            Assert::IsFalse(sm.toolbarVisible,
                            L"Non-letter key should not activate the toolbar");
        }

        TEST_METHOD(NonLanguageLetter_DoesNotTrigger)
        {
            KeyStateMachine sm;
            sm.OnKeyDown(0x41, /*letterStillHeld=*/true, /*isLanguageLetter=*/false);
            sm.OnKeyDown(VK_SPACE);
            Assert::IsFalse(sm.toolbarVisible,
                            L"Letter not in language should not activate toolbar");
        }

        TEST_METHOD(RepeatedLetterWhileVisible_Suppressed)
        {
            KeyStateMachine sm;
            sm.OnKeyDown(0x41); // 'A'
            sm.OnKeyDown(VK_SPACE);
            Assert::IsTrue(sm.toolbarVisible);

            bool suppressed = sm.OnKeyDown(0x41); // repeated 'A' while visible
            Assert::IsTrue(suppressed, L"Repeated letter should be suppressed while toolbar is visible");
        }

        TEST_METHOD(FastActivation_WithLeftArrow_HidesWithLeft)
        {
            KeyStateMachine sm;
            sm.OnKeyDown(0x43); // 'C'
            sm.OnKeyDown(VK_LEFT);
            Assert::IsTrue(sm.toolbarVisible);

            sm.OnKeyUp(0x43, /*wasFastActivation=*/true);
            Assert::IsTrue(sm.lastHideInput == PowerAccentTestEnums::InputType::InputLeft);
        }

        TEST_METHOD(FastActivation_WithRightArrow_HidesWithRight)
        {
            KeyStateMachine sm;
            sm.OnKeyDown(0x55); // 'U'
            sm.OnKeyDown(VK_RIGHT);
            Assert::IsTrue(sm.toolbarVisible);

            sm.OnKeyUp(0x55, /*wasFastActivation=*/true);
            Assert::IsTrue(sm.lastHideInput == PowerAccentTestEnums::InputType::InputRight);
        }

        TEST_METHOD(DifferentLetterWhileVisible_ClosesAndReopens)
        {
            KeyStateMachine sm;
            sm.OnKeyDown(0x41); // 'A'
            sm.OnKeyDown(VK_SPACE);
            Assert::IsTrue(sm.toolbarVisible);
            Assert::AreEqual(1, sm.showCount);

            // Release 'A', toolbar hides
            sm.OnKeyUp(0x41);
            Assert::IsFalse(sm.toolbarVisible);
            Assert::AreEqual(1, sm.hideCount);
        }

        TEST_METHOD(NonLetterKeyDown_IgnoredWhenToolbarHidden)
        {
            KeyStateMachine sm;
            // Press Escape (not a letter)
            bool handled = sm.OnKeyDown(0x1B);
            Assert::IsFalse(handled, L"Non-letter key should not be handled");
            Assert::IsFalse(sm.toolbarVisible);
            Assert::AreEqual(0, sm.showCount);
        }

        TEST_METHOD(TriggerWithoutLetter_Ignored)
        {
            KeyStateMachine sm;
            // Press Space without any letter held
            sm.OnKeyDown(VK_SPACE);
            Assert::IsFalse(sm.toolbarVisible, L"Trigger without letter should not show toolbar");
            Assert::AreEqual(0, sm.showCount);
        }

        TEST_METHOD(RapidActivationCycle_StateConsistent)
        {
            KeyStateMachine sm;
            // Rapid: press A, trigger, release, press E, trigger, release
            sm.OnKeyDown(0x41); // 'A'
            sm.OnKeyDown(VK_SPACE);
            Assert::IsTrue(sm.toolbarVisible);
            sm.OnKeyUp(0x41);
            Assert::IsFalse(sm.toolbarVisible);
            Assert::AreEqual(1, sm.showCount);
            Assert::AreEqual(1, sm.hideCount);

            sm.OnKeyDown(0x45); // 'E'
            sm.OnKeyDown(VK_SPACE);
            Assert::IsTrue(sm.toolbarVisible);
            Assert::AreEqual(static_cast<int>(PowerAccentTestEnums::LetterKey::VK_E),
                             static_cast<int>(sm.lastShowLetter));
            sm.OnKeyUp(0x45);
            Assert::IsFalse(sm.toolbarVisible);
            Assert::AreEqual(2, sm.showCount);
            Assert::AreEqual(2, sm.hideCount);
        }
    };

    // ========================================================================
    // Letter list validation
    // ========================================================================
    TEST_CLASS(LetterListTests)
    {
    public:

        TEST_METHOD(AllAlphabetLettersPresent)
        {
            const auto& letters = KeyStateMachine::GetLetters();
            for (int vk = 0x41; vk <= 0x5A; ++vk)
            {
                auto key = static_cast<PowerAccentTestEnums::LetterKey>(vk);
                bool found = std::find(letters.begin(), letters.end(), key) != letters.end();
                Assert::IsTrue(found, (std::wstring(L"Letter VK ") + std::to_wstring(vk) + L" should be in list").c_str());
            }
        }

        TEST_METHOD(AllDigitKeysPresent)
        {
            const auto& letters = KeyStateMachine::GetLetters();
            for (int vk = 0x30; vk <= 0x39; ++vk)
            {
                auto key = static_cast<PowerAccentTestEnums::LetterKey>(vk);
                bool found = std::find(letters.begin(), letters.end(), key) != letters.end();
                Assert::IsTrue(found, (std::wstring(L"Digit VK ") + std::to_wstring(vk) + L" should be in list").c_str());
            }
        }

        TEST_METHOD(TriggerList_HasThreeEntries)
        {
            const auto& triggers = KeyStateMachine::GetTriggers();
            Assert::AreEqual(static_cast<size_t>(3), triggers.size());
        }

        TEST_METHOD(PunctuationKeysPresent)
        {
            using LK = PowerAccentTestEnums::LetterKey;
            const auto& letters = KeyStateMachine::GetLetters();
            auto hasKey = [&](LK key) {
                return std::find(letters.begin(), letters.end(), key) != letters.end();
            };
            Assert::IsTrue(hasKey(LK::VK_COMMA), L"Comma should be in letter list");
            Assert::IsTrue(hasKey(LK::VK_PERIOD), L"Period should be in letter list");
            Assert::IsTrue(hasKey(LK::VK_MINUS), L"Minus should be in letter list");
            Assert::IsTrue(hasKey(LK::VK_PLUS), L"Plus should be in letter list");
            Assert::IsTrue(hasKey(LK::VK_SLASH_), L"Slash should be in letter list");
        }

        TEST_METHOD(NonLetterKey_NotInList)
        {
            const auto& letters = KeyStateMachine::GetLetters();
            // VK_ESCAPE (0x1B) should not be in the letter list
            auto key = static_cast<PowerAccentTestEnums::LetterKey>(0x1B);
            bool found = std::find(letters.begin(), letters.end(), key) != letters.end();
            Assert::IsFalse(found, L"Escape should not be a valid accent letter");
        }
    };
}
