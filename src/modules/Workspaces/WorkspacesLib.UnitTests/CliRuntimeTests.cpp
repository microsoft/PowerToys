// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#include "pch.h"
#include <wil/resource.h>
#include <filesystem>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace WorkspacesLibUnitTests
{
    TEST_CLASS (CliRuntimeTests)
    {
        static void Run(const std::wstring& scenario, bool workerError = false)
        {
            wchar_t module[32768]{};
            Assert::IsTrue(GetModuleFileNameW(reinterpret_cast<HMODULE>(&__ImageBase), module, ARRAYSIZE(module)) > 0);
            auto executable = std::filesystem::path(module).parent_path() / L"WorkspacesCliRuntimeFixture.exe";
            Assert::IsTrue(std::filesystem::is_regular_file(executable));
            std::filesystem::path staged;
            auto removeStaged = wil::scope_exit([&] {
                if (!staged.empty())
                    DeleteFileW(staged.c_str());
            });
            if (scenario.starts_with(L"ui-startup"))
            {
                GUID guid{};
                Assert::IsTrue(SUCCEEDED(CoCreateGuid(&guid)));
                wchar_t id[40]{};
                StringFromGUID2(guid, id, ARRAYSIZE(id));
                // The real helper resolves the UI beside its executable, as it does in the CLI worker.
                staged = executable.parent_path().parent_path().parent_path() / (std::wstring(L"WorkspacesUiStartupTest-") + id + L".exe");
                Assert::IsTrue(CopyFileW(executable.c_str(), staged.c_str(), TRUE) != FALSE);
                executable = staged;
            }
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
            Assert::AreEqual(DWORD{ WAIT_OBJECT_0 }, WaitForSingleObject(process.hProcess, scenario.starts_with(L"ui-startup") ? 25000 : 12000), scenario.c_str());
            DWORD code{};
            GetExitCodeProcess(process.hProcess, &code);
            char buffer[4096]{};
            DWORD received{};
            Assert::IsTrue(ReadFile(read.get(), buffer, sizeof(buffer), &received, nullptr) != FALSE);
            const std::string output(buffer, received);
            Microsoft::VisualStudio::CppUnitTestFramework::Logger::WriteMessage(output.c_str());
            Assert::AreEqual(DWORD{ workerError ? 2ul : 0ul }, code, scenario.c_str());
            const auto result = json::JsonObject::Parse(winrt::to_hstring(output));
            if (workerError)
            {
                Assert::AreEqual(std::wstring(L"invalidArguments"), std::wstring(result.GetNamedObject(L"error").GetNamedString(L"code")));
                Assert::AreEqual(4u, result.Size());
                return;
            }
            Assert::IsTrue(result.GetNamedBoolean(L"passed"));
            Assert::AreEqual(1u, result.Size(), L"Only final JSON, no prompt content, may reach stdout.");
        }

    public:
        TEST_METHOD (UnconfirmedArrangementExplanationUsesLocalizedResources)
        {
            Run(L"launched-localization");
        }

        TEST_METHOD (SharedUiHelperDeliversFirstWarningAfterColdStartup)
        {
            Run(L"ui-startup");
        }

        TEST_METHOD (SharedUiHelperStartupCanBeCanceledWithoutAWarning)
        {
            Run(L"ui-startup-canceled");
        }

        TEST_METHOD (ConfirmationUiUsesWaitOnlyAccessToItsOwner)
        {
            Run(L"ui-startup-restricted-owner");
        }

        TEST_METHOD (WorkerResourceFailureKeepsCombinedOutputJsonOnly)
        {
            Run(L"worker-resource-error", true);
        }

        TEST_METHOD (ConsoleCodePageRestoredOnSuccessAndFailure)
        {
            Run(L"code-page");
        }

    };
}
