// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#include <windows.h>
#include <objbase.h>
#include <sddl.h>
#include <fcntl.h>
#include <io.h>
#include <cstdio>
#include <iostream>
#include <filesystem>
#include <algorithm>
#include <vector>
#include <common/interop/pipe_caller_auth.h>
#include <WorkspacesLib/CliCommands.h>
#include <WorkspacesLib/IPCHelper.h>
#include "WorkerHandoff.h"

namespace WorkspacesCli
{
    namespace
    {
        constexpr DWORD HandoffTimeoutMs = 10000;

        struct ProcessContext
        {
            std::vector<BYTE> sid;
            DWORD session = 0;
            DWORD integrity = 0;
            bool elevated = false;
        };

        struct Packet
        {
            uint32_t version = 1;
            uint32_t ownerPid = 0;
            std::array<uint64_t, 7> handles{};
        };
        static_assert(sizeof(Packet) == 64);

        [[noreturn]] void Fail(const char* message)
        {
            throw Error(7, L"deElevationFailed", message);
        }

        std::vector<BYTE> TokenData(HANDLE token, TOKEN_INFORMATION_CLASS information)
        {
            DWORD required = 0;
            GetTokenInformation(token, information, nullptr, 0, &required);
            if (!required)
                Fail("Cannot read process token information.");
            std::vector<BYTE> buffer(required);
            if (!GetTokenInformation(token, information, buffer.data(), required, &required))
                Fail("Cannot read process token information.");
            return buffer;
        }

        ProcessContext Context(HANDLE process)
        {
            wil::unique_handle token;
            if (!OpenProcessToken(process, TOKEN_QUERY, token.put()))
                Fail("Cannot inspect the process identity.");
            const auto user = TokenData(token.get(), TokenUser);
            const auto sid = reinterpret_cast<const TOKEN_USER*>(user.data())->User.Sid;
            if (!IsValidSid(sid))
                Fail("Invalid process user identity.");
            ProcessContext context;
            context.sid.resize(GetLengthSid(sid));
            if (!CopySid(static_cast<DWORD>(context.sid.size()), context.sid.data(), sid))
                Fail("Cannot copy process identity.");
            const auto session = TokenData(token.get(), TokenSessionId);
            context.session = *reinterpret_cast<const DWORD*>(session.data());
            const auto elevation = TokenData(token.get(), TokenElevation);
            context.elevated = reinterpret_cast<const TOKEN_ELEVATION*>(elevation.data())->TokenIsElevated != 0;
            const auto integrity = TokenData(token.get(), TokenIntegrityLevel);
            const auto label = reinterpret_cast<const TOKEN_MANDATORY_LABEL*>(integrity.data())->Label.Sid;
            if (!IsValidSid(label) || !*GetSidSubAuthorityCount(label))
                Fail("Invalid process integrity level.");
            context.integrity = *GetSidSubAuthority(label, *GetSidSubAuthorityCount(label) - 1);
            return context;
        }

        bool SameContext(const ProcessContext& first, const ProcessContext& second)
        {
            return first.session != 0 && first.session == second.session &&
                   first.sid == second.sid;
        }

        std::wstring PipeName(const std::wstring& operationId)
        {
            return L"\\\\.\\pipe\\PowerToys.Workspaces.Handoff." + NormalizeId(operationId);
        }

