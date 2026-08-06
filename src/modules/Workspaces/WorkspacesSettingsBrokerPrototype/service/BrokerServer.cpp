// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "BrokerServer.h"

#include "../common/Protocol.h"
#include "CallerAuth.h"
#include "Storage.h"
#include "Tables.h"

#include <sddl.h>

#include <algorithm>
#include <array>
#include <chrono>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <unordered_map>
#include <vector>

namespace SettingsBrokerPrototype
{
    namespace
    {
        enum class IoResult
        {
            Ok,
            TimedOut,
            Stopping,
            Failed,
        };

        using Deadline = std::chrono::steady_clock::time_point;

        DWORD RemainingMilliseconds(const Deadline& deadline)
        {
            const auto remaining = std::chrono::duration_cast<std::chrono::milliseconds>(
                deadline - std::chrono::steady_clock::now());
            if (remaining.count() <= 0)
            {
                return 0;
            }
            return static_cast<DWORD>(
                (std::min)(remaining.count(), static_cast<int64_t>(MAXDWORD)));
        }

        std::mutex g_quotaMutex;
        std::unordered_map<std::wstring, uint32_t> g_activeBySid;

        class SidQuota
        {
        public:
            explicit SidQuota(const std::wstring& sid) : m_sid(sid)
            {
                std::lock_guard guard(g_quotaMutex);
                auto& count = g_activeBySid[m_sid];
                if (count < kPerSidConnectionLimit)
                {
                    ++count;
                    m_acquired = true;
                }
            }

            ~SidQuota()
            {
                if (!m_acquired)
                {
                    return;
                }
                std::lock_guard guard(g_quotaMutex);
                const auto found = g_activeBySid.find(m_sid);
                if (found != g_activeBySid.end() && --found->second == 0)
                {
                    g_activeBySid.erase(found);
                }
            }

            bool Acquired() const noexcept
            {
                return m_acquired;
            }

        private:
            std::wstring m_sid;
            bool m_acquired = false;
        };

        class PipeDisconnect
        {
        public:
            explicit PipeDisconnect(HANDLE pipe) : m_pipe(pipe)
            {
            }

            ~PipeDisconnect()
            {
                DisconnectNamedPipe(m_pipe);
            }

        private:
            HANDLE m_pipe;
        };

        IoResult WaitForIo(HANDLE pipe,
                           HANDLE stopEvent,
                           OVERLAPPED& overlapped,
                           DWORD& transferred,
                           DWORD timeoutMs)
        {
            HANDLE waits[] = { overlapped.hEvent, stopEvent };
            const DWORD wait = WaitForMultipleObjects(ARRAYSIZE(waits),
                                                      waits,
                                                      FALSE,
                                                      timeoutMs);
            if (wait == WAIT_OBJECT_0 &&
                GetOverlappedResult(pipe, &overlapped, &transferred, FALSE))
            {
                return IoResult::Ok;
            }

            CancelIoEx(pipe, &overlapped);
            if (WaitForSingleObject(overlapped.hEvent, kCancelCompletionTimeoutMs) !=
                WAIT_OBJECT_0)
            {
                TerminateProcess(GetCurrentProcess(), ERROR_OPERATION_ABORTED);
                ExitProcess(ERROR_OPERATION_ABORTED);
            }
            DWORD ignored = 0;
            GetOverlappedResult(pipe, &overlapped, &ignored, FALSE);
            if (wait == WAIT_OBJECT_0 + 1)
            {
                return IoResult::Stopping;
            }
            if (wait == WAIT_TIMEOUT)
            {
                return IoResult::TimedOut;
            }
            return IoResult::Failed;
        }

