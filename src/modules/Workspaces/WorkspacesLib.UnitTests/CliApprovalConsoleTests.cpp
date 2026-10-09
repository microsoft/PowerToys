// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#include "pch.h"
#include <wil/resource.h>
#include <filesystem>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace WorkspacesLibUnitTests
{
    TEST_CLASS (CliApprovalConsoleTests)
    {
        static void Run(const std::wstring& scenario)
        {
            wchar_t module[32768]{};
            Assert::IsTrue(GetModuleFileNameW(reinterpret_cast<HMODULE>(&__ImageBase), module, ARRAYSIZE(module)) > 0);
            const auto executable = std::filesystem::path(module).parent_path() / L"WorkspacesCliApprovalFixture.exe";
            Assert::IsTrue(std::filesystem::is_regular_file(executable));
            SECURITY_ATTRIBUTES sa{ sizeof(sa), nullptr, TRUE };
            wil::unique_handle read, write;
            Assert::IsTrue(CreatePipe(read.put(), write.put(), &sa, 4096) != FALSE);
            Assert::IsTrue(SetHandleInformation(read.get(), HANDLE_FLAG_INHERIT, 0) != FALSE);
            wil::unique_handle input(CreateFileW(L"NUL", GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, &sa, OPEN_EXISTING, 0, nullptr));
            STARTUPINFOW startup{ sizeof(startup) };
            startup.dwFlags = STARTF_USESHOWWINDOW | STARTF_USESTDHANDLES;
            startup.wShowWindow = SW_HIDE;
            startup.hStdInput = input.get();
            startup.hStdOutput = write.get();
            startup.hStdError = write.get();
            auto command = L"\"" + executable.wstring() + L"\" " + scenario;
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
            Assert::AreEqual(DWORD{ WAIT_OBJECT_0 }, WaitForSingleObject(process.hProcess, 12000), scenario.c_str());
            DWORD code{};
            GetExitCodeProcess(process.hProcess, &code);
            char buffer[4096]{};
            DWORD received{};
            Assert::IsTrue(ReadFile(read.get(), buffer, sizeof(buffer), &received, nullptr) != FALSE);
            const std::string output(buffer, received);
            Microsoft::VisualStudio::CppUnitTestFramework::Logger::WriteMessage(output.c_str());
            Assert::AreEqual(DWORD{ 0 }, code, scenario.c_str());
            const auto result = json::JsonObject::Parse(winrt::to_hstring(output));
            Assert::IsTrue(result.GetNamedBoolean(L"passed"));
            Assert::AreEqual(1u, result.Size(), L"Only final JSON, no prompt content, may reach stdout.");
        }

    public:
        TEST_METHOD (IsolatedConsoleAllowsOnceAndSkipsByDefault)
        {
            Run(L"allow");
            Run(L"skip");
            Run(L"default");
        }
        TEST_METHOD (IsolatedConsoleRejectsInvalidInputAndRestoresMode)
        {
            Run(L"invalid");
        }
        TEST_METHOD (IsolatedConsoleHandlesEofAndRedirection)
        {
            Run(L"eof");
            Run(L"redirected-input");
            Run(L"redirected-error");
        }
        TEST_METHOD (IsolatedConsoleRespondsToCancellationAndDeadline)
        {
            Run(L"cancel");
            Run(L"timeout");
        }
        TEST_METHOD (IsolatedConsoleDoesNotReuseApprovalOrTypeahead)
        {
            Run(L"two-requests");
            Run(L"typeahead");
        }

        TEST_METHOD (InputDuringPromptOutputCannotApproveWithoutFreshInput)
        {
            Run(L"during-output-timeout");
        }

        TEST_METHOD (InputDuringPromptOutputDoesNotOverrideFreshSkip)
        {
            Run(L"during-output-skip");
        }

        TEST_METHOD (InputDuringPromptOutputRequiresFreshAllow)
        {
            Run(L"during-output-allow");
        }
    };
}
