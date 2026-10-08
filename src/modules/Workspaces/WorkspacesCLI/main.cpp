// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <algorithm>
#include <atomic>
#include <iostream>
#include <set>
#include <shellapi.h>
#include <spdlog/sinks/null_sink.h>
#include <wil/resource.h>
#include <wil/stl.h>
#include <wil/win32_helpers.h>

#include <common/logger/logger.h>
#include <common/utils/elevation.h>
#include <WorkspacesLib/CliCommands.h>
#include <WorkspacesLib/AppUtils.h>
#include <WorkspacesLib/OperationLifetime.h>
#include <WorkspacesLib/WorkspaceStore.h>
#include <Launcher.h>
#include <common/version/version.h>
#include "Resources.h"
#include "CommandLogging.h"
#include "ApprovalChannel.h"
#include "ConsoleApproval.h"
#include "JsonOutput.h"
#include "WorkerHandoff.h"

namespace
{
    std::atomic<HANDLE> cancelHandle{ nullptr };

    BOOL WINAPI Cancel(DWORD type)
    {
        const auto handle = cancelHandle.load();
        if ((type == CTRL_C_EVENT || type == CTRL_BREAK_EVENT) && handle)
        {
            SetEvent(handle);
            return TRUE;
        }
        return FALSE;
    }

    void Check(BOOL success, const char* message)
    {
        if (!success)
            throw WorkspacesCli::Error(1, L"internalError", message);
    }

    std::wstring ExePath()
    {
        std::wstring path;
        winrt::check_hresult(wil::GetModuleFileNameW(nullptr, path));
        return path;
    }

    void CheckLaunchContext(bool requireNonElevated = true)
    {
        DWORD session = 0;
        if ((requireNonElevated && !WorkspacesCli::IsMediumProcess(GetCurrentProcess())) ||
            !ProcessIdToSessionId(GetCurrentProcessId(), &session) || session == 0)
            throw WorkspacesCli::Error(7, L"unsupportedContext", "Workspaces requires a signed-in user session and a non-elevated execution worker.");
    }

    void Print(const json::JsonObject& output, bool asJson, bool workerOutput = false)
    {
        try
        {
            CliResources::Localize(output);
        }
        catch (const WorkspacesCli::Error&)
        {
            if (!output.HasKey(L"error"))
                throw;
            std::cerr << "Workspaces CLI: localized error resources are unavailable.\n";
        }
        if (asJson)
        {
            std::cout << (workerOutput ? winrt::to_string(output.Stringify()) : WorkspacesCli::FormatJson(output)) << '\n';
            return;
        }
        if (output.HasKey(L"error"))
        {
            const auto error = output.GetNamedObject(L"error");
            std::cerr << winrt::to_string(error.GetNamedString(L"code")) << ": "
                      << winrt::to_string(error.GetNamedString(L"message")) << '\n';
        }
        else if (output.HasKey(L"workspaces") && output.GetNamedString(L"view") == L"summary")
        {
            std::cout << winrt::to_string(CliResources::Get(IDS_CLITABLEHEADER));
            for (const auto& item : output.GetNamedArray(L"workspaces"))
            {
                const auto project = item.GetObjectW();
                std::cout << winrt::to_string(project.GetNamedString(L"id")) << '\t'
                          << winrt::to_string(project.GetNamedString(L"name")) << '\t';
                bool first = true;
                for (const auto& app : project.GetNamedArray(L"applications"))
                {
                    if (!first)
                        std::cout << ", ";
                    std::cout << winrt::to_string(app.GetObjectW().GetNamedString(L"application"));
                    first = false;
                }
                std::cout << '\n';
            }
        }
        else if (output.HasKey(L"workspaces"))
            std::cout << winrt::to_string(CliResources::Get(IDS_CLICOMPLETED)) << '\n'
                      << winrt::to_string(output.Stringify()) << '\n';
        else
            std::cout << winrt::to_string(CliResources::Get(output.GetNamedString(L"state") == L"completed" ? IDS_CLICOMPLETED :
                                                            output.GetNamedString(L"state") == L"partial"   ? IDS_CLIPARTIAL :
                                                                                                              IDS_CLIFAILED))
                      << '\n'
                      << winrt::to_string(output.GetNamedObject(L"result").Stringify()) << '\n';
        if (output.HasKey(L"warnings"))
        {
            for (const auto& warning : output.GetNamedArray(L"warnings"))
                std::cerr << winrt::to_string(CliResources::Get(IDS_CLIWARNINGPREFIX)) << winrt::to_string(warning.GetObjectW().GetNamedString(L"message")) << '\n';
        }
    }

