#include "pch.h"

#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include <keyboardmanager/KeyboardManagerEngineLibrary/KeyboardEventHandlers.h>
#include <filesystem>
#include <sddl.h>

#pragma comment(lib, "advapi32.lib")

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace
{
    constexpr DWORD gracefulExitCode = 42;

    struct TargetContext
    {
        HANDLE closeReceived;
        bool ignoreClose;
    };

    struct RevertImpersonation
    {
        ~RevertImpersonation()
        {
            RevertToSelf();
        }
    };

    LRESULT CALLBACK TargetWindowProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
    {
        if (message == WM_CLOSE)
        {
            const auto context = reinterpret_cast<TargetContext*>(GetWindowLongPtrW(window, GWLP_USERDATA));
            SetEvent(context->closeReceived);
            if (context->ignoreClose)
            {
                // An unresponsive target must not block close requests to other instances.
                Sleep(INFINITE);
            }
            ExitProcess(gracefulExitCode);
        }
        return DefWindowProcW(window, message, wParam, lParam);
    }
}

// A real, isolated child process hosted by a uniquely named copy of rundll32.
// Its window stays hidden: these engine tests never activate a desktop window.
extern "C" __declspec(dllexport) void CALLBACK CloseAndEndTestTargetW(HWND, HINSTANCE instance, LPWSTR command, int)
{
    const std::wstring arguments{ command };
    const auto separator = arguments.find(L' ');
    const auto eventName = arguments.substr(0, separator);
    const auto mode = arguments.substr(separator + 1);
    winrt::handle ready{ OpenEventW(EVENT_MODIFY_STATE, FALSE, eventName.c_str()) };
    if (!ready)
    {
        ExitProcess(1);
    }

    if (mode == L"no-window")
    {
        // Remove rundll32's hidden host window too, so ShowWindow cannot activate it.
        EnumThreadWindows(
            GetCurrentThreadId(), [](HWND window, LPARAM) -> BOOL {
                DestroyWindow(window);
                return TRUE;
            },
            0);
        SetEvent(ready.get());
        Sleep(INFINITE);
    }

    winrt::handle closeReceived{ OpenEventW(EVENT_MODIFY_STATE, FALSE, (eventName + L"-close").c_str()) };
    TargetContext context{ closeReceived.get(), mode == L"ignore" };
    if (mode != L"no-window")
    {
        WNDCLASSW windowClass{};
        windowClass.lpfnWndProc = TargetWindowProc;
        windowClass.hInstance = instance;
        windowClass.lpszClassName = L"KeyboardManagerCloseAndEndTest";
        if (!RegisterClassW(&windowClass))
        {
            ExitProcess(2);
        }
        const HWND window = CreateWindowExW(0, windowClass.lpszClassName, L"", WS_OVERLAPPED, 0, 0, 0, 0, nullptr, nullptr, instance, nullptr);
        if (!window)
        {
            ExitProcess(3);
        }
        SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(&context));
    }
    SetEvent(ready.get());
    MSG message;
    while (GetMessageW(&message, nullptr, 0, 0) > 0)
    {
        DispatchMessageW(&message);
    }
}

