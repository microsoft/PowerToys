// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
// Tests token/handle/console transport only. No workspace or user application is launched.
#include <windows.h>
#include <objbase.h>
#include <iostream>
#include <filesystem>
#include <spdlog/sinks/null_sink.h>
#include <common/logger/logger.h>
#include <common/utils/elevation.h>
#include <WorkspacesCLI/WorkerHandoff.h>
#include <WorkspacesCLI/ApprovalChannel.h>
#include <WorkspacesCLI/ConsoleApproval.h>
#include <WorkspacesLib/OperationLifetime.h>

namespace
{
    void Check(bool condition)
    {
        if (!condition)
            throw std::runtime_error("Handoff fixture assertion failed");
    }

    std::filesystem::path OwnPath()
    {
        wchar_t path[32768]{};
        Check(GetModuleFileNameW(nullptr, path, ARRAYSIZE(path)) != 0);
        return path;
    }

    json::JsonObject Child(const std::wstring& id, DWORD parentPid)
    {
        auto handles = WorkspacesCli::ReceiveWorkerHandles(id, parentPid);
        handles.BindStandardStreams();
        WorkspacesCli::OperationLifetime lifetime(parentPid, 15000, handles.ownerLifetime.get());
        Check(WorkspacesCli::IsMediumProcess(GetCurrentProcess()));
        wil::unique_mapview_ptr<DWORD> data(static_cast<DWORD*>(MapViewOfFile(handles.snapshot.get(), FILE_MAP_READ, 0, 0, sizeof(DWORD))));
        Check(data && *data == 42);
        const auto writeView = MapViewOfFile(handles.snapshot.get(), FILE_MAP_WRITE, 0, 0, sizeof(DWORD));
        if (writeView)
            UnmapViewOfFile(writeView);
        Check(writeView == nullptr);
        Check(SetEvent(handles.cancel.get()) == FALSE);
        DWORD parentExit{};
        Check(GetExitCodeProcess(handles.ownerLifetime.get(), &parentExit) == FALSE);
        WorkspacesCli::WorkerApproval approval(handles.approvalRequests.get(), handles.approvalReplies.get(), [&] {
            return WaitForSingleObject(handles.cancel.get(), 0) == WAIT_OBJECT_0;
        });
        const auto decision = approval.Request(L"Controlled test", L"C:\\not-executed.exe", L"", { SignatureVerification::Status::Unsigned, TRUST_E_NOSIGNATURE, {}, {} });
        json::JsonObject result;
        result.SetNamedValue(L"medium", json::value(true));
        result.SetNamedValue(L"readOnlySnapshot", json::value(true));
        result.SetNamedValue(L"readOnlyCancel", json::value(true));
        result.SetNamedValue(L"syncOnlyOwner", json::value(true));
        result.SetNamedValue(L"decision", json::value(static_cast<int>(decision)));
        return result;
    }

