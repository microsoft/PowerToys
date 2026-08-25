#include "pch.h"

// Suppressing 26466 - Don't use static_cast downcasts - in CppUnitTest.h
#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "MockedInput.h"

#include <algorithm>
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
                const ULONG_PTR extraInfo = 0)
            {
                keyboardData.vkCode = key;
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
            bool pendingWork = false;
            bool prepared = false;
            TextExpansionResult prepareResult = TextExpansionResult::NoMatch;
            TextExpansionResult completeResult = TextExpansionResult::Replaced;
            TextExpansionResult cancelResult = TextExpansionResult::FailedUnchanged;

            int startCalls = 0;
            int stopCalls = 0;
            int activateCalls = 0;
            int completeCalls = 0;
            int cancelCalls = 0;
            int cleanupCalls = 0;
            int trackCalls = 0;
            int resetBufferCalls = 0;
            DWORD lastTrackedKey = 0;
            WPARAM lastTrackedMessage = 0;
            Shortcut lastActivation;
            std::vector<DWORD> lastActivationModifierKeys;
            std::vector<TextExpansionCandidate> lastCandidates;

            bool Start() override
            {
                ++startCalls;
                return startResult;
            }

            void Stop() noexcept override
            {
                ++stopCalls;
                prepared = false;
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
                lastActivation = request.activationShortcut;
                lastActivationModifierKeys = request.activationModifierKeys;
                lastCandidates = request.candidates;
                prepared = prepareResult == TextExpansionResult::Prepared;
                return prepareResult;
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
                Assert::IsTrue(controller->Start());
            }

            TextExpansionController::EventDisposition Begin(
                const DWORD key,
                const WPARAM message = WM_KEYDOWN,
                const DWORD flags = 0)
            {
                TestKeyEvent keyEvent(key, message, flags);
                return controller->BeginKeyboardEvent(&keyEvent.event);
            }

            intptr_t Activate(const DWORD key, const TextExpansionTable& rules)
            {
                return controller->TryActivate(input, key, rules);
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

        void AssertContainsKey(const std::vector<DWORD>& keys, const DWORD expected)
        {
            Assert::IsTrue(std::find(keys.begin(), keys.end(), expected) != keys.end());
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

            Assert::IsTrue(controller.Start());
            Assert::IsTrue(controller.Start());
            Assert::AreEqual(1, backendView->startCalls);

            controller.Stop();
            Assert::AreEqual(1, backendView->stopCalls);
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
            Assert::AreEqual(static_cast<DWORD>(VK_SPACE), fixture.backend->lastActivation.GetActionKey());
            Assert::AreEqual(static_cast<size_t>(1), fixture.backend->lastCandidates.size());
            Assert::AreEqual(std::wstring(L"brb"), fixture.backend->lastCandidates[0].sourceText);
            Assert::AreEqual(0, fixture.queueCalls);
            Assert::AreEqual(0, fixture.backend->completeCalls);

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(1, fixture.queueCalls);
            Assert::AreEqual(static_cast<size_t>(1), fixture.queuedGenerations.size());
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
        }

        TEST_METHOD (ArbitraryCapturedSingleKey_ShouldNotBeRestrictedByExpansionAllowlist)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_F8 }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_F8));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_F8, rules)));
            Assert::AreEqual(1, fixture.backend->activateCalls);
            Assert::AreEqual(static_cast<DWORD>(VK_F8), fixture.backend->lastActivation.GetActionKey());
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
                static_cast<int>(ModifierKey::Both),
                static_cast<int>(fixture.backend->lastActivation.ctrlKey));
            Assert::AreEqual(static_cast<size_t>(1), fixture.backend->lastActivationModifierKeys.size());
            AssertContainsKey(fixture.backend->lastActivationModifierKeys, VK_LCONTROL);
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
                static_cast<int>(ModifierKey::Both),
                static_cast<int>(fixture.backend->lastActivation.ctrlKey));
            Assert::AreEqual(
                static_cast<int>(ModifierKey::Both),
                static_cast<int>(fixture.backend->lastActivation.shiftKey));
            Assert::AreEqual(static_cast<size_t>(2), fixture.backend->lastActivationModifierKeys.size());
            AssertContainsKey(fixture.backend->lastActivationModifierKeys, VK_LCONTROL);
            AssertContainsKey(fixture.backend->lastActivationModifierKeys, VK_LSHIFT);
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

        TEST_METHOD (CtrlEnter_NewOppositeSideModifierPress_ShouldDelayCommitUntilItIsReleased)
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
            fixture.SetRightCtrl(true);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftCtrl(false);
            Assert::AreEqual(0, fixture.queueCalls);

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_RCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetRightCtrl(false);
            Assert::AreEqual(1, fixture.queueCalls);
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
        }

        TEST_METHOD (MainEnterRule_ShouldActivateFromNumpadEnterAndKeepPhysicalPressPaired)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const DWORD numpadEnter = Helpers::EncodeKeyNumpadOrigin(VK_RETURN, true);
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"sig", { VK_RETURN }, L"signature") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(numpadEnter));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(numpadEnter, rules)));
            Assert::AreEqual(static_cast<DWORD>(VK_RETURN), fixture.backend->lastActivation.GetActionKey());
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
            Assert::AreEqual(static_cast<size_t>(1), fixture.backend->lastCandidates.size());
            Assert::AreEqual(std::wstring(L"ctrl replacement"), fixture.backend->lastCandidates[0].replacementText);
            Assert::AreEqual(static_cast<size_t>(1), fixture.backend->lastCandidates[0].profileIndex);
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
            Assert::AreEqual(static_cast<size_t>(2), fixture.backend->lastCandidates.size());
            Assert::AreEqual(std::wstring(L"first"), fixture.backend->lastCandidates[0].replacementText);
            Assert::AreEqual(static_cast<size_t>(0), fixture.backend->lastCandidates[0].profileIndex);
            Assert::AreEqual(std::wstring(L"second"), fixture.backend->lastCandidates[1].replacementText);
            Assert::AreEqual(static_cast<size_t>(1), fixture.backend->lastCandidates[1].profileIndex);
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
            Assert::AreEqual(static_cast<size_t>(1), fixture.backend->lastCandidates.size());
            Assert::AreEqual(std::wstring(L"enabled"), fixture.backend->lastCandidates[0].replacementText);
        }

        TEST_METHOD (PendingActivation_ShouldSuppressOppositeSideAndRecoveryModifiersUntilAllAreReleased)
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
            fixture.SetRightCtrl(true);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LSHIFT));
            fixture.SetLeftShift(true);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_RETURN, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(0, fixture.queueCalls);

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftCtrl(false);
            Assert::AreEqual(0, fixture.queueCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_RCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetRightCtrl(false);
            Assert::AreEqual(0, fixture.queueCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LSHIFT, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftShift(false);
            Assert::AreEqual(1, fixture.queueCalls);
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
        }

        TEST_METHOD (PendingActivation_ShouldSuppressOriginalModifierRepressAndWaitForItsRelease)
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
            fixture.SetLeftCtrl(true);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_RETURN, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(0, fixture.queueCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftCtrl(false);
            Assert::AreEqual(1, fixture.queueCalls);
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
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
            Assert::AreEqual(static_cast<size_t>(2), fixture.backend->lastActivationModifierKeys.size());
            AssertContainsKey(fixture.backend->lastActivationModifierKeys, VK_LWIN);
            AssertContainsKey(fixture.backend->lastActivationModifierKeys, VK_LMENU);

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

        TEST_METHOD (PreparedActivation_ShouldBlockAndPairOtherPhysicalInputUntilCompletion)
        {
            ControllerFixture fixture;
            fixture.backend->prepareResult = TextExpansionResult::Prepared;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_SPACE }, L"expanded") };

            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin(VK_SPACE));
            Assert::AreEqual(1, static_cast<int>(fixture.Activate(VK_SPACE, rules)));
            Assert::AreEqual(0, fixture.queueCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin('A'));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin('A'));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin('A', WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(0, fixture.queueCalls);

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_SPACE, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(1, fixture.queueCalls);

            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, fixture.Begin('A'));
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

        TEST_METHOD (QueuedActivation_ShouldDeferCompletionAndRequeueSameGenerationWhenActivationKeysAreRepressed)
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
            const uint64_t generation = fixture.queuedGenerations.front();

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_RETURN));
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LCONTROL));
            fixture.SetLeftCtrl(true);

            AssertResult(TextExpansionResult::FailedUnchanged, fixture.Complete());
            Assert::AreEqual(0, fixture.backend->completeCalls);
            Assert::AreEqual(static_cast<size_t>(0), fixture.queuedGenerations.size());

            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_RETURN, WM_KEYUP, LLKHF_UP));
            Assert::AreEqual(1, fixture.queueCalls);
            AssertDisposition(TextExpansionController::EventDisposition::Suppress, fixture.Begin(VK_LCONTROL, WM_KEYUP, LLKHF_UP));
            fixture.SetLeftCtrl(false);
            Assert::AreEqual(2, fixture.queueCalls);
            Assert::AreEqual(static_cast<size_t>(1), fixture.queuedGenerations.size());
            Assert::AreEqual(generation, fixture.queuedGenerations.front());

            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
            Assert::AreEqual(1, fixture.backend->completeCalls);
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

        TEST_METHOD (BackendStartFailure_ShouldLeaveActivationAsPassthrough)
        {
            auto backend = std::make_unique<FakeTextExpansionBackend>();
            auto* backendView = backend.get();
            backendView->startResult = false;
            TextExpansionController controller(std::move(backend));
            KeyboardManagerInput::MockedInput input;
            const TextExpansionTable rules{ MakeRule(L"rule-id", L"brb", { VK_SPACE }, L"expanded") };

            Assert::IsFalse(controller.Start());
            TestKeyEvent down(VK_SPACE);
            AssertDisposition(TextExpansionController::EventDisposition::FreshActionKeyDown, controller.BeginKeyboardEvent(&down.event));
            Assert::AreEqual(0, static_cast<int>(controller.TryActivate(input, VK_SPACE, rules)));
            Assert::AreEqual(0, backendView->activateCalls);
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
