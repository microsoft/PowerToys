#include "pch.h"

// Suppressing 26466 - Don't use static_cast downcasts - in CppUnitTest.h
#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "MockedInput.h"

#include <common/interop/shared_constants.h>
#include <keyboardmanager/common/Helpers.h>
#include <keyboardmanager/KeyboardManagerEngineLibrary/TextExpansionController.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace TextExpansionEngineTests
{
    namespace
    {
        struct TestKeyEvent
        {
            KBDLLHOOKSTRUCT keyboardData{};
            LowlevelKeyboardEvent event{};

            explicit TestKeyEvent(
                const DWORD key,
                const WPARAM message = WM_KEYDOWN,
                const DWORD flags = 0,
                const ULONG_PTR extraInfo = 0,
                const DWORD scanCode = 0)
            {
                keyboardData.vkCode = key;
                keyboardData.scanCode = scanCode;
                keyboardData.flags = flags;
                keyboardData.dwExtraInfo = extraInfo;
                event.wParam = message;
                event.lParam = &keyboardData;
            }
        };

        TextExpansionRule MakeRule(
            const wchar_t* id,
            const wchar_t* sourceText,
            const std::initializer_list<int32_t> activationKeys,
            const wchar_t* replacementText,
            const bool enabled = true)
        {
            return {
                .id = id,
                .sourceText = sourceText,
                .activation = Shortcut(std::vector<int32_t>(activationKeys)),
                .replacementText = replacementText,
                .enabled = enabled,
            };
        }

        class FakeTextExpansionBackend : public ITextExpansionBackend
        {
        public:
            bool startResult = true;
            bool ready = false;
            bool pendingWork = false;
            bool prepared = false;
            bool recoveryResult = true;
            DWORD recoveryKey = 0;
            std::function<void(const TextExpansionRecoveryRequest&)> replayRecovery;
            TextExpansionResult prepareResult = TextExpansionResult::NoMatch;
            TextExpansionResult completeResult = TextExpansionResult::Replaced;
            TextExpansionResult cancelResult = TextExpansionResult::FailedUnchanged;

            int startCalls = 0;
            int stopCalls = 0;
            int activateCalls = 0;
            int completeCalls = 0;
            int recoveryCalls = 0;
            int cancelCalls = 0;
            int cleanupCalls = 0;
            int trackCalls = 0;
            int resetBufferCalls = 0;
            DWORD lastTrackedKey = 0;
            WPARAM lastTrackedMessage = 0;
            std::shared_ptr<const TextExpansionIndex> lastIndex;
            DWORD lastActionKey = 0;
            uint8_t lastModifierMask = 0;
            TextExpansionRecoveryRequest lastRecovery;

            bool Start() override
            {
                ++startCalls;
                ready = startResult;
                return ready;
            }

            void Stop() noexcept override
            {
                ++stopCalls;
                ready = false;
                prepared = false;
            }

            bool IsReady() const noexcept override
            {
                return ready;
            }

            bool HasRecoveryKeyState() const noexcept override
            {
                return recoveryKey != 0;
            }

            bool HandleRecoveryKeyEvent(const LowlevelKeyboardEvent* data) noexcept override
            {
                if (!data || !data->lParam || recoveryKey == 0 ||
                    Helpers::ClearKeyNumpadOrigin(data->lParam->vkCode) != recoveryKey)
                {
                    return false;
                }
                if (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP)
                {
                    recoveryKey = 0;
                }
                return true;
            }

            void TrackKeyboardEvent(const LowlevelKeyboardEvent* data) noexcept override
            {
                ++trackCalls;
                if (data && data->lParam)
                {
                    lastTrackedKey = data->lParam->vkCode;
                    lastTrackedMessage = data->wParam;
                }
            }

            void ResetBuffer() noexcept override
            {
                ++resetBufferCalls;
            }

            TextExpansionResult PrepareActivation(const TextExpansionRequest& request) override
            {
                ++activateCalls;
                lastIndex = request.index;
                lastActionKey = request.actionKey;
                lastModifierMask = request.modifierMask;
                prepared = prepareResult == TextExpansionResult::Prepared;
                return prepareResult;
            }

            const TextExpansionIndex::IndexedRule* FindLastRule(const std::wstring_view trackedText) const
            {
                if (!lastIndex)
                {
                    return nullptr;
                }
                const auto match = lastIndex->FindLongestMatch(lastActionKey, lastModifierMask, trackedText);
                return match ? lastIndex->GetRule(*match) : nullptr;
            }

            TextExpansionResult CompletePendingActivation() noexcept override
            {
                ++completeCalls;
                if (!prepared)
                {
                    return TextExpansionResult::FailedUnchanged;
                }
                prepared = false;
                return completeResult;
            }

            TextExpansionResult CancelPendingActivation() noexcept override
            {
                ++cancelCalls;
                prepared = false;
                return cancelResult;
            }

            bool RecoverPendingActivation(const TextExpansionRecoveryRequest& request) noexcept override
            {
                ++recoveryCalls;
                lastRecovery = request;
                prepared = false;
                if (recoveryResult && replayRecovery)
                {
                    replayRecovery(request);
                }
                return recoveryResult;
            }

            void RetryPendingCleanup() noexcept override
            {
                ++cleanupCalls;
            }

            bool ShouldBlockNewInput() const noexcept override
            {
                return pendingWork || prepared;
            }

            bool HasPendingWork() const noexcept override
            {
                return pendingWork || prepared;
            }
        };

        struct ControllerFixture
        {
            KeyboardManagerInput::MockedInput input;
            FakeTextExpansionBackend* backend = nullptr;
            std::unique_ptr<TextExpansionController> controller;
            bool queueResult = true;
            int queueCalls = 0;
            std::vector<uint64_t> queuedGenerations;

            ControllerFixture()
            {
                auto fakeBackend = std::make_unique<FakeTextExpansionBackend>();
                backend = fakeBackend.get();
                controller = std::make_unique<TextExpansionController>(
                    std::move(fakeBackend),
                    [this](const uint64_t generation) {
                        ++queueCalls;
                        if (queueResult)
                        {
                            queuedGenerations.push_back(generation);
                        }
                        return queueResult;
                    });
                Assert::IsTrue(controller->Start(input));
                backend->replayRecovery = [this](const TextExpansionRecoveryRequest& request) {
                    TestKeyEvent replay(
                        request.replayKey,
                        WM_KEYDOWN,
                        request.replayExtended ? LLKHF_EXTENDED : 0,
                        KeyboardManagerConstants::KEYBOARDMANAGER_TEXT_EXPANSION_REPLAY_FLAG,
                        request.replayScanCode);
                    controller->BeginKeyboardEvent(&replay.event);
                };
            }

            TextExpansionController::EventDisposition Begin(
                const DWORD key,
                const WPARAM message = WM_KEYDOWN,
                const DWORD flags = 0,
                const DWORD scanCode = 0,
                const ULONG_PTR extraInfo = 0)
            {
                TestKeyEvent keyEvent(key, message, flags, extraInfo, scanCode);
                return controller->BeginKeyboardEvent(&keyEvent.event);
            }

            intptr_t Activate(
                const DWORD key,
                const TextExpansionTable& rules,
                const DWORD flags = 0,
                const DWORD scanCode = 0)
            {
                TestKeyEvent event(key, WM_KEYDOWN, flags, 0, scanCode);
                if (!controller->SetTextExpansions(rules))
                {
                    return 0;
                }
                return controller->TryActivate(input, &event.event);
            }

            TextExpansionResult Complete()
            {
                // A transaction generation is only queued after every physical key in
                // the activation shortcut has been released.
                Assert::IsFalse(queuedGenerations.empty());
                const uint64_t generation = queuedGenerations.front();
                queuedGenerations.erase(queuedGenerations.begin());
                return controller->CompletePendingActivation(generation);
            }

            void SetLeftCtrl(const bool down)
            {
                input.SetKeyboardState(VK_LCONTROL, down);
                input.SetKeyboardState(VK_CONTROL, down || input.GetVirtualKeyState(VK_RCONTROL));
            }

            void SetLeftShift(const bool down)
            {
                input.SetKeyboardState(VK_LSHIFT, down);
                input.SetKeyboardState(VK_SHIFT, down || input.GetVirtualKeyState(VK_RSHIFT));
            }

            void SetRightCtrl(const bool down)
            {
                input.SetKeyboardState(VK_RCONTROL, down);
                input.SetKeyboardState(VK_CONTROL, down || input.GetVirtualKeyState(VK_LCONTROL));
            }

            void SetLeftAlt(const bool down)
            {
                input.SetKeyboardState(VK_LMENU, down);
                input.SetKeyboardState(VK_MENU, down);
            }

            void SetLeftWin(const bool down)
            {
                input.SetKeyboardState(VK_LWIN, down);
            }
        };

        void AssertDisposition(
            const TextExpansionController::EventDisposition expected,
            const TextExpansionController::EventDisposition actual)
        {
            Assert::AreEqual(static_cast<int>(expected), static_cast<int>(actual));
        }

        void AssertResult(const TextExpansionResult expected, const TextExpansionResult actual)
        {
            Assert::AreEqual(static_cast<int>(expected), static_cast<int>(actual));
        }

        void VerifyPassthroughResult(const TextExpansionResult result)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = result;
            const TextExpansionTable rules{ MakeRule(L"id-1", L"brb", { VK_SPACE }, L"be right back") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(0, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            Assert::AreEqual(1, fixture.backend->activateCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_SPACE));
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
        }
    }

    TEST_CLASS (TextExpansionControllerTests)
    {
    public:
        TEST_METHOD (StartAndStop_ShouldOwnBackendLifecycle)
        {
            auto backend = std::make_unique<FakeTextExpansionBackend>();
            auto* backendView = backend.get();
            TextExpansionController controller(std::move(backend));
            KeyboardManagerInput::MockedInput input;

            Assert::IsTrue(controller.Start(input));
            Assert::IsTrue(controller.Start(input));
            Assert::AreEqual(1, backendView->startCalls);

            controller.Stop();
            Assert::AreEqual(1, backendView->stopCalls);
            TestKeyEvent stoppedKey('A');
            AssertDisposition(TextExpansionController::EventDisposition::Ignore, controller.BeginKeyboardEvent(&stoppedKey.event));
            Assert::IsFalse(controller.HasPendingWork());
        }

        TEST_METHOD (TrackKeyboardEventAndResetBuffer_ShouldForwardToBackend)
        {
            ControllerFixture fixture;
            TestKeyEvent keyEvent('A');

            fixture.controller->TrackKeyboardEvent(&keyEvent.event);
            Assert::AreEqual(1, fixture.backend->trackCalls);
            Assert::AreEqual(static_cast<DWORD>('A'), fixture.backend->lastTrackedKey);
            Assert::AreEqual(
                static_cast<uint64_t>(WM_KEYDOWN),
                static_cast<uint64_t>(fixture.backend->lastTrackedMessage));

            fixture.controller->ResetBuffer();
            Assert::AreEqual(1, fixture.backend->resetBufferCalls);
        }

        TEST_METHOD (SingleSpace_ShouldActivateMatchingRule)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_SPACE }, L"be right back") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            Assert::AreEqual(1, fixture.backend->activateCalls);
            Assert::AreEqual(static_cast<DWORD>(VK_SPACE), fixture.backend->lastActionKey);
            const auto* matchedRule = fixture.backend->FindLastRule(L"brb");
            Assert::IsNotNull(matchedRule);
            Assert::AreEqual(std::wstring(L"brb"), matchedRule->sourceText);
            Assert::AreEqual(0, fixture.queueCalls);
            Assert::AreEqual(0, fixture.backend->completeCalls);

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(1, fixture.queueCalls);
            Assert::AreEqual(static_cast<size_t>(1), fixture.queuedGenerations.size());
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
            Assert::IsFalse(fixture.controller->HasPendingWork());
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
        }

        TEST_METHOD (ArbitraryCapturedSingleKey_ShouldNotBeRestrictedByExpansionAllowlist)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_F8 }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_F8));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_F8, rules)));
            Assert::AreEqual(1, fixture.backend->activateCalls);
            Assert::AreEqual(static_cast<DWORD>(VK_F8), fixture.backend->lastActionKey);
        }

        TEST_METHOD (CtrlSpace_ShouldUseNormalCapturedShortcutSemantics)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            fixture.SetLeftCtrl(true);
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_CONTROL, VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            Assert::AreEqual(1, fixture.backend->activateCalls);
            Assert::AreEqual(
                static_cast<int>(TextExpansionModifiers::LeftCtrl),
                static_cast<int>(fixture.backend->lastModifierMask));
        }

        TEST_METHOD (CtrlShiftEnter_ShouldActivateWithAllCapturedModifiers)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            fixture.SetLeftCtrl(true);
            fixture.SetLeftShift(true);
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"sig", { VK_CONTROL, VK_SHIFT, VK_RETURN }, L"signature") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_RETURN));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_RETURN, rules)));
            Assert::AreEqual(1, fixture.backend->activateCalls);
            Assert::AreEqual(
                static_cast<int>(TextExpansionModifiers::LeftCtrl | TextExpansionModifiers::LeftShift),
                static_cast<int>(fixture.backend->lastModifierMask));
        }

        TEST_METHOD (CtrlEnter_ActionUpFirst_ShouldQueueOnlyAfterModifierUp)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"sig", { VK_CONTROL, VK_RETURN }, L"signature") };

            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LCONTROL));
            fixture.SetLeftCtrl(true);
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_RETURN));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_RETURN, rules)));
            Assert::AreEqual(0, fixture.queueCalls);

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_RETURN));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_RETURN, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(0, fixture.queueCalls);

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LCONTROL));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftCtrl(false);
            Assert::AreEqual(1, fixture.queueCalls);
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
        }

        TEST_METHOD (CtrlEnter_ModifierUpFirst_ShouldQueueOnlyAfterActionUp)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"sig", { VK_CONTROL, VK_RETURN }, L"signature") };

            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LCONTROL));
            fixture.SetLeftCtrl(true);
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_RETURN));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_RETURN, rules)));
            Assert::AreEqual(0, fixture.queueCalls);

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LCONTROL));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftCtrl(false);
            Assert::AreEqual(0, fixture.queueCalls);

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_RETURN));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_RETURN, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(1, fixture.queueCalls);
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
        }

        TEST_METHOD (CtrlEnter_NewOppositeSideModifierPress_ShouldRecoverTriggerAndPassModifier)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"sig", { VK_CONTROL, VK_RETURN }, L"signature") };

            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LCONTROL));
            fixture.SetLeftCtrl(true);
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_RETURN));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_RETURN, rules)));

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_RETURN, WM_KEYUP, LLKHF_UP));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_RCONTROL));
            Assert::AreEqual(1, fixture.backend->recoveryCalls);
            Assert::AreEqual(static_cast<DWORD>(VK_RETURN), fixture.backend->lastRecovery.actionKey);
            Assert::AreEqual(static_cast<DWORD>(VK_RCONTROL), fixture.backend->lastRecovery.replayKey);
            Assert::AreEqual(
                static_cast<int>(0),
                static_cast<int>(fixture.backend->lastRecovery.releasedActivationModifierMask));
            Assert::AreEqual(0, fixture.backend->completeCalls);
            fixture.SetRightCtrl(true);
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftCtrl(false);
            Assert::AreEqual(0, fixture.queueCalls);

            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_RCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetRightCtrl(false);
            Assert::AreEqual(0, fixture.queueCalls);
            Assert::AreEqual(0, fixture.backend->cancelCalls);
            Assert::IsFalse(fixture.controller->HasPendingWork());
        }

        TEST_METHOD (MainEnterRule_ShouldActivateFromNumpadEnterAndKeepPhysicalPressPaired)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const DWORD numpadEnter = Helpers::EncodeKeyNumpadOrigin(VK_RETURN, true);
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"sig", { VK_RETURN }, L"signature") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(numpadEnter));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(numpadEnter, rules)));
            Assert::AreEqual(static_cast<DWORD>(VK_RETURN), fixture.backend->lastActionKey);
            Assert::AreEqual(0, fixture.queueCalls);

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(numpadEnter));
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_RETURN, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(0, fixture.queueCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(numpadEnter, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(1, fixture.queueCalls);
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(numpadEnter, WM_KEYUP, LLKHF_UP));
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(numpadEnter));
            Assert::AreEqual(1, fixture.backend->activateCalls);
        }

        TEST_METHOD (NumpadAliasChange_ShouldKeepPhysicalPressPairedByScanCode)
        {
            ControllerFixture fixture;
            constexpr DWORD numpadScanCode = 0x52;
            TestKeyEvent down(VK_NUMPAD0, WM_KEYDOWN, 0, 0, numpadScanCode);
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.controller->BeginKeyboardEvent(&down.event));
            Assert::IsTrue(fixture.controller->HasPendingWork());

            const DWORD numpadInsert = VK_INSERT | Helpers::GetNumpadOriginEncodingBit();
            TestKeyEvent up(numpadInsert, WM_KEYUP, LLKHF_UP, 0, numpadScanCode);
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.controller->BeginKeyboardEvent(&up.event));
            Assert::IsFalse(fixture.controller->HasPendingWork());
        }

        TEST_METHOD (Shortcut_ShouldRequireExactModifierSet)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            fixture.SetLeftCtrl(true);
            fixture.SetLeftShift(true);
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_CONTROL, VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(0, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            Assert::AreEqual(0, fixture.backend->activateCalls);
        }

        TEST_METHOD (ModifierConsumedByHigherPriorityRemap_ShouldNotActivateExpansionUntilPhysicalKeyUp)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            fixture.SetLeftCtrl(true);
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_CONTROL, VK_SPACE }, L"expanded") };

            TestKeyEvent ctrlDown(VK_LCONTROL);
            AssertDisposition(
                TextExpansionController::EventDisposition::Continue,
                fixture.controller->BeginKeyboardEvent(&ctrlDown.event));
            fixture.controller->NotifyHigherPriorityEventHandled(&ctrlDown.event);
            Assert::AreEqual(1, fixture.backend->resetBufferCalls);

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(0, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            Assert::AreEqual(0, fixture.backend->activateCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));

            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftCtrl(false);
            Assert::IsFalse(fixture.controller->HasPendingWork());
        }

        TEST_METHOD (AloneModifier_ShouldRemainEligibleForActivationChord)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            fixture.SetLeftCtrl(true);
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_CONTROL, VK_SPACE }, L"expanded") };

            TestKeyEvent ctrlDown(VK_LCONTROL);
            AssertDisposition(
                TextExpansionController::EventDisposition::Continue,
                fixture.controller->BeginKeyboardEvent(&ctrlDown.event));
            fixture.controller->NotifyAloneRemapEventHandled(&ctrlDown.event, false);
            Assert::AreEqual(0, fixture.backend->resetBufferCalls);

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            Assert::AreEqual(1, fixture.backend->activateCalls);
        }

        TEST_METHOD (AloneTap_ShouldInvalidateBufferedTextOnRelease)
        {
            ControllerFixture fixture;
            TestKeyEvent ctrlDown(VK_LCONTROL);
            TestKeyEvent ctrlUp(VK_LCONTROL, WM_KEYUP, LLKHF_UP);

            AssertDisposition(
                TextExpansionController::EventDisposition::Continue,
                fixture.controller->BeginKeyboardEvent(&ctrlDown.event));
            fixture.controller->NotifyAloneRemapEventHandled(&ctrlDown.event, false);
            Assert::AreEqual(0, fixture.backend->resetBufferCalls);

            AssertDisposition(
                TextExpansionController::EventDisposition::Continue,
                fixture.controller->BeginKeyboardEvent(&ctrlUp.event));
            fixture.controller->NotifyAloneRemapEventHandled(&ctrlUp.event, true);
            Assert::AreEqual(1, fixture.backend->resetBufferCalls);
        }

        TEST_METHOD (SameActionKey_ShouldPassOnlyExactActivationCandidates)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            fixture.SetLeftCtrl(true);
            const TextExpansionTable rules{
                MakeRule(L"plain", L"brb", { VK_SPACE }, L"plain replacement"),
                MakeRule(L"ctrl", L"brb", { VK_CONTROL, VK_SPACE }, L"ctrl replacement"),
            };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            const auto* matchedRule = fixture.backend->FindLastRule(L"brb");
            Assert::IsNotNull(matchedRule);
            Assert::AreEqual(std::wstring(L"ctrl replacement"), matchedRule->replacementText);
            Assert::AreEqual(static_cast<size_t>(1), matchedRule->profileIndex);
        }

        TEST_METHOD (ChordRule_ShouldNotActivate)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            Shortcut chord(std::vector<int32_t>{ VK_CONTROL, 'K' });
            chord.SetSecondKey('C');
            TextExpansionRule rule{
                .id = L"rule-id",
                .sourceText = L"brb",
                .activation = chord,
                .replacementText = L"expanded",
            };

            fixture.SetLeftCtrl(true);
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin('K'));
            Assert::AreEqual(0, static_cast<int>(fixture.Activate('K', { rule })));
            Assert::AreEqual(0, fixture.backend->activateCalls);
        }

        TEST_METHOD (Prepared_ShouldSuppressRepeatAndMatchingKeyUpUntilReleaseQueuesCompletion)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            Assert::AreEqual(0, fixture.queueCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE));
            Assert::AreEqual(0, fixture.queueCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(1, fixture.queueCalls);
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(1, fixture.queueCalls);
            Assert::AreEqual(1, fixture.backend->activateCalls);
        }

        TEST_METHOD (NoMatch_ShouldPassEntirePhysicalPress)
        {
            VerifyPassthroughResult(TextExpansionResult::NoMatch);
        }

        TEST_METHOD (FailedUnchanged_ShouldPassEntirePhysicalPress)
        {
            VerifyPassthroughResult(TextExpansionResult::FailedUnchanged);
        }

        TEST_METHOD (FailedChangedOrUnknown_ShouldSuppressEntirePhysicalPress)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::FailedChangedOrUnknown;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
        }

        TEST_METHOD (PassedFreshDown_ShouldNeverActivateOnAutoRepeat)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::NoMatch;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(0, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, fixture.backend->activateCalls);
        }

        TEST_METHOD (ExistingRemapConsumption_ShouldPreventRepeatFromBecomingActivation)
        {
            ControllerFixture fixture;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_SPACE }, L"expanded") };

            // The caller gives existing remaps first chance after BeginKeyboardEvent.
            // Simulate the remap consuming the fresh down by intentionally not calling TryActivate.
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));

            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_SPACE));
            Assert::AreEqual(0, fixture.backend->activateCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));

            // A genuinely new physical press may activate after the remap no longer consumes it.
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
        }

        TEST_METHOD (DuplicateRules_ShouldReachBackendInProfileOrderAndIgnoreGuids)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{
                MakeRule(L"first-guid", L"brb", { VK_SPACE }, L"first"),
                MakeRule(L"second-guid", L"brb", { VK_SPACE }, L"second"),
            };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            const auto* matchedRule = fixture.backend->FindLastRule(L"brb");
            Assert::IsNotNull(matchedRule);
            Assert::AreEqual(std::wstring(L"first"), matchedRule->replacementText);
            Assert::AreEqual(static_cast<size_t>(0), matchedRule->profileIndex);
        }

        TEST_METHOD (DisabledRules_ShouldNotReachBackend)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{
                MakeRule(L"disabled", L"brb", { VK_SPACE }, L"disabled", false),
                MakeRule(L"enabled", L"brb", { VK_SPACE }, L"enabled"),
            };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            const auto* matchedRule = fixture.backend->FindLastRule(L"brb");
            Assert::IsNotNull(matchedRule);
            Assert::AreEqual(std::wstring(L"enabled"), matchedRule->replacementText);
        }

        TEST_METHOD (PendingActivation_NewModifiersShouldRecoverThenContinueNormally)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"sig", { VK_CONTROL, VK_RETURN }, L"signature") };

            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LCONTROL));
            fixture.SetLeftCtrl(true);
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_RETURN));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_RETURN, rules)));
            Assert::AreEqual(0, fixture.queueCalls);

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_RCONTROL));
            Assert::AreEqual(1, fixture.backend->recoveryCalls);
            Assert::AreEqual(static_cast<DWORD>(VK_RETURN), fixture.backend->lastRecovery.actionKey);
            Assert::AreEqual(static_cast<DWORD>(VK_RCONTROL), fixture.backend->lastRecovery.replayKey);
            fixture.SetRightCtrl(true);
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LSHIFT));
            Assert::AreEqual(1, fixture.backend->recoveryCalls);
            fixture.SetLeftShift(true);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_RETURN, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(0, fixture.queueCalls);

            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftCtrl(false);
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_RCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetRightCtrl(false);
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LSHIFT, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftShift(false);
            Assert::AreEqual(0, fixture.queueCalls);
            Assert::AreEqual(0, fixture.backend->completeCalls);
            Assert::IsFalse(fixture.controller->HasPendingWork());
        }

        TEST_METHOD (PendingActivation_OriginalModifierRepressShouldRecoverAndContinueNormally)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"sig", { VK_CONTROL, VK_RETURN }, L"signature") };

            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LCONTROL));
            fixture.SetLeftCtrl(true);
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_RETURN));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_RETURN, rules)));

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftCtrl(false);
            Assert::AreEqual(0, fixture.queueCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LCONTROL));
            Assert::AreEqual(1, fixture.backend->recoveryCalls);
            Assert::AreEqual(static_cast<DWORD>(VK_RETURN), fixture.backend->lastRecovery.actionKey);
            Assert::AreEqual(static_cast<DWORD>(VK_LCONTROL), fixture.backend->lastRecovery.replayKey);
            Assert::AreEqual(
                static_cast<int>(TextExpansionModifiers::LeftCtrl),
                static_cast<int>(fixture.backend->lastRecovery.releasedActivationModifierMask));
            fixture.SetLeftCtrl(true);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_RETURN, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(0, fixture.queueCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftCtrl(false);
            Assert::AreEqual(0, fixture.queueCalls);
            Assert::AreEqual(0, fixture.backend->completeCalls);
            Assert::IsFalse(fixture.controller->HasPendingWork());
        }

        TEST_METHOD (PendingActivation_ShouldSuppressOriginalWinAndAltReleaseUntilActionIsReleased)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{
                MakeRule(
                    L"rule-id",
                    L"sig",
                    { CommonSharedConstants::VK_WIN_BOTH, VK_MENU, VK_RETURN },
                    L"signature"),
            };

            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LWIN));
            fixture.SetLeftWin(true);
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LMENU, WM_SYSKEYDOWN));
            fixture.SetLeftAlt(true);
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_RETURN, WM_SYSKEYDOWN));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_RETURN, rules)));
            Assert::AreEqual(
                static_cast<int>(TextExpansionModifiers::LeftWin | TextExpansionModifiers::LeftAlt),
                static_cast<int>(fixture.backend->lastModifierMask));

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LWIN, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftWin(false);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LMENU, WM_SYSKEYUP, LLKHF_UP));
            fixture.SetLeftAlt(false);
            Assert::AreEqual(0, fixture.queueCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_RETURN, WM_SYSKEYUP, LLKHF_UP));
            Assert::AreEqual(1, fixture.queueCalls);
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
        }

        TEST_METHOD (PendingPressOrBackendTransaction_ShouldBlockReload)
        {
            ControllerFixture fixture;
            Assert::IsFalse(fixture.controller->HasPendingWork());

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin('A'));
            Assert::IsTrue(fixture.controller->HasPendingWork());
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin('A', WM_KEYUP, LLKHF_UP));
            Assert::IsFalse(fixture.controller->HasPendingWork());

            fixture.backend->pendingWork = true;
            Assert::IsTrue(fixture.controller->HasPendingWork());
        }

        TEST_METHOD (PendingBackendWork_ShouldSuppressAndPairNewModifierPress)
        {
            ControllerFixture fixture;
            fixture.backend->pendingWork = true;

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LCONTROL));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LCONTROL));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LCONTROL, WM_KEYUP, LLKHF_UP));
        }

        TEST_METHOD (BackendRecoveryFault_ShouldPassSuppressedPhysicalPressThroughToItsKeyUp)
        {
            ControllerFixture fixture;
            fixture.backend->pendingWork = true;
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LCONTROL));

            fixture.backend->pendingWork = false;
            fixture.backend->ready = false;
            fixture.backend->recoveryKey = VK_LCONTROL;
            fixture.controller->RetryPendingBackendWork();

            AssertDisposition(TextExpansionController::EventDisposition::ForcePassThrough, fixture.Begin(VK_LCONTROL));
            AssertDisposition(TextExpansionController::EventDisposition::ForcePassThrough, fixture.Begin(VK_LCONTROL, WM_KEYUP, LLKHF_UP));
            Assert::IsFalse(fixture.controller->HasPendingWork());
        }

        TEST_METHOD (PendingBackendWork_ShouldSuppressPreexistingKeyRepeatButPassItsKeyUp)
        {
            ControllerFixture fixture;

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin('B'));
            fixture.backend->pendingWork = true;

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin('B'));
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin('B', WM_KEYUP, LLKHF_UP));
        }

        TEST_METHOD (HeldSourceKey_ShouldAllowPreparingActivation)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"ab", { VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin('B'));
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            Assert::AreEqual(1, fixture.backend->activateCalls);

            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin('B', WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(0, fixture.queueCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(1, fixture.queueCalls);
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
        }

        TEST_METHOD (PreparedActivation_ShouldCancelAndPassOtherPhysicalInput)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            Assert::AreEqual(0, fixture.queueCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin('A'));
            Assert::AreEqual(1, fixture.backend->recoveryCalls);
            Assert::AreEqual(0, fixture.backend->cancelCalls);
            Assert::AreEqual(static_cast<DWORD>(VK_SPACE), fixture.backend->lastRecovery.actionKey);
            Assert::AreEqual(static_cast<DWORD>('A'), fixture.backend->lastRecovery.replayKey);

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin('A', WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(0, fixture.queueCalls);
            Assert::AreEqual(0, fixture.backend->completeCalls);
            Assert::IsFalse(fixture.controller->HasPendingWork());
        }

        TEST_METHOD (MissingReplayAcknowledgement_ShouldClearOnPhysicalRelease)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            fixture.backend->replayRecovery = {};
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin('A'));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            Assert::IsTrue(fixture.controller->HasPendingWork());

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin('A', WM_KEYUP, LLKHF_UP));
            Assert::IsFalse(fixture.controller->HasPendingWork());
        }

        TEST_METHOD (MissingReplayAcknowledgement_ShouldUseNextPhysicalDownAsFreshPress)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            fixture.backend->replayRecovery = {};
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin('A'));
            Assert::IsTrue(fixture.controller->HasPendingWork());

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin('A'));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin('A', WM_KEYUP, LLKHF_UP));
            Assert::IsFalse(fixture.controller->HasPendingWork());
        }

        TEST_METHOD (MissingReplayAcknowledgement_ShouldIgnoreDifferentPhysicalIdentity)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            fixture.backend->replayRecovery = {};
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin('A', WM_KEYDOWN, 0, 0x1E));

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin('B', WM_KEYDOWN, 0, 0x30));
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin('B', WM_KEYUP, LLKHF_UP, 0x30));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            Assert::IsTrue(fixture.controller->HasPendingWork());
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin('A', WM_KEYUP, LLKHF_UP, 0x1E));
            Assert::IsFalse(fixture.controller->HasPendingWork());
        }

        TEST_METHOD (MissingReplayAcknowledgement_ShouldKeepKeypadAndExtendedIdentitySeparate)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            fixture.backend->replayRecovery = {};
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_SPACE }, L"expanded") };
            const DWORD numpadInsert = VK_INSERT | Helpers::GetNumpadOriginEncodingBit();

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            AssertDisposition(
                TextExpansionController::EventDisposition::Suppress,
                fixture.Begin(numpadInsert, WM_KEYDOWN, 0, 0x52));

            AssertDisposition(
                TextExpansionController::EventDisposition::Continue,
                fixture.Begin(VK_INSERT, WM_KEYUP, LLKHF_EXTENDED | LLKHF_UP, 0x52));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            Assert::IsTrue(fixture.controller->HasPendingWork());
            AssertDisposition(
                TextExpansionController::EventDisposition::Suppress,
                fixture.Begin(numpadInsert, WM_KEYUP, LLKHF_UP, 0x52));
            Assert::IsFalse(fixture.controller->HasPendingWork());
        }

        TEST_METHOD (QueuedActivation_ShouldBeInvalidatedByNewPhysicalInput)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(1, fixture.queueCalls);

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin('A'));
            Assert::AreEqual(1, fixture.backend->recoveryCalls);
            Assert::AreEqual(0, fixture.backend->cancelCalls);
            Assert::AreEqual(static_cast<DWORD>(VK_SPACE), fixture.backend->lastRecovery.actionKey);
            Assert::AreEqual(static_cast<DWORD>('A'), fixture.backend->lastRecovery.replayKey);
            AssertResult(TextExpansionResult::FailedUnchanged, fixture.Complete());
            Assert::AreEqual(0, fixture.backend->completeCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin('A', WM_KEYUP, LLKHF_UP));
            Assert::IsFalse(fixture.controller->HasPendingWork());
        }

        TEST_METHOD (QueuedActivation_SecondActionPressShouldRecoverAndReplay)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(1, fixture.queueCalls);

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, fixture.backend->recoveryCalls);
            Assert::AreEqual(static_cast<DWORD>(VK_SPACE), fixture.backend->lastRecovery.replayKey);
            AssertResult(TextExpansionResult::FailedUnchanged, fixture.Complete());
            Assert::AreEqual(0, fixture.backend->completeCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            Assert::IsFalse(fixture.controller->HasPendingWork());
        }

        TEST_METHOD (PreparedActivation_ShouldRecoverBeforeNewModifierUsesNormalRemapPipeline)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_CONTROL, VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LCONTROL));
            fixture.SetLeftCtrl(true);
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LSHIFT));
            Assert::AreEqual(1, fixture.backend->recoveryCalls);
            fixture.SetLeftShift(true);

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin('A'));
            Assert::AreEqual(1, fixture.backend->recoveryCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin('A', WM_KEYUP, LLKHF_UP));
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LSHIFT, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftShift(false);
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftCtrl(false);
            Assert::IsFalse(fixture.controller->HasPendingWork());
        }

        TEST_METHOD (ActivationRecoveryFailure_ShouldKeepNewPhysicalPressPairedAndSuppressed)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            fixture.backend->recoveryResult = false;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin('A'));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin('A', WM_KEYUP, LLKHF_UP));
            Assert::IsFalse(fixture.controller->HasPendingWork());
        }

        TEST_METHOD (PreparedNumpadEnterActivation_ShouldPreservePhysicalTriggerIdentityDuringRecovery)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            constexpr DWORD scanCode = 0x1C;
            const DWORD numpadEnter = Helpers::EncodeKeyNumpadOrigin(VK_RETURN, true);
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"sig", { VK_RETURN }, L"signature") };

            AssertDisposition(
                TextExpansionController::EventDisposition::FreshActionKeyDown,
                fixture.Begin(numpadEnter, WM_KEYDOWN, LLKHF_EXTENDED, scanCode));
            Assert::AreEqual(
                1,
                static_cast<int>(fixture.Activate(numpadEnter, rules, LLKHF_EXTENDED, scanCode)));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin('A'));
            Assert::AreEqual(static_cast<DWORD>(VK_RETURN), fixture.backend->lastRecovery.actionKey);
            Assert::AreEqual(scanCode, fixture.backend->lastRecovery.actionScanCode);
            Assert::IsTrue(fixture.backend->lastRecovery.actionExtended);
            Assert::AreEqual(static_cast<DWORD>('A'), fixture.backend->lastRecovery.replayKey);
            AssertDisposition(
                TextExpansionController::EventDisposition::Suppress,
                fixture.Begin(numpadEnter, WM_KEYUP, LLKHF_EXTENDED | LLKHF_UP, scanCode));
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin('A', WM_KEYUP, LLKHF_UP));
        }

        TEST_METHOD (PreparedModifierActivation_ShouldRecoverWithoutReleasingHeldModifier)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_CONTROL, VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LCONTROL));
            fixture.SetLeftCtrl(true);
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin('A'));
            Assert::AreEqual(1, fixture.backend->recoveryCalls);
            Assert::AreEqual(0, fixture.backend->cancelCalls);
            Assert::AreEqual(
                static_cast<int>(0),
                static_cast<int>(fixture.backend->lastRecovery.releasedActivationModifierMask));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin('A', WM_KEYUP, LLKHF_UP));
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftCtrl(false);
            Assert::AreEqual(0, fixture.queueCalls);
            Assert::IsFalse(fixture.controller->HasPendingWork());
        }

        TEST_METHOD (PreparedModifierActivation_ShouldRecoverReleasedModifierAndPassNewInput)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_CONTROL, VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LCONTROL));
            fixture.SetLeftCtrl(true);
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftCtrl(false);

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin('A'));
            Assert::AreEqual(1, fixture.backend->recoveryCalls);
            Assert::AreEqual(0, fixture.backend->cancelCalls);
            Assert::AreEqual(
                static_cast<int>(TextExpansionModifiers::LeftCtrl),
                static_cast<int>(fixture.backend->lastRecovery.releasedActivationModifierMask));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin('A', WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(0, fixture.queueCalls);
            Assert::IsFalse(fixture.controller->HasPendingWork());
        }

        TEST_METHOD (PreparedMultiModifierActivation_ShouldRecoverPartialModifierReleaseAndPassNewInput)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_CONTROL, VK_SHIFT, VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LCONTROL));
            fixture.SetLeftCtrl(true);
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LSHIFT));
            fixture.SetLeftShift(true);
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftCtrl(false);

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin('A'));
            Assert::AreEqual(1, fixture.backend->recoveryCalls);
            Assert::AreEqual(0, fixture.backend->cancelCalls);
            Assert::AreEqual(
                static_cast<int>(TextExpansionModifiers::LeftCtrl),
                static_cast<int>(fixture.backend->lastRecovery.releasedActivationModifierMask));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin('A', WM_KEYUP, LLKHF_UP));
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LSHIFT, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftShift(false);
            Assert::IsFalse(fixture.controller->HasPendingWork());
        }

        TEST_METHOD (QueueFailureAfterRelease_ShouldCancelButKeepEntireTriggerPressSuppressedWhenRollbackIsExact)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            fixture.backend->cancelResult = TextExpansionResult::FailedUnchanged;
            fixture.queueResult = false;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            Assert::AreEqual(0, fixture.queueCalls);
            Assert::AreEqual(0, fixture.backend->cancelCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(1, fixture.queueCalls);
            Assert::AreEqual(1, fixture.backend->cancelCalls);
            Assert::AreEqual(static_cast<size_t>(0), fixture.queuedGenerations.size());
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
        }

        TEST_METHOD (QueueFailureAfterRelease_ShouldCancelAndKeepEntireTriggerPressSuppressedWhenStateIsUnknown)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            fixture.backend->cancelResult = TextExpansionResult::FailedChangedOrUnknown;
            fixture.queueResult = false;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            Assert::AreEqual(0, fixture.queueCalls);
            Assert::AreEqual(0, fixture.backend->cancelCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(1, fixture.queueCalls);
            Assert::AreEqual(1, fixture.backend->cancelCalls);
            Assert::AreEqual(static_cast<size_t>(0), fixture.queuedGenerations.size());
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
        }

        TEST_METHOD (QueuedActivation_SecondActionPressShouldRecoverBeforeModifierRepress)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"sig", { VK_CONTROL, VK_RETURN }, L"signature") };

            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LCONTROL));
            fixture.SetLeftCtrl(true);
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_RETURN));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_RETURN, rules)));

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_RETURN, WM_KEYUP, LLKHF_UP));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftCtrl(false);
            Assert::AreEqual(1, fixture.queueCalls);

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_RETURN));
            Assert::AreEqual(1, fixture.backend->recoveryCalls);
            Assert::AreEqual(static_cast<DWORD>(VK_RETURN), fixture.backend->lastRecovery.actionKey);
            Assert::AreEqual(static_cast<DWORD>(VK_RETURN), fixture.backend->lastRecovery.replayKey);
            Assert::AreEqual(
                static_cast<int>(TextExpansionModifiers::LeftCtrl),
                static_cast<int>(fixture.backend->lastRecovery.releasedActivationModifierMask));
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LCONTROL));
            fixture.SetLeftCtrl(true);

            AssertResult(TextExpansionResult::FailedUnchanged, fixture.Complete());
            Assert::AreEqual(0, fixture.backend->completeCalls);
            Assert::AreEqual(static_cast<size_t>(0), fixture.queuedGenerations.size());

            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_RETURN, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(1, fixture.queueCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Continue, fixture.Begin(VK_LCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftCtrl(false);
            Assert::AreEqual(1, fixture.queueCalls);
            Assert::AreEqual(static_cast<size_t>(0), fixture.queuedGenerations.size());
            Assert::AreEqual(0, fixture.backend->completeCalls);
            Assert::AreEqual(0, fixture.backend->cancelCalls);
            Assert::IsFalse(fixture.controller->HasPendingWork());
        }

        TEST_METHOD (StaleGeneration_ShouldNotCommitNewPendingActivation)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            Assert::AreEqual(0, fixture.queueCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(1, fixture.queueCalls);
            const uint64_t generation = fixture.queuedGenerations.front();

            AssertResult(TextExpansionResult::FailedUnchanged, fixture.controller->CompletePendingActivation(generation + 1));
            Assert::AreEqual(0, fixture.backend->completeCalls);
            AssertResult(TextExpansionResult::Replaced, fixture.controller->CompletePendingActivation(generation));
            Assert::AreEqual(1, fixture.backend->completeCalls);
        }

        TEST_METHOD (InactiveBackend_ShouldIgnoreKeyboardEventsWithoutTrackingPresses)
        {
            auto backend = std::make_unique<FakeTextExpansionBackend>();
            auto* backendView = backend.get();
            TextExpansionController controller(std::move(backend));

            TestKeyEvent actionDown(VK_SPACE);
            TestKeyEvent actionUp(VK_SPACE, WM_KEYUP, LLKHF_UP);
            TestKeyEvent modifierDown(VK_LCONTROL);
            AssertDisposition(TextExpansionController::EventDisposition::Ignore, controller.BeginKeyboardEvent(&actionDown.event));
            AssertDisposition(TextExpansionController::EventDisposition::Ignore, controller.BeginKeyboardEvent(&actionUp.event));
            AssertDisposition(TextExpansionController::EventDisposition::Ignore, controller.BeginKeyboardEvent(&modifierDown.event));
            controller.TrackKeyboardEvent(&actionDown.event);
            controller.ResetBuffer();

            Assert::IsFalse(controller.HasPendingWork());
            Assert::AreEqual(0, backendView->trackCalls);
            Assert::AreEqual(0, backendView->resetBufferCalls);
        }

        TEST_METHOD (BackendStartFailure_ShouldRemainInactive)
        {
            auto backend = std::make_unique<FakeTextExpansionBackend>();
            auto* backendView = backend.get();
            backendView->startResult = false;
            TextExpansionController controller(std::move(backend));
            KeyboardManagerInput::MockedInput input;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_SPACE }, L"expanded") };

            Assert::IsFalse(controller.Start(input));
            Assert::IsTrue(controller.SetTextExpansions(rules));
            TestKeyEvent down(VK_SPACE);
            AssertDisposition(TextExpansionController::EventDisposition::Ignore, controller.BeginKeyboardEvent(&down.event));
            Assert::AreEqual(0, static_cast<int>(controller.TryActivate(input, &down.event)));
            Assert::AreEqual(0, backendView->activateCalls);
            Assert::IsFalse(controller.HasPendingWork());
        }

        TEST_METHOD (Start_ShouldTreatAlreadyHeldActionKeyAsPreexistingUntilItsKeyUp)
        {
            auto backend = std::make_unique<FakeTextExpansionBackend>();
            auto* backendView = backend.get();
            backendView->prepareResult = TextExpansionResult::Prepared;
            TextExpansionController controller(std::move(backend));
            KeyboardManagerInput::MockedInput input;
            input.SetKeyboardState(VK_F8, true);
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_F8 }, L"expanded") };

            Assert::IsTrue(controller.SetTextExpansions(rules));
            Assert::IsTrue(controller.Start(input));
            TestKeyEvent repeat(VK_F8);
            AssertDisposition(TextExpansionController::EventDisposition::ForcePassThrough, controller.BeginKeyboardEvent(&repeat.event));
            controller.TrackKeyboardEvent(&repeat.event);
            Assert::AreEqual(1, backendView->trackCalls);
            Assert::AreEqual(0, backendView->activateCalls);

            input.SetKeyboardState(VK_F8, false);
            TestKeyEvent up(VK_F8, WM_KEYUP, LLKHF_UP);
            AssertDisposition(TextExpansionController::EventDisposition::ForcePassThrough, controller.BeginKeyboardEvent(&up.event));
            Assert::IsFalse(controller.HasPendingWork());

            TestKeyEvent freshDown(VK_F8);
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, controller.BeginKeyboardEvent(&freshDown.event));
            Assert::AreEqual(1, static_cast<int>(controller.TryActivate(input, &freshDown.event)));
            Assert::AreEqual(1, backendView->activateCalls);
        }

        TEST_METHOD (Arming_ShouldSurviveNumpadVirtualKeyAliasChanges)
        {
            auto backend = std::make_unique<FakeTextExpansionBackend>();
            TextExpansionController controller(std::move(backend));
            KeyboardManagerInput::MockedInput input;
            input.SetKeyboardState(VK_NUMPAD0, true);
            Assert::IsTrue(controller.Start(input));

            constexpr DWORD numpadScanCode = 0x52;
            const DWORD numpadInsert = VK_INSERT | Helpers::GetNumpadOriginEncodingBit();
            TestKeyEvent repeat(numpadInsert, WM_KEYDOWN, 0, 0, numpadScanCode);
            AssertDisposition(TextExpansionController::EventDisposition::ForcePassThrough, controller.BeginKeyboardEvent(&repeat.event));
            TestKeyEvent up(numpadInsert, WM_KEYUP, LLKHF_UP, 0, numpadScanCode);
            AssertDisposition(TextExpansionController::EventDisposition::ForcePassThrough, controller.BeginKeyboardEvent(&up.event));

            input.SetKeyboardState(VK_NUMPAD0, false);
            Assert::IsFalse(controller.HasPendingWork());
            TestKeyEvent freshDown('B');
            TestKeyEvent freshUp('B', WM_KEYUP, LLKHF_UP);
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, controller.BeginKeyboardEvent(&freshDown.event));
            AssertDisposition(TextExpansionController::EventDisposition::Continue, controller.BeginKeyboardEvent(&freshUp.event));
            Assert::IsFalse(controller.HasPendingWork());
        }

        TEST_METHOD (Start_ShouldArmForHeldCancelKey)
        {
            auto backend = std::make_unique<FakeTextExpansionBackend>();
            TextExpansionController controller(std::move(backend));
            KeyboardManagerInput::MockedInput input;
            input.SetKeyboardState(VK_CANCEL, true);
            Assert::IsTrue(controller.Start(input));

            input.SetKeyboardState(VK_CANCEL, false);
            TestKeyEvent up(VK_CANCEL, WM_KEYUP, LLKHF_UP);
            AssertDisposition(TextExpansionController::EventDisposition::ForcePassThrough, controller.BeginKeyboardEvent(&up.event));
        }

        TEST_METHOD (Arming_ShouldWaitForEveryHeldActionKeyRelease)
        {
            auto backend = std::make_unique<FakeTextExpansionBackend>();
            TextExpansionController controller(std::move(backend));
            KeyboardManagerInput::MockedInput input;
            input.SetKeyboardState('A', true);
            input.SetKeyboardState('B', true);
            Assert::IsTrue(controller.Start(input));

            TestKeyEvent aUp('A', WM_KEYUP, LLKHF_UP);
            AssertDisposition(TextExpansionController::EventDisposition::ForcePassThrough, controller.BeginKeyboardEvent(&aUp.event));
            input.SetKeyboardState('A', false);
            Assert::IsTrue(controller.HasPendingWork());

            TestKeyEvent bUp('B', WM_KEYUP, LLKHF_UP);
            AssertDisposition(TextExpansionController::EventDisposition::ForcePassThrough, controller.BeginKeyboardEvent(&bUp.event));
            input.SetKeyboardState('B', false);
            Assert::IsFalse(controller.HasPendingWork());
        }

        TEST_METHOD (InjectedEvents_ShouldBeIgnored)
        {
            ControllerFixture fixture;
            TestKeyEvent injected(VK_SPACE, WM_KEYDOWN, 0, CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG);
            AssertDisposition(TextExpansionController::EventDisposition::Ignore, fixture.controller->BeginKeyboardEvent(&injected.event));
            Assert::IsFalse(fixture.controller->HasPendingWork());
        }
    };
}