    void ValidateLaunch(const WorkspacesData::WorkspacesProject& project)
    {
        if (project.apps.empty())
            throw WorkspacesCli::Error(8, L"invalidData", "Workspace has no applications.");
        std::set<std::wstring> appIds;
        std::set<unsigned int> monitors;
        for (const auto& monitor : project.monitors)
        {
            if (!monitor.dpi || !monitors.insert(monitor.number).second)
                throw WorkspacesCli::Error(8, L"invalidData", "Workspace has invalid monitors.");
        }
        for (const auto& app : project.apps)
        {
            std::wstring id;
            try
            {
                id = WorkspacesCli::NormalizeId(app.id);
            }
            catch (const WorkspacesCli::Error&)
            {
                throw WorkspacesCli::Error(8, L"invalidData", "Workspace has an invalid application ID.");
            }
            if (!appIds.insert(id).second ||
                !monitors.contains(app.monitor) || app.position.width <= 0 || app.position.height <= 0)
                throw WorkspacesCli::Error(8, L"invalidData", "Workspace has invalid application IDs or placement.");
        }
    }

    int Worker(const std::vector<std::wstring>& args, HANDLE ownerLifetime = nullptr)
    {
        if (args.size() != 4 && args.size() != 6)
            throw WorkspacesCli::Error(2, L"invalidArguments", "Invalid worker arguments.");
        const auto mapping = reinterpret_cast<HANDLE>(std::stoull(args[1]));
        const auto cancel = reinterpret_cast<HANDLE>(std::stoull(args[2]));
        const auto parentPid = static_cast<DWORD>(std::stoul(args[3]));
        for (const auto handle : { mapping, cancel, GetStdHandle(STD_INPUT_HANDLE), GetStdHandle(STD_OUTPUT_HANDLE) })
            Check(SetHandleInformation(handle, HANDLE_FLAG_INHERIT, 0), "Cannot isolate worker handles.");
        wil::unique_mapview_ptr<DWORD> header(static_cast<DWORD*>(MapViewOfFile(mapping, FILE_MAP_READ, 0, 0, sizeof(DWORD))));
        if (!header || *header == 0 || *header > WorkspacesCli::MaxPayload)
            throw WorkspacesCli::Error(8, L"invalidData", "Invalid operation request.");
        const auto size = *header;
        wil::unique_mapview_ptr<char> view(static_cast<char*>(MapViewOfFile(mapping, FILE_MAP_READ, 0, 0, sizeof(DWORD) + size)));
        if (!view)
            throw WorkspacesCli::Error(8, L"invalidData", "Cannot map operation request.");
        const auto request = json::JsonObject::Parse(winrt::to_hstring(std::string(view.get() + sizeof(DWORD), size)));
        if (request.GetNamedNumber(L"protocolVersion") != 1)
            throw WorkspacesCli::Error(8, L"invalidData", "Unsupported launch protocol.");
        CliLaunchOptions options;
        options.operationId = WorkspacesCli::NormalizeId(request.GetNamedString(L"operationId").c_str());
        options.timeoutSeconds = static_cast<DWORD>(request.GetNamedNumber(L"timeout"));
        options.deadline = static_cast<ULONGLONG>(request.GetNamedNumber(L"deadline"));
        options.cancelEvent = cancel;
        if (options.timeoutSeconds < 1 || options.timeoutSeconds > 600 ||
            options.deadline <= GetTickCount64())
            throw WorkspacesCli::Error(9, L"timeout", "Operation deadline has expired.");
        WorkspacesCli::OperationLifetime lifetime(parentPid, options.timeoutSeconds * 1000 + 5000, ownerLifetime);
        std::optional<WorkspacesCli::WorkerApproval> approval;
        if (args.size() == 6)
        {
            const auto requestWrite = reinterpret_cast<HANDLE>(std::stoull(args[4]));
            const auto responseRead = reinterpret_cast<HANDLE>(std::stoull(args[5]));
            if (GetFileType(requestWrite) != FILE_TYPE_PIPE || GetFileType(responseRead) != FILE_TYPE_PIPE)
                throw WorkspacesCli::Error(8, L"invalidData", "Invalid confirmation handles.");
            Check(SetHandleInformation(requestWrite, HANDLE_FLAG_INHERIT, 0), "Cannot isolate approval request handle.");
            Check(SetHandleInformation(responseRead, HANDLE_FLAG_INHERIT, 0), "Cannot isolate approval response handle.");
            approval.emplace(requestWrite, responseRead, [&] {
                return WaitForSingleObject(cancel, 0) == WAIT_OBJECT_0 || GetTickCount64() >= options.deadline;
            });
            options.requestApproval = [&](const auto& name, const auto& path, const auto& arguments, const auto& verification) {
                return approval->Request(name, path, arguments, verification);
            };
        }
        WorkspacesCli::CheckEnabled();
        CheckLaunchContext();
        auto project = WorkspacesData::WorkspacesProjectJSON::FromJson(request.GetNamedObject(L"workspace"));
        if (!project)
            throw WorkspacesCli::Error(8, L"invalidData", "Invalid operation snapshot.");
        Utils::Apps::UpdateWorkspacesApps(*project, Utils::Apps::GetAppsList());
        ValidateLaunch(*project);
        wil::unique_mutex_nothrow mutex(CreateMutexW(nullptr, TRUE, L"Local\\PowerToys_WorkspacesLauncher_InstanceMutex"));
        const auto mutexError = GetLastError();
        if (!mutex)
            throw WorkspacesCli::Error(5, L"busy", "Cannot acquire the launcher instance lock.");
        if (mutexError == ERROR_ALREADY_EXISTS)
            throw WorkspacesCli::Error(5, L"busy", "Another workspace launch is running.");
        auto release = wil::scope_exit([&] { ReleaseMutex(mutex.get()); });
        if (WaitForSingleObject(cancel, 0) == WAIT_OBJECT_0)
            throw WorkspacesCli::Error(12, L"canceled", "Operation canceled before starting applications.");
        CliLogging::Initialize();
        Logger::info("Workspaces CLI: launch worker started");
        SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        std::vector<WorkspacesData::WorkspacesProject> unusedStore;
        int exitCode = 1;
        json::JsonObject result;
        {
            Launcher launcher(*project, unusedStore, InvokePoint::LaunchAndEdit, &options);
            if (launcher.GetArrangerError() != ERROR_SUCCESS)
                throw WorkspacesCli::Error(7, launcher.GetArrangerError() == ERROR_CANCELLED ? L"consentDenied" : L"arrangerFailed", "Window arranger could not start or elevation was declined.");
            if (WaitForSingleObject(cancel, 0) == WAIT_OBJECT_0)
                throw WorkspacesCli::Error(12, L"canceled", "Stopped scheduling further applications; already launched apps remain open.");
            if (GetTickCount64() >= options.deadline)
                throw WorkspacesCli::Error(9, L"timeout", "Launch deadline expired; applications already started remain open.");
            if (!launcher.HasArrangerResult())
                throw WorkspacesCli::Error(9, L"outcomeUnknown", "Arranger exited without its final result.");
            result = WorkspacesCli::LaunchResult(*project, launcher.GetResult(), options.operationId, exitCode,
                                                launcher.GetApplicationErrors(), launcher.GetApprovalDecisions());
        }
        const auto now = std::chrono::system_clock::to_time_t(std::chrono::system_clock::now());
        const auto saved = WorkspaceStore::UpdateLastLaunched(WorkspacesCli::SettingsRoot() / L"Workspaces" / L"workspaces.json", project->id, now);
        const auto persistence = saved == WorkspaceStore::UpdateResult::Updated    ? L"updated" :
                                 saved == WorkspaceStore::UpdateResult::Conflict   ? L"conflict" :
                                 saved == WorkspaceStore::UpdateResult::Unverified ? L"unverified" :
                                                                                     L"failed";
        result.GetNamedObject(L"result").SetNamedValue(L"persistenceStatus", json::value(persistence));
        if (saved == WorkspaceStore::UpdateResult::Unverified)
        {
            exitCode = 11;
            result.SetNamedValue(L"state", json::value(L"failed"));
            result.SetNamedValue(L"error", WorkspacesCli::Failure(L"launch", { exitCode, L"persistenceUnverified", "Workspace persistence could not be verified." }).GetNamedObject(L"error"));
        }
        else if (saved != WorkspaceStore::UpdateResult::Updated)
        {
            json::JsonObject warning;
            warning.SetNamedValue(L"code", json::value(saved == WorkspaceStore::UpdateResult::Conflict ? L"metadataSaveConflict" : L"metadataSaveFailed"));
            warning.SetNamedValue(L"message", json::value(L"The launch result is available, but launch history could not be saved. Do not relaunch just to update history."));
            result.GetNamedArray(L"warnings").Append(warning);
        }
        Logger::info("Workspaces CLI: launch worker finished, exit={}, metadata={}", exitCode, static_cast<int>(saved));
        Logger::flush();
        std::cout << winrt::to_string(result.Stringify()) << '\n';
        return exitCode;
    }

