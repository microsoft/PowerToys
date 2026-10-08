// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#include "pch.h"
#include <atomic>
#include <future>
#include <WorkspacesCLI/ApprovalChannel.h>
#include <WorkspacesCLI/ConsoleApproval.h>
#include <WorkspacesLib/CliCommands.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace WorkspacesLibUnitTests
{
    namespace
    {
        enum class ReplyKind
        {
            Allow,
            Skip,
            EarlyAllow,
            WrongId,
            Unavailable,
            Disconnect,
            Cancel,
            InvalidChoice,
            DuplicateShown,
        };

        json::JsonObject Receive(WorkspacesCli::ApprovalReader& reader)
        {
            const auto deadline = GetTickCount64() + 5000;
            while (GetTickCount64() < deadline)
            {
                if (const auto value = reader.Poll())
                    return *value;
                Sleep(1);
            }
            Assert::Fail(L"Expected a bounded approval frame.");
        }

        void Send(WorkspacesCli::ApprovalWriter& writer, const json::JsonObject& message)
        {
            writer.Start(message);
            const auto deadline = GetTickCount64() + 5000;
            while (writer.Poll() == WorkspacesCli::WriteState::Pending && GetTickCount64() < deadline)
                Sleep(1);
            Assert::IsTrue(writer.Idle());
        }

        LaunchDecision Exchange(ReplyKind replyKind)
        {
            auto requests = WorkspacesCli::ApprovalPipe::Create();
            auto replies = WorkspacesCli::ApprovalPipe::Create();
            std::atomic<bool> canceled = false;
            const auto deadline = GetTickCount64() + 5000;
            auto future = std::async(std::launch::async, [&] {
                winrt::init_apartment();
                WorkspacesCli::WorkerApproval worker(requests.write.get(), replies.read.get(), [&] {
                    return canceled.load() || GetTickCount64() >= deadline;
                });
                return worker.Request(L"Example", L"C:\\example.exe", L"--example", { SignatureVerification::Status::Unsigned, TRUST_E_NOSIGNATURE, {}, {} });
            });
            auto cleanup = wil::scope_exit([&] {
                canceled = true;
                if (future.valid())
                    future.wait();
            });
            WorkspacesCli::ApprovalReader requestReader(requests.read.get());
            WorkspacesCli::ApprovalWriter replyWriter(replies.write.get());
            const auto request = Receive(requestReader);
            Assert::AreEqual(std::wstring(L"elevation-warning"), std::wstring(request.GetNamedString(L"type")));
            const auto id = std::wstring(request.GetNamedString(L"requestId"));
            if (replyKind == ReplyKind::Cancel)
                canceled = true;
            else if (replyKind == ReplyKind::Disconnect)
                replies.write.reset();
            else if (replyKind == ReplyKind::Unavailable)
                Send(replyWriter, WorkspacesCli::ApprovalMessage(L"confirmation-unavailable", id));
            else
            {
                if (replyKind != ReplyKind::EarlyAllow)
                    Send(replyWriter, WorkspacesCli::ApprovalMessage(L"warning-shown", id));
                if (replyKind == ReplyKind::DuplicateShown)
                    Send(replyWriter, WorkspacesCli::ApprovalMessage(L"warning-shown", id));
                auto response = WorkspacesCli::ApprovalMessage(L"elevation-response",
                                                               replyKind == ReplyKind::WrongId ? L"{682A3A9E-D18D-4DC6-B2AF-E6ABD92F42A9}" : id);
                response.SetNamedValue(L"choice", json::value(replyKind == ReplyKind::Skip ? L"skip" : replyKind == ReplyKind::InvalidChoice ? L"yes" :
                                                                                                                                               L"run"));
                Send(replyWriter, response);
            }
            Assert::IsTrue(future.wait_for(std::chrono::seconds(3)) == std::future_status::ready);
            return future.get();
        }
    }

    TEST_CLASS (CliApprovalTests)
    {
    public:
        TEST_METHOD (EnterDefaultsToSkipAndAllowRequiresEnter)
        {
            WorkspacesCli::ApprovalInput empty;
            Assert::IsTrue(empty.Push(L'\r') == LaunchDecision::Skipped);
            WorkspacesCli::ApprovalInput allow;
            Assert::IsFalse(allow.Push(L'A').has_value());
            Assert::IsTrue(allow.Push(L'\r') == LaunchDecision::Approved);
            WorkspacesCli::ApprovalInput skip;
            Assert::IsFalse(skip.Push(L's').has_value());
            Assert::IsTrue(skip.Push(L'\r') == LaunchDecision::Skipped);
        }

        TEST_METHOD (InvalidInputNeverBecomesApprovalByTruncation)
        {
            WorkspacesCli::ApprovalInput input;
            for (auto value : std::wstring(L"allow"))
                Assert::IsFalse(input.Push(value).has_value());
            Assert::IsFalse(input.Push(L'\r').has_value());
            Assert::IsTrue(input.InvalidChoice());
            Assert::IsTrue(input.Push(L'\r') == LaunchDecision::Skipped);
            WorkspacesCli::ApprovalInput longInput;
            for (int i = 0; i < 100; ++i)
                Assert::IsFalse(longInput.Push(L'A').has_value());
            for (int i = 0; i < 100; ++i)
                Assert::IsFalse(longInput.Push(L'\b').has_value());
            Assert::IsFalse(longInput.Push(L'\r').has_value());
        }

        TEST_METHOD (EofAndCancellationAreNotUserSkip)
        {
            WorkspacesCli::ApprovalInput input;
            Assert::IsTrue(input.Push(26) == LaunchDecision::UiUnavailable);
            Assert::IsTrue(input.Push(4) == LaunchDecision::UiUnavailable);
            Assert::IsTrue(input.Push(3) == LaunchDecision::Canceled);
            Assert::IsTrue(input.Push(27) == LaunchDecision::Canceled);
        }

        TEST_METHOD (UntrustedDisplayTextCannotInjectTerminalControls)
        {
            const std::wstring value = L"app\x001b[2J\nname\u202Eexe";
            const auto escaped = WorkspacesCli::EscapeApprovalText(value);
            Assert::AreEqual(std::wstring(L"app\\u001B[2J\\u000Aname\\u202Eexe"), escaped);
            Assert::AreEqual(std::wstring(L"\u8bb0\u4e8b\u672c"), WorkspacesCli::EscapeApprovalText(L"\u8bb0\u4e8b\u672c"));
        }

        TEST_METHOD (AllowAndSkipUseOneTimeShownRequest)
        {
            Assert::IsTrue(Exchange(ReplyKind::Allow) == LaunchDecision::Approved);
            Assert::IsTrue(Exchange(ReplyKind::Skip) == LaunchDecision::Skipped);
        }

        TEST_METHOD (EarlyOrMismatchedApprovalIsRejected)
        {
            Assert::IsTrue(Exchange(ReplyKind::EarlyAllow) == LaunchDecision::InvalidResponse);
            Assert::IsTrue(Exchange(ReplyKind::WrongId) == LaunchDecision::InvalidResponse);
            Assert::IsTrue(Exchange(ReplyKind::InvalidChoice) == LaunchDecision::InvalidResponse);
            Assert::IsTrue(Exchange(ReplyKind::DuplicateShown) == LaunchDecision::InvalidResponse);
        }

        TEST_METHOD (MissingPresenterAndDisconnectFailClosed)
        {
            Assert::IsTrue(Exchange(ReplyKind::Unavailable) == LaunchDecision::UiUnavailable);
            Assert::IsTrue(Exchange(ReplyKind::Disconnect) == LaunchDecision::UiUnavailable);
        }

        TEST_METHOD (CancellationInterruptsPendingApproval)
        {
            Assert::IsTrue(Exchange(ReplyKind::Cancel) == LaunchDecision::Canceled);
        }

        TEST_METHOD (FramedMessagesRemainSeparateAndBounded)
        {
            auto pipe = WorkspacesCli::ApprovalPipe::Create();
            WorkspacesCli::ApprovalReader reader(pipe.read.get());
            WorkspacesCli::ApprovalWriter writer(pipe.write.get());
            const std::wstring id = L"{682A3A9E-D18D-4DC6-B2AF-E6ABD92F42A9}";
            const auto message = WorkspacesCli::ApprovalMessage(L"warning-shown", id);
            Send(writer, message);
            Assert::AreEqual(id, std::wstring(Receive(reader).GetNamedString(L"requestId")));
            Send(writer, message);
            Assert::AreEqual(id, std::wstring(Receive(reader).GetNamedString(L"requestId")));
            json::JsonObject oversized;
            oversized.SetNamedValue(L"value", json::value(std::wstring(WorkspacesCli::MaxApprovalBytes, L'x')));
            Assert::ExpectException<WorkspacesCli::Error>([&] { writer.Start(oversized); });
        }

        TEST_METHOD (RedirectedConsoleDoesNotReadPipedApproval)
        {
            auto input = WorkspacesCli::ApprovalPipe::Create();
            auto output = WorkspacesCli::ApprovalPipe::Create();
            WorkspacesCli::ConsoleApproval console(input.read.get(), output.write.get(), {});
            json::JsonObject request;
            Assert::IsFalse(console.Begin(request));
        }

        TEST_METHOD (TruncatedAndOversizedFramesFailClosed)
        {
            for (const DWORD length : { DWORD{ 0 }, WorkspacesCli::MaxApprovalBytes + 1, DWORD{ 20 } })
            {
                auto pipe = WorkspacesCli::ApprovalPipe::Create();
                WorkspacesCli::ApprovalReader reader(pipe.read.get());
                wil::unique_handle ready(CreateEventW(nullptr, TRUE, FALSE, nullptr));
                OVERLAPPED operation{};
                operation.hEvent = ready.get();
                DWORD written{};
                if (!WriteFile(pipe.write.get(), &length, sizeof(length), &written, &operation))
                {
                    Assert::AreEqual(DWORD{ ERROR_IO_PENDING }, GetLastError());
                    Assert::IsTrue(GetOverlappedResult(pipe.write.get(), &operation, &written, TRUE) != FALSE);
                }
                if (length == 20)
                {
                    Assert::IsFalse(reader.Poll().has_value());
                    pipe.write.reset();
                }
                Assert::ExpectException<WorkspacesCli::Error>([&] { reader.Poll(); });
            }
        }

        TEST_METHOD (NoninteractiveFrontendReturnsUnavailableNotSkip)
        {
            auto requests = WorkspacesCli::ApprovalPipe::Create();
            auto replies = WorkspacesCli::ApprovalPipe::Create();
            auto fakeInput = WorkspacesCli::ApprovalPipe::Create();
            WorkspacesCli::ConsoleApproval console(fakeInput.read.get(), fakeInput.write.get(), {});
            wil::unique_handle cancel(CreateEventW(nullptr, TRUE, FALSE, nullptr));
            WorkspacesCli::FrontendApproval frontend(requests.read.get(), replies.write.get(), console, cancel.get());
            WorkspacesCli::ApprovalWriter writer(requests.write.get());
            WorkspacesCli::ApprovalReader reader(replies.read.get());
            auto message = WorkspacesCli::ApprovalMessage(L"elevation-warning", L"{682A3A9E-D18D-4DC6-B2AF-E6ABD92F42A9}");
            Send(writer, message);
            const auto deadline = GetTickCount64() + 3000;
            std::optional<json::JsonObject> reply;
            while (GetTickCount64() < deadline && !reply)
            {
                frontend.Poll();
                reply = reader.Poll();
                Sleep(1);
            }
            Assert::IsTrue(reply.has_value());
            Assert::AreEqual(std::wstring(L"confirmation-unavailable"), std::wstring(reply->GetNamedString(L"type")));
            Assert::IsFalse(reply->HasKey(L"choice"));
        }

        TEST_METHOD (SkippedAndUnavailableAreNotUacDenial)
        {
            WorkspacesData::WorkspacesProject project{};
            WorkspacesData::WorkspacesProject::Application app{};
            app.id = L"test-app";
            project.apps.push_back(app);
            WorkspacesData::LaunchingAppStateMap states;
            states.emplace(app, WorkspacesData::LaunchingAppState{ app, nullptr, LaunchingState::Skipped });
            int exitCode{};
            auto result = WorkspacesCli::LaunchResult(project, states, L"test", exitCode, { { app.id, ERROR_CANCELLED } }, { { app.id, LaunchDecision::Skipped } });
            auto item = result.GetNamedObject(L"result").GetNamedArray(L"applications").GetObjectAt(0);
            Assert::AreEqual(std::wstring(L"skipped"), std::wstring(item.GetNamedString(L"state")));
            Assert::AreEqual(std::wstring(L"skipped"), std::wstring(item.GetNamedString(L"code")));
            Assert::IsFalse(item.HasKey(L"nativeError"));
            Assert::AreEqual(10, exitCode);
            states.at(app).state = LaunchingState::Failed;
            result = WorkspacesCli::LaunchResult(project, states, L"test", exitCode, { { app.id, ERROR_NOT_READY } }, { { app.id, LaunchDecision::UiUnavailable } });
            item = result.GetNamedObject(L"result").GetNamedArray(L"applications").GetObjectAt(0);
            Assert::AreEqual(std::wstring(L"confirmationRequired"), std::wstring(item.GetNamedString(L"code")));
            states.at(app).state = LaunchingState::Canceled;
            result = WorkspacesCli::LaunchResult(project, states, L"test", exitCode, { { app.id, ERROR_CANCELLED } }, { { app.id, LaunchDecision::Approved } });
            item = result.GetNamedObject(L"result").GetNamedArray(L"applications").GetObjectAt(0);
            Assert::AreEqual(std::wstring(L"consentDenied"), std::wstring(item.GetNamedString(L"code")));
        }
    };
}