    json::JsonObject Parent(const std::wstring& scenario)
    {
        wil::unique_handle mapping(CreateFileMappingW(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE, 0, sizeof(DWORD), nullptr));
        wil::unique_handle cancel(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        Check(mapping && cancel);
        wil::unique_mapview_ptr<DWORD> data(static_cast<DWORD*>(MapViewOfFile(mapping.get(), FILE_MAP_WRITE, 0, 0, sizeof(DWORD))));
        Check(static_cast<bool>(data));
        *data = 42;
        wil::unique_handle read, write;
        Check(CreatePipe(read.put(), write.put(), nullptr, 0) != FALSE);
        wil::unique_handle input(CreateFileW(L"NUL", GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, 0, nullptr));
        auto requests = WorkspacesCli::ApprovalPipe::Create();
        auto responses = WorkspacesCli::ApprovalPipe::Create();
        GUID guid{};
        Check(SUCCEEDED(CoCreateGuid(&guid)));
        wchar_t id[40]{};
        StringFromGUID2(guid, id, ARRAYSIZE(id));
        auto process = WorkspacesCli::StartMediumWorker(OwnPath().wstring(), id, { mapping.get(), cancel.get(), input.get(), write.get(), requests.write.get(), responses.read.get() }, GetTickCount64() + 10000);
        auto stop = wil::scope_exit([&] {
            if (WaitForSingleObject(process.hProcess, 0) == WAIT_TIMEOUT)
            {
                TerminateProcess(process.hProcess, 1);
                WaitForSingleObject(process.hProcess, 2000);
            }
        });
        Check(WorkspacesCli::SameUserSession(GetCurrentProcess(), process.hProcess));
        Check(WorkspacesCli::IsMediumProcess(process.hProcess));
        write.reset();
        requests.write.reset();
        responses.read.reset();
        wil::unique_handle consoleInput(CreateFileW(L"CONIN$", GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, 0, nullptr));
        wil::unique_handle consoleOutput(CreateConsoleScreenBuffer(GENERIC_READ | GENERIC_WRITE,
                                                                   FILE_SHARE_READ | FILE_SHARE_WRITE,
                                                                   nullptr,
                                                                   CONSOLE_TEXTMODE_BUFFER,
                                                                   nullptr));
        Check(consoleInput && consoleOutput);
        WorkspacesCli::ConsoleApproval prompt(consoleInput.get(), consoleOutput.get(), { L"Test only", L"App: ", L"Path: ", L"Args: ", L"Reason: ", L"Status: ", L"A or S: ", L"Invalid" });
        WorkspacesCli::FrontendApproval presenter(requests.read.get(), responses.write.get(), prompt, cancel.get());
        bool replied = false;
        std::string output;
        const auto deadline = GetTickCount64() + 10000;
        for (;;)
        {
            Check(GetTickCount64() < deadline);
            presenter.Poll();
            if (prompt.Active() && !replied)
            {
                replied = true;
                if (scenario == L"cancel")
                    SetEvent(cancel.get());
                else
                {
                    const auto choice = scenario == L"allow" ? L"A\r" : L"\r";
                    for (const auto value : std::wstring(choice))
                    {
                        INPUT_RECORD record{};
                        record.EventType = KEY_EVENT;
                        record.Event.KeyEvent.bKeyDown = TRUE;
                        record.Event.KeyEvent.wRepeatCount = 1;
                        record.Event.KeyEvent.uChar.UnicodeChar = value;
                        DWORD written{};
                        Check(WriteConsoleInputW(consoleInput.get(), &record, 1, &written) && written == 1);
                    }
                }
            }
            DWORD available{};
            if (PeekNamedPipe(read.get(), nullptr, 0, nullptr, &available, nullptr) && available)
            {
                char bytes[4096];
                DWORD count{};
                Check(ReadFile(read.get(), bytes, (std::min)(available, DWORD{ sizeof(bytes) }), &count, nullptr) != FALSE);
                output.append(bytes, count);
                continue;
            }
            if (WaitForSingleObject(process.hProcess, 10) == WAIT_OBJECT_0)
            {
                if (PeekNamedPipe(read.get(), nullptr, 0, nullptr, &available, nullptr) && available)
                    continue;
                break;
            }
        }
        DWORD exitCode{};
        Check(GetExitCodeProcess(process.hProcess, &exitCode) && exitCode == 0);
        const auto result = json::JsonObject::Parse(winrt::to_hstring(output));
        Check(result.GetNamedBoolean(L"medium") && result.GetNamedBoolean(L"readOnlySnapshot") &&
              result.GetNamedBoolean(L"readOnlyCancel") && result.GetNamedBoolean(L"syncOnlyOwner"));
        const auto expected = scenario == L"allow"  ? LaunchDecision::Approved :
                              scenario == L"cancel" ? LaunchDecision::Canceled :
                                                      LaunchDecision::Skipped;
        Check(result.GetNamedNumber(L"decision") == static_cast<int>(expected));
        result.SetNamedValue(L"parentElevated", json::value(is_process_elevated()));
        result.SetNamedValue(L"passed", json::value(true));
        return result;
    }
}

int wmain(int argc, wchar_t** argv)
{
    Logger::init(std::vector<spdlog::sink_ptr>{ std::make_shared<spdlog::sinks::null_sink_mt>() });
    json::JsonObject result;
    int exitCode = 0;
    try
    {
        winrt::init_apartment();
        Check(argc >= 3);
        if (std::wstring_view(argv[1]) == L"--launch-connect")
        {
            Check(argc == 4);
            result = Child(argv[2], static_cast<DWORD>(std::stoul(argv[3])));
        }
        else if (std::wstring_view(argv[2]) == L"all")
        {
            json::JsonArray results;
            for (const auto scenario : { L"allow", L"skip", L"cancel" })
                results.Append(Parent(scenario));
            result.SetNamedValue(L"passed", json::value(true));
            result.SetNamedValue(L"cases", results);
        }
        else
            result = Parent(argv[2]);
    }
    catch (const WorkspacesCli::Error& error)
    {
        exitCode = error.exitCode;
        result = WorkspacesCli::Failure(L"handoff-test", error);
    }
    catch (const std::exception&)
    {
        exitCode = 1;
        result.SetNamedValue(L"passed", json::value(false));
    }
    catch (const winrt::hresult_error&)
    {
        exitCode = 1;
        result.SetNamedValue(L"passed", json::value(false));
    }
    const auto output = winrt::to_string(result.Stringify()) + "\n";
    std::cout << output;
    if (argc == 4 && std::wstring_view(argv[1]) == L"--parent")
    {
        const auto id = WorkspacesCli::NormalizeId(argv[3]);
        const auto path = OwnPath().parent_path() / (L"handoff-" + id + L".json");
        wil::unique_handle file(CreateFileW(path.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL, nullptr));
        DWORD written{};
        if (!file || !WriteFile(file.get(), output.data(), static_cast<DWORD>(output.size()), &written, nullptr) || written != output.size())
            return 1;
    }
    return exitCode;
}
