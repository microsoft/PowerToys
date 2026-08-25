#include "pch.h"

// Suppressing 26466 - Don't use static_cast downcasts - in CppUnitTest.h
#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "MockedInput.h"

#include <algorithm>
#include <cstdint>
#include <common/interop/shared_constants.h>
#include <functional>
#include <keyboardmanager/KeyboardManagerEngineLibrary/BufferTextExpansionBackend.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <utility>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace TextExpansionEngineTests
{
    namespace
    {
        using InputContext = BufferTextExpansionBackend::InputContext;

        struct TestKeyEvent
        {
            KBDLLHOOKSTRUCT keyboardData{};
            LowlevelKeyboardEvent event{};

            explicit TestKeyEvent(
                const DWORD key,
                const DWORD scanCode = 0,
                const WPARAM message = WM_KEYDOWN,
                const DWORD flags = 0,
                const ULONG_PTR extraInfo = 0)
            {
                keyboardData.vkCode = key;
                keyboardData.scanCode = scanCode;
                keyboardData.flags = flags;
                keyboardData.dwExtraInfo = extraInfo;
                event.wParam = message;
                event.lParam = &keyboardData;
            }
        };

        InputContext MakeContext(const uintptr_t identity)
        {
            return {
                .foregroundWindow = reinterpret_cast<HWND>((identity * 2) + 1),
                .focusedWindow = reinterpret_cast<HWND>((identity * 2) + 2),
                .processId = static_cast<DWORD>(identity + 100),
            };
        }

        void AssertResult(const TextExpansionResult expected, const TextExpansionResult actual)
        {
            Assert::AreEqual(static_cast<int>(expected), static_cast<int>(actual));
        }

        bool IsKeyDown(const INPUT& input, const WORD key)
        {
            return input.type == INPUT_KEYBOARD && input.ki.wVk == key &&
                   (input.ki.dwFlags & KEYEVENTF_KEYUP) == 0;
        }

        size_t CountKeyDowns(
            const std::vector<std::vector<INPUT>>& batches,
            const WORD key)
        {
            size_t count = 0;
            for (const auto& batch : batches)
            {
                count += static_cast<size_t>(std::count_if(
                    batch.begin(),
                    batch.end(),
                    [key](const INPUT& input) { return IsKeyDown(input, key); }));
            }
            return count;
        }

        void AssertBackspacePair(const std::vector<INPUT>& batch)
        {
            Assert::AreEqual(static_cast<size_t>(2), batch.size());
            Assert::AreEqual(static_cast<WORD>(VK_BACK), batch[0].ki.wVk);
            Assert::IsTrue((batch[0].ki.dwFlags & KEYEVENTF_KEYUP) == 0);
            Assert::AreEqual(static_cast<WORD>(VK_BACK), batch[1].ki.wVk);
            Assert::IsTrue((batch[1].ki.dwFlags & KEYEVENTF_KEYUP) != 0);
        }

        struct BackendFixture
        {
            KeyboardManagerInput::MockedInput input;
            InputContext currentContext = MakeContext(1);
            std::function<InputContext()> contextBehavior;
            BufferTextExpansionBackend::TextProvider textBehavior;
            size_t contextCalls = 0;
            size_t textProviderCalls = 0;
            std::unique_ptr<BufferTextExpansionBackend> backend;

            BackendFixture()
            {
                contextBehavior = [this] { return currentContext; };
                textBehavior = [](KeyboardManagerInput::InputInterface&, const LowlevelKeyboardEvent*, bool) {
                    return BufferTextExpansionBackend::TextEvent{};
                };
                backend = std::make_unique<BufferTextExpansionBackend>(
                    input,
                    [this](
                        KeyboardManagerInput::InputInterface& input,
                        const LowlevelKeyboardEvent* event,
                        const bool capsLockOn) {
                        ++textProviderCalls;
                        return textBehavior(input, event, capsLockOn);
                    },
                    [this] {
                        ++contextCalls;
                        return contextBehavior();
                    });
                Assert::IsTrue(backend->Start());
            }

            void TrackKey(
                const DWORD key,
                const DWORD scanCode = 0,
                const WPARAM message = WM_KEYDOWN,
                const DWORD flags = 0,
                const ULONG_PTR extraInfo = 0)
            {
                TestKeyEvent event(key, scanCode, message, flags, extraInfo);
                backend->TrackKeyboardEvent(&event.event);
            }

            void TrackText(const std::wstring_view text)
            {
                for (const wchar_t unit : text)
                {
                    TrackKey(VK_PACKET, static_cast<DWORD>(unit));
                }
            }

            TextExpansionRequest Request(
                const std::initializer_list<int32_t> activationKeys = { VK_SPACE },
                std::vector<TextExpansionCandidate> candidates = {
                    { L"brb", L"be right back", 0 },
                },
                std::vector<DWORD> activationModifierKeys = {})
            {
                return {
                    .activationShortcut = Shortcut(std::vector<int32_t>(activationKeys)),
                    .activationModifierKeys = std::move(activationModifierKeys),
                    .candidates = std::move(candidates),
                };
            }

            TextExpansionResult Prepare(const TextExpansionRequest& request)
            {
                return backend->PrepareActivation(request);
            }

            TextExpansionResult Complete()
            {
                return backend->CompletePendingActivation();
            }

            void SetLeftCtrl(const bool down)
            {
                input.SetKeyboardState(VK_LCONTROL, down);
                input.SetKeyboardState(VK_CONTROL, down);
            }

            void SetAltGr(const bool down)
            {
                input.SetKeyboardState(VK_LCONTROL, down);
                input.SetKeyboardState(VK_CONTROL, down);
                input.SetKeyboardState(VK_RMENU, down);
                input.SetKeyboardState(VK_MENU, down);
            }
        };
    }

    TEST_CLASS (BufferTextExpansionBackendTests)
    {
    public:
        TEST_METHOD (PrepareAndComplete_ShouldChooseLongestTypedSuffixAndBackspaceFullSource)
        {
            BackendFixture fixture;
            fixture.TrackText(L"please brb");
            const auto request = fixture.Request(
                { VK_SPACE },
                {
                    { L"rb", L"short", 0 },
                    { L"brb", L"longest", 1 },
                    { L"unrelated", L"unused", 2 },
                });

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());

            const auto& batches = fixture.input.GetSentInputBatches();
            Assert::IsTrue(batches.size() > 3);
            AssertBackspacePair(batches[0]);
            AssertBackspacePair(batches[1]);
            AssertBackspacePair(batches[2]);
            Assert::AreEqual(static_cast<size_t>(3), CountKeyDowns(batches, VK_BACK));
            Assert::IsTrue((batches[3][0].ki.dwFlags & KEYEVENTF_UNICODE) != 0);
            Assert::AreEqual(static_cast<WORD>(L'l'), batches[3][0].ki.wScan);
            Assert::AreEqual(std::wstring(L"longest"), fixture.input.GetInjectedUnicodeText());
        }

        TEST_METHOD (TrackKeyboardEvent_ShouldAppendPhysicalKeyTextEventAndActivate)
        {
            BackendFixture fixture;
            fixture.textBehavior = [](KeyboardManagerInput::InputInterface&, const LowlevelKeyboardEvent*, bool) {
                return BufferTextExpansionBackend::TextEvent{
                    .kind = BufferTextExpansionBackend::TextEventKind::Text,
                    .text = L"brb",
                };
            };

            fixture.TrackKey('B');
            Assert::AreEqual(static_cast<size_t>(1), fixture.textProviderCalls);
            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(fixture.Request()));
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
            Assert::AreEqual(
                static_cast<size_t>(3),
                CountKeyDowns(fixture.input.GetSentInputBatches(), VK_BACK));
        }

        TEST_METHOD (TrackKeyboardEvent_ShouldClearBufferForDeadKey)
        {
            BackendFixture fixture;
            fixture.TrackText(L"brb");
            fixture.textBehavior = [](KeyboardManagerInput::InputInterface&, const LowlevelKeyboardEvent*, bool) {
                return BufferTextExpansionBackend::TextEvent{
                    .kind = BufferTextExpansionBackend::TextEventKind::DeadKey,
                };
            };

            fixture.TrackKey(VK_OEM_7);
            Assert::AreEqual(static_cast<size_t>(1), fixture.textProviderCalls);
            AssertResult(TextExpansionResult::NoMatch, fixture.Prepare(fixture.Request()));
        }

        TEST_METHOD (PrepareAndComplete_ShouldUseFirstProfileRuleWhenDuplicateCandidatesTie)
        {
            BackendFixture fixture;
            fixture.TrackText(L"brb");
            const auto request = fixture.Request(
                { VK_SPACE },
                {
                    { L"brb", L"first", 3 },
                    { L"brb", L"second", 4 },
                });

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
            Assert::AreEqual(std::wstring(L"first"), fixture.input.GetInjectedUnicodeText());
        }

        TEST_METHOD (TrackKeyboardEvent_ShouldApplyPhysicalBackspaceBeforeActivation)
        {
            BackendFixture fixture;
            fixture.TrackText(L"brx");
            fixture.TrackKey(VK_BACK);
            fixture.TrackText(L"b");

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(fixture.Request()));
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
            Assert::AreEqual(
                static_cast<size_t>(3),
                CountKeyDowns(fixture.input.GetSentInputBatches(), VK_BACK));
        }

        TEST_METHOD (TrackKeyboardEvent_ShouldClearBufferForCtrlNonActivationInput)
        {
            BackendFixture fixture;
            fixture.TrackText(L"brb");
            fixture.SetLeftCtrl(true);
            fixture.TrackKey('V');
            fixture.SetLeftCtrl(false);

            AssertResult(TextExpansionResult::NoMatch, fixture.Prepare(fixture.Request()));
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
            Assert::AreEqual(static_cast<size_t>(0), fixture.textProviderCalls);
        }

        TEST_METHOD (TrackKeyboardEvent_ShouldAppendPrintableAltGrTextWithoutClearingExistingBuffer)
        {
            BackendFixture fixture;
            fixture.TrackText(L"br");
            fixture.textBehavior = [](KeyboardManagerInput::InputInterface&, const LowlevelKeyboardEvent*, bool) {
                return BufferTextExpansionBackend::TextEvent{
                    .kind = BufferTextExpansionBackend::TextEventKind::Text,
                    .text = L"b",
                };
            };
            fixture.SetAltGr(true);
            fixture.TrackKey('B', 0, WM_SYSKEYDOWN);
            fixture.SetAltGr(false);

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(fixture.Request()));
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
        }

        TEST_METHOD (TrackKeyboardEvent_ShouldExposePhysicalCapsLockToggleToTextProvider)
        {
            BackendFixture fixture;
            std::vector<bool> observedCapsLockStates;
            fixture.textBehavior = [&observedCapsLockStates](
                                       KeyboardManagerInput::InputInterface&,
                                       const LowlevelKeyboardEvent*,
                                       const bool capsLockOn) {
                observedCapsLockStates.push_back(capsLockOn);
                return BufferTextExpansionBackend::TextEvent{};
            };

            fixture.TrackKey('A');
            fixture.TrackKey(VK_CAPITAL);
            fixture.TrackKey(VK_CAPITAL, 0, WM_KEYUP, LLKHF_UP);
            fixture.TrackKey('B');

            Assert::AreEqual(static_cast<size_t>(2), observedCapsLockStates.size());
            Assert::IsTrue(observedCapsLockStates[0] != observedCapsLockStates[1]);
        }

        TEST_METHOD (TrackKeyboardEvent_ShouldExposeInjectedCapsLockToggleToTextProvider)
        {
            BackendFixture fixture;
            std::vector<bool> observedCapsLockStates;
            fixture.textBehavior = [&observedCapsLockStates](
                                       KeyboardManagerInput::InputInterface&,
                                       const LowlevelKeyboardEvent*,
                                       const bool capsLockOn) {
                observedCapsLockStates.push_back(capsLockOn);
                return BufferTextExpansionBackend::TextEvent{};
            };

            fixture.TrackKey('A');
            fixture.TrackKey(
                VK_CAPITAL,
                0,
                WM_KEYDOWN,
                0,
                KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG);
            fixture.TrackKey(
                VK_CAPITAL,
                0,
                WM_KEYUP,
                LLKHF_UP,
                KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG);
            fixture.TrackKey('B');

            Assert::AreEqual(static_cast<size_t>(2), observedCapsLockStates.size());
            Assert::IsTrue(observedCapsLockStates[0] != observedCapsLockStates[1]);
        }

        TEST_METHOD (TrackKeyboardEvent_ShouldIgnoreOwnInjectedInput)
        {
            BackendFixture fixture;
            fixture.TrackText(L"br");
            fixture.TrackKey(
                VK_PACKET,
                static_cast<DWORD>(L'x'),
                WM_KEYDOWN,
                0,
                CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG);
            fixture.TrackText(L"b");

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(fixture.Request()));
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
        }

        TEST_METHOD (ModifierActivation_ShouldPreserveBufferAndReleaseCapturedModifierBeforeBackspaces)
        {
            BackendFixture fixture;
            fixture.TrackText(L"sig");
            fixture.SetLeftCtrl(true);
            fixture.TrackKey(VK_LCONTROL);
            const auto request = fixture.Request(
                { VK_CONTROL, VK_SPACE },
                { { L"sig", L"signature", 0 } },
                { VK_LCONTROL });

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            fixture.SetLeftCtrl(false);
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());

            const auto& batches = fixture.input.GetSentInputBatches();
            Assert::IsFalse(batches.empty());
            Assert::AreEqual(static_cast<size_t>(3), batches.front().size());
            Assert::AreEqual(static_cast<WORD>(VK_LCONTROL), batches.front()[2].ki.wVk);
            Assert::IsTrue((batches.front()[2].ki.dwFlags & KEYEVENTF_KEYUP) != 0);
            Assert::AreEqual(static_cast<size_t>(3), CountKeyDowns(batches, VK_BACK));
            Assert::AreEqual(std::wstring(L"signature"), fixture.input.GetInjectedUnicodeText());
        }

        TEST_METHOD (Prepare_ShouldClearTypedBufferWhenInputContextChanges)
        {
            BackendFixture fixture;
            fixture.TrackText(L"brb");
            fixture.currentContext = MakeContext(2);

            AssertResult(TextExpansionResult::NoMatch, fixture.Prepare(fixture.Request()));
            fixture.currentContext = MakeContext(1);
            AssertResult(TextExpansionResult::NoMatch, fixture.Prepare(fixture.Request()));
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
        }

        TEST_METHOD (TrackKeyboardEvent_ShouldAllowRetypingAfterInputContextChanges)
        {
            BackendFixture fixture;
            fixture.TrackText(L"brb");
            fixture.currentContext = MakeContext(2);
            fixture.TrackText(L"brb");

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(fixture.Request()));
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
            Assert::AreEqual(
                static_cast<size_t>(3),
                CountKeyDowns(fixture.input.GetSentInputBatches(), VK_BACK));
        }

        TEST_METHOD (Complete_ShouldSendOneBackspaceForSurrogatePairSource)
        {
            BackendFixture fixture;
            const std::wstring emoji{
                static_cast<wchar_t>(0xD83D),
                static_cast<wchar_t>(0xDE00),
            };
            fixture.TrackText(emoji);
            const auto request = fixture.Request(
                { VK_SPACE },
                { { emoji, L"smile", 0 } });

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
            Assert::AreEqual(
                static_cast<size_t>(1),
                CountKeyDowns(fixture.input.GetSentInputBatches(), VK_BACK));
        }

        TEST_METHOD (Complete_ShouldSendSurrogatePairReplacementInSingleFourEventBatch)
        {
            BackendFixture fixture;
            const std::wstring emoji{
                static_cast<wchar_t>(0xD83D),
                static_cast<wchar_t>(0xDE00),
            };
            fixture.TrackText(L"a");
            const auto request = fixture.Request(
                { VK_SPACE },
                { { L"a", emoji, 0 } });

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());

            const auto& batches = fixture.input.GetSentInputBatches();
            Assert::AreEqual(static_cast<size_t>(2), batches.size());
            AssertBackspacePair(batches[0]);
            Assert::AreEqual(static_cast<size_t>(4), batches[1].size());
            Assert::AreEqual(static_cast<WORD>(0xD83D), batches[1][0].ki.wScan);
            Assert::IsTrue((batches[1][0].ki.dwFlags & KEYEVENTF_KEYUP) == 0);
            Assert::AreEqual(static_cast<WORD>(0xD83D), batches[1][1].ki.wScan);
            Assert::IsTrue((batches[1][1].ki.dwFlags & KEYEVENTF_KEYUP) != 0);
            Assert::AreEqual(static_cast<WORD>(0xDE00), batches[1][2].ki.wScan);
            Assert::IsTrue((batches[1][2].ki.dwFlags & KEYEVENTF_KEYUP) == 0);
            Assert::AreEqual(static_cast<WORD>(0xDE00), batches[1][3].ki.wScan);
            Assert::IsTrue((batches[1][3].ki.dwFlags & KEYEVENTF_KEYUP) != 0);
            Assert::AreEqual(emoji, fixture.input.GetInjectedUnicodeText());
        }

        TEST_METHOD (Complete_ShouldRejectPreparedActivationAfterBufferReset)
        {
            BackendFixture fixture;
            fixture.TrackText(L"brb");

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(fixture.Request()));
            fixture.backend->ResetBuffer();
            AssertResult(TextExpansionResult::FailedUnchanged, fixture.Complete());
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
        }

        TEST_METHOD (Complete_ShouldStopBeforeInputWhenPreparedTargetChanges)
        {
            BackendFixture fixture;
            fixture.TrackText(L"brb");
            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(fixture.Request()));
            fixture.currentContext = MakeContext(2);

            AssertResult(TextExpansionResult::FailedUnchanged, fixture.Complete());
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
        }

        TEST_METHOD (Complete_ShouldStopBetweenBackspacesWhenTargetChanges)
        {
            BackendFixture fixture;
            fixture.TrackText(L"brb");
            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(fixture.Request()));
            size_t sendCalls = 0;
            fixture.input.SetSendVirtualInputInjectedCount([&](const std::vector<INPUT>& inputs) {
                ++sendCalls;
                if (sendCalls == 1)
                {
                    fixture.currentContext = MakeContext(2);
                }
                return inputs.size();
            });

            AssertResult(TextExpansionResult::FailedChangedOrUnknown, fixture.Complete());
            Assert::AreEqual(static_cast<size_t>(1), sendCalls);
            Assert::AreEqual(
                static_cast<size_t>(1),
                CountKeyDowns(fixture.input.GetSentInputBatches(), VK_BACK));
            Assert::AreEqual(std::wstring(), fixture.input.GetInjectedUnicodeText());
        }

        TEST_METHOD (Complete_ShouldRemainUnchangedWhenFirstBackspaceSendsNothing)
        {
            BackendFixture fixture;
            fixture.TrackText(L"a");
            const auto request = fixture.Request(
                { VK_SPACE },
                { { L"a", L"expanded", 0 } });
            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            fixture.input.SetSendVirtualInputInjectedCount([](const std::vector<INPUT>&) {
                return static_cast<size_t>(0);
            });

            AssertResult(TextExpansionResult::FailedUnchanged, fixture.Complete());
            Assert::AreEqual(static_cast<size_t>(1), fixture.input.GetSentInputBatches().size());
            Assert::AreEqual(std::wstring(), fixture.input.GetInjectedUnicodeText());
            Assert::IsFalse(fixture.backend->HasPendingWork());
        }

        TEST_METHOD (Complete_ShouldQueueAndRetryKeyUpCleanupAfterPartialBackspace)
        {
            BackendFixture fixture;
            fixture.TrackText(L"a");
            const auto request = fixture.Request(
                { VK_SPACE },
                { { L"a", L"expanded", 0 } });
            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            size_t sendCalls = 0;
            fixture.input.SetSendVirtualInputInjectedCount([&](const std::vector<INPUT>& inputs) {
                ++sendCalls;
                if (sendCalls == 1)
                {
                    return static_cast<size_t>(1);
                }
                if (sendCalls == 2)
                {
                    return static_cast<size_t>(0);
                }
                return inputs.size();
            });

            AssertResult(TextExpansionResult::FailedChangedOrUnknown, fixture.Complete());
            Assert::AreEqual(static_cast<size_t>(2), sendCalls);
            Assert::IsTrue(fixture.backend->ShouldBlockNewInput());
            const auto& attemptedBatches = fixture.input.GetSentInputBatches();
            Assert::AreEqual(static_cast<size_t>(2), attemptedBatches.size());
            AssertBackspacePair(attemptedBatches[0]);
            Assert::AreEqual(static_cast<size_t>(1), attemptedBatches[1].size());
            Assert::AreEqual(static_cast<WORD>(VK_BACK), attemptedBatches[1][0].ki.wVk);
            Assert::IsTrue((attemptedBatches[1][0].ki.dwFlags & KEYEVENTF_KEYUP) != 0);

            fixture.input.SetSendVirtualInputInjectedCount([](const std::vector<INPUT>& inputs) {
                return inputs.size();
            });
            fixture.backend->RetryPendingCleanup();
            Assert::IsFalse(fixture.backend->ShouldBlockNewInput());
            Assert::AreEqual(static_cast<size_t>(3), fixture.input.GetSentInputBatches().size());
        }

        TEST_METHOD (Complete_ShouldReportChangedWhenReplacementFailsAfterBackspaces)
        {
            BackendFixture fixture;
            fixture.TrackText(L"a");
            const auto request = fixture.Request(
                { VK_SPACE },
                { { L"a", L"expanded", 0 } });
            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            size_t sendCalls = 0;
            fixture.input.SetSendVirtualInputInjectedCount([&](const std::vector<INPUT>& inputs) {
                ++sendCalls;
                return sendCalls == 1 ? inputs.size() : static_cast<size_t>(0);
            });

            AssertResult(TextExpansionResult::FailedChangedOrUnknown, fixture.Complete());
            Assert::AreEqual(static_cast<size_t>(2), sendCalls);
            Assert::AreEqual(static_cast<size_t>(1), CountKeyDowns(fixture.input.GetSentInputBatches(), VK_BACK));
            Assert::AreEqual(std::wstring(), fixture.input.GetInjectedUnicodeText());
        }

        TEST_METHOD (Complete_ShouldEmitCrLfAsOneBareEnterPress)
        {
            BackendFixture fixture;
            fixture.TrackText(L"a");
            const auto request = fixture.Request(
                { VK_SPACE },
                { { L"a", L"first\r\nsecond", 0 } });

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());

            std::vector<INPUT> enterEvents;
            for (const auto& batch : fixture.input.GetSentInputBatches())
            {
                for (const auto& input : batch)
                {
                    if (input.type == INPUT_KEYBOARD && input.ki.wVk == VK_RETURN)
                    {
                        enterEvents.push_back(input);
                    }
                    Assert::AreNotEqual(static_cast<WORD>(VK_SHIFT), input.ki.wVk);
                }
            }

            Assert::AreEqual(static_cast<size_t>(2), enterEvents.size());
            Assert::IsTrue((enterEvents[0].ki.dwFlags & KEYEVENTF_KEYUP) == 0);
            Assert::IsTrue((enterEvents[1].ki.dwFlags & KEYEVENTF_KEYUP) != 0);
            Assert::AreEqual(std::wstring(L"firstsecond"), fixture.input.GetInjectedUnicodeText());
        }

        TEST_METHOD (CancelPendingActivation_ShouldClearPendingWorkWithoutInput)
        {
            BackendFixture fixture;
            fixture.TrackText(L"brb");
            const auto request = fixture.Request();

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            Assert::IsTrue(fixture.backend->HasPendingWork());
            AssertResult(TextExpansionResult::FailedUnchanged, fixture.backend->CancelPendingActivation());
            Assert::IsFalse(fixture.backend->HasPendingWork());
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
            AssertResult(TextExpansionResult::FailedUnchanged, fixture.Complete());
            AssertResult(TextExpansionResult::NoMatch, fixture.Prepare(request));
        }

        TEST_METHOD (Prepare_ShouldRejectSecondActivationWithoutReplacingFirstPendingRequest)
        {
            BackendFixture fixture;
            fixture.TrackText(L"brb");
            const auto firstRequest = fixture.Request(
                { VK_SPACE },
                { { L"brb", L"first", 0 } });
            const auto secondRequest = fixture.Request(
                { VK_SPACE },
                { { L"brb", L"second", 0 } });

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(firstRequest));
            AssertResult(TextExpansionResult::FailedUnchanged, fixture.Prepare(secondRequest));
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
            Assert::AreEqual(std::wstring(L"first"), fixture.input.GetInjectedUnicodeText());
        }

        TEST_METHOD (Stop_ShouldClearPendingActivationAndTypedBuffer)
        {
            BackendFixture fixture;
            fixture.TrackText(L"brb");
            const auto request = fixture.Request();

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            fixture.backend->Stop();
            Assert::IsFalse(fixture.backend->HasPendingWork());
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
            AssertResult(TextExpansionResult::FailedUnchanged, fixture.Complete());

            Assert::IsTrue(fixture.backend->Start());
            AssertResult(TextExpansionResult::NoMatch, fixture.Prepare(request));
        }
    };
}