namespace RemappingLogicTests
{
    TEST_CLASS (CloseAndEndTaskTests)
    {
        std::filesystem::path executable;
        std::filesystem::path frameworkCopy;
        std::wstring modulePath;
        std::vector<winrt::handle> processes;
        std::vector<winrt::handle> closeNotifications;
        unsigned int nextEvent = 0;

        std::wstring EventName()
        {
            return executable.stem().wstring() + L"-" + std::to_wstring(nextEvent++);
        }

        std::wstring Arguments(const std::wstring& eventName, const wchar_t* mode)
        {
            return L"\"" + std::filesystem::path{ modulePath }.generic_wstring() + L"\",CloseAndEndTestTarget " + eventName + L" " + mode;
        }

        DWORD CreateTargetProcess(const std::wstring& file, const std::wstring& arguments, const std::wstring& workingDirectory)
        {
            auto command = L"\"" + file + L"\" " + arguments;
            STARTUPINFOW startup{ sizeof(startup) };
            PROCESS_INFORMATION info{};
            if (!CreateProcessW(file.c_str(), command.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, workingDirectory.c_str(), &startup, &info))
            {
                return GetLastError();
            }
            winrt::handle thread{ info.hThread };
            processes.emplace_back(info.hProcess);
            return ERROR_SUCCESS;
        }

        void WaitForTargetReady(HANDLE ready, HANDLE process)
        {
            const HANDLE startupResults[]{ ready, process };
            if (WaitForMultipleObjects(ARRAYSIZE(startupResults), startupResults, FALSE, 10000) != WAIT_OBJECT_0)
            {
                DWORD code = 0;
                GetExitCodeProcess(process, &code);
                Assert::Fail((L"Target failed to start (exit code " + std::to_wstring(code) + L")").c_str());
            }
        }

        HANDLE StartTarget(const wchar_t* mode = L"ignore")
        {
            const auto eventName = EventName();
            winrt::handle ready{ CreateEventW(nullptr, TRUE, FALSE, eventName.c_str()) };
            Assert::IsTrue(static_cast<bool>(ready));
            closeNotifications.emplace_back(CreateEventW(nullptr, TRUE, FALSE, (eventName + L"-close").c_str()));
            Assert::IsTrue(static_cast<bool>(closeNotifications.back()));
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), CreateTargetProcess(executable.wstring(), Arguments(eventName, mode), std::filesystem::path{ modulePath }.parent_path().wstring()));
            WaitForTargetReady(ready.get(), processes.back().get());
            return processes.back().get();
        }

        void RunShortcut(Shortcut::ProgramAlreadyRunningAction action, const std::wstring& arguments = {}, const KeyboardEventHandlers::ProgramLauncher::LaunchCallback& launchProgram = {})
        {
            Shortcut shortcut;
            shortcut.runProgramFilePath = executable.wstring();
            shortcut.runProgramArgs = arguments;
            shortcut.startWindowType = Shortcut::StartWindowType::Minimized;
            shortcut.alreadyRunningAction = action;
            unsigned int launchCount = 0;
            KeyboardEventHandlers::CreateOrShowProcessForShortcut(shortcut, [&](const auto& file, const auto& args, const auto& workingDirectory) {
                ++launchCount;
                if (launchProgram)
                {
                    launchProgram(file, args, workingDirectory);
                }
            });
            Assert::AreEqual(launchProgram ? 1u : 0u, launchCount);
        }

        DWORD ExitCode(HANDLE process)
        {
            Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), WaitForSingleObject(process, 10000));
            DWORD code = 0;
            Assert::IsTrue(GetExitCodeProcess(process, &code) != FALSE);
            return code;
        }

        void LaunchWithShortcut(Shortcut::ProgramAlreadyRunningAction action)
        {
            const auto eventName = EventName();
            winrt::handle ready{ CreateEventW(nullptr, TRUE, FALSE, eventName.c_str()) };
            Assert::IsTrue(static_cast<bool>(ready));
            bool launchedExpectedTarget = false;
            DWORD launchError = ERROR_SUCCESS;
            // CI may have no Explorer shell. Keep the real engine decisions and resolved
            // launch parameters, but create the child directly and retain its cleanup handle.
            RunShortcut(action, Arguments(eventName, L"no-window"), [&](const auto& file, const auto& arguments, const auto& workingDirectory) {
                launchedExpectedTarget = file == executable.wstring();
                launchError = CreateTargetProcess(file, arguments, workingDirectory);
            });
            Assert::IsTrue(launchedExpectedTarget);
            Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), launchError);
            WaitForTargetReady(ready.get(), processes.back().get());
        }

    public:
        TEST_METHOD_INITIALIZE (Initialize)
        {
            HMODULE module = nullptr;
            Assert::IsTrue(GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                                              reinterpret_cast<LPCWSTR>(&CloseAndEndTestTargetW),
                                              &module) != FALSE);
            wchar_t path[MAX_PATH];
            Assert::IsTrue(GetModuleFileNameW(module, path, ARRAYSIZE(path)) != 0);
            modulePath = path;
            Assert::IsTrue(GetTempPathW(ARRAYSIZE(path), path) != 0);
            const auto name = L"kbm-close-" + std::to_wstring(GetCurrentProcessId()) + L"-" + std::to_wstring(GetTickCount64());
            const auto directory = std::filesystem::path{ path } / name;
            Assert::IsTrue(CreateDirectoryW(directory.c_str(), nullptr) != FALSE);
            executable = directory / (name + L".exe");
            // rundll32 must be able to load this test DLL outside the test host.