    json::JsonObject Launch(const WorkspacesData::WorkspacesProject& project, DWORD timeoutSeconds, int& exitCode)
    {
        CheckLaunchContext(false);
        const bool elevatedCaller = is_process_elevated();
        const auto arranger = std::filesystem::path(ExePath()).parent_path() / L"PowerToys.WorkspacesWindowArranger.exe";
        if (!std::filesystem::is_regular_file(arranger))
            throw WorkspacesCli::Error(6, L"unavailable", "The matching window arranger is unavailable.");
        ValidateLaunch(project);
        GUID guid{};
        winrt::check_hresult(CoCreateGuid(&guid));
        wchar_t guidText[40]{};
        StringFromGUID2(guid, guidText, ARRAYSIZE(guidText));
        const auto operationId = WorkspacesCli::NormalizeId(guidText);
        const auto deadline = GetTickCount64() + timeoutSeconds * 1000ULL;
        json::JsonObject request;
        request.SetNamedValue(L"protocolVersion", json::value(1));
        request.SetNamedValue(L"operationId", json::value(operationId));
        request.SetNamedValue(L"timeout", json::value(timeoutSeconds));
        request.SetNamedValue(L"deadline", json::value(deadline));
        request.SetNamedValue(L"workspace", WorkspacesData::WorkspacesProjectJSON::ToJson(project));
        const auto bytes = winrt::to_string(request.Stringify());
        if (bytes.size() > WorkspacesCli::MaxPayload)
            throw WorkspacesCli::Error(8, L"invalidData", "Workspace exceeds the operation payload limit.");
        SECURITY_ATTRIBUTES attributes{ sizeof(attributes), nullptr, TRUE };
        wil::unique_handle mapping(CreateFileMappingW(INVALID_HANDLE_VALUE, &attributes, PAGE_READWRITE, 0, static_cast<DWORD>(sizeof(DWORD) + bytes.size()), nullptr));
        wil::unique_handle cancel(CreateEventW(&attributes, TRUE, FALSE, nullptr));
        if (!mapping || !cancel)
            throw WorkspacesCli::Error(1, L"internalError", "Cannot create operation handles.");
        cancelHandle = cancel.get();
        Check(SetConsoleCtrlHandler(Cancel, TRUE), "Cannot register console cancellation.");
        auto removeHandler = wil::scope_exit([&] {
            SetConsoleCtrlHandler(Cancel, FALSE);
            cancelHandle = nullptr;
        });
        wil::unique_mapview_ptr<char> view(static_cast<char*>(MapViewOfFile(mapping.get(), FILE_MAP_WRITE, 0, 0, 0)));
        if (!view)
            throw WorkspacesCli::Error(1, L"internalError", "Cannot create operation snapshot.");
        *reinterpret_cast<DWORD*>(view.get()) = static_cast<DWORD>(bytes.size());
        memcpy(view.get() + sizeof(DWORD), bytes.data(), bytes.size());
        wil::unique_handle read;
        wil::unique_handle write;
        Check(CreatePipe(read.put(), write.put(), &attributes, 0), "Cannot create result pipe.");
        Check(SetHandleInformation(read.get(), HANDLE_FLAG_INHERIT, 0), "Cannot restrict result handle inheritance.");
        auto approvalRequests = WorkspacesCli::ApprovalPipe::Create();
        auto approvalResponses = WorkspacesCli::ApprovalPipe::Create();
        Check(SetHandleInformation(approvalRequests.write.get(), HANDLE_FLAG_INHERIT, HANDLE_FLAG_INHERIT),
              "Cannot inherit confirmation requests.");
        Check(SetHandleInformation(approvalResponses.read.get(), HANDLE_FLAG_INHERIT, HANDLE_FLAG_INHERIT),
              "Cannot inherit confirmation replies.");
        wil::unique_handle input(CreateFileW(L"NUL", GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, &attributes, OPEN_EXISTING, 0, nullptr));
        const auto executable = ExePath();
        wil::unique_process_information process;
        if (elevatedCaller)
        {
            process = WorkspacesCli::StartMediumWorker(executable, operationId,
                {mapping.get(), cancel.get(), input.get(), write.get(), approvalRequests.write.get(), approvalResponses.read.get()}, deadline);
        }
        else
        {
            SIZE_T attributeBytes = 0;
            InitializeProcThreadAttributeList(nullptr, 1, 0, &attributeBytes);
            std::vector<char> attributeStorage(attributeBytes);
            auto list = reinterpret_cast<LPPROC_THREAD_ATTRIBUTE_LIST>(attributeStorage.data());
            Check(InitializeProcThreadAttributeList(list, 1, 0, &attributeBytes), "Cannot initialize handle inheritance.");
            auto deleteList = wil::scope_exit([&] { DeleteProcThreadAttributeList(list); });
            HANDLE inherited[] = { mapping.get(), cancel.get(), write.get(), input.get(),
                                   approvalRequests.write.get(), approvalResponses.read.get() };
            Check(UpdateProcThreadAttribute(list, 0, PROC_THREAD_ATTRIBUTE_HANDLE_LIST, inherited, sizeof(inherited), nullptr, nullptr),
                  "Cannot restrict inherited operation handles.");
            STARTUPINFOEXW startup{};
            startup.StartupInfo.cb = sizeof(startup);
            startup.StartupInfo.dwFlags = STARTF_USESTDHANDLES;
            startup.StartupInfo.hStdInput = input.get();
            startup.StartupInfo.hStdOutput = write.get();
            startup.StartupInfo.hStdError = write.get();
            startup.lpAttributeList = list;
            std::wstring command = L"\"" + executable + L"\" --launch-worker " +
                std::to_wstring(reinterpret_cast<uintptr_t>(mapping.get())) + L" " +
                std::to_wstring(reinterpret_cast<uintptr_t>(cancel.get())) + L" " + std::to_wstring(GetCurrentProcessId()) + L" " +
                std::to_wstring(reinterpret_cast<uintptr_t>(approvalRequests.write.get())) + L" " +
                std::to_wstring(reinterpret_cast<uintptr_t>(approvalResponses.read.get()));
            Check(CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr, TRUE,
                EXTENDED_STARTUPINFO_PRESENT | CREATE_NO_WINDOW, nullptr, nullptr, &startup.StartupInfo, &process),
                "Cannot start launch worker.");
        }
        write.reset();
        approvalRequests.write.reset();
        approvalResponses.read.reset();
        auto cleanup = wil::scope_exit([&] {
            if (WaitForSingleObject(process.hProcess, 0) == WAIT_TIMEOUT)
            {
                SetEvent(cancel.get());
                if (WaitForSingleObject(process.hProcess, 2000) == WAIT_TIMEOUT)
                {
                    TerminateProcess(process.hProcess, 9);
                    WaitForSingleObject(process.hProcess, 2000);
                }
            }
        });
        WorkspacesCli::ConsoleApproval prompt(GetStdHandle(STD_INPUT_HANDLE), GetStdHandle(STD_ERROR_HANDLE),
            CliResources::ApprovalText());
        WorkspacesCli::FrontendApproval presenter(approvalRequests.read.get(), approvalResponses.write.get(), prompt, cancel.get());
        std::string response;
        ULONGLONG cancelDeadline = 0;
        for (;;)
        {
            const auto now = GetTickCount64();
            if (now >= deadline)
                throw WorkspacesCli::Error(9, L"outcomeUnknown", "Wait ended without a final result. Already launched applications remain open; do not retry automatically.");
            if (WaitForSingleObject(cancel.get(), 0) == WAIT_OBJECT_0)
            {
                presenter.Stop();
                if (!cancelDeadline)
                    cancelDeadline = now + 2000;
                else if (now >= cancelDeadline)
                    throw WorkspacesCli::Error(9, L"outcomeUnknown", "Cancellation was not acknowledged before the wait ended.");
            }
            presenter.Poll();
            DWORD available = 0;
            if (PeekNamedPipe(read.get(), nullptr, 0, nullptr, &available, nullptr) && available)
            {
                char buffer[4096];
                DWORD received = 0;
                Check(ReadFile(read.get(), buffer, (std::min)(available, static_cast<DWORD>(sizeof(buffer))), &received, nullptr),
                      "Cannot read worker result.");
                response.append(buffer, received);
                if (response.size() > WorkspacesCli::MaxPayload)
                    throw WorkspacesCli::Error(9, L"outcomeUnknown", "Worker returned an oversized result.");
                continue;
            }
            if (WaitForSingleObject(process.hProcess, 0) == WAIT_OBJECT_0)
            {
                if (PeekNamedPipe(read.get(), nullptr, 0, nullptr, &available, nullptr) && available)
                    continue;
                break;
            }
            Sleep(20);
        }
        presenter.Stop();
        DWORD processExit = 1;
        Check(GetExitCodeProcess(process.hProcess, &processExit), "Cannot read worker exit status.");
        if (response.empty())
            throw WorkspacesCli::Error(9, L"outcomeUnknown", "Worker exited without a final result.");
        json::JsonObject output;
        try
        {
            output = json::JsonObject::Parse(winrt::to_hstring(response));
        }
        catch (const winrt::hresult_error&)
        {
            throw WorkspacesCli::Error(9, L"outcomeUnknown", "Worker returned an invalid result.");
        }
        if (output.GetNamedNumber(L"schemaVersion") != 1 || output.GetNamedString(L"command") != L"launch")
            throw WorkspacesCli::Error(9, L"outcomeUnknown", "Worker result contract mismatch.");
        exitCode = static_cast<int>(processExit);
        if (output.HasKey(L"result") &&
            output.GetNamedObject(L"result").GetNamedString(L"operationId") != operationId)
            throw WorkspacesCli::Error(9, L"outcomeUnknown", "Worker returned a different operation result.");
        return output;
    }
}

