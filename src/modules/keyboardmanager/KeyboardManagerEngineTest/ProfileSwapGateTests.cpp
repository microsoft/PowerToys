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
#include <keyboardmanager/common/Helpers.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>

#include <condition_variable>
#include <mutex>
#include <thread>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RemappingLogicTests
{
    // Tests for applying profile changes only where a key-down and its key-up resolve under one profile.
    // The mocked hook calls ProfileSwapGate::OnHookEvent, the same dispatch KeyboardManager::HookProc
    // uses (classification by LLKHF_INJECTED / dwExtraInfo, numpad-origin encoding, tracked vs KBM's own
    // input). The test's key presses are delivered as physical input (no LLKHF_INJECTED) with the scan
    // code MapVirtualKey reports; input the handlers send is delivered as injected.
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
        int tablesReplaced = 0;
        std::atomic_bool loadingSettings = false;

        // The chain KeyboardManager::HandleKeyboardHookEvent runs (it adds only the settings-loading and
        // editor-open checks), with the gate's answer on whether shortcuts may run.
        intptr_t HandleEvent(LowlevelKeyboardEvent* data, bool shortcutsAllowed)
        {
            return KeyboardEventHandlers::HandleRemapEvent(mockedInputHandler, data, testState, shortcutsAllowed);
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

        // Mirrors KeyboardManager::OnSettingsChanged's in-place apply (State::ApplyReloadInPlace).
        ProfileSwapGate::OfferResult Offer(std::unique_ptr<State> configuration, Outcome outcome = Outcome::Loaded, const std::function<void(const std::wstring&)>& onRequested = nullptr)
        {
            return gate->Offer(
                outcome, std::move(configuration), [this](State& inPlace) {
                    ++applyInPlaceCalls;
                    if (testState.ApplyReloadInPlace(inPlace, loadingSettings))
                    {
                        ++tablesReplaced;
                    }
                },
                onRequested);
        }

        // Scan code (low byte) and extended flag (E0 prefix) of a virtual key, as MapVirtualKey reports.
        static WORD ScanOf(WORD vk)
        {
            return static_cast<WORD>(MapVirtualKeyW(vk, MAPVK_VK_TO_VSC_EX) & 0xFF);
        }

        static bool IsExtended(WORD vk)
        {
            return (MapVirtualKeyW(vk, MAPVK_VK_TO_VSC_EX) & 0xFF00) == 0xE000;
        }

        static constexpr INPUT KeyInput(WORD vk, bool up, WORD scan, bool extended, ULONG_PTR extraInfo = 0)
        {
            INPUT input{ .type = INPUT_KEYBOARD };
            input.ki.wVk = vk;
            input.ki.wScan = scan;
            input.ki.dwFlags = (up ? KEYEVENTF_KEYUP : 0) | (extended ? KEYEVENTF_EXTENDEDKEY : 0);
            input.ki.dwExtraInfo = extraInfo;
            return input;
        }

        // A physical key event with an explicit scan code (for keys whose virtual key depends on NumLock).
        void Physical(WORD vk, bool up, WORD scan, bool extended)
        {
            mockedInputHandler.SendPhysicalInput({ KeyInput(vk, up, scan, extended) });
        }

        void Press(WORD vk)
        {
            Physical(vk, false, ScanOf(vk), IsExtended(vk));
        }

        void Release(WORD vk)
        {
            Physical(vk, true, ScanOf(vk), IsExtended(vk));
        }

        // A key event injected by another program (SendInput without KBM's marker in dwExtraInfo).
        void ForeignInject(WORD vk, bool up, ULONG_PTR extraInfo = 0)
        {
            mockedInputHandler.SendVirtualInput({ KeyInput(vk, up, ScanOf(vk), IsExtended(vk), extraInfo) });
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
            tablesReplaced = 0;
            now = std::chrono::steady_clock::time_point{} + std::chrono::hours(1);

            gate = std::make_unique<ProfileSwapGate>(
                [this] { return now; },
                [this](DWORD vk) { return IsDown(vk); },
                [this](const std::vector<INPUT>& inputs) { mockedInputHandler.SendVirtualInput(inputs); });

            testState.currentConfig = L"A";
            gate->SetAppliedProfile(L"A");

            mockedInputHandler.SetHookProc([this](LowlevelKeyboardEvent* data) -> intptr_t {
                return gate->OnHookEvent(
                    data,
                    [this](LowlevelKeyboardEvent* event, bool shortcutsAllowed) { return HandleEvent(event, shortcutsAllowed); },
                    [this](std::unique_ptr<State> staged) { Adopt(std::move(staged)); });
            });
        }

        TEST_METHOD_CLEANUP(CleanupTestEnv)
        {
            mockedInputHandler.SetDeferInjectedInput(false);
        }

        // Scenario: A leaves X unmapped, B maps X to Y. X is held when B is staged and
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
        // long. What A's output left down is released first (the injected target), and the held keys'
        // later physical events follow A's answer to their key-downs, never the new profile.
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
            Assert::IsFalse(IsDown('T')); // T, left down by A, released at the swap
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

            Assert::IsTrue(gate->ApplyStageWithoutHook({ { 'X', ScanOf('X'), IsExtended('X') } }, [this](std::unique_ptr<State> staged) { Adopt(std::move(staged)); }));
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

        // A modifier that is already down when a profile takes effect without a hook is a pass-through
        // orphan: its key-up never reaches the handlers. So it must not start a shortcut of the new
        // profile (the shortcut's output would stay down after the modifier is released). The shortcut
        // works from the modifier's next press. Nothing here waits for the forced swap.
        TEST_METHOD (ApplyWithoutHook_HeldModifierDoesNotStartShortcut_NextPressDoes)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey('A');
            Shortcut dest;
            dest.SetKey(VK_SHIFT);
            dest.SetKey('B');

            // Ctrl was pressed while the (empty) profile had no hook.
            mockedInputHandler.SetKeyboardState(VK_LCONTROL, true);
            mockedInputHandler.SetKeyboardState(VK_CONTROL, true);
            auto b = MakeProfile(L"B");
            b->AddOSLevelShortcut(src, dest);
            Offer(std::move(b));
            Assert::IsTrue(gate->ApplyStageWithoutHook({ { VK_LCONTROL, ScanOf(VK_LCONTROL), IsExtended(VK_LCONTROL) } }, [this](std::unique_ptr<State> staged) { Adopt(std::move(staged)); }));

            // Full press and release with the held Ctrl: A passes through as Ctrl+A, nothing is injected.
            mockedInputHandler.SetSendVirtualInputTestHandler([](LowlevelKeyboardEvent* data) {
                return (data->lParam->flags & LLKHF_INJECTED) != 0;
            });
            Press('A');
            Assert::IsTrue(IsDown('A'));
            Assert::IsTrue(IsDown(VK_CONTROL));
            Assert::IsFalse(IsDown(VK_SHIFT));
            Assert::IsFalse(IsDown('B'));
            Assert::IsFalse(testState.osLevelShortcutReMap.begin()->second.isShortcutInvoked);
            Release('A');
            Release(VK_LCONTROL);
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());
            Assert::IsFalse(IsDown('A'));
            Assert::IsFalse(IsDown(VK_CONTROL));
            Assert::IsFalse(IsDown(VK_LCONTROL));
            Assert::IsFalse(IsDown(VK_SHIFT));
            Assert::IsTrue(gate->HeldKeyCount() == 0);

            // A key typed afterwards carries no modifier.
            Press('Q');
            Assert::IsTrue(IsDown('Q'));
            Assert::IsFalse(IsDown(VK_CONTROL));
            Assert::IsFalse(IsDown(VK_SHIFT));
            Release('Q');

            // From the next press of Ctrl the shortcut works, and its release finishes it.
            Press(VK_LCONTROL);
            Press('A');
            Assert::IsTrue(IsDown(VK_SHIFT));
            Assert::IsTrue(IsDown('B'));
            Assert::IsFalse(IsDown(VK_CONTROL));
            Release('A');
            Assert::IsFalse(IsDown('B'));
            Release(VK_LCONTROL);
            Assert::IsFalse(IsDown(VK_SHIFT));
            Assert::IsFalse(IsDown(VK_CONTROL));
            Assert::IsFalse(testState.osLevelShortcutReMap.begin()->second.isShortcutInvoked);
            Assert::IsTrue(gate->HeldKeyCount() == 0);
        }

        // Same, with the modifier released before the action key.
        TEST_METHOD (ApplyWithoutHook_HeldModifierDoesNotStartShortcut_ModifierReleasedFirst)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey('A');
            Shortcut dest;
            dest.SetKey(VK_SHIFT);
            dest.SetKey('B');

            mockedInputHandler.SetKeyboardState(VK_LCONTROL, true);
            mockedInputHandler.SetKeyboardState(VK_CONTROL, true);
            auto b = MakeProfile(L"B");
            b->AddOSLevelShortcut(src, dest);
            Offer(std::move(b));
            Assert::IsTrue(gate->ApplyStageWithoutHook({ { VK_LCONTROL, ScanOf(VK_LCONTROL), IsExtended(VK_LCONTROL) } }, [this](std::unique_ptr<State> staged) { Adopt(std::move(staged)); }));

            Press('A');
            Release(VK_LCONTROL);
            Release('A');
            Assert::IsFalse(IsDown('A'));
            Assert::IsFalse(IsDown('B'));
            Assert::IsFalse(IsDown(VK_CONTROL));
            Assert::IsFalse(IsDown(VK_SHIFT));
            Assert::IsFalse(testState.osLevelShortcutReMap.begin()->second.isShortcutInvoked);
            Assert::IsTrue(gate->HeldKeyCount() == 0);
        }

        // The same hole without any profile switch: the hook is installed (engine start, or the first
        // mapping added to an empty profile) while a modifier is held.
        TEST_METHOD (HookReinstalled_HeldModifierDoesNotStartShortcut_NextPressDoes)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey('A');
            Shortcut dest;
            dest.SetKey(VK_SHIFT);
            dest.SetKey('B');
            testState.AddOSLevelShortcut(src, dest);

            mockedInputHandler.SetKeyboardState(VK_RCONTROL, true);
            mockedInputHandler.SetKeyboardState(VK_CONTROL, true);
            gate->ResetHeldKeys({ { VK_RCONTROL, ScanOf(VK_RCONTROL), IsExtended(VK_RCONTROL) } });

            Press('A');
            Assert::IsTrue(IsDown('A'));
            Assert::IsFalse(IsDown(VK_SHIFT));
            Release('A');
            Release(VK_RCONTROL);
            Assert::IsFalse(IsDown(VK_CONTROL));
            Assert::IsFalse(IsDown(VK_SHIFT));
            Assert::IsTrue(gate->HeldKeyCount() == 0);

            Press(VK_RCONTROL);
            Press('A');
            Assert::IsTrue(IsDown(VK_SHIFT));
            Assert::IsTrue(IsDown('B'));
            Release('A');
            Release(VK_RCONTROL);
            Assert::IsFalse(IsDown(VK_SHIFT));
            Assert::IsFalse(IsDown('B'));
            Assert::IsFalse(IsDown(VK_CONTROL));
        }

        // The same for a shortcut that applies to one app only.
        TEST_METHOD (ApplyWithoutHook_HeldModifierDoesNotStartAppSpecificShortcut)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey('A');
            Shortcut dest;
            dest.SetKey(VK_SHIFT);
            dest.SetKey('B');
            const std::wstring testApp = L"testapp.exe";
            mockedInputHandler.SetForegroundProcess(testApp);

            mockedInputHandler.SetKeyboardState(VK_LCONTROL, true);
            mockedInputHandler.SetKeyboardState(VK_CONTROL, true);
            auto b = MakeProfile(L"B");
            b->AddAppSpecificShortcut(testApp, src, dest);
            Offer(std::move(b));
            Assert::IsTrue(gate->ApplyStageWithoutHook({ { VK_LCONTROL, ScanOf(VK_LCONTROL), IsExtended(VK_LCONTROL) } }, [this](std::unique_ptr<State> staged) { Adopt(std::move(staged)); }));

            Press('A');
            Assert::IsTrue(IsDown('A'));
            Assert::IsFalse(IsDown(VK_SHIFT));
            Assert::IsFalse(IsDown('B'));
            Release('A');
            Release(VK_LCONTROL);
            Assert::IsFalse(IsDown(VK_CONTROL));
            Assert::IsFalse(IsDown(VK_SHIFT));

            // From the next press it works.
            Press(VK_LCONTROL);
            Press('A');
            Assert::IsTrue(IsDown(VK_SHIFT));
            Assert::IsTrue(IsDown('B'));
            Release('A');
            Release(VK_LCONTROL);
            Assert::IsFalse(IsDown(VK_SHIFT));
            Assert::IsFalse(IsDown('B'));
        }

        // An orphan modifier that the system reports up (its key-up was lost while no hook was there)
        // cannot take part in a shortcut, so it must not keep shortcuts off: the record can stay for a
        // long time.
        TEST_METHOD (ApplyWithoutHook_OrphanModifierTheSystemReportsUp_DoesNotHoldShortcutsBack)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey('A');
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey('V');

            // Right Shift was in the snapshot, but the system no longer reports it down.
            auto b = MakeProfile(L"B");
            b->AddOSLevelShortcut(src, dest);
            Offer(std::move(b));
            Assert::IsTrue(gate->ApplyStageWithoutHook({ { VK_RSHIFT, ScanOf(VK_RSHIFT), IsExtended(VK_RSHIFT) } }, [this](std::unique_ptr<State> staged) { Adopt(std::move(staged)); }));
            Assert::IsFalse(IsDown(VK_RSHIFT));

            Press(VK_LCONTROL);
            Press('A');
            Assert::IsTrue(IsDown(VK_MENU));
            Assert::IsTrue(IsDown('V'));
            Release('A');
            Release(VK_LCONTROL);
            Assert::IsFalse(IsDown(VK_MENU));
            Assert::IsFalse(IsDown('V'));
        }

        // While a held orphan modifier holds shortcuts back, a single-key remap of the new profile
        // still applies to keys pressed after the switch.
        TEST_METHOD (ApplyWithoutHook_HeldModifier_SingleKeyRemapStillApplies)
        {
            mockedInputHandler.SetKeyboardState(VK_LCONTROL, true);
            mockedInputHandler.SetKeyboardState(VK_CONTROL, true);
            auto b = MakeProfile(L"B");
            b->AddSingleKeyRemap('X', static_cast<DWORD>('Y'));
            Offer(std::move(b));
            Assert::IsTrue(gate->ApplyStageWithoutHook({ { VK_LCONTROL, ScanOf(VK_LCONTROL), IsExtended(VK_LCONTROL) } }, [this](std::unique_ptr<State> staged) { Adopt(std::move(staged)); }));

            Press('X');
            Assert::IsTrue(IsDown('Y'));
            Assert::IsFalse(IsDown('X'));
            Release('X');
            Assert::IsFalse(IsDown('Y'));
            Release(VK_LCONTROL);
            Assert::IsFalse(IsDown(VK_CONTROL));
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

        // Forced variant: the shortcut is still held when the swap is forced. Its target (left down by A)
        // is released at the swap, and B starts clean.
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

        // Review: a forced swap releases only what the old profile left down. A pending "Alone" key
        // (its key-up lost) must not be resolved as a tap: its action never fires, neither at the swap
        // nor when the real key-up finally arrives.
        TEST_METHOD (ForcedSwap_PendingAloneKey_DoesNotFireTap)
        {
            testState.AddSingleKeyAloneRemap(VK_RCONTROL, static_cast<DWORD>(VK_IME_ON));
            Press(VK_RCONTROL); // suppressed, pending as a tap candidate
            Assert::IsFalse(IsDown(VK_RCONTROL));

            Offer(MakeProfile(L"B"));
            now += std::chrono::seconds(31);
            CountEventsOf(VK_IME_ON);
            Press('Q');
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());
            Release('Q');

            Release(VK_RCONTROL); // answered for the orphan: suppressed, as its key-down was
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());
            Assert::IsFalse(IsDown(VK_RCONTROL));
            Assert::IsTrue(gate->HeldKeyCount() == 0);
        }

        // Review: Ctrl+Shift+C -> Alt+V, C released (V released, Alt still down), and both source
        // modifiers' key-ups lost. The forced swap releases Alt and must not restore a source modifier
        // (replaying Shift-up through the shortcut handler injected Ctrl-down, left down for good).
        TEST_METHOD (ForcedSwap_TwoModifierShortcut_LeavesNoModifierDown)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(VK_SHIFT);
            src.SetKey('C');
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey('V');
            testState.AddOSLevelShortcut(src, dest);

            Press(VK_CONTROL);
            Press(VK_SHIFT);
            Press('C');
            Release('C');
            Assert::IsTrue(IsDown(VK_MENU));
            Assert::IsFalse(IsDown('V'));
            Assert::IsFalse(IsDown(VK_CONTROL));
            Assert::IsFalse(IsDown(VK_SHIFT));

            Offer(MakeProfile(L"B"));
            now += std::chrono::seconds(31);
            Press('Q');
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            Assert::IsFalse(IsDown(VK_CONTROL));
            Assert::IsFalse(IsDown(VK_SHIFT));
            Assert::IsFalse(IsDown(VK_MENU));
            Assert::IsFalse(IsDown('V'));
            Release('Q');
        }

        // Same, with C still held at the forced swap: the target key and modifier are released, no source
        // modifier is restored, and the real key-ups that come later leave nothing down.
        TEST_METHOD (ForcedSwap_TwoModifierShortcutStillHeld_LeavesNoModifierDown)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(VK_SHIFT);
            src.SetKey('C');
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey('V');
            testState.AddOSLevelShortcut(src, dest);

            Press(VK_CONTROL);
            Press(VK_SHIFT);
            Press('C');
            Assert::IsTrue(IsDown(VK_MENU));
            Assert::IsTrue(IsDown('V'));

            Offer(MakeProfile(L"B"));
            now += std::chrono::seconds(31);
            Press('Q');
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            Assert::IsFalse(IsDown(VK_CONTROL));
            Assert::IsFalse(IsDown(VK_SHIFT));
            Assert::IsFalse(IsDown(VK_MENU));
            Assert::IsFalse(IsDown('V'));
            Release('Q');

            Release('C');
            Release(VK_SHIFT);
            Release(VK_CONTROL);
            Assert::IsFalse(IsDown(VK_CONTROL));
            Assert::IsFalse(IsDown(VK_SHIFT));
            Assert::IsFalse(IsDown(VK_MENU));
            Assert::IsFalse(IsDown('V'));
            Assert::IsFalse(IsDown('C'));
            Assert::IsTrue(gate->HeldKeyCount() == 0);
        }

        // While a shortcut is invoked, another key's key-down is suppressed and re-sent by the handlers
        // with the reprocess tag, so the system has that key down as KBM's output. A forced swap must
        // release it like any other output: the key's own key-up is suppressed later for the orphan,
        // as its key-down was, and would otherwise leave it down for good.
        TEST_METHOD (ForcedSwap_ReprocessedKeyDown_IsReleased)
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
            Press('K'); // suppressed, re-sent by KBM with the reprocess tag
            Assert::IsTrue(IsDown('K'));

            Offer(MakeProfile(L"B"));
            now += std::chrono::seconds(31);
            Press('Q');
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            Assert::IsFalse(IsDown('K'));
            Release('Q');

            Release('K');
            Release('C');
            Release(VK_CONTROL);
            Assert::IsFalse(IsDown('K'));
            Assert::IsFalse(IsDown(VK_MENU));
            Assert::IsFalse(IsDown('V'));
            Assert::IsFalse(IsDown(VK_CONTROL));
            Assert::IsTrue(gate->HeldKeyCount() == 0);
        }

        // KBM's output that a key-up from another source has already released is not released again,
        // also when the key-up names the left/right key of a generic modifier target. Output nobody
        // released (Shift here) still is.
        TEST_METHOD (ForcedSwap_OutputReleasedByAnotherSource_IsNotReleasedAgain)
        {
            testState.AddSingleKeyRemap('X', static_cast<DWORD>('T'));
            testState.AddSingleKeyRemap('Y', static_cast<DWORD>(VK_CONTROL));
            testState.AddSingleKeyRemap('Z', static_cast<DWORD>(VK_SHIFT));
            Press('X');
            Press('Y');
            Press('Z');
            Assert::IsTrue(IsDown('T'));

            // The user presses and releases the targets themselves: T, and the left Ctrl.
            Press('T');
            Release('T');
            Press(VK_LCONTROL);
            Release(VK_LCONTROL);

            Offer(MakeProfile(L"B"));
            now += std::chrono::seconds(31);

            int releasedAgain = 0;
            int shiftReleased = 0;
            mockedInputHandler.SetSendVirtualInputTestHandler([&releasedAgain, &shiftReleased](LowlevelKeyboardEvent* data) {
                const DWORD vk = data->lParam->vkCode;
                if ((data->lParam->flags & LLKHF_INJECTED) && (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP))
                {
                    if (vk == 'T' || vk == VK_CONTROL || vk == VK_LCONTROL || vk == VK_RCONTROL)
                    {
                        ++releasedAgain;
                    }
                    else if (vk == VK_SHIFT || vk == VK_LSHIFT || vk == VK_RSHIFT)
                    {
                        ++shiftReleased;
                    }
                }

                return false;
            });

            Press('Q');
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            Assert::AreEqual(0, releasedAgain);
            Assert::AreEqual(1, shiftReleased);
            Assert::IsFalse(IsDown(VK_SHIFT));
            Release('Q');
        }

        // A reinstalled hook did not see the events while it was away, so it forgets what KBM's output
        // left down: a forced swap afterwards injects nothing for it.
        TEST_METHOD (HookReinstalled_ForgetsOwnOutput_ForcedSwapInjectsNothing)
        {
            testState.AddSingleKeyRemap('X', static_cast<DWORD>('T'));
            Press('X');
            Assert::IsTrue(IsDown('T'));

            // X is reported down, so its seeded record is not dropped as stale and the swap is forced.
            mockedInputHandler.SetKeyboardState('X', true);
            gate->ResetHeldKeys({ { 'X', ScanOf('X'), IsExtended('X') } });

            Offer(MakeProfile(L"B"));
            now += std::chrono::seconds(31);
            CountEventsOf('T');
            Press('Q');
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            Assert::IsTrue(gate->OrphanKeyCount() == 1); // forced: X is still held
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());
            Release('Q');
        }

        // The hook's classification: only LLKHF_INJECTED with one of KBM's exact tags is KBM's own output.
        // Other odd tags (bit 0 set) are other software's input: AutoHotkey's default SendLevel-0 tag
        // 0xFFC3D44D, other PowerToys modules' 0x5555 / 0x5557, or the bare bit.
        TEST_METHOD (ClassifyKeyEvent_SeparatesPhysicalForeignAndOwnInput)
        {
            using Source = ProfileSwapGate::KeyEventSource;
            Assert::IsTrue(ProfileSwapGate::ClassifyKeyEvent(0, 0) == Source::Physical);
            Assert::IsTrue(ProfileSwapGate::ClassifyKeyEvent(LLKHF_EXTENDED | LLKHF_UP, 0) == Source::Physical);
            Assert::IsTrue(ProfileSwapGate::ClassifyKeyEvent(LLKHF_INJECTED, 0) == Source::ForeignInjected);
            Assert::IsTrue(ProfileSwapGate::ClassifyKeyEvent(LLKHF_INJECTED, 0x1234560) == Source::ForeignInjected);
            Assert::IsTrue(ProfileSwapGate::ClassifyKeyEvent(LLKHF_INJECTED, CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG) == Source::ForeignInjected);
            Assert::IsTrue(ProfileSwapGate::ClassifyKeyEvent(LLKHF_INJECTED, 0xFFC3D44D) == Source::ForeignInjected);
            Assert::IsTrue(ProfileSwapGate::ClassifyKeyEvent(LLKHF_INJECTED, 0x5555) == Source::ForeignInjected);
            Assert::IsTrue(ProfileSwapGate::ClassifyKeyEvent(LLKHF_INJECTED, 0x5557) == Source::ForeignInjected);
            Assert::IsTrue(ProfileSwapGate::ClassifyKeyEvent(LLKHF_INJECTED, KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG) == Source::OwnInjected);
            Assert::IsTrue(ProfileSwapGate::ClassifyKeyEvent(LLKHF_INJECTED, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG) == Source::OwnInjected);
            Assert::IsTrue(ProfileSwapGate::ClassifyKeyEvent(LLKHF_INJECTED, KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG) == Source::OwnInjected);
            Assert::IsTrue(ProfileSwapGate::ClassifyKeyEvent(LLKHF_INJECTED, KeyboardManagerConstants::KEYBOARDMANAGER_REPROCESS_FLAG) == Source::OwnReprocessed);
            Assert::IsTrue(ProfileSwapGate::ClassifyKeyEvent(0, KeyboardManagerConstants::KEYBOARDMANAGER_REPROCESS_FLAG) == Source::Physical);
            Assert::IsTrue((KeyboardManagerConstants::KEYBOARDMANAGER_REPROCESS_FLAG & CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG) == 0);
        }

        // The same for input injected by another program (e.g. Mouse Without Borders): X is held
        // through it under A (unmapped), B (X->Y) is staged, and the user types on a real keyboard. X's
        // injected key-up must still be resolved under A, or B would turn it into a Y-up and leave X down.
        TEST_METHOD (ForeignInjectedKey_IsReleasedUnderProfileItWasPressedUnder)
        {
            ForeignInject('X', false);
            Assert::IsTrue(IsDown('X'));
            Assert::IsTrue(gate->HeldKeyCount() == 1);

            auto b = MakeProfile(L"B");
            b->AddSingleKeyRemap('X', static_cast<DWORD>('Y'));
            Offer(std::move(b));

            CountEventsOf('Y');
            Press('Z');
            Release('Z');
            Assert::IsTrue(gate->HasStage());

            ForeignInject('X', true);
            Assert::IsFalse(IsDown('X'));
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());

            // KBM's own output is not tracked: under B, X->Y holds only X (not the injected Y).
            Press('X');
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            Assert::IsTrue(IsDown('Y'));
            Assert::IsTrue(gate->HeldKeyCount() == 1);
            Release('X');
            Assert::IsFalse(IsDown('Y'));
            Assert::IsTrue(gate->HeldKeyCount() == 0);
        }

        // The stale rule for an injected key is the one for physical keys: an injected key never repeats,
        // so silence alone means nothing; it is dropped only once GetAsyncKeyState also reports it up.
        TEST_METHOD (ForeignInjectedKey_StaleOnlyWhenAsyncUp)
        {
            ForeignInject('X', false); // passes through under A
            Offer(MakeProfile(L"B"));

            now += std::chrono::seconds(10);
            Press('Z');
            Release('Z');
            Assert::IsTrue(gate->HasStage()); // silent, but the system still has X down

            mockedInputHandler.SetKeyboardState('X', false); // the injector's key-up was lost
            Press('Z');
            Assert::IsFalse(gate->HasStage());
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            Release('Z');
        }

        // Review: another program injects a chord with an odd dwExtraInfo that is not KBM's (here
        // AutoHotkey's default tag). A's shortcut handler remaps it (it skips only KBM's exact shortcut
        // tag), so the chord must be tracked like any foreign input: held across a switch to an empty
        // profile, it blocks the swap and its key-ups are resolved under A, releasing A's targets.
        TEST_METHOD (ForeignOddTaggedChord_HeldAcrossSwitch_ReleasedUnderOldProfile)
        {
            constexpr ULONG_PTR AutoHotkeyTag = 0xFFC3D44D;

            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey('C');
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey('V');
            testState.AddOSLevelShortcut(src, dest);

            ForeignInject(VK_CONTROL, false, AutoHotkeyTag);
            ForeignInject('C', false, AutoHotkeyTag);
            Assert::IsTrue(IsDown(VK_MENU));
            Assert::IsTrue(IsDown('V'));
            Assert::IsTrue(gate->HeldKeyCount() == 2);

            Offer(MakeProfile(L"B"));
            Press('Z');
            Release('Z');
            Assert::IsTrue(gate->HasStage());
            Assert::AreEqual(std::wstring(L"A"), gate->AppliedProfile());

            ForeignInject('C', true, AutoHotkeyTag);
            ForeignInject(VK_CONTROL, true, AutoHotkeyTag);
            Assert::IsFalse(IsDown(VK_MENU));
            Assert::IsFalse(IsDown('V'));
            Assert::IsFalse(IsDown(VK_CONTROL));
            Assert::IsTrue(gate->HeldKeyCount() == 0);

            Press('Z');
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            Release('Z');
        }

        // The same odd-tagged chord, held until the swap is forced: A's targets are released at the swap,
        // and the injector's later key-ups are answered for the orphans, leaving nothing down.
        TEST_METHOD (ForeignOddTaggedChord_ForcedSwap_LeavesNothingDown)
        {
            constexpr ULONG_PTR AutoHotkeyTag = 0xFFC3D44D;

            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey('C');
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey('V');
            testState.AddOSLevelShortcut(src, dest);

            ForeignInject(VK_CONTROL, false, AutoHotkeyTag);
            ForeignInject('C', false, AutoHotkeyTag);
            Assert::IsTrue(IsDown(VK_MENU));

            Offer(MakeProfile(L"B"));
            now += std::chrono::seconds(31);
            Press('Z');
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            Assert::IsFalse(IsDown(VK_MENU));
            Assert::IsFalse(IsDown('V'));
            Release('Z');

            ForeignInject('C', true, AutoHotkeyTag);
            ForeignInject(VK_CONTROL, true, AutoHotkeyTag);
            Assert::IsFalse(IsDown(VK_MENU));
            Assert::IsFalse(IsDown('V'));
            Assert::IsFalse(IsDown(VK_CONTROL));
            Assert::IsFalse(IsDown('C'));
            Assert::IsTrue(gate->HeldKeyCount() == 0);
        }

        // Another program's odd-tagged key-up (here AutoHotkey's) for a key held on the keyboard. The
        // single-key handlers ignore such input, so A's output stays down. The gate must keep the key
        // held as well: otherwise the key's next auto-repeat counts as a fresh press, applies B, and B
        // resolves the real key-up, leaving A's output down.
        TEST_METHOD (ForeignOddTaggedKeyUp_DoesNotEndPhysicalPress)
        {
            constexpr ULONG_PTR AutoHotkeyTag = 0xFFC3D44D;

            testState.AddSingleKeyRemap('X', static_cast<DWORD>('T'));
            Press('X');
            Assert::IsTrue(IsDown('T'));

            auto b = MakeProfile(L"B");
            b->AddSingleKeyRemap('X', static_cast<DWORD>('Q'));
            Offer(std::move(b));

            ForeignInject('X', true, AutoHotkeyTag);
            Assert::IsTrue(IsDown('T')); // A's handler ignores it
            Assert::IsTrue(gate->HeldKeyCount() == 1);

            Press('X'); // auto-repeat
            Assert::AreEqual(std::wstring(L"A"), gate->AppliedProfile());

            Release('X'); // resolved under A
            Assert::IsFalse(IsDown('T'));
            Assert::IsFalse(IsDown('Q'));
            Assert::IsTrue(gate->HeldKeyCount() == 0);

            Press('Z');
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            Release('Z');
        }

        // A numpad key's virtual key can change between its key-down and key-up (NumLock / Shift), while
        // its scan code stays the same: the key-up must still release the held key.
        TEST_METHOD (NumpadKey_VirtualKeyChangesBetweenDownAndUp_ScanCodeIdentifiesKey)
        {
            Physical(VK_NUMPAD4, false, 0x4B, false);
            Assert::IsTrue(gate->HeldKeyCount() == 1);
            Offer(MakeProfile(L"B"));

            Press('Z');
            Release('Z');
            Assert::IsTrue(gate->HasStage());

            // Same physical key, reported as the numpad Left arrow now (not extended: numpad origin).
            Physical(VK_LEFT, true, 0x4B, false);
            Assert::IsTrue(gate->HeldKeyCount() == 0);

            Press('Q');
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            Release('Q');
        }

        // An orphan seeded from GetAsyncKeyState carries an estimated scan code. For the numpad Enter
        // (VK_RETURN, real scan code E0 1C) MapVirtualKey estimates 1C, so the key-up's id differs; it is
        // matched by virtual key instead and passes through, not resolved by the new profile.
        TEST_METHOD (SeededOrphan_WrongEstimatedScanCode_MatchedByVirtualKey)
        {
            const DWORD numpadEnter = Helpers::EncodeKeyNumpadOrigin(VK_RETURN, true);
            mockedInputHandler.SetKeyboardState(VK_RETURN, true);
            auto b = MakeProfile(L"B");
            b->AddSingleKeyRemap(numpadEnter, static_cast<DWORD>(VK_TAB));
            Offer(std::move(b));

            Assert::IsTrue(gate->ApplyStageWithoutHook({ { VK_RETURN, 0x1C, false } }, [this](std::unique_ptr<State> staged) { Adopt(std::move(staged)); }));
            Assert::IsTrue(gate->OrphanKeyCount() == 1);

            CountEventsOf(VK_TAB);
            Physical(VK_RETURN, true, 0x1C, true);
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());
            Assert::IsFalse(IsDown(VK_RETURN));
            Assert::IsTrue(gate->HeldKeyCount() == 0);
        }

        // Same, with an auto-repeat (the real id) before the key-up: the repeat, matched by virtual key,
        // must move the record to the real id, or the key-up would find no record and be resolved by B.
        TEST_METHOD (SeededOrphan_WrongEstimatedScanCode_RepeatReKeysRecord)
        {
            const DWORD numpadEnter = Helpers::EncodeKeyNumpadOrigin(VK_RETURN, true);
            mockedInputHandler.SetKeyboardState(VK_RETURN, true);
            auto b = MakeProfile(L"B");
            b->AddSingleKeyRemap(numpadEnter, static_cast<DWORD>(VK_TAB));
            Offer(std::move(b));

            Assert::IsTrue(gate->ApplyStageWithoutHook({ { VK_RETURN, 0x1C, false } }, [this](std::unique_ptr<State> staged) { Adopt(std::move(staged)); }));
            Assert::IsTrue(gate->OrphanKeyCount() == 1);

            CountEventsOf(VK_TAB);
            Physical(VK_RETURN, false, 0x1C, true);
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());
            Assert::IsTrue(gate->OrphanKeyCount() == 1);

            Physical(VK_RETURN, true, 0x1C, true);
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());
            Assert::IsFalse(IsDown(VK_RETURN));
            Assert::IsTrue(gate->HeldKeyCount() == 0);
        }

        // A transient load failure keeps a pending stage, so the requested profile is that stage's, not
        // cleared; with nothing staged it is cleared so the tracker can retry.
        TEST_METHOD (TransientFailure_RequestedProfileFollowsPendingStage)
        {
            std::vector<std::wstring> requested;
            const auto record = [&requested](const std::wstring& profile) { requested.push_back(profile); };

            Offer(MakeProfile(L"C"), Outcome::Failed, record);
            Offer(MakeProfile(L"B"), Outcome::Loaded, record);
            Offer(MakeProfile(L"C"), Outcome::Failed, record);

            Assert::AreEqual(size_t{ 3 }, requested.size());
            Assert::AreEqual(std::wstring{}, requested[0]);
            Assert::AreEqual(std::wstring(L"B"), requested[1]);
            Assert::AreEqual(std::wstring(L"B"), requested[2]);
            Assert::AreEqual(std::wstring(L"B"), gate->StagedProfile());
        }

        // A pending "switch" to the applied profile (A -> B superseded by A -> A) does not hold back an
        // edit of A: the edit is applied in place and the A -> A stage is cancelled.
        TEST_METHOD (SameProfileReload_CancelsPendingSwitchToAppliedProfile)
        {
            Press('X');
            Offer(MakeProfile(L"B"));
            Assert::IsTrue(Offer(MakeProfile(L"A")) == ProfileSwapGate::OfferResult::Staged);

            std::wstring requested = L"unset";
            auto edited = MakeProfile(L"A");
            edited->AddSingleKeyRemap('Q', static_cast<DWORD>('R'));
            Assert::IsTrue(Offer(std::move(edited), Outcome::Loaded, [&requested](const std::wstring& profile) { requested = profile; }) == ProfileSwapGate::OfferResult::AppliedInPlace);
            Assert::AreEqual(1, applyInPlaceCalls);
            Assert::IsFalse(gate->HasStage());
            Assert::AreEqual(std::wstring{}, requested);
            Assert::IsTrue(testState.GetSingleKeyRemap('Q').has_value());
            Release('X');
        }

        // The forced swap as Windows orders it (assumed): input injected from the hook callback reaches
        // the hook after the callback returns. The key-ups that release A's output therefore arrive after
        // the swap and are seen by the new profile's handlers. They carry KBM's shortcut tag; this pins
        // down that B leaves them alone.
        TEST_METHOD (HalfInvokedShortcut_ForcedSwap_DeferredInjection)
        {
            mockedInputHandler.SetDeferInjectedInput(true);

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

            // Record which profile is live when the injected V key-up reaches the hook.
            std::wstring profileAtInjectedVUp;
            mockedInputHandler.SetHookProc([this, &profileAtInjectedVUp](LowlevelKeyboardEvent* data) -> intptr_t {
                if ((data->lParam->flags & LLKHF_INJECTED) && data->lParam->vkCode == 'V' && (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP))
                {
                    profileAtInjectedVUp = testState.currentConfig;
                }

                return gate->OnHookEvent(
                    data,
                    [this](LowlevelKeyboardEvent* event, bool shortcutsAllowed) { return HandleEvent(event, shortcutsAllowed); },
                    [this](std::unique_ptr<State> staged) { Adopt(std::move(staged)); });
            });

            now += std::chrono::seconds(31);
            Press('Q');
            Assert::AreEqual(std::wstring(L"B"), testState.currentConfig);

            // The V-up releasing A's output was injected at the swap, so B's handlers are the ones that see it.
            Assert::AreEqual(std::wstring(L"B"), profileAtInjectedVUp);

            // In this configuration B leaves the injected key-ups alone: nothing is left down and B's
            // shortcut is not marked invoked.
            Assert::IsFalse(IsDown(VK_MENU));
            Assert::IsFalse(IsDown('V'));
            Assert::IsFalse(testState.osLevelShortcutReMap.begin()->second.isShortcutInvoked);
            Release('Q');

            Release('C');
            Release(VK_CONTROL);
            Assert::IsFalse(IsDown(VK_MENU));
            Assert::IsFalse(IsDown('V'));
            Assert::IsFalse(IsDown(VK_CONTROL));
            Assert::IsFalse(testState.osLevelShortcutReMap.begin()->second.isShortcutInvoked);
            Assert::IsTrue(gate->HeldKeyCount() == 0);
        }

        // While a shortcut is invoked, another key-down is re-sent by the handlers so that they
        // process it again. The re-send uses MapVirtualKey's scan code: for the numpad Enter (real E0 1C)
        // that is 1C without E0, a different key id. It must not become a held key of its own (its
        // key-up never comes: the physical key-up has the physical id), or it would block every later
        // swap until the forced one.
        TEST_METHOD (ReprocessedKeyDown_DifferentScanCode_LeavesNoHeldKey)
        {
            mockedInputHandler.SetDeferInjectedInput(true);

            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey('C');
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey('V');
            testState.AddOSLevelShortcut(src, dest);

            // Watch for the re-sent key-down, so the test fails loudly if the handlers stop sending it.
            int reprocessed = 0;
            mockedInputHandler.SetSendVirtualInputTestHandler([&reprocessed](LowlevelKeyboardEvent* data) {
                if ((data->lParam->flags & LLKHF_INJECTED) && Helpers::ClearKeyNumpadOrigin(data->lParam->vkCode) == VK_RETURN && data->wParam == WM_KEYDOWN)
                {
                    ++reprocessed;
                }

                return false;
            });

            Press(VK_CONTROL);
            Press('C');
            Assert::IsTrue(IsDown(VK_MENU));
            Physical(VK_RETURN, false, 0x1C, true); // numpad Enter, Ctrl still held
            Assert::AreEqual(1, reprocessed);

            Physical(VK_RETURN, true, 0x1C, true);
            Release('C');
            Release(VK_CONTROL);
            Assert::IsTrue(gate->HeldKeyCount() == 0);

            // Nothing is held, so a staged profile is applied at the next fresh key-down.
            Offer(MakeProfile(L"B"));
            Press('Q');
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            Release('Q');
        }

        // An orphan made by a forced swap carries the real id of its key. Another physical key with
        // the same virtual key (the NumLock-off numpad 4 is VK_LEFT, scan 4B, while the arrow key is
        // E0 4B) must not be taken for it, or that key's key-up would remove the orphan and the arrow's
        // real key-up would then be resolved by the new profile.
        TEST_METHOD (ForcedSwapOrphan_NotMatchedBySameVirtualKeyOfOtherKey)
        {
            Physical(VK_LEFT, false, 0x4B, true); // arrow key, unmapped under A, passes through

            auto b = MakeProfile(L"B");
            b->AddSingleKeyRemap(VK_LEFT, static_cast<DWORD>('X'));
            Offer(std::move(b));

            now += std::chrono::seconds(31);
            Press('Z');
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            Assert::IsTrue(gate->OrphanKeyCount() == 1);
            Release('Z');

            Physical(VK_LEFT, false, 0x4B, false); // numpad 4 with NumLock off
            Physical(VK_LEFT, true, 0x4B, false);
            Assert::IsTrue(gate->OrphanKeyCount() == 1);

            // The arrow's own key-up is answered for the orphan (pass through), not mapped by B.
            CountEventsOf('X');
            Physical(VK_LEFT, true, 0x4B, true);
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());
            Assert::IsTrue(gate->HeldKeyCount() == 0);
        }

        // A seeded orphan's estimated id is settled by the key's own first event (here an
        // auto-repeat with the exact id). From then on it is matched by id only, like any other record.
        TEST_METHOD (SeededOrphan_ConfirmedByOwnEvent_NotMatchedBySameVirtualKeyOfOtherKey)
        {
            mockedInputHandler.SetKeyboardState(VK_LEFT, true);
            auto b = MakeProfile(L"B");
            b->AddSingleKeyRemap(VK_LEFT, static_cast<DWORD>('X'));
            Offer(std::move(b));

            Assert::IsTrue(gate->ApplyStageWithoutHook({ { VK_LEFT, 0x4B, true } }, [this](std::unique_ptr<State> staged) { Adopt(std::move(staged)); }));
            Physical(VK_LEFT, false, 0x4B, true); // the arrow key still auto-repeating

            Physical(VK_LEFT, false, 0x4B, false); // numpad 4 with NumLock off
            Physical(VK_LEFT, true, 0x4B, false);
            Assert::IsTrue(gate->OrphanKeyCount() == 1);

            CountEventsOf('X');
            Physical(VK_LEFT, true, 0x4B, true);
            Assert::AreEqual(0, mockedInputHandler.GetSendVirtualInputCallCount());
            Assert::IsTrue(gate->HeldKeyCount() == 0);
        }

        // Gate side of a failed switch: a switch that fails before any reload reports the staged profile, or empty
        // once the stage has been applied by a key-down. KeyboardManager::SwitchActiveProfile's use of
        // this is not covered (KeyboardManager cannot be constructed in tests).
        TEST_METHOD (ReportPendingProfile_FollowsStage)
        {
            std::wstring pending = L"unset";
            const auto record = [&pending](const std::wstring& profile) { pending = profile; };

            gate->ReportPendingProfile(record);
            Assert::AreEqual(std::wstring{}, pending);

            Offer(MakeProfile(L"B"));
            gate->ReportPendingProfile(record);
            Assert::AreEqual(std::wstring(L"B"), pending);

            Press('Q');
            Release('Q');
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            gate->ReportPendingProfile(record);
            Assert::AreEqual(std::wstring{}, pending);
        }

        // Review: saving the keyboard assignments (or anything else that reloads the live profile without
        // changing its remaps) must not drop a held shortcut's runtime state. Ctrl+A -> Shift+B is held,
        // the reload runs, then the chord is released: Shift and B must be released too.
        TEST_METHOD (SameProfileReload_UnchangedBindings_KeepsHeldShortcutState)
        {
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey('A');
            Shortcut dest;
            dest.SetKey(VK_SHIFT);
            dest.SetKey('B');
            testState.AddOSLevelShortcut(src, dest);

            Press(VK_CONTROL);
            Press('A');
            Assert::IsTrue(IsDown(VK_SHIFT));
            Assert::IsTrue(IsDown('B'));

            auto same = MakeProfile(L"A");
            same->AddOSLevelShortcut(src, dest);
            Assert::IsTrue(Offer(std::move(same)) == ProfileSwapGate::OfferResult::AppliedInPlace);
            Assert::AreEqual(1, applyInPlaceCalls);

            Release('A');
            Release(VK_CONTROL);
            Assert::IsFalse(IsDown(VK_SHIFT));
            Assert::IsFalse(IsDown('B'));
            Assert::IsFalse(IsDown(VK_CONTROL));
            Assert::AreEqual(0, tablesReplaced);
        }

        // Same for a pending "Alone" key: an unchanged reload keeps it pending, so its tap still fires.
        TEST_METHOD (SameProfileReload_UnchangedBindings_KeepsPendingAloneKey)
        {
            testState.AddSingleKeyAloneRemap(VK_RCONTROL, static_cast<DWORD>(VK_IME_ON));
            Press(VK_RCONTROL);

            auto same = MakeProfile(L"A");
            same->AddSingleKeyAloneRemap(VK_RCONTROL, static_cast<DWORD>(VK_IME_ON));
            Offer(std::move(same));

            CountEventsOf(VK_IME_ON);
            Release(VK_RCONTROL);
            Assert::AreEqual(2, mockedInputHandler.GetSendVirtualInputCallCount()); // tap: down + up
            Assert::IsFalse(IsDown(VK_RCONTROL));
        }

        // A reload that does change a binding still replaces the tables in place right away.
        TEST_METHOD (SameProfileReload_ChangedBinding_ReplacesTables)
        {
            testState.AddSingleKeyRemap('X', static_cast<DWORD>('Y'));

            auto edited = MakeProfile(L"A");
            edited->AddSingleKeyRemap('X', static_cast<DWORD>('Z'));
            Assert::IsTrue(Offer(std::move(edited)) == ProfileSwapGate::OfferResult::AppliedInPlace);
            Assert::AreEqual(1, tablesReplaced);

            Press('X');
            Assert::IsTrue(IsDown('Z'));
            Assert::IsFalse(IsDown('Y'));
            Release('X');
            Assert::IsFalse(IsDown('Z'));
        }

        // HasSameBindings compares every stored field of every table, and ignores runtime state.
        TEST_METHOD (HasSameBindings_DetectsEveryStoredChange_IgnoresRuntimeState)
        {
            Shortcut ctrlC;
            ctrlC.SetKey(VK_CONTROL);
            ctrlC.SetKey('C');
            Shortcut altV;
            altV.SetKey(VK_MENU);
            altV.SetKey('V');
            Shortcut ctrlR;
            ctrlR.SetKey(VK_CONTROL);
            ctrlR.SetKey('R');
            Shortcut runNotepad;
            runNotepad.operationType = Shortcut::OperationType::RunProgram;
            runNotepad.runProgramFilePath = L"notepad.exe";
            runNotepad.runProgramArgs = L"a.txt";

            struct Options
            {
                std::wstring app = L"app.exe";
                bool exactMatch = false;
            };

            const auto build = [&](const Options& options, const std::function<void(State&)>& tweak) {
                State configuration;
                configuration.AddSingleKeyRemap('X', static_cast<DWORD>('Y'));
                configuration.AddSingleKeyAloneRemap(VK_RCONTROL, static_cast<DWORD>(VK_ESCAPE));
                configuration.AddSingleKeyToTextRemap('T', L"text");
                Shortcut source = ctrlC;
                source.exactMatch = options.exactMatch;
                configuration.AddOSLevelShortcut(source, altV);
                configuration.AddOSLevelShortcut(ctrlR, runNotepad);
                configuration.AddAppSpecificShortcut(options.app, ctrlC, altV);
                if (tweak)
                {
                    tweak(configuration);
                }

                return configuration;
            };

            const State base = build({}, nullptr);
            Assert::IsTrue(base.HasSameBindings(build({}, nullptr)));

            // Runtime state and the configuration name do not count.
            Assert::IsTrue(base.HasSameBindings(build({}, [](State& c) {
                c.osLevelShortcutReMap.begin()->second.isShortcutInvoked = true;
                c.osLevelShortcutReMap.begin()->second.isOriginalActionKeyPressed = true;
                c.osLevelShortcutReMapSortedKeys.front().SetChordStarted(true);
                c.currentConfig = L"Other";
            })));

            const std::vector<std::function<void(State&)>> changes{
                [](State& c) { c.singleKeyReMap['X'] = static_cast<DWORD>('Z'); },
                [](State& c) { c.AddSingleKeyRemap('Q', static_cast<DWORD>('W')); },
                [](State& c) { c.aloneSingleKeyReMap[VK_RCONTROL] = static_cast<DWORD>(VK_TAB); },
                [](State& c) { c.singleKeyToTextReMap['T'] = std::wstring(L"other"); },
                [](State& c) { c.ClearSingleKeyToTextRemaps(); },
                [ctrlC](State& c) { std::get<Shortcut>(c.osLevelShortcutReMap.find(ctrlC)->second.targetShortcut).actionKey = 'B'; },
                [ctrlR](State& c) { std::get<Shortcut>(c.osLevelShortcutReMap.find(ctrlR)->second.targetShortcut).runProgramArgs = L"b.txt"; },
                [ctrlR](State& c) { std::get<Shortcut>(c.osLevelShortcutReMap.find(ctrlR)->second.targetShortcut).elevationLevel = Shortcut::ElevationLevel::Elevated; },
                [ctrlR](State& c) { c.osLevelShortcutReMap.find(ctrlR)->second.targetShortcut = static_cast<DWORD>('R'); },
                [ctrlC](State& c) { std::get<Shortcut>(c.appSpecificShortcutReMap[L"app.exe"].find(ctrlC)->second.targetShortcut).actionKey = 'B'; },
            };

            for (size_t i = 0; i < changes.size(); ++i)
            {
                Assert::IsFalse(base.HasSameBindings(build({}, changes[i])), (L"change " + std::to_wstring(i)).c_str());
            }

            Assert::IsFalse(base.HasSameBindings(build({ .app = L"other.exe" }, nullptr)));
            Assert::IsFalse(base.HasSameBindings(build({ .exactMatch = true }, nullptr)));
        }

        // Hooks driven by ReconcileHooks in the tests: installed state, a counter per call, and an
        // optional action run inside uninstall (to interleave the settings thread there).
        struct FakeHooks
        {
            bool installed = true;
            bool mouseHook = false;
            int installCalls = 0;
            int uninstallCalls = 0;
            std::function<void()> duringUninstall;
        };

        ProfileSwapGate::HookControl HookControlFor(FakeHooks& hooks)
        {
            return {
                [&hooks] { return hooks.installed; },
                [this] { return !testState.singleKeyReMap.empty() || !testState.osLevelShortcutReMap.empty() || !testState.aloneSingleKeyReMap.empty(); },
                [this] { return !testState.aloneSingleKeyReMap.empty(); },
                [this, &hooks](const bool mouseHook) {
                    ++hooks.installCalls;
                    hooks.mouseHook = mouseHook;
                    if (!hooks.installed)
                    {
                        // As KeyboardManager::StartLowlevelKeyboardHook: a new hook forgets the held keys.
                        gate->ResetHeldKeys({});
                    }

                    hooks.installed = true;
                },
                [&hooks] {
                    ++hooks.uninstallCalls;
                    if (hooks.duringUninstall)
                    {
                        auto action = std::move(hooks.duringUninstall);
                        hooks.duringUninstall = nullptr;
                        action();
                    }
                    hooks.installed = false;
                    hooks.mouseHook = false;
                },
                [] { return std::vector<ProfileSwapGate::KeySnapshot>{}; }
            };
        }

        void Reconcile(FakeHooks& hooks)
        {
            gate->ReconcileHooks(HookControlFor(hooks), [this](std::unique_ptr<State> staged) { Adopt(std::move(staged)); });
        }

        // Review: after a switch to an empty profile the hook thread decides to unhook; before the
        // removal, the settings thread applies the profile's first mapping in place (and, seeing the hook
        // still installed, does not ask for it). The reconciliation must not end with the mapping live
        // and no hook.
        TEST_METHOD (ReconcileHooks_MappingAddedBeforeUnhook_HookStaysInstalled)
        {
            FakeHooks hooks;
            hooks.duringUninstall = [this] {
                auto edited = MakeProfile(L"A");
                edited->AddSingleKeyRemap('X', static_cast<DWORD>('Y'));
                Assert::IsTrue(Offer(std::move(edited)) == ProfileSwapGate::OfferResult::AppliedInPlace);
            };

            Reconcile(hooks);
            Assert::IsTrue(testState.GetSingleKeyRemap('X').has_value());
            Assert::IsTrue(hooks.installed);
            Assert::AreEqual(1, hooks.uninstallCalls);
            Assert::AreEqual(1, hooks.installCalls);
        }

        // The ordinary cases: an empty profile removes the hook once, a non-empty one installs it, and a
        // stage offered during the removal is applied without the hook (then its tables decide).
        TEST_METHOD (ReconcileHooks_FollowsLiveTablesAndAppliesLateStage)
        {
            FakeHooks hooks;
            Reconcile(hooks);
            Assert::IsFalse(hooks.installed);
            Assert::AreEqual(1, hooks.uninstallCalls);
            Assert::AreEqual(0, hooks.installCalls);

            testState.AddSingleKeyRemap('X', static_cast<DWORD>('Y'));
            Reconcile(hooks);
            Assert::IsTrue(hooks.installed);
            Assert::AreEqual(1, hooks.installCalls);

            testState.ClearSingleKeyRemaps();
            hooks.duringUninstall = [this] {
                auto b = MakeProfile(L"B");
                b->AddSingleKeyRemap('X', static_cast<DWORD>('W'));
                Assert::IsTrue(Offer(std::move(b)) == ProfileSwapGate::OfferResult::Staged);
            };
            Reconcile(hooks);
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            Assert::IsFalse(gate->HasStage());
            Assert::IsTrue(hooks.installed);
            Assert::AreEqual(2, hooks.uninstallCalls);
            Assert::AreEqual(2, hooks.installCalls);
        }

        // The settings thread replaces the tables in place one table at a time, under the gate lock. A
        // reconciliation running meanwhile on the hook thread must not see them half-replaced (all
        // empty for a moment, here when the last single-key remap is replaced by a shortcut) and remove
        // the hook: a new hook would forget the held keys and KBM's output left down.
        TEST_METHOD (ReconcileHooks_DoesNotReadTablesMidReplace)
        {
            testState.AddSingleKeyRemap('X', static_cast<DWORD>('Y'));

            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey('C');
            Shortcut dest;
            dest.SetKey(VK_MENU);
            dest.SetKey('V');
            auto edited = MakeProfile(L"A");
            edited->AddOSLevelShortcut(src, dest);

            std::mutex signalMutex;
            std::condition_variable signal;
            bool midReplace = false;
            std::thread settingsThread([&] {
                gate->Offer(Outcome::Loaded, std::move(edited), [&](State& inPlace) {
                    testState.ClearSingleKeyRemaps(); // the first table is replaced: every table is empty
                    {
                        std::lock_guard<std::mutex> lock(signalMutex);
                        midReplace = true;
                    }

                    signal.notify_all();
                    std::this_thread::sleep_for(std::chrono::milliseconds(300));
                    testState.ApplyReloadInPlace(inPlace, loadingSettings);
                });
            });

            {
                std::unique_lock<std::mutex> lock(signalMutex);
                signal.wait(lock, [&midReplace] { return midReplace; });
            }

            FakeHooks hooks;
            Reconcile(hooks);
            settingsThread.join();

            Assert::IsFalse(testState.osLevelShortcutReMap.empty());
            Assert::IsTrue(hooks.installed);
            Assert::AreEqual(0, hooks.uninstallCalls);
        }

        // OnSettingsChanged posts a reconciliation after every reload, so most of them find nothing to
        // change. Such a reconciliation must leave the hook, the held keys and KBM's held output alone.
        TEST_METHOD (ReconcileHooks_ExtraMessages_KeepHeldKeysAndOutput)
        {
            testState.AddSingleKeyRemap('X', static_cast<DWORD>('T'));
            Press('X');
            Assert::IsTrue(IsDown('T'));

            FakeHooks hooks;
            Reconcile(hooks);
            Reconcile(hooks);
            Assert::IsTrue(hooks.installed);
            Assert::AreEqual(0, hooks.uninstallCalls);
            Assert::IsTrue(gate->HeldKeyCount() == 1);
            Assert::IsTrue(gate->OrphanKeyCount() == 0);
            Assert::IsFalse(hooks.mouseHook);

            // KBM's output is still known: a forced swap releases it.
            Offer(MakeProfile(L"B"));
            now += std::chrono::seconds(31);
            Press('Q');
            Assert::AreEqual(std::wstring(L"B"), gate->AppliedProfile());
            Assert::IsFalse(IsDown('T'));
            Release('Q');

            // The mouse hook follows the "Alone" remaps, decided from the same read of the tables.
            testState.AddSingleKeyAloneRemap(VK_RCONTROL, static_cast<DWORD>(VK_ESCAPE));
            Reconcile(hooks);
            Assert::IsTrue(hooks.installed);
            Assert::IsTrue(hooks.mouseHook);
        }
    };
}
