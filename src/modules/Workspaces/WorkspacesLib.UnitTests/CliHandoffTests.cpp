// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#include "pch.h"
#include <WorkspacesCLI/WorkerHandoff.h>
#include <WorkspacesLib/CliCommands.h>
#include <wil/resource.h>
#include <filesystem>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace WorkspacesLibUnitTests
{
    TEST_CLASS (CliHandoffTests)
    {
    public:
        TEST_METHOD (CurrentProcessIdentityMatchesItself)
        {
            Assert::IsTrue(WorkspacesCli::SameUserSession(GetCurrentProcess(), GetCurrentProcess()));
        }

        TEST_METHOD (InvalidProcessAndOperationFailClosed)
        {
            Assert::ExpectException<WorkspacesCli::Error>([] {
                WorkspacesCli::IsMediumProcess(nullptr);
            });
            Assert::ExpectException<WorkspacesCli::Error>([] {
                WorkspacesCli::ReceiveWorkerHandles(L"..\\not-a-guid", GetCurrentProcessId());
            });
            Assert::ExpectException<WorkspacesCli::Error>([] {
                WorkspacesCli::ReceiveWorkerHandles(L"{EF762D6C-EDBF-4C45-98F2-878BBF67583F}", 0);
            });
        }

        TEST_METHOD (CanceledOrExpiredStartupDoesNotCreateAWorker)
        {
            wil::unique_handle cancel(CreateEventW(nullptr, TRUE, TRUE, nullptr));
            Assert::IsTrue(static_cast<bool>(cancel));
            WorkspacesCli::WorkerHandles handles;
            handles.cancel = cancel.get();
            try
            {
                WorkspacesCli::StartMediumWorker(L"must-not-start.exe",
                                                 L"{EF762D6C-EDBF-4C45-98F2-878BBF67583F}",
                                                 handles,
                                                 GetTickCount64() + 10000);
                Assert::Fail(L"Canceled startup must not proceed.");
            }
            catch (const WorkspacesCli::Error& error)
            {
                Assert::AreEqual(12, error.exitCode);
            }
            ResetEvent(cancel.get());
            try
            {
                WorkspacesCli::StartMediumWorker(L"must-not-start.exe",
                                                 L"{EF762D6C-EDBF-4C45-98F2-878BBF67583F}",
                                                 handles,
                                                 0);
                Assert::Fail(L"Expired startup must not proceed.");
            }
            catch (const WorkspacesCli::Error& error)
            {
                Assert::AreEqual(9, error.exitCode);
            }
        }

#ifdef _DEBUG
        TEST_METHOD (ExplorerWorkerReceivesOnlyExpectedCapabilitiesAndDecisions)
        {
            wchar_t module[32768]{};
            Assert::IsTrue(GetModuleFileNameW(reinterpret_cast<HMODULE>(&__ImageBase), module, ARRAYSIZE(module)) > 0);
            const auto executable = std::filesystem::path(module).parent_path() / L"WorkspacesCliHandoffFixture.exe";
            Assert::IsTrue(std::filesystem::is_regular_file(executable));
            SECURITY_ATTRIBUTES attributes{ sizeof(attributes), nullptr, TRUE };
            wil::unique_handle read, write;
            Assert::IsTrue(CreatePipe(read.put(), write.put(), &attributes, 4096) != FALSE);
            Assert::IsTrue(SetHandleInformation(read.get(), HANDLE_FLAG_INHERIT, 0) != FALSE);
            wil::unique_handle input(CreateFileW(L"NUL", GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, &attributes, OPEN_EXISTING, 0, nullptr));
            STARTUPINFOW startup{ sizeof(startup) };
            startup.dwFlags = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW;
            startup.wShowWindow = SW_HIDE;
            startup.hStdInput = input.get();
            startup.hStdOutput = write.get();
            startup.hStdError = write.get();
            auto command = L"\"" + executable.wstring() + L"\" --parent all";
            wil::unique_process_information process;
            Assert::IsTrue(CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr, TRUE, CREATE_NEW_CONSOLE, nullptr, nullptr, &startup, &process) != FALSE);
            write.reset();
            auto cleanup = wil::scope_exit([&] {
                if (WaitForSingleObject(process.hProcess, 0) == WAIT_TIMEOUT)
                {
                    TerminateProcess(process.hProcess, 1);
                    WaitForSingleObject(process.hProcess, 2000);
                }
            });
            Assert::AreEqual(DWORD{ WAIT_OBJECT_0 }, WaitForSingleObject(process.hProcess, 35000));
            DWORD exitCode{};
            Assert::IsTrue(GetExitCodeProcess(process.hProcess, &exitCode) != FALSE);
            char buffer[4096]{};
            DWORD received{};
            Assert::IsTrue(ReadFile(read.get(), buffer, sizeof(buffer), &received, nullptr) != FALSE);
            const std::string output(buffer, received);
            Microsoft::VisualStudio::CppUnitTestFramework::Logger::WriteMessage(output.c_str());
            Assert::AreEqual(DWORD{ 0 }, exitCode);
            const auto result = json::JsonObject::Parse(winrt::to_hstring(output));
            Assert::IsTrue(result.GetNamedBoolean(L"passed"));
            const auto cases = result.GetNamedArray(L"cases");
            Assert::AreEqual(3u, cases.Size());
            for (const auto& value : cases)
            {
                const auto item = value.GetObjectW();
                Assert::IsTrue(item.GetNamedBoolean(L"medium"));
                Assert::IsTrue(item.GetNamedBoolean(L"readOnlySnapshot"));
                Assert::IsTrue(item.GetNamedBoolean(L"readOnlyCancel"));
                Assert::IsTrue(item.GetNamedBoolean(L"syncOnlyOwner"));
                Assert::IsTrue(item.GetNamedBoolean(L"passed"));
            }
        }
#endif
    };
}