        IoResult ReadExact(HANDLE pipe,
                           HANDLE stopEvent,
                           void* buffer,
                           DWORD bytes,
                           const Deadline& deadline)
        {
            auto* destination = static_cast<BYTE*>(buffer);
            while (bytes > 0)
            {
                HANDLE event = CreateEventW(nullptr, TRUE, FALSE, nullptr);
                if (!event)
                {
                    return IoResult::Failed;
                }
                OVERLAPPED overlapped{};
                overlapped.hEvent = event;
                DWORD transferred = 0;
                IoResult result = IoResult::Ok;
                if (!ReadFile(pipe, destination, bytes, &transferred, &overlapped))
                {
                    if (GetLastError() != ERROR_IO_PENDING)
                    {
                        CloseHandle(event);
                        return IoResult::Failed;
                    }
                    result = WaitForIo(pipe,
                                       stopEvent,
                                       overlapped,
                                       transferred,
                                       RemainingMilliseconds(deadline));
                }
                CloseHandle(event);
                if (result != IoResult::Ok || transferred == 0)
                {
                    return result == IoResult::Ok ? IoResult::Failed : result;
                }
                destination += transferred;
                bytes -= transferred;
            }
            return IoResult::Ok;
        }

        IoResult ReadExact(HANDLE pipe, HANDLE stopEvent, void* buffer, DWORD bytes)
        {
            return ReadExact(
                pipe,
                stopEvent,
                buffer,
                bytes,
                std::chrono::steady_clock::now() +
                    std::chrono::milliseconds(kIoTimeoutMs));
        }

        IoResult WriteExact(HANDLE pipe,
                            HANDLE stopEvent,
                            const void* buffer,
                            DWORD bytes,
                            const Deadline& deadline)
        {
            const auto* source = static_cast<const BYTE*>(buffer);
            while (bytes > 0)
            {
                HANDLE event = CreateEventW(nullptr, TRUE, FALSE, nullptr);
                if (!event)
                {
                    return IoResult::Failed;
                }
                OVERLAPPED overlapped{};
                overlapped.hEvent = event;
                DWORD transferred = 0;
                IoResult result = IoResult::Ok;
                if (!WriteFile(pipe, source, bytes, &transferred, &overlapped))
                {
                    if (GetLastError() != ERROR_IO_PENDING)
                    {
                        CloseHandle(event);
                        return IoResult::Failed;
                    }
                    result = WaitForIo(pipe,
                                       stopEvent,
                                       overlapped,
                                       transferred,
                                       RemainingMilliseconds(deadline));
                }
                CloseHandle(event);
                if (result != IoResult::Ok || transferred == 0)
                {
                    return result == IoResult::Ok ? IoResult::Failed : result;
                }
                source += transferred;
                bytes -= transferred;
            }
            return IoResult::Ok;
        }

        bool SendResponse(HANDLE pipe,
                          HANDLE stopEvent,
                          Status status,
                          uint16_t negotiatedMinor,
                          uint32_t capabilities,
                          const std::vector<BYTE>& payload = {})
        {
            ResponseHeader response{
                kResponseMagic,
                sizeof(ResponseHeader),
                kProtocolMajor,
                negotiatedMinor,
                static_cast<uint16_t>(status),
                0,
                capabilities,
                static_cast<uint32_t>(payload.size()),
            };
            const Deadline deadline =
                std::chrono::steady_clock::now() +
                std::chrono::milliseconds(kIoTimeoutMs);
            if (WriteExact(pipe,
                           stopEvent,
                           &response,
                           sizeof(response),
                           deadline) != IoResult::Ok)
            {
                return false;
            }
            if (!payload.empty() &&
                WriteExact(pipe,
                           stopEvent,
                           payload.data(),
                           static_cast<DWORD>(payload.size()),
                           deadline) != IoResult::Ok)
            {
                return false;
            }
            BYTE ack = 0;
            return ReadExact(pipe, stopEvent, &ack, sizeof(ack)) == IoResult::Ok &&
                   ack == kResponseConsumedAck;
        }