#if defined(_M_ARM64)
            const wchar_t* frameworkName = L"Microsoft.VisualStudio.TestTools.CppUnitTestFramework.arm64.dll";
#else
            const wchar_t* frameworkName = L"Microsoft.VisualStudio.TestTools.CppUnitTestFramework.x64.dll";
#endif
            Assert::IsTrue(GetModuleFileNameW(GetModuleHandleW(frameworkName), path, ARRAYSIZE(path)) != 0);
            frameworkCopy = directory / frameworkName;
            Assert::IsTrue(CopyFileW(path, frameworkCopy.c_str(), TRUE) != FALSE);
            Assert::IsTrue(GetSystemDirectoryW(path, ARRAYSIZE(path)) != 0);
            Assert::IsTrue(CopyFileW((std::filesystem::path{ path } / L"rundll32.exe").c_str(), executable.c_str(), TRUE) != FALSE);
        }

        TEST_METHOD_CLEANUP (Cleanup)
        {
            for (const auto& process : processes)
            {
                if (process && WaitForSingleObject(process.get(), 0) == WAIT_TIMEOUT)
                {
                    TerminateProcess(process.get(), 99);
                    WaitForSingleObject(process.get(), 10000);
                }
            }
            processes.clear();
            closeNotifications.clear();
            if (!executable.empty())
            {
                Assert::IsTrue(DeleteFileW(executable.c_str()) != FALSE);
                Assert::IsTrue(DeleteFileW(frameworkCopy.c_str()) != FALSE);
                Assert::IsTrue(RemoveDirectoryW(executable.parent_path().c_str()) != FALSE);
            }
        }

        TEST_METHOD (GracefulExit_ShouldNotBeTerminated)
        {
            const HANDLE process = StartTarget(L"graceful");
            RunShortcut(Shortcut::ProgramAlreadyRunningAction::CloseAndEndTask);
            Assert::AreEqual(gracefulExitCode, ExitCode(process));
        }

        TEST_METHOD (IgnoredClose_ShouldEscalateAfterGracePeriod_Asynchronously)
        {
            const HANDLE process = StartTarget();
            const auto started = GetTickCount64();
            RunShortcut(Shortcut::ProgramAlreadyRunningAction::CloseAndEndTask);
            Assert::IsTrue(GetTickCount64() - started < 800);
            Assert::AreEqual(static_cast<DWORD>(WAIT_TIMEOUT), WaitForSingleObject(process, 500));
            Assert::AreEqual(static_cast<DWORD>(0), ExitCode(process));
            Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), WaitForSingleObject(closeNotifications[0].get(), 0));
            Assert::IsTrue(GetTickCount64() - started >= 900);
        }

        TEST_METHOD (MultipleInstances_ShouldShareOneDeadline_AndAllReceiveClose)
        {
            const HANDLE first = StartTarget();
            const HANDLE graceful = StartTarget(L"graceful");
            const HANDLE last = StartTarget();
            const auto started = GetTickCount64();
            RunShortcut(Shortcut::ProgramAlreadyRunningAction::CloseAndEndTask);
            Assert::AreEqual(gracefulExitCode, ExitCode(graceful));
            Assert::AreEqual(static_cast<DWORD>(0), ExitCode(first));
            Assert::AreEqual(static_cast<DWORD>(0), ExitCode(last));
            Assert::IsTrue(GetTickCount64() - started < 1800);
            for (const auto& notification : closeNotifications)
            {
                Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), WaitForSingleObject(notification.get(), 0));
            }
        }

        TEST_METHOD (InaccessibleProcess_ShouldBeSkipped)
        {
            const HANDLE process = StartTarget(L"graceful");
            // An elevated test host may have SeDebugPrivilege, which bypasses process DACLs.
            // Disable it only on this thread, leaving the process token untouched.
            winrt::handle token;
            Assert::IsTrue(OpenProcessToken(GetCurrentProcess(), TOKEN_DUPLICATE | TOKEN_QUERY, token.put()) != FALSE);
            winrt::handle impersonation;
            Assert::IsTrue(DuplicateTokenEx(token.get(), TOKEN_IMPERSONATE | TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, nullptr, SecurityImpersonation, TokenImpersonation, impersonation.put()) != FALSE);
            TOKEN_PRIVILEGES privileges{};
            privileges.PrivilegeCount = 1;
            Assert::IsTrue(LookupPrivilegeValueW(nullptr, SE_DEBUG_NAME, &privileges.Privileges[0].Luid) != FALSE);
            Assert::IsTrue(AdjustTokenPrivileges(impersonation.get(), FALSE, &privileges, 0, nullptr, nullptr) != FALSE);
            Assert::IsTrue(SetThreadToken(nullptr, impersonation.get()) != FALSE);
            RevertImpersonation revert;

            PSECURITY_DESCRIPTOR descriptor = nullptr;
            Assert::IsTrue(ConvertStringSecurityDescriptorToSecurityDescriptorW(
                               L"D:(D;;0x0001;;;WD)(A;;GA;;;WD)", SDDL_REVISION_1, &descriptor, nullptr) != FALSE);
            const BOOL secured = SetKernelObjectSecurity(process, DACL_SECURITY_INFORMATION, descriptor);
            LocalFree(descriptor);
            Assert::IsTrue(secured != FALSE);
            winrt::handle inaccessible{ OpenProcess(SYNCHRONIZE | PROCESS_TERMINATE, FALSE, GetProcessId(process)) };
            Assert::IsFalse(static_cast<bool>(inaccessible));

            Assert::IsTrue(KeyboardEventHandlers::CloseAndEndProcessesByName(executable.filename().wstring()));
            Assert::AreEqual(static_cast<DWORD>(WAIT_TIMEOUT), WaitForSingleObject(process, 1200));
            Assert::AreEqual(static_cast<DWORD>(WAIT_TIMEOUT), WaitForSingleObject(closeNotifications[0].get(), 0));

            // Matching but unverifiable processes must still suppress the launch path.
            descriptor = nullptr;
            Assert::IsTrue(ConvertStringSecurityDescriptorToSecurityDescriptorW(
                               L"D:(D;;0x1000;;;WD)(A;;GA;;;WD)", SDDL_REVISION_1, &descriptor, nullptr) != FALSE);
            const BOOL queryDenied = SetKernelObjectSecurity(process, DACL_SECURITY_INFORMATION, descriptor);
            LocalFree(descriptor);
            Assert::IsTrue(queryDenied != FALSE);
            winrt::handle unverifiable{ OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, GetProcessId(process)) };
            Assert::IsFalse(static_cast<bool>(unverifiable));
            Assert::IsTrue(KeyboardEventHandlers::CloseAndEndProcessesByName(executable.filename().wstring()));
            Assert::AreEqual(static_cast<DWORD>(WAIT_TIMEOUT), WaitForSingleObject(process, 1200));
            Assert::AreEqual(static_cast<DWORD>(WAIT_TIMEOUT), WaitForSingleObject(closeNotifications[0].get(), 0));
        }

        TEST_METHOD (AlreadyExitedProcess_ShouldNotBeTargeted)
        {
            const HANDLE process = StartTarget();
            Assert::IsTrue(TerminateProcess(process, 77) != FALSE);
            Assert::AreEqual(static_cast<DWORD>(77), ExitCode(process));
            Assert::IsFalse(KeyboardEventHandlers::CloseAndEndProcessesByName(executable.filename().wstring()));
        }

        TEST_METHOD (NewInstanceDuringGracePeriod_ShouldNotBeTargeted)
        {
            const HANDLE first = StartTarget();
            Assert::IsTrue(KeyboardEventHandlers::CloseAndEndProcessesByName(executable.filename().wstring()));
            Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), WaitForSingleObject(closeNotifications[0].get(), 10000));
            const HANDLE later = StartTarget(L"graceful");
            Assert::AreEqual(static_cast<DWORD>(0), ExitCode(first));
            Assert::AreEqual(static_cast<DWORD>(WAIT_TIMEOUT), WaitForSingleObject(later, 200));
        }

        TEST_METHOD (ReplacementBetweenSnapshotAndValidation_ShouldNotBeTargeted)
        {
            const HANDLE original = StartTarget();
            auto snapshot = KeyboardEventHandlers::ProcessCloser::SnapshotProcess(original);
            Assert::IsTrue(snapshot.has_value());
            Assert::IsTrue(TerminateProcess(original, 77) != FALSE);
            Assert::AreEqual(static_cast<DWORD>(77), ExitCode(original));

            const HANDLE replacement = StartTarget(L"graceful");
            const auto replacementIdentity = KeyboardEventHandlers::ProcessCloser::SnapshotProcess(replacement);
            Assert::IsTrue(replacementIdentity.has_value());
            Assert::AreEqual(snapshot->imagePath, replacementIdentity->imagePath);
            Assert::IsTrue(CompareFileTime(&snapshot->creationTime, &replacementIdentity->creationTime) < 0);
            // Windows cannot be asked to reuse a specific PID. Substitute the replacement's
            // PID in the stale snapshot to deterministically exercise that exact open gap.
            snapshot->pid = GetProcessId(replacement);
            KeyboardEventHandlers::ProcessCloser::CloseAndEndProcesses({ *snapshot });
            Assert::AreEqual(static_cast<DWORD>(WAIT_TIMEOUT), WaitForSingleObject(replacement, 1200));
            Assert::AreEqual(static_cast<DWORD>(WAIT_TIMEOUT), WaitForSingleObject(closeNotifications.back().get(), 0));
        }

        TEST_METHOD (DifferentExecutableIdentity_ShouldNotBeTargeted)
        {
            const HANDLE process = StartTarget(L"graceful");
            auto snapshot = KeyboardEventHandlers::ProcessCloser::SnapshotProcess(process);
            Assert::IsTrue(snapshot.has_value());
            // Same PID, creation time, and basename, but a different full image path.
            snapshot->imagePath = (executable.parent_path() / L"other" / executable.filename()).wstring();
            KeyboardEventHandlers::ProcessCloser::CloseAndEndProcesses({ *snapshot });
            Assert::AreEqual(static_cast<DWORD>(WAIT_TIMEOUT), WaitForSingleObject(process, 1200));
            Assert::AreEqual(static_cast<DWORD>(WAIT_TIMEOUT), WaitForSingleObject(closeNotifications.back().get(), 0));
        }

        TEST_METHOD (HandleWithoutQueryAccess_ShouldBeUnverifiable)
        {
            const HANDLE process = StartTarget(L"graceful");
            winrt::handle synchronizeOnly{ OpenProcess(SYNCHRONIZE, FALSE, GetProcessId(process)) };
            Assert::IsTrue(static_cast<bool>(synchronizeOnly));
            Assert::IsFalse(KeyboardEventHandlers::ProcessCloser::SnapshotProcess(synchronizeOnly.get()).has_value());
        }

        TEST_METHOD (NoMatchingProcess_ShouldReturnFalse)
        {
            Assert::IsFalse(KeyboardEventHandlers::CloseAndEndProcessesByName(executable.filename().wstring()));
        }

        TEST_METHOD (CloseAndEndTaskWithoutRunningTarget_ShouldLaunchNormally)
        {
            LaunchWithShortcut(Shortcut::ProgramAlreadyRunningAction::CloseAndEndTask);
            Assert::AreEqual(static_cast<size_t>(1), processes.size());
        }

        TEST_METHOD (StartAnother_ShouldLaunchWithoutClosingExistingProcess)
        {
            const HANDLE existing = StartTarget(L"no-window");
            LaunchWithShortcut(Shortcut::ProgramAlreadyRunningAction::StartAnother);
            Assert::AreEqual(static_cast<size_t>(2), processes.size());
            Assert::AreEqual(static_cast<DWORD>(WAIT_TIMEOUT), WaitForSingleObject(existing, 0));
        }

        TEST_METHOD (OtherExistingProcessActions_ShouldRetainTheirBehavior)
        {
            const HANDLE process = StartTarget(L"no-window");
            Assert::IsNull(KeyboardEventHandlers::FindMainWindow(GetProcessId(process), true));
            for (const auto action : { Shortcut::ProgramAlreadyRunningAction::DoNothing,
                                       Shortcut::ProgramAlreadyRunningAction::ShowWindow,
                                       Shortcut::ProgramAlreadyRunningAction::Close })
            {
                RunShortcut(action);
                Assert::AreEqual(static_cast<DWORD>(WAIT_TIMEOUT), WaitForSingleObject(process, 1200));
                Assert::AreEqual(static_cast<size_t>(1), KeyboardEventHandlers::GetProcessesIdByName(executable.filename().wstring()).size());
            }
            RunShortcut(Shortcut::ProgramAlreadyRunningAction::EndTask);
            Assert::AreEqual(static_cast<DWORD>(0), ExitCode(process));
        }
    };
}