        std::wstring OwnPath()
        {
            std::wstring path(32768, L'\0');
            const auto length = GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));
            if (!length || length == path.size())
                Fail("Cannot locate the worker executable.");
            path.resize(length);
            return path;
        }

        void WaitForIo(OVERLAPPED& operation, HANDLE owner, HANDLE cancel, ULONGLONG deadline)
        {
            for (;;)
            {
                if (cancel && WaitForSingleObject(cancel, 0) == WAIT_OBJECT_0)
                    throw Error(12, L"canceled", "Worker startup was canceled.");
                if (GetTickCount64() >= deadline)
                    Fail("Worker handoff timed out.");
                if (WaitForSingleObject(operation.hEvent, 20) == WAIT_OBJECT_0)
                    return;
                if (owner && WaitForSingleObject(owner, 0) == WAIT_OBJECT_0)
                    Fail("Worker handoff peer exited.");
            }
        }

        void Transfer(HANDLE pipe, void* data, DWORD length, bool write, HANDLE owner, HANDLE cancel, ULONGLONG deadline)
        {
            DWORD offset = 0;
            while (offset < length)
            {
                wil::unique_handle ready(CreateEventW(nullptr, TRUE, FALSE, nullptr));
                if (!ready)
                    Fail("Cannot create handoff completion event.");
                OVERLAPPED operation{};
                operation.hEvent = ready.get();
                DWORD count = 0;
                const BOOL completed = write ?
                                           WriteFile(pipe, static_cast<BYTE*>(data) + offset, length - offset, &count, &operation) :
                                           ReadFile(pipe, static_cast<BYTE*>(data) + offset, length - offset, &count, &operation);
                const DWORD error = completed ? ERROR_SUCCESS : GetLastError();
                if (!completed && error != ERROR_IO_PENDING)
                    Fail("Worker handoff transfer failed.");
                auto drain = wil::scope_exit([&] {
                    if (error == ERROR_IO_PENDING)
                    {
                        CancelIoEx(pipe, &operation);
                        DWORD ignored{};
                        GetOverlappedResult(pipe, &operation, &ignored, TRUE);
                    }
                });
                if (!completed)
                {
                    WaitForIo(operation, owner, cancel, deadline);
                    if (!GetOverlappedResult(pipe, &operation, &count, FALSE))
                        Fail("Worker handoff transfer failed.");
                }
                if (!count)
                    Fail("Worker handoff returned no data.");
                offset += count;
            }
        }

        uint64_t DuplicateFor(HANDLE target, HANDLE source, DWORD access, bool sameAccess = false)
        {
            HANDLE remote = nullptr;
            if (!DuplicateHandle(GetCurrentProcess(), source, target, &remote, access, FALSE, sameAccess ? DUPLICATE_SAME_ACCESS : 0))
                Fail("Cannot transfer a worker handle.");
            return reinterpret_cast<uint64_t>(remote);
        }

        void ValidatePeer(HANDLE pipe, DWORD expectedPid, bool server)
        {
            const auto policy = IPCHelper::ModulePeer(std::filesystem::path(OwnPath()).filename().wstring(), expectedPid);
            interop_auth::VerificationCache cache;
            const auto result = server ? interop_auth::AuthenticateServer(pipe, policy, cache) :
                                         interop_auth::AuthenticateClient(pipe, policy, cache);
            if (!result.accepted)
                Fail("Worker handoff peer identity was rejected.");
        }
    }

    bool SameUserSession(HANDLE first, HANDLE second)
    {
        return SameContext(Context(first), Context(second));
    }

    bool IsMediumProcess(HANDLE process)
    {
        const auto context = Context(process);
        return context.session != 0 && !context.elevated && context.integrity == SECURITY_MANDATORY_MEDIUM_RID;
    }

    wil::unique_process_information StartMediumWorker(const std::wstring& executable, const std::wstring& operationId, const WorkerHandles& handles, ULONGLONG deadline)
    {
        if (handles.cancel && WaitForSingleObject(handles.cancel, 0) == WAIT_OBJECT_0)
            throw Error(12, L"canceled", "Worker startup was canceled.");
        if (GetTickCount64() >= deadline)
            throw Error(9, L"timeout", "Worker startup deadline expired.");
        auto self = Context(GetCurrentProcess());
        if (!self.session || self.integrity < SECURITY_MANDATORY_MEDIUM_RID ||
            self.integrity > SECURITY_MANDATORY_HIGH_RID)
            Fail("Unsupported caller context.");
        const auto shellWindow = GetShellWindow();
        DWORD shellPid = 0;
        if (!shellWindow || !GetWindowThreadProcessId(shellWindow, &shellPid) || !shellPid)
            Fail("No desktop shell is available for de-elevation.");
        wil::unique_handle shell(OpenProcess(PROCESS_CREATE_PROCESS | PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE, FALSE, shellPid));
        if (!shell || !SameContext(self, Context(shell.get())) || !IsMediumProcess(shell.get()))
            Fail("The desktop shell is not a same-user non-elevated process.");

        wil::unique_hlocal_string sid;
        if (!ConvertSidToStringSidW(self.sid.data(), sid.put()))
            Fail("Cannot create the handoff access policy.");
        const auto sddl = std::wstring(L"D:P(A;;GR;;;") + sid.get() + L")";
        wil::unique_hlocal_security_descriptor security;
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl.c_str(), SDDL_REVISION_1, security.put(), nullptr))
            Fail("Cannot create handoff security.");
        SECURITY_ATTRIBUTES attributes{ sizeof(attributes), security.get(), FALSE };
        const auto name = PipeName(operationId);
        wil::unique_handle pipe(CreateNamedPipeW(name.c_str(),
                                                 PIPE_ACCESS_OUTBOUND | FILE_FLAG_OVERLAPPED | FILE_FLAG_FIRST_PIPE_INSTANCE,
                                                 PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
                                                 1,
                                                 sizeof(Packet),
                                                 0,
                                                 0,
                                                 &attributes));
        if (!pipe)
            Fail("Cannot create the private worker handoff.");
        wil::unique_handle ready(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        if (!ready)
            Fail("Cannot create the handoff connection event.");
        OVERLAPPED connection{};
        connection.hEvent = ready.get();
        const bool connected = ConnectNamedPipe(pipe.get(), &connection) != FALSE;
        const auto connectionError = connected ? ERROR_SUCCESS : GetLastError();
        if (!connected && connectionError != ERROR_IO_PENDING && connectionError != ERROR_PIPE_CONNECTED)
            Fail("Cannot prepare the worker handoff.");
        auto drainConnection = wil::scope_exit([&] {
            if (connectionError == ERROR_IO_PENDING)
            {
                CancelIoEx(pipe.get(), &connection);
                DWORD ignored{};
                GetOverlappedResult(pipe.get(), &connection, &ignored, TRUE);
            }
        });

        SIZE_T size = 0;
        InitializeProcThreadAttributeList(nullptr, 1, 0, &size);
        std::vector<BYTE> storage(size);
        auto attributeList = reinterpret_cast<LPPROC_THREAD_ATTRIBUTE_LIST>(storage.data());
        if (!InitializeProcThreadAttributeList(attributeList, 1, 0, &size))
            Fail("Cannot prepare the medium worker process.");
        auto releaseAttributes = wil::scope_exit([&] { DeleteProcThreadAttributeList(attributeList); });
        HANDLE parent = shell.get();
        if (!UpdateProcThreadAttribute(attributeList, 0, PROC_THREAD_ATTRIBUTE_PARENT_PROCESS, &parent, sizeof(parent), nullptr, nullptr))
            Fail("Cannot select the non-elevated process context.");
        STARTUPINFOEXW startup{};
        startup.StartupInfo.cb = sizeof(startup);
        startup.lpAttributeList = attributeList;
        auto command = L"\"" + executable + L"\" --launch-connect " + NormalizeId(operationId) +
                       L" " + std::to_wstring(GetCurrentProcessId());
        const auto directory = std::filesystem::path(executable).parent_path().wstring();
        wil::unique_process_information process;
        if (!CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr, FALSE, EXTENDED_STARTUPINFO_PRESENT | CREATE_SUSPENDED | CREATE_NO_WINDOW, nullptr, directory.c_str(), &startup.StartupInfo, &process))
            Fail("Cannot create a non-elevated worker.");
        bool delivered = false;
        auto stopUndelivered = wil::scope_exit([&] {
            if (!delivered)
            {
                TerminateProcess(process.hProcess, 7);
                WaitForSingleObject(process.hProcess, 2000);
            }
        });
        // Do not run any worker code or transfer capabilities until its actual token is verified.
        if (!SameUserSession(GetCurrentProcess(), process.hProcess) || !IsMediumProcess(process.hProcess))
            Fail("Worker was not created in the required non-elevated context.");
        if (ResumeThread(process.hThread) == static_cast<DWORD>(-1))
            Fail("Cannot start the non-elevated worker.");
        const auto handoffDeadline = (std::min)(deadline, GetTickCount64() + HandoffTimeoutMs);
        if (connectionError == ERROR_IO_PENDING)
        {
            WaitForIo(connection, process.hProcess, handles.cancel, handoffDeadline);
            DWORD ignored{};
            if (!GetOverlappedResult(pipe.get(), &connection, &ignored, FALSE))
                Fail("Worker handoff connection failed.");
        }
        ValidatePeer(pipe.get(), process.dwProcessId, false);
        if (!SameUserSession(GetCurrentProcess(), process.hProcess) || !IsMediumProcess(process.hProcess))
            Fail("Worker identity changed before handoff.");
        Packet packet;
        packet.ownerPid = GetCurrentProcessId();
        packet.handles = {
            DuplicateFor(process.hProcess, handles.snapshot, FILE_MAP_READ),
            DuplicateFor(process.hProcess, handles.cancel, SYNCHRONIZE),
            DuplicateFor(process.hProcess, handles.input, 0, true),
            DuplicateFor(process.hProcess, handles.result, 0, true),
            DuplicateFor(process.hProcess, handles.approvalRequests, 0, true),
            DuplicateFor(process.hProcess, handles.approvalReplies, 0, true),
            DuplicateFor(process.hProcess, GetCurrentProcess(), SYNCHRONIZE),
        };
        Transfer(pipe.get(), &packet, sizeof(packet), true, process.hProcess, handles.cancel, handoffDeadline);
        delivered = true;
        return process;
    }

    ReceivedWorkerHandles ReceiveWorkerHandles(const std::wstring& operationId, DWORD ownerPid)
    {
        if (!ownerPid || !IsMediumProcess(GetCurrentProcess()))
            Fail("Worker must run in a non-elevated user context.");
        wil::unique_handle owner(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, ownerPid));
        if (!owner || !SameUserSession(GetCurrentProcess(), owner.get()))
            Fail("Worker owner is not in the same user session.");
        const auto name = PipeName(operationId);
        const auto deadline = GetTickCount64() + HandoffTimeoutMs;
        wil::unique_handle pipe;
        for (;;)
        {
            pipe.reset(CreateFileW(name.c_str(), GENERIC_READ, 0, nullptr, OPEN_EXISTING, FILE_FLAG_OVERLAPPED | SECURITY_SQOS_PRESENT | SECURITY_IDENTIFICATION, nullptr));
            if (pipe)
                break;
            const auto error = GetLastError();
            if ((error != ERROR_FILE_NOT_FOUND && error != ERROR_PIPE_BUSY) || GetTickCount64() >= deadline)
                Fail("Cannot connect to the worker owner.");
            Sleep(20);
        }
        ValidatePeer(pipe.get(), ownerPid, true);
        Packet packet{};
        Transfer(pipe.get(), &packet, sizeof(packet), false, nullptr, nullptr, deadline);
        if (packet.version != 1 || packet.ownerPid != ownerPid)
            Fail("Invalid worker handoff protocol.");
        for (const auto value : packet.handles)
        {
            if (!value || value == UINT64_MAX || value > UINTPTR_MAX)
                Fail("Invalid transferred worker handle.");
        }
        ReceivedWorkerHandles result;
        result.ownerPid = ownerPid;
        result.snapshot.reset(reinterpret_cast<HANDLE>(packet.handles[0]));
        result.cancel.reset(reinterpret_cast<HANDLE>(packet.handles[1]));
        result.input.reset(reinterpret_cast<HANDLE>(packet.handles[2]));
        result.result.reset(reinterpret_cast<HANDLE>(packet.handles[3]));
        result.approvalRequests.reset(reinterpret_cast<HANDLE>(packet.handles[4]));
        result.approvalReplies.reset(reinterpret_cast<HANDLE>(packet.handles[5]));
        result.ownerLifetime.reset(reinterpret_cast<HANDLE>(packet.handles[6]));
        return result;
    }

    void ReceivedWorkerHandles::BindStandardStreams() const
    {
        FILE* stream = nullptr;
        if (freopen_s(&stream, "NUL", "w", stdout) || freopen_s(&stream, "NUL", "w", stderr))
            Fail("Cannot attach worker standard streams.");
        HANDLE copy = nullptr;
        if (!DuplicateHandle(GetCurrentProcess(), result.get(), GetCurrentProcess(), &copy, 0, FALSE, DUPLICATE_SAME_ACCESS))
            Fail("Cannot attach the worker result stream.");
        const int descriptor = _open_osfhandle(reinterpret_cast<intptr_t>(copy), _O_WRONLY | _O_BINARY);
        if (descriptor < 0)
        {
            CloseHandle(copy);
            Fail("Cannot create the worker result descriptor.");
        }
        auto closeDescriptor = wil::scope_exit([&] { _close(descriptor); });
        if (_dup2(descriptor, _fileno(stdout)) || _dup2(descriptor, _fileno(stderr)) ||
            !SetStdHandle(STD_INPUT_HANDLE, input.get()) || !SetStdHandle(STD_OUTPUT_HANDLE, result.get()) ||
            !SetStdHandle(STD_ERROR_HANDLE, result.get()))
            Fail("Cannot connect worker standard streams.");
        std::cout.clear();
        std::cerr.clear();
    }
}