        void HandleRequest(HANDLE pipe, HANDLE stopEvent)
        {
            CallerIdentity caller;
            const HRESULT authentication = AuthenticateCaller(pipe, caller);
            if (caller.sid.empty())
            {
                return;
            }

            SidQuota quota(caller.sid);
            PipeDisconnect disconnect(pipe);
            if (!quota.Acquired())
            {
                return;
            }
            if (FAILED(authentication))
            {
                SendResponse(pipe, stopEvent, Status::AuthRejected, 0, 0);
                return;
            }

            RequestHeader request{};
            const IoResult headerRead = ReadExact(pipe, stopEvent, &request, sizeof(request));
            if (headerRead != IoResult::Ok)
            {
                if (headerRead == IoResult::TimedOut)
                {
                    SendResponse(pipe, stopEvent, Status::Timeout, 0, 0);
                }
                return;
            }

            if (request.magic != kRequestMagic ||
                request.headerBytes != sizeof(RequestHeader))
            {
                SendResponse(pipe, stopEvent, Status::BadRequest, 0, 0);
                return;
            }
            if (request.major != kProtocolMajor)
            {
                SendResponse(pipe,
                             stopEvent,
                             Status::UnsupportedMajor,
                             kMaxProtocolMinor,
                             kCapabilities);
                return;
            }
            if (request.minor < kMinProtocolMinor ||
                request.minor > kMaxProtocolMinor)
            {
                SendResponse(pipe,
                             stopEvent,
                             Status::UnsupportedMinor,
                             kMaxProtocolMinor,
                             kCapabilities);
                return;
            }
            if (request.payloadBytes > kMaxPayloadBytes)
            {
                SendResponse(pipe,
                             stopEvent,
                             Status::PayloadTooLarge,
                             request.minor,
                             request.minor >= 1 ? kCapabilities : 0);
                return;
            }

            std::vector<BYTE> payload(request.payloadBytes);
            if (!payload.empty() &&
                ReadExact(pipe,
                          stopEvent,
                          payload.data(),
                          static_cast<DWORD>(payload.size())) != IoResult::Ok)
            {
                SendResponse(pipe,
                             stopEvent,
                             Status::BadRequest,
                             request.minor,
                             request.minor >= 1 ? kCapabilities : 0);
                return;
            }

            const auto opcode = static_cast<Opcode>(request.opcode);
            const uint32_t capabilities = request.minor >= 1 ? kCapabilities : 0;
            if (opcode == Opcode::Ping)
            {
                if (!payload.empty())
                {
                    SendResponse(pipe,
                                 stopEvent,
                                 Status::BadRequest,
                                 request.minor,
                                 capabilities);
                    return;
                }
                SendResponse(pipe, stopEvent, Status::Ok, request.minor, capabilities);
                return;
            }

            const TrustedTarget* target = FindTarget(request.targetId);
            if (!target || !BindingAllowsTarget(*caller.binding, request.targetId))
            {
                SendResponse(pipe,
                             stopEvent,
                             Status::TargetDenied,
                             request.minor,
                             capabilities);
                return;
            }

            if (opcode == Opcode::Get)
            {
                if (!payload.empty())
                {
                    SendResponse(pipe,
                                 stopEvent,
                                 Status::BadRequest,
                                 request.minor,
                                 capabilities);
                    return;
                }
                std::vector<BYTE> stored;
                const HRESULT result = ReadValue(caller.sid, *target, stored);
                if (result == HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND) ||
                    result == HRESULT_FROM_WIN32(ERROR_PATH_NOT_FOUND))
                {
                    SendResponse(pipe,
                                 stopEvent,
                                 Status::NotFound,
                                 request.minor,
                                 capabilities);
                }
                else
                {
                    SendResponse(pipe,
                                 stopEvent,
                                 SUCCEEDED(result) ? Status::Ok : Status::IoError,
                                 request.minor,
                                 capabilities,
                                 SUCCEEDED(result) ? stored : std::vector<BYTE>{});
                }
                return;
            }

            if (opcode == Opcode::Put)
            {
                const HRESULT result = WriteValue(caller.sid, *target, payload);
                SendResponse(pipe,
                             stopEvent,
                             SUCCEEDED(result) ? Status::Ok : Status::IoError,
                             request.minor,
                             capabilities);
                return;
            }

            SendResponse(pipe,
                         stopEvent,
                         Status::UnknownOpcode,
                         request.minor,
                         capabilities);
        }

