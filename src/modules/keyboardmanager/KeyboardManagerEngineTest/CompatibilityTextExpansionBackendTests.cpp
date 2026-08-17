#include "pch.h"

// Suppressing 26466 - Don't use static_cast downcasts - in CppUnitTest.h
#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "MockedInput.h"

#include <algorithm>
#include <stdexcept>
#include <keyboardmanager/KeyboardManagerEngineLibrary/CompatibilityTextExpansionBackend.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace TextExpansionEngineTests
{
    namespace
    {
        void AssertResult(const TextExpansionResult expected, const TextExpansionResult actual)
        {
            Assert::AreEqual(static_cast<int>(expected), static_cast<int>(actual));
        }

        class FakeTextExpansionTextContext : public ITextExpansionTextContext
        {
        public:
            bool startResult = true;
            bool pendingWork = false;
            bool rollbackResult = true;
            bool confirmResult = true;
            bool targetContextCurrent = true;
            bool automaticCaretMatch = false;
            std::wstring caretPrefix;
            TextExpansionPreparationResult prepareResult{
                .status = TextExpansionPreparationStatus::Prepared,
                .replacementText = L"expanded",
                .profileIndex = 0,
            };
            std::vector<bool> verifyResults{ true, true };

            int startCalls = 0;
            int stopCalls = 0;
            int prepareCalls = 0;
            int verifyCalls = 0;
            int confirmCalls = 0;
            int rollbackCalls = 0;
            int markCommittedCalls = 0;
            int finishCalls = 0;
            mutable size_t targetContextCurrentCalls = 0;
            std::vector<TextExpansionCandidate> lastCandidates;
            std::wstring lastConfirmedReplacement;
            std::vector<bool> targetContextCurrentResults;
            mutable std::vector<std::wstring> operations;
            std::chrono::steady_clock::time_point prepareDeadline{};
            std::vector<std::chrono::steady_clock::time_point> verifyDeadlines;
            std::vector<std::chrono::steady_clock::time_point> confirmDeadlines;
            std::vector<std::chrono::steady_clock::time_point> rollbackDeadlines;
            std::vector<std::chrono::steady_clock::time_point> finishDeadlines;

            bool Start() override
            {
                ++startCalls;
                return startResult;
            }

            void Stop() noexcept override
            {
                ++stopCalls;
                pendingWork = false;
            }

            TextExpansionPreparationResult Prepare(
                const std::vector<TextExpansionCandidate>& candidates,
                const std::chrono::steady_clock::time_point deadline) override
            {
                ++prepareCalls;
                operations.push_back(L"prepare");
                prepareDeadline = deadline;
                lastCandidates = candidates;
                if (!automaticCaretMatch)
                {
                    pendingWork = prepareResult.status == TextExpansionPreparationStatus::Prepared;
                    return prepareResult;
                }

                const auto selected = SelectTextExpansionCandidate(candidates, caretPrefix);
                if (!selected)
                {
                    return { .status = TextExpansionPreparationStatus::NoMatch };
                }

                pendingWork = true;
                return {
                    .status = TextExpansionPreparationStatus::Prepared,
                    .replacementText = candidates[*selected].replacementText,
                    .profileIndex = candidates[*selected].profileIndex,
                };
            }

            bool VerifyPreparedSelection(const std::chrono::steady_clock::time_point deadline) override
            {
                operations.push_back(L"verify");
                verifyDeadlines.push_back(deadline);
                const size_t resultIndex = static_cast<size_t>(verifyCalls++);
                return resultIndex < verifyResults.size() ? verifyResults[resultIndex] : true;
            }

            bool ConfirmReplacement(
                const std::wstring_view replacementText,
                const std::chrono::steady_clock::time_point deadline) override
            {
                ++confirmCalls;
                operations.push_back(L"confirm");
                confirmDeadlines.push_back(deadline);
                lastConfirmedReplacement = replacementText;
                return confirmResult;
            }

            bool IsTargetContextCurrent(std::chrono::steady_clock::time_point) override
            {
                operations.push_back(L"target");
                const size_t resultIndex = targetContextCurrentCalls++;
                return resultIndex < targetContextCurrentResults.size() ?
                           targetContextCurrentResults[resultIndex] :
                           targetContextCurrent;
            }

            bool IsTargetWindowCurrent() const noexcept override
            {
                operations.push_back(L"target-window");
                const size_t resultIndex = targetContextCurrentCalls++;
                return resultIndex < targetContextCurrentResults.size() ?
                           targetContextCurrentResults[resultIndex] :
                           targetContextCurrent;
            }

            bool Rollback(const std::chrono::steady_clock::time_point deadline) override
            {
                ++rollbackCalls;
                operations.push_back(L"rollback");
                rollbackDeadlines.push_back(deadline);
                if (rollbackResult)
                {
                    pendingWork = false;
                }
                return rollbackResult;
            }

            void MarkCommitted() noexcept override
            {
                ++markCommittedCalls;
                operations.push_back(L"mark-committed");
            }

            void Finish(const std::chrono::steady_clock::time_point deadline) noexcept override
            {
                ++finishCalls;
                operations.push_back(L"finish");
                finishDeadlines.push_back(deadline);
                pendingWork = false;
            }

            bool HasPendingWork() const noexcept override
            {
                return pendingWork;
            }

            bool ShouldBlockNewInput() const noexcept override
            {
                return pendingWork;
            }
        };

        struct BackendFixture
        {
            KeyboardManagerInput::MockedInput input;
            FakeTextExpansionTextContext* context = nullptr;
            std::unique_ptr<CompatibilityTextExpansionBackend> backend;

            BackendFixture()
            {
                auto fakeContext = std::make_unique<FakeTextExpansionTextContext>();
                context = fakeContext.get();
                backend = std::make_unique<CompatibilityTextExpansionBackend>(input, std::move(fakeContext));
                Assert::IsTrue(backend->Start());
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
                    .deadline = std::chrono::steady_clock::now() + std::chrono::seconds(1),
                };
            }

            void SetLeftCtrl(const bool down)
            {
                input.SetKeyboardState(VK_LCONTROL, down);
                input.SetKeyboardState(VK_CONTROL, down);
            }

            void SetLeftShift(const bool down)
            {
                input.SetKeyboardState(VK_LSHIFT, down);
                input.SetKeyboardState(VK_SHIFT, down);
            }

            TextExpansionResult Prepare(const TextExpansionRequest& request)
            {
                return backend->PrepareActivation(request);
            }

            TextExpansionResult Complete()
            {
                return backend->CompletePendingActivation();
            }

        };
    }

    TEST_CLASS (CompatibilityTextExpansionBackendTests)
    {
    public:
        TEST_METHOD (Prepare_ShouldNotSendInputForOwnedActivationModifiers)
        {
            BackendFixture fixture;
            fixture.SetLeftCtrl(true);
            const auto request = fixture.Request(
                { VK_CONTROL, VK_SPACE },
                { { L"brb", L"expanded", 0 } },
                { VK_LCONTROL });

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
            Assert::AreEqual(0, fixture.context->markCommittedCalls);
            Assert::AreEqual(0, fixture.context->finishCalls);
            Assert::AreEqual(0, fixture.context->confirmCalls);
            AssertResult(TextExpansionResult::FailedChangedOrUnknown, fixture.backend->CancelPendingActivation());
        }

        TEST_METHOD (PrepareAndComplete_ShouldUseCaretSuffixAndChooseLongestCandidate)
        {
            BackendFixture fixture;
            fixture.context->automaticCaretMatch = true;
            fixture.context->caretPrefix = L"please brb";
            const auto request = fixture.Request(
                { VK_SPACE },
                {
                    { L"rb", L"short", 0 },
                    { L"brb", L"longest", 1 },
                    { L"unrelated", L"unused", 2 },
                });

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
            const auto completionStarted = std::chrono::steady_clock::now();
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
            Assert::AreEqual(std::wstring(L"longest"), fixture.input.GetInjectedUnicodeText());
            Assert::AreEqual(1, fixture.context->markCommittedCalls);
            Assert::AreEqual(1, fixture.context->finishCalls);
            Assert::AreEqual(1, fixture.context->confirmCalls);
            Assert::AreEqual(std::wstring(L"longest"), fixture.context->lastConfirmedReplacement);
            Assert::AreEqual(static_cast<size_t>(1), fixture.context->finishDeadlines.size());
            Assert::AreEqual(static_cast<size_t>(1), fixture.context->confirmDeadlines.size());
            Assert::IsTrue(fixture.context->finishDeadlines[0] != request.deadline);
            Assert::IsTrue(fixture.context->confirmDeadlines[0] != request.deadline);
            Assert::IsTrue(fixture.context->finishDeadlines[0] > completionStarted);
            Assert::IsTrue(fixture.context->confirmDeadlines[0] > completionStarted);
        }

        TEST_METHOD (PrepareAndComplete_ShouldUseFirstProfileRuleWhenDuplicateCandidatesTie)
        {
            BackendFixture fixture;
            fixture.context->automaticCaretMatch = true;
            fixture.context->caretPrefix = L"brb";
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

        TEST_METHOD (CandidateSelection_ShouldReturnOriginalIndexForLongestSuffix)
        {
            const std::vector<TextExpansionCandidate> candidates{
                { L"b", L"one", 0 },
                { L"brb", L"three", 1 },
                { L"rb", L"two", 2 },
            };

            const auto selected = SelectTextExpansionCandidate(candidates, L"please brb");

            Assert::IsTrue(selected.has_value());
            Assert::AreEqual(static_cast<size_t>(1), *selected);
        }

        TEST_METHOD (CandidateSelection_ShouldKeepFirstDuplicateInProfileOrder)
        {
            const std::vector<TextExpansionCandidate> candidates{
                { L"brb", L"first", 9 },
                { L"brb", L"second", 2 },
            };

            const auto selected = SelectTextExpansionCandidate(candidates, L"brb");

            Assert::IsTrue(selected.has_value());
            Assert::AreEqual(static_cast<size_t>(0), *selected);
        }

        TEST_METHOD (ReplacementConfirmation_ShouldRejectAppendWithoutDeletingSource)
        {
            Assert::IsFalse(IsTextExpansionReplacementExact(L"brbexpanded", L"expanded"));
        }

        TEST_METHOD (ReplacementConfirmation_ShouldAcceptExactReplacementRange)
        {
            Assert::IsTrue(IsTextExpansionReplacementExact(L"expanded", L"expanded"));
        }

        TEST_METHOD (Prepare_ShouldReturnNoMatchWhenCaretSuffixDoesNotMatch)
        {
            BackendFixture fixture;
            fixture.context->automaticCaretMatch = true;
            fixture.context->caretPrefix = L"different text";

            AssertResult(TextExpansionResult::NoMatch, fixture.Prepare(fixture.Request()));
            Assert::AreEqual(0, fixture.context->verifyCalls);
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
            AssertResult(TextExpansionResult::FailedUnchanged, fixture.Complete());
        }

        TEST_METHOD (Prepare_ShouldPropagateUnsupportedContext)
        {
            BackendFixture fixture;
            fixture.context->prepareResult.status = TextExpansionPreparationStatus::UnsupportedContext;

            AssertResult(TextExpansionResult::UnsupportedContext, fixture.Prepare(fixture.Request()));
            Assert::AreEqual(0, fixture.context->verifyCalls);
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
        }

        TEST_METHOD (Prepare_ShouldTreatNonCollapsedOrUnmatchedSelectionAsNoMatch)
        {
            BackendFixture fixture;
            fixture.context->prepareResult.status = TextExpansionPreparationStatus::NoMatch;

            AssertResult(TextExpansionResult::NoMatch, fixture.Prepare(fixture.Request()));
            Assert::AreEqual(0, fixture.context->rollbackCalls);
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
        }

        TEST_METHOD (Prepare_ShouldPropagateContextTimeoutBeforeSelectionMutation)
        {
            BackendFixture fixture;
            fixture.context->prepareResult.status = TextExpansionPreparationStatus::FailedUnchanged;

            AssertResult(TextExpansionResult::FailedUnchanged, fixture.Prepare(fixture.Request()));
            Assert::AreEqual(0, fixture.context->rollbackCalls);
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
        }

        TEST_METHOD (Prepare_ShouldPropagateContextTimeoutAfterUnknownSelectionMutation)
        {
            BackendFixture fixture;
            fixture.context->prepareResult.status = TextExpansionPreparationStatus::FailedChangedOrUnknown;

            AssertResult(TextExpansionResult::FailedChangedOrUnknown, fixture.Prepare(fixture.Request()));
            Assert::AreEqual(0, fixture.context->rollbackCalls);
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
        }

        TEST_METHOD (Prepare_ShouldRollbackWhenPreparedSelectionCannotBeVerified)
        {
            BackendFixture fixture;
            fixture.context->verifyResults = { false };
            fixture.context->rollbackResult = true;

            AssertResult(TextExpansionResult::FailedUnchanged, fixture.Prepare(fixture.Request()));
            Assert::AreEqual(1, fixture.context->rollbackCalls);
            Assert::AreEqual(0, fixture.context->finishCalls);
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
        }

        TEST_METHOD (Prepare_ShouldFailClosedWhenPreparedSelectionRollbackFails)
        {
            BackendFixture fixture;
            fixture.context->verifyResults = { false };
            fixture.context->rollbackResult = false;

            AssertResult(TextExpansionResult::FailedChangedOrUnknown, fixture.Prepare(fixture.Request()));
            Assert::AreEqual(1, fixture.context->rollbackCalls);
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
        }

        TEST_METHOD (Complete_ShouldUseFreshVerifyDeadlineAndRollbackBeforeFirstInput)
        {
            BackendFixture fixture;
            fixture.context->verifyResults = { true, false };
            fixture.context->rollbackResult = true;
            const auto request = fixture.Request();

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            const auto completionStarted = std::chrono::steady_clock::now();
            AssertResult(TextExpansionResult::FailedUnchanged, fixture.Complete());

            Assert::AreEqual(static_cast<size_t>(2), fixture.context->verifyDeadlines.size());
            Assert::IsTrue(fixture.context->verifyDeadlines[1] != request.deadline);
            Assert::IsTrue(fixture.context->verifyDeadlines[1] > completionStarted);
            Assert::AreEqual(static_cast<size_t>(1), fixture.context->rollbackDeadlines.size());
            Assert::IsTrue(fixture.context->rollbackDeadlines[0] != request.deadline);
            Assert::IsTrue(fixture.context->rollbackDeadlines[0] > completionStarted);
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
        }

        TEST_METHOD (Complete_ShouldFailClosedWhenFreshVerificationRollbackFails)
        {
            BackendFixture fixture;
            fixture.context->verifyResults = { true, false };
            fixture.context->rollbackResult = false;
            const auto request = fixture.Request();

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            AssertResult(TextExpansionResult::FailedChangedOrUnknown, fixture.Complete());
            Assert::AreEqual(1, fixture.context->rollbackCalls);
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
        }

        TEST_METHOD (Complete_ShouldOnlyPerformCheapTargetWindowChecksBetweenTextBatches)
        {
            BackendFixture fixture;
            fixture.context->prepareResult.replacementText = L"abcd";
            const auto request = fixture.Request();

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            fixture.context->operations.clear();
            fixture.input.SetSendVirtualInputInjectedCount([&](const std::vector<INPUT>& inputs) {
                fixture.context->operations.push_back(L"send-input");
                return inputs.size();
            });

            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
            Assert::AreEqual(fixture.context->prepareResult.replacementText.size(), fixture.context->targetContextCurrentCalls);

            size_t firstSend = fixture.context->operations.size();
            size_t lastSend = 0;
            size_t sendCount = 0;
            for (size_t index = 0; index < fixture.context->operations.size(); ++index)
            {
                if (fixture.context->operations[index] == L"send-input")
                {
                    firstSend = (std::min)(firstSend, index);
                    lastSend = index;
                    ++sendCount;
                }
            }

            Assert::AreEqual(fixture.context->prepareResult.replacementText.size(), sendCount);
            Assert::IsTrue(firstSend < lastSend);
            for (size_t index = firstSend + 1; index < lastSend; ++index)
            {
                const auto& operation = fixture.context->operations[index];
                Assert::IsFalse(
                    operation == L"prepare" || operation == L"verify" || operation == L"target" ||
                    operation == L"rollback" || operation == L"finish" || operation == L"confirm");
            }
            Assert::AreEqual(1, fixture.context->markCommittedCalls);
        }

        TEST_METHOD (Complete_ShouldStopBeforeNextTextUnitWhenTargetWindowChanges)
        {
            BackendFixture fixture;
            fixture.context->prepareResult.replacementText = L"ab";
            fixture.context->targetContextCurrentResults = { true, false };
            const auto request = fixture.Request();

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            AssertResult(TextExpansionResult::FailedChangedOrUnknown, fixture.Complete());
            Assert::AreEqual(std::wstring(L"a"), fixture.input.GetInjectedUnicodeText());
            Assert::AreEqual(1, fixture.context->markCommittedCalls);
            Assert::AreEqual(0, fixture.context->rollbackCalls);
            Assert::AreEqual(1, fixture.context->finishCalls);
        }

        TEST_METHOD (Complete_ShouldSendEachUnicodeUnitInItsOwnInputBatch)
        {
            BackendFixture fixture;
            fixture.context->prepareResult.replacementText = L"PowerToys expansion works";
            const auto request = fixture.Request();

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
            Assert::AreEqual(fixture.context->prepareResult.replacementText.size(), fixture.input.GetSentInputBatches().size());
            for (const auto& batch : fixture.input.GetSentInputBatches())
            {
                Assert::AreEqual(static_cast<size_t>(2), batch.size());
            }
            Assert::AreEqual(fixture.context->prepareResult.replacementText, fixture.input.GetInjectedUnicodeText());
            Assert::AreEqual(1, fixture.context->markCommittedCalls);
        }

        TEST_METHOD (Complete_ShouldRollbackWhenFirstTextUnitSendsNothing)
        {
            BackendFixture fixture;
            fixture.input.SetSendVirtualInputInjectedCount([](const std::vector<INPUT>&) { return 0; });
            fixture.context->rollbackResult = true;
            const auto request = fixture.Request();

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            AssertResult(TextExpansionResult::FailedUnchanged, fixture.Complete());
            Assert::AreEqual(1, fixture.context->rollbackCalls);
            Assert::AreEqual(0, fixture.context->markCommittedCalls);
            Assert::AreEqual(0, fixture.context->finishCalls);
        }

        TEST_METHOD (Complete_ShouldFailClosedWhenFirstTextUnitSendsNothingAndRollbackFails)
        {
            BackendFixture fixture;
            fixture.input.SetSendVirtualInputInjectedCount([](const std::vector<INPUT>&) { return 0; });
            fixture.context->rollbackResult = false;
            const auto request = fixture.Request();

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            AssertResult(TextExpansionResult::FailedChangedOrUnknown, fixture.Complete());
            Assert::AreEqual(1, fixture.context->rollbackCalls);
            Assert::AreEqual(0, fixture.context->markCommittedCalls);
        }

        TEST_METHOD (Complete_ShouldMarkCommittedAndNotRollbackAfterPartialTextInjection)
        {
            BackendFixture fixture;
            fixture.context->prepareResult.replacementText = L"be right back";
            int calls = 0;
            fixture.input.SetSendVirtualInputInjectedCount([&](const std::vector<INPUT>& inputs) {
                ++calls;
                return calls == 1 ? static_cast<size_t>(1) : inputs.size();
            });
            const auto request = fixture.Request();

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            AssertResult(TextExpansionResult::FailedChangedOrUnknown, fixture.Complete());
            Assert::AreEqual(std::wstring(L"b"), fixture.input.GetInjectedUnicodeText());
            Assert::AreEqual(static_cast<size_t>(2), fixture.input.GetSentInputBatches().size());
            Assert::AreEqual(1, fixture.context->markCommittedCalls);
            Assert::AreEqual(0, fixture.context->rollbackCalls);
            Assert::AreEqual(1, fixture.context->finishCalls);
            Assert::AreEqual(0, fixture.context->confirmCalls);
        }

        TEST_METHOD (Complete_ShouldNotRollbackWhenLaterTextInjectionThrowsAfterCommit)
        {
            BackendFixture fixture;
            fixture.context->prepareResult.replacementText = L"ab";
            int calls = 0;
            fixture.input.SetSendVirtualInputInjectedCount([&](const std::vector<INPUT>& inputs) -> size_t {
                ++calls;
                if (calls == 2)
                {
                    throw std::runtime_error("simulated SendInput exception");
                }
                return inputs.size();
            });
            const auto request = fixture.Request();

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            AssertResult(TextExpansionResult::FailedChangedOrUnknown, fixture.Complete());
            Assert::AreEqual(std::wstring(L"a"), fixture.input.GetInjectedUnicodeText());
            Assert::AreEqual(1, fixture.context->markCommittedCalls);
            Assert::AreEqual(0, fixture.context->rollbackCalls);
            Assert::AreEqual(1, fixture.context->finishCalls);
            Assert::AreEqual(0, fixture.context->confirmCalls);
        }

        TEST_METHOD (Complete_ShouldFailClosedWithoutRollbackWhenUIACannotConfirmCommittedReplacement)
        {
            BackendFixture fixture;
            fixture.context->confirmResult = false;
            const auto request = fixture.Request();

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            AssertResult(TextExpansionResult::FailedChangedOrUnknown, fixture.Complete());
            Assert::AreEqual(1, fixture.context->markCommittedCalls);
            Assert::AreEqual(0, fixture.context->rollbackCalls);
            Assert::AreEqual(1, fixture.context->finishCalls);
            Assert::AreEqual(1, fixture.context->confirmCalls);
            Assert::AreEqual(std::wstring(L"expanded"), fixture.context->lastConfirmedReplacement);
        }

        TEST_METHOD (Complete_ShouldEmitCrLfAsOneBareEnterPress)
        {
            BackendFixture fixture;
            fixture.context->prepareResult.replacementText = L"first\r\nsecond";
            const auto request = fixture.Request();

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
        }

        TEST_METHOD (Complete_ShouldReleaseStoredOriginalModifiersEvenWhenCurrentKeyStateIsUp)
        {
            BackendFixture fixture;
            fixture.SetLeftCtrl(true);
            fixture.SetLeftShift(true);
            const auto request = fixture.Request(
                { VK_CONTROL, VK_SHIFT, VK_RETURN },
                { { L"sig", L"signature", 0 } },
                { VK_LCONTROL, VK_LSHIFT });
            fixture.context->prepareResult.replacementText = L"signature";

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
            fixture.SetLeftCtrl(false);
            fixture.SetLeftShift(false);
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());

            const auto& modifierBatch = fixture.input.GetSentInputBatches().front();
            Assert::AreEqual(static_cast<size_t>(4), modifierBatch.size());
            Assert::AreEqual(static_cast<WORD>(VK_LCONTROL), modifierBatch[2].ki.wVk);
            Assert::IsTrue((modifierBatch[2].ki.dwFlags & KEYEVENTF_KEYUP) != 0);
            Assert::AreEqual(static_cast<WORD>(VK_LSHIFT), modifierBatch[3].ki.wVk);
            Assert::IsTrue((modifierBatch[3].ki.dwFlags & KEYEVENTF_KEYUP) != 0);
            Assert::AreEqual(std::wstring(L"signature"), fixture.input.GetInjectedUnicodeText());
        }

        TEST_METHOD (Complete_ShouldRollbackSelectionButReturnUnknownForPartialModifierInjection)
        {
            BackendFixture fixture;
            fixture.SetLeftCtrl(true);
            fixture.SetLeftShift(true);
            fixture.input.SetSendVirtualInputInjectedCount([](const std::vector<INPUT>& inputs) {
                // dummy down/up + the first modifier up; the second modifier up is blocked
                return (std::min)(static_cast<size_t>(3), inputs.size());
            });
            const auto request = fixture.Request(
                { VK_CONTROL, VK_SHIFT, VK_RETURN },
                { { L"sig", L"signature", 0 } },
                { VK_LCONTROL, VK_LSHIFT });

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            fixture.SetLeftCtrl(false);
            fixture.SetLeftShift(false);
            AssertResult(TextExpansionResult::FailedChangedOrUnknown, fixture.Complete());
            const auto& modifierBatch = fixture.input.GetSentInputBatches().front();
            Assert::AreEqual(static_cast<size_t>(4), modifierBatch.size());
            Assert::AreEqual(static_cast<WORD>(VK_LCONTROL), modifierBatch[2].ki.wVk);
            Assert::AreEqual(static_cast<WORD>(VK_LSHIFT), modifierBatch[3].ki.wVk);
            Assert::AreEqual(1, fixture.context->rollbackCalls);
            Assert::AreEqual(0, fixture.context->markCommittedCalls);
            Assert::AreEqual(0, fixture.context->finishCalls);
        }

        TEST_METHOD (Complete_ShouldRollbackSelectionButReturnUnknownWhenModifierChangesSelection)
        {
            BackendFixture fixture;
            fixture.SetLeftCtrl(true);
            fixture.context->verifyResults = { true, true, false };
            const auto request = fixture.Request(
                { VK_CONTROL, VK_SPACE },
                { { L"brb", L"be right back", 0 } },
                { VK_LCONTROL });

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            AssertResult(TextExpansionResult::FailedChangedOrUnknown, fixture.Complete());
            Assert::AreEqual(1, fixture.context->rollbackCalls);
            Assert::AreEqual(0, fixture.context->markCommittedCalls);
            Assert::AreEqual(0, fixture.context->finishCalls);
        }

        TEST_METHOD (Complete_ShouldUseRollbackOutcomeWhenModifierInjectionIsBlocked)
        {
            BackendFixture fixture;
            fixture.SetLeftCtrl(true);
            fixture.input.SetSendVirtualInputInjectedCount([](const std::vector<INPUT>&) { return 0; });
            fixture.context->rollbackResult = false;
            const auto request = fixture.Request(
                { VK_CONTROL, VK_SPACE },
                { { L"brb", L"be right back", 0 } },
                { VK_LCONTROL });

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            AssertResult(TextExpansionResult::FailedChangedOrUnknown, fixture.Complete());
            Assert::AreEqual(1, fixture.context->rollbackCalls);
            Assert::AreEqual(0, fixture.context->markCommittedCalls);
        }

        TEST_METHOD (Complete_ShouldRemainChangedWhenStoredModifierReleaseMustBeRetried)
        {
            BackendFixture fixture;
            fixture.SetLeftCtrl(true);
            fixture.input.SetSendVirtualInputInjectedCount([](const std::vector<INPUT>&) { return 0; });
            fixture.context->rollbackResult = true;
            const auto request = fixture.Request(
                { VK_CONTROL, VK_SPACE },
                { { L"brb", L"be right back", 0 } },
                { VK_LCONTROL });

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            AssertResult(TextExpansionResult::FailedChangedOrUnknown, fixture.Complete());
            Assert::AreEqual(1, fixture.context->rollbackCalls);
            Assert::AreEqual(0, fixture.context->markCommittedCalls);
            Assert::AreEqual(0, fixture.context->finishCalls);
        }

        TEST_METHOD (Prepare_ShouldRejectSecondActivationAndPreserveFirstPendingReplacement)
        {
            BackendFixture fixture;
            fixture.context->prepareResult.replacementText = L"first";
            const auto firstRequest = fixture.Request();

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(firstRequest));
            fixture.context->prepareResult.replacementText = L"second";
            AssertResult(TextExpansionResult::FailedUnchanged, fixture.Prepare(fixture.Request()));
            Assert::AreEqual(1, fixture.context->prepareCalls);
            AssertResult(TextExpansionResult::Replaced, fixture.Complete());
            Assert::AreEqual(std::wstring(L"first"), fixture.input.GetInjectedUnicodeText());
        }

        TEST_METHOD (CancelPendingActivation_ShouldRollbackWithoutInputAndClearPendingWork)
        {
            BackendFixture fixture;
            const auto request = fixture.Request();

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            Assert::IsTrue(fixture.backend->ShouldBlockNewInput());
            Assert::IsTrue(fixture.backend->HasPendingWork());
            AssertResult(TextExpansionResult::FailedUnchanged, fixture.backend->CancelPendingActivation());
            Assert::AreEqual(1, fixture.context->rollbackCalls);
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
            Assert::IsFalse(fixture.backend->ShouldBlockNewInput());
            Assert::IsFalse(fixture.backend->HasPendingWork());
            AssertResult(TextExpansionResult::FailedUnchanged, fixture.Complete());
        }

        TEST_METHOD (Stop_ShouldRollbackPreparedSelectionWithoutInputAndRejectStaleCompletion)
        {
            BackendFixture fixture;
            const auto request = fixture.Request();

            AssertResult(TextExpansionResult::Prepared, fixture.Prepare(request));
            fixture.backend->Stop();
            Assert::AreEqual(1, fixture.context->rollbackCalls);
            Assert::AreEqual(1, fixture.context->stopCalls);
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
            Assert::IsFalse(fixture.backend->HasPendingWork());
            AssertResult(TextExpansionResult::FailedUnchanged, fixture.Complete());
            Assert::AreEqual(static_cast<size_t>(0), fixture.input.GetSentInputBatches().size());
        }

        TEST_METHOD (HasPendingWork_ShouldIncludeTextContextState)
        {
            BackendFixture fixture;
            Assert::IsFalse(fixture.backend->HasPendingWork());
            fixture.context->pendingWork = true;
            Assert::IsTrue(fixture.backend->HasPendingWork());
        }

        TEST_METHOD (StartFailure_ShouldPreventPreparation)
        {
            KeyboardManagerInput::MockedInput input;
            auto context = std::make_unique<FakeTextExpansionTextContext>();
            auto* contextView = context.get();
            contextView->startResult = false;
            CompatibilityTextExpansionBackend backend(input, std::move(context));

            Assert::IsFalse(backend.Start());
            TextExpansionRequest request{
                .activationShortcut = Shortcut(VK_SPACE),
                .activationModifierKeys = {},
                .candidates = { { L"brb", L"expanded", 0 } },
                .deadline = std::chrono::steady_clock::now() + std::chrono::seconds(1),
            };
            AssertResult(TextExpansionResult::UnsupportedContext, backend.PrepareActivation(request));
            Assert::AreEqual(0, contextView->prepareCalls);
            Assert::AreEqual(static_cast<size_t>(0), input.GetSentInputBatches().size());
        }
    };
}