int wmain(int argc, wchar_t** argv)
{
    std::vector<std::wstring> args(argv + 1, argv + argc);
    const bool connectedWorker = !args.empty() && args.front() == L"--launch-connect";
    const bool worker = connectedWorker || (!args.empty() && args.front() == L"--launch-worker");
    const bool asJson = worker || WorkspacesCli::WantsJson(args);
    std::wstring command = worker ? L"launch" : (args.empty() ? L"" : args.front());
    Logger::init(std::vector<spdlog::sink_ptr>{ std::make_shared<spdlog::sinks::null_sink_mt>() });
    try
    {
        winrt::init_apartment();
        SetConsoleOutputCP(CP_UTF8);
        if (connectedWorker)
        {
            if (args.size() != 3)
                throw WorkspacesCli::Error(2, L"invalidArguments", "Invalid worker handoff arguments.");
            auto handles = WorkspacesCli::ReceiveWorkerHandles(args[1], static_cast<DWORD>(std::stoul(args[2])));
            handles.BindStandardStreams();
            const std::vector<std::wstring> workerArguments{
                L"--launch-worker",
                std::to_wstring(reinterpret_cast<uintptr_t>(handles.snapshot.get())),
                std::to_wstring(reinterpret_cast<uintptr_t>(handles.cancel.get())),
                std::to_wstring(handles.ownerPid),
                std::to_wstring(reinterpret_cast<uintptr_t>(handles.approvalRequests.get())),
                std::to_wstring(reinterpret_cast<uintptr_t>(handles.approvalReplies.get())),
            };
            return Worker(workerArguments, handles.ownerLifetime.get());
        }
        if (worker)
            return Worker(args);
        const auto options = WorkspacesCli::Parse(args);
        if (options.command == L"--help")
        {
            std::cout << winrt::to_string(CliResources::Get(IDS_CLIHELP));
            return 0;
        }
        if (options.command == L"--version")
        {
            std::cout << "PowerToys Workspaces CLI " << FILE_VERSION_STRING << '\n';
            return 0;
        }
        WorkspacesCli::CheckEnabled();
        const auto projects = WorkspacesCli::Select(WorkspacesCli::LoadWorkspaces(), options);
        int exitCode = 0;
        auto result = options.command == L"list" ? WorkspacesCli::ListResult(projects, options.details) :
                                                   Launch(projects.front(), options.timeoutSeconds, exitCode);
        Print(result, options.json);
        return exitCode;
    }
    catch (const WorkspacesCli::Error& error)
    {
        Print(WorkspacesCli::Failure(command, error), asJson, worker);
        return error.exitCode;
    }
    catch (const winrt::hresult_error&)
    {
        Print(WorkspacesCli::Failure(command, { 1, L"internalError", "A Windows operation failed." }), asJson, worker);
        return 1;
    }
    catch (const std::exception&)
    {
        Print(WorkspacesCli::Failure(command, { 1, L"internalError", "Unexpected Workspaces CLI failure." }), asJson, worker);
        return 1;
    }
}