        std::wstring PipeSddl()
        {
            std::wstring sddl =
                L"D:(A;;0x12008b;;;AU)(A;;GA;;;SY)(A;;GA;;;BA)";

            DWORD sidBytes = 0;
            DWORD domainChars = 0;
            SID_NAME_USE use{};
            LookupAccountNameW(nullptr,
                               kServiceAccount,
                               nullptr,
                               &sidBytes,
                               nullptr,
                               &domainChars,
                               &use);
            if (sidBytes == 0)
            {
                return sddl;
            }

            std::vector<BYTE> sid(sidBytes);
            std::vector<wchar_t> domain(domainChars);
            if (!LookupAccountNameW(nullptr,
                                    kServiceAccount,
                                    sid.data(),
                                    &sidBytes,
                                    domain.data(),
                                    &domainChars,
                                    &use))
            {
                return sddl;
            }

            LPWSTR sidText = nullptr;
            if (ConvertSidToStringSidW(sid.data(), &sidText))
            {
                sddl += L"(A;;GA;;;";
                sddl += sidText;
                sddl += L")";
                LocalFree(sidText);
            }
            return sddl;
        }

        HANDLE CreatePipe(bool firstInstance)
        {
            PSECURITY_DESCRIPTOR descriptor = nullptr;
            const std::wstring sddl = PipeSddl();
            if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(
                    sddl.c_str(),
                    SDDL_REVISION_1,
                    &descriptor,
                    nullptr))
            {
                return INVALID_HANDLE_VALUE;
            }

            SECURITY_ATTRIBUTES attributes{
                sizeof(SECURITY_ATTRIBUTES),
                descriptor,
                FALSE,
            };
            DWORD openMode = PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED;
            if (firstInstance)
            {
                openMode |= FILE_FLAG_FIRST_PIPE_INSTANCE;
            }
            HANDLE pipe = CreateNamedPipeW(kPipeName,
                                           openMode,
                                           PIPE_TYPE_BYTE | PIPE_READMODE_BYTE |
                                               PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
                                           kWorkerCount,
                                           64 * 1024,
                                           64 * 1024,
                                           kIoTimeoutMs,
                                           &attributes);
            LocalFree(descriptor);
            return pipe;
        }

        void Worker(HANDLE pipe, HANDLE stopEvent)
        {
            while (WaitForSingleObject(stopEvent, 0) != WAIT_OBJECT_0)
            {
                HANDLE event = CreateEventW(nullptr, TRUE, FALSE, nullptr);
                if (!event)
                {
                    return;
                }
                OVERLAPPED overlapped{};
                overlapped.hEvent = event;
                bool connected = false;
                if (ConnectNamedPipe(pipe, &overlapped))
                {
                    connected = true;
                }
                else
                {
                    const DWORD error = GetLastError();
                    if (error == ERROR_PIPE_CONNECTED)
                    {
                        connected = true;
                    }
                    else if (error == ERROR_IO_PENDING)
                    {
                        DWORD ignored = 0;
                        const IoResult result =
                            WaitForIo(pipe,
                                      stopEvent,
                                      overlapped,
                                      ignored,
                                      kIoTimeoutMs);
                        connected = result == IoResult::Ok;
                    }
                }
                CloseHandle(event);

                if (WaitForSingleObject(stopEvent, 0) == WAIT_OBJECT_0)
                {
                    return;
                }
                if (connected)
                {
                    HandleRequest(pipe, stopEvent);
                    DisconnectNamedPipe(pipe);
                }
            }
        }
    }

    DWORD RunBrokerServer(HANDLE stopEvent)
    {
        std::array<HANDLE, kWorkerCount> pipes{};
        pipes.fill(INVALID_HANDLE_VALUE);
        for (size_t index = 0; index < pipes.size(); ++index)
        {
            pipes[index] = CreatePipe(index == 0);
            if (pipes[index] == INVALID_HANDLE_VALUE)
            {
                const DWORD error = GetLastError();
                for (HANDLE pipe : pipes)
                {
                    if (pipe != INVALID_HANDLE_VALUE)
                    {
                        CloseHandle(pipe);
                    }
                }
                return error;
            }
        }

        std::vector<std::thread> workers;
        workers.reserve(pipes.size());
        for (HANDLE pipe : pipes)
        {
            workers.emplace_back(Worker, pipe, stopEvent);
        }

        WaitForSingleObject(stopEvent, INFINITE);
        for (HANDLE pipe : pipes)
        {
            CancelIoEx(pipe, nullptr);
        }
        for (auto& worker : workers)
        {
            worker.join();
        }
        for (HANDLE pipe : pipes)
        {
            CloseHandle(pipe);
        }
        return ERROR_SUCCESS;
    }
}
