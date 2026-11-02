#include "pch.h"

// Suppressing 26466 - Don't use static_cast downcasts - in CppUnitTest.h
#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "MockedInput.h"
#include <keyboardmanager/KeyboardManagerEngineLibrary/State.h>
#include <keyboardmanager/KeyboardManagerEngineLibrary/KeyboardEventHandlers.h>
#include <keyboardmanager/KeyboardManagerEngineLibrary/ProfileSwapGate.h>
#include "TestHelpers.h"
#include <common/interop/shared_constants.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RemappingLogicTests
{
    // Tests for applying profile changes only where a key-down and its key-up resolve under one profile.
    // The mocked hook mirrors KeyboardManager::HookProc: physical input goes through the gate, injected
    // input is handled by the live tables directly. MockedInput sets no LLKHF flags, so the test treats
    // dwExtraInfo == 0 (the test's own driving input) as physical.
    TEST_CLASS (ProfileSwapGateTests)
    {
    private:
        using Outcome = MappingConfiguration::LoadOutcome;

        KeyboardManagerInput::MockedInput mockedInputHandler;
        State testState;
        std::chrono::steady_clock::time_point now{};
        std::unique_ptr<ProfileSwapGate> gate;
        std::vector<std::unique_ptr<State>> retired;
        int applyInPlaceCalls = 0;

        // Mirrors KeyboardManager::HandleKeyboardHookEvent (without the settings-loading / editor checks).
        intptr_t HandleEvent(LowlevelKeyboardEvent* data)
        {
            if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
            {
                return 1;
            }

            if (KeyboardEventHandlers::HandleSingleKeyAloneRemapEvent(mockedInputHandler, data, testState) == 1)
            {
                return 1;
            }

            if (KeyboardEventHandlers::HandleSingleKeyRemapEvent(mockedInputHandler, data, testState) == 1)
            {
                return 1;
            }

            if (KeyboardEventHandlers::HandleAppSpecificShortcutRemapEvent(mockedInputHandler, data, testState) == 1)
            {
                return 1;
            }

            if (KeyboardEventHandlers::HandleSingleKeyToTextRemapEvent(mockedInputHandler, data, testState) == 1)
            {
                return 1;
            }

            return KeyboardEventHandlers::HandleOSLevelShortcutRemapEvent(mockedInputHandler, data, testState);
        }

        void Adopt(std::unique_ptr<State> staged)
        {
            testState.AdoptConfiguration(*staged, true);
            retired.push_back(std::move(staged));
        }

        static std::unique_ptr<State> MakeProfile(const std::wstring& name)
        {
            auto profile = std::make_unique<State>();
            profile->currentConfig = name;
            return profile;
        }

        ProfileSwapGate::OfferResult Offer(std::unique_ptr<State> configuration, Outcome outcome = Outcome::Loaded)
        {
            return gate->Offer(outcome, std::move(configuration), [this](State& inPlace) {
                ++applyInPlaceCalls;
                testState.AdoptConfiguration(inPlace, false);
            });
        }

        void Press(WORD vk)
        {
            std::vector<INPUT> inputs{ { .type = INPUT_KEYBOARD, .ki = { .wVk = vk } } };
            mockedInputHandler.SendVirtualInput(inputs);
        }

        void Release(WORD vk)
        {
            std::vector<INPUT> inputs{ { .type = INPUT_KEYBOARD, .ki = { .wVk = vk, .dwFlags = KEYEVENTF_KEYUP } } };
            mockedInputHandler.SendVirtualInput(inputs);
        }

        // Count every key-down or key-up of `vk` that reaches the mocked hook (physical or injected).
        void CountEventsOf(DWORD vk)
        {
            mockedInputHandler.SetSendVirtualInputTestHandler([vk](LowlevelKeyboardEvent* data) {
                return data->lParam->vkCode == vk;
            });
        }

        bool IsDown(DWORD vk)
        {
            return mockedInputHandler.GetVirtualKeyState(static_cast<int>(vk));
        }

    public:
        TEST_METHOD_INITIALIZE(InitializeTestEnv)
        {
            TestHelpers::ResetTestEnv(mockedInputHandler, testState);
            retired.clear();
            applyInPlaceCalls = 0;
            now = std::chrono::steady_clock::time_point{} + std::chrono::hours(1);

            gate = std::make_unique<ProfileSwapGate>(
                [this] { return now; },
                [this](DWORD vk) { return IsDown(vk); });

            testState.currentConfig = L"A";
            gate->SetAppliedProfile(L"A");

            mockedInputHandler.SetHookProc([this](LowlevelKeyboardEvent* data) -> intptr_t {
                if (data->lParam->dwExtraInfo == 0)
                {
                    return gate->OnPhysicalKeyEvent(
                        data,
                        [this](LowlevelKeyboardEvent* event) { return HandleEvent(event); },
                        [this](std::unique_ptr<State> staged) { Adopt(std::move(staged)); });
                }

                gate->OnInjectedKeyEvent(data);
                return HandleEvent(data);
            });
        }

        // The reviewer's scenario: A leaves X unmapped, B maps X to Y. X is held when B is staged and
        // another key is typed; X's key-up must be resolved under A (X-up delivered, Y never pressed).
        TEST_METHOD (HeldKey_IsReleasedUnderProfileItWasPressedUnder)
        {
            Press('X');
            Assert::IsTrue(IsDown('X'));

            auto b = MakeProfile(L"B");
            b->AddSingleKeyRemap('X', static_cast<DWORD>('Y'));
            Assert::IsTrue(Offer(std::move(b)) == ProfileSwapGate::OfferResult::Staged);

            CountEventsOf('Y');

            // A fresh key-down while X is held must not apply B.
            Press('Z');
            Release('Z');
            Assert::IsTrue(gate->HasStage());
            Assert::AreEqual(std::wstring(L"A"), gate->AppliedProfile());

            Release('X');
            Assert::IsFalse(IsDown('X'));
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());

            // A key-up never applies a stage; the next fresh key-down does, and is mapped under B.
            Assert::IsTrue(gate->HasStage());
            Press('X');
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            Assert::AreEqual(std::wstring(L"B"), testState.currentConfig);
            Assert::IsFalse(gate->HasStage());
            Assert::IsFalse(IsDown('X'));
            Assert::IsTrue(IsDown('Y'));

            Release('X');
            Assert::IsFalse(IsDown('Y'));
            Assert::IsTrue(gate->HeldKeyCount() == 0);
        }

        // An auto-repeat key-down of a held key is not a fresh press and never applies a stage.
        TEST_METHOD (AutoRepeat_NeverAppliesStage)
        {
            Press('X');

            auto b = MakeProfile(L"B");
            b->AddSingleKeyRemap('X', static_cast<DWORD>('Y'));
            Offer(std::move(b));

            CountEventsOf('Y');
            for (int i = 0; i < 5; ++i)
            {
                now += std::chrono::milliseconds(33);
                Press('X');
            }

            Assert::IsTrue(gate->HasStage());
            Assert::IsTrue(IsDown('X'));
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());

            Release('X');
            Assert::IsFalse(IsDown('X'));
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());
        }

        // Only the newest of several unapplied stages is applied.
        TEST_METHOD (SeveralStages_OnlyLastIsApplied)
        {
            auto b = MakeProfile(L"B");
            b->AddSingleKeyRemap('X', static_cast<DWORD>('Y'));
            auto c = MakeProfile(L"C");
            c->AddSingleKeyRemap('X', static_cast<DWORD>('W'));

            Assert::IsTrue(Offer(std::move(b)) == ProfileSwapGate::OfferResult::Staged);
            Assert::IsTrue(Offer(std::move(c)) == ProfileSwapGate::OfferResult::Staged);
            Assert::AreEqual(std::wstring(L"C"), gate->StagedProfile());

            Press('X');
            Assert::AreEqual(std::wstring(L"C"), gate->AppliedProfile());
            Assert::IsTrue(IsDown('W'));
            Assert::IsFalse(IsDown('Y'));
            Release('X');
            Assert::IsFalse(IsDown('W'));
        }

        // A transient load failure changes nothing: live tables and any pending stage are kept.
        TEST_METHOD (TransientFailure_IsDiscarded)
        {
            testState.AddSingleKeyRemap('X', static_cast<DWORD>('Y'));

            Assert::IsTrue(Offer(MakeProfile(L"B"), Outcome::Failed) == ProfileSwapGate::OfferResult::Discarded);
            Assert::IsFalse(gate->HasStage());
            Assert::AreEqual(0, applyInPlaceCalls);

            Offer(MakeProfile(L"B"));
            Assert::IsTrue(Offer(MakeProfile(L"C"), Outcome::Failed) == ProfileSwapGate::OfferResult::Discarded);
            Assert::AreEqual(std::wstring(L"B"), gate->StagedProfile());

            // A's mapping is still live until B is applied.
            Assert::IsTrue(testState.GetSingleKeyRemap('X').has_value());
        }

        // A profile whose file is confirmed absent is staged as an empty configuration and, once
        // applied, leaves no mapping of the previous profile live.
        TEST_METHOD (ConfirmedAbsent_StagesEmptyConfiguration)
        {
            testState.AddSingleKeyRemap('X', static_cast<DWORD>('Y'));

            Assert::IsTrue(Offer(MakeProfile(L"Gone"), Outcome::ConfirmedAbsent) == ProfileSwapGate::OfferResult::Staged);
            Assert::IsTrue(testState.GetSingleKeyRemap('X').has_value());

            Press('X');
            Assert::AreEqual(std::wstring(L"Gone"), testState.currentConfig);
            Assert::IsTrue(testState.singleKeyReMap.empty());
            Assert::IsTrue(IsDown('X'));
            Assert::IsFalse(IsDown('Y'));
            Release('X');
            Assert::IsFalse(IsDown('X'));
        }

        // A reload of the applied profile (an editor edit) is applied in place right away, as before;
        // while a switch is pending it supersedes that switch through the gate instead.
        TEST_METHOD (SameProfileReload_AppliedInPlaceUnlessSwitchPending)
        {
            Press('X');

            auto edited = MakeProfile(L"A");
            edited->AddSingleKeyRemap('Q', static_cast<DWORD>('R'));
            Assert::IsTrue(Offer(std::move(edited)) == ProfileSwapGate::OfferResult::AppliedInPlace);
            Assert::AreEqual(1, applyInPlaceCalls);
            Assert::IsTrue(testState.GetSingleKeyRemap('Q').has_value());
            Assert::IsFalse(gate->HasStage());

            Offer(MakeProfile(L"B"));
            Assert::IsTrue(Offer(MakeProfile(L"A")) == ProfileSwapGate::OfferResult::Staged);
            Assert::AreEqual(1, applyInPlaceCalls);
            Assert::AreEqual(std::wstring(L"A"), gate->StagedProfile());
            Release('X');
        }

        // A key the system saw is dropped as stale only after HeldKeyStaleTimeout without events AND
        // once GetAsyncKeyState reports it up; a key the hook suppressed is never dropped that way.
        TEST_METHOD (StaleKey_DroppedOnlyWhenSilentAndAsyncUp)
        {
            Press('X'); // passes through (A leaves X unmapped)
            Offer(MakeProfile(L"B"));

            // Silent but still down according to GetAsyncKeyState: kept, no swap.
            now += std::chrono::seconds(4);
            Press('Z');
            Release('Z');
            Assert::IsTrue(gate->HasStage());

            // Its key-up was lost (async state up) but it has not been silent long enough yet.
            mockedInputHandler.SetKeyboardState('X', false);
            now += std::chrono::seconds(1);
            Press('X'); // still believed held: an auto-repeat, not a fresh press
            now += std::chrono::seconds(2);
            Press('Z');
            Release('Z');
            Assert::IsTrue(gate->HasStage());

            // Silent for HeldKeyStaleTimeout and up: dropped, so the next fresh key-down applies B.
            mockedInputHandler.SetKeyboardState('X', false);
            now += std::chrono::seconds(3);
            Press('Z');
            Assert::IsFalse(gate->HasStage());
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            Release('Z');
        }

        TEST_METHOD (SuppressedKey_NeverDroppedAsStale)
        {
            testState.AddSingleKeyRemap('X', static_cast<DWORD>('T'));
            Press('X'); // suppressed, T injected
            Assert::IsFalse(IsDown('X'));
            Offer(MakeProfile(L"B"));

            now += std::chrono::seconds(10);
            Press('Z');
            Release('Z');
            Assert::IsTrue(gate->HasStage());
            Assert::IsTrue(IsDown('T'));

            Release('X');
            Assert::IsFalse(IsDown('T'));
        }

        // Forced swap: the stage has waited ForcedSwapTimeout and every held key has been silent that
        // long. The held keys' key-ups are replayed under the old profile first (releasing the injected
        // target), and their later physical events follow the old profile's answer, never the new one.
        TEST_METHOD (ForcedSwap_ReleasesOldTargetsAndOrphansHeldKeys)
        {
            testState.AddSingleKeyRemap('X', static_cast<DWORD>('T'));
            Press('X'); // suppressed under A, T injected
            Press('P'); // unmapped under A, passes through
            Assert::IsTrue(IsDown('T'));
            Assert::IsTrue(IsDown('P'));

            auto b = MakeProfile(L"B");
            b->AddSingleKeyRemap('X', static_cast<DWORD>('Q'));
            b->AddSingleKeyRemap('P', static_cast<DWORD>('R'));
            Offer(std::move(b));

            // Not yet: the stage has not waited long enough.
            now += std::chrono::seconds(20);
            Press('Z');
            Release('Z');
            Assert::IsTrue(gate->HasStage());

            now += std::chrono::seconds(11);
            Press('Z');
            Assert::IsFalse(gate->HasStage());
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            Assert::IsFalse(IsDown('T')); // T-up injected under A before the swap
            Assert::IsTrue(gate->OrphanKeyCount() == 2);
            Release('Z');

            // Orphans: X-up is suppressed as A would have, P-up passes through; B's targets never fire.
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* data) {
                return data->lParam->vkCode == 'Q' || data->lParam->vkCode == 'R';
            });
            Release('X');
            Release('P');
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());
            Assert::IsFalse(IsDown('P'));
            Assert::IsFalse(IsDown('X'));
            Assert::IsTrue(gate->HeldKeyCount() == 0);

            // A fresh press is mapped under B.
            Press('X');
            Assert::IsTrue(IsDown('Q'));
            Release('X');
            Assert::IsFalse(IsDown('Q'));
        }

        // A held key that produced an event recently (e.g. still auto-repeating) blocks the forced swap.
        TEST_METHOD (ForcedSwap_NotWhileHeldKeyIsActive)
        {
            Press('X');
            Offer(MakeProfile(L"B"));

            now += std::chrono::seconds(31);
            Press('X'); // auto-repeat: the key is really held
            now += std::chrono::seconds(9);
            Press('Z');
            Release('Z');
            Assert::IsTrue(gate->HasStage());
            Release('X');
        }

        // Applying a stage while no hook is installed: keys already down passed through unremapped, so
        // their key-ups pass through too instead of being resolved by the new profile.
        TEST_METHOD (ApplyWithoutHook_KeysDownBecomePassThroughOrphans)
        {
            Assert::IsFalse(gate->ApplyStageWithoutHook({}, [this](std::unique_ptr<State> staged) { Adopt(std::move(staged)); }));

            mockedInputHandler.SetKeyboardState('X', true);
            auto b = MakeProfile(L"B");
            b->AddSingleKeyRemap('X', static_cast<DWORD>('Y'));
            Offer(std::move(b));

            Assert::IsTrue(gate->ApplyStageWithoutHook({ { 'X', 0, false } }, [this](std::unique_ptr<State> staged) { Adopt(std::move(staged)); }));
            Assert::AreEqual(std::wstring(L"B"), testState.currentConfig);

            CountEventsOf('Y');
            Press('X'); // auto-repeat of the orphan: still passes through
            Release('X');
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());
            Assert::IsFalse(IsDown('X'));

            Press('X');
            Assert::IsTrue(IsDown('Y'));
            Release('X');
        }

        // A shortcut invoked under A is finished under A; B's tables start with no invocation state.
        TEST_METHOD (HalfInvokedShortcut_DoesNotLeakAcrossSwap)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey('C');
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey('V');
            testState.AddOSLevelShortcut(src, dest);

            Press(VK_CONTROL);
            Press('C');
            Assert::IsTrue(IsDown(VK_MENU));
            Assert::IsTrue(IsDown('V'));

            auto b = MakeProfile(L"B");
            b->AddOSLevelShortcut(src, dest);
            Offer(std::move(b));

            Release('C');
            Release(VK_CONTROL);
            Assert::IsTrue(gate->HasStage());
            Assert::IsFalse(IsDown(VK_MENU));
            Assert::IsFalse(IsDown('V'));
            Assert::IsFalse(IsDown(VK_CONTROL));

            Press('Q');
            Release('Q');
            Assert::AreEqual(std::wstring(L"B"), testState.currentConfig);
            Assert::IsFalse(testState.osLevelShortcutReMap.begin()->second.isShortcutInvoked);

            // B's copy of the shortcut works from a clean state.
            Press(VK_CONTROL);
            Press('C');
            Assert::IsTrue(IsDown(VK_MENU));
            Assert::IsTrue(IsDown('V'));
            Release('C');
            Release(VK_CONTROL);
            Assert::IsFalse(IsDown(VK_MENU));
            Assert::IsFalse(IsDown('V'));
            Assert::IsFalse(IsDown(VK_CONTROL));
        }

        // A shortcut remap injects a key-up for the physically held source modifier, so GetAsyncKeyState
        // reports it up although it is held. Such a key must not be dropped as stale, or a later fresh
        // key-down would apply B while Ctrl is still down and Ctrl's key-up would be resolved by B.
        TEST_METHOD (HeldModifierReleasedByInjection_IsNotDroppedAsStale)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey('C');
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey('V');
            testState.AddOSLevelShortcut(src, dest);

            Press(VK_CONTROL);
            Press('C');
            Release('C');
            Assert::IsFalse(IsDown(VK_CONTROL)); // released by the remap's injected key-up

            auto b = MakeProfile(L"B");
            b->AddSingleKeyRemap(VK_CONTROL, static_cast<DWORD>(VK_SHIFT));
            Offer(std::move(b));

            now += std::chrono::seconds(5);
            Press('Q');
            Release('Q');
            Assert::IsTrue(gate->HasStage());

            Release(VK_CONTROL); // resolved under A
            Assert::IsFalse(IsDown(VK_MENU));
            Assert::IsFalse(IsDown('V'));
            Assert::IsFalse(IsDown(VK_SHIFT));

            Press('Q');
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            Release('Q');
            Assert::IsFalse(IsDown(VK_SHIFT));
            Assert::IsFalse(IsDown(VK_MENU));
        }

        // Forced variant: the shortcut is still held when the swap is forced. Its target is released
        // under A by the replayed key-ups, and B starts clean.
        TEST_METHOD (HalfInvokedShortcut_ForcedSwap_ReleasesTargetUnderOldProfile)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey('C');
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey('V');
            testState.AddOSLevelShortcut(src, dest);

            Press(VK_CONTROL);
            Press('C');
            Assert::IsTrue(IsDown(VK_MENU));
            Assert::IsTrue(IsDown('V'));

            auto b = MakeProfile(L"B");
            b->AddOSLevelShortcut(src, dest);
            Offer(std::move(b));

            now += std::chrono::seconds(31);
            Press('Q');
            Assert::AreEqual(std::wstring(L"B"), testState.currentConfig);
            Assert::IsFalse(IsDown(VK_MENU));
            Assert::IsFalse(IsDown('V'));
            Assert::IsFalse(testState.osLevelShortcutReMap.begin()->second.isShortcutInvoked);
            Release('Q');

            // The real key-ups arrive later and are answered for the orphans, not by B.
            Release('C');
            Release(VK_CONTROL);
            Assert::IsFalse(IsDown(VK_MENU));
            Assert::IsFalse(IsDown('V'));
            Assert::IsFalse(IsDown(VK_CONTROL));
            Assert::IsFalse(testState.osLevelShortcutReMap.begin()->second.isShortcutInvoked);
            Assert::IsTrue(gate->HeldKeyCount() == 0);
        }
    };
}
