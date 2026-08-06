// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "ClientProtocol.h"

#include <winsvc.h>

#include <algorithm>
#include <chrono>
#include <memory>

namespace SettingsBrokerPrototype
{
    namespace
    {
        struct HandleCloser
        {
            void operator()(void* value) const noexcept
            {
                if (value && value != INVALID_HANDLE_VALUE)
                {
                    CloseHandle(static_cast<HANDLE>(value));
                }
            }
        };

        struct ServiceHandleCloser
        {
            void operator()(void* value) const noexcept
            {
                if (value)
                {
                    CloseServiceHandle(static_cast<SC_HANDLE>(value));
                }
            }
        };

        using unique_handle = std::unique_ptr<void, HandleCloser>;
        using unique_service_handle = std::unique_ptr<void, ServiceHandleCloser>;
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

        bool CompleteOverlapped(HANDLE pipe,
                                OVERLAPPED& overlapped,
                                const Deadline& deadline,
                                DWORD& transferred,
                                std::wstring& error)
        {
            const DWORD wait = WaitForSingleObject(overlapped.hEvent,
                                                   RemainingMilliseconds(deadline));
            if (wait == WAIT_OBJECT_0 &&
                GetOverlappedResult(pipe, &overlapped, &transferred, FALSE))
            {
                return true;
            }

            const DWORD operationError =
                wait == WAIT_TIMEOUT ? ERROR_TIMEOUT : GetLastError();
            CancelIoEx(pipe, &overlapped);
            if (WaitForSingleObject(overlapped.hEvent, kCancelCompletionTimeoutMs) !=
                WAIT_OBJECT_0)
            {
                TerminateProcess(GetCurrentProcess(), ERROR_OPERATION_ABORTED);
                ExitProcess(ERROR_OPERATION_ABORTED);
            }
            DWORD ignored = 0;
            GetOverlappedResult(pipe, &overlapped, &ignored, FALSE);
            SetLastError(operationError);
            error = operationError == ERROR_TIMEOUT
                        ? L"I/O timed out"
                        : L"overlapped I/O failed: " + std::to_wstring(operationError);
            return false;
        }

        bool WriteAll(HANDLE pipe,
                      const void* buffer,
                      DWORD bytes,
                      const Deadline& deadline,
                      std::wstring& error)
        {
            const auto* source = static_cast<const BYTE*>(buffer);
            while (bytes > 0)
            {
                unique_handle event(CreateEventW(nullptr, TRUE, FALSE, nullptr));
                if (!event)
                {
                    error = L"CreateEvent failed: " + std::to_wstring(GetLastError());
                    return false;
                }
                OVERLAPPED overlapped{};
                overlapped.hEvent = event.get();
                DWORD written = 0;
                if (!WriteFile(pipe, source, bytes, &written, &overlapped))
                {
                    const DWORD writeError = GetLastError();
                    if (writeError != ERROR_IO_PENDING)
                    {
                        error = L"pipe write failed: " + std::to_wstring(writeError);
                        return false;
                    }
                    if (!CompleteOverlapped(pipe,
                                            overlapped,
                                            deadline,
                                            written,
                                            error))
                    {
                        return false;
                    }
                }
                if (written == 0)
                {
                    error = L"pipe write completed with zero bytes";
                    return false;
                }
                source += written;
                bytes -= written;
            }
            return true;
        }

        bool ReadAll(HANDLE pipe,
                     void* buffer,
                     DWORD bytes,
                     const Deadline& deadline,
                     std::wstring& error)
        {
            auto* destination = static_cast<BYTE*>(buffer);
            while (bytes > 0)
            {
                unique_handle event(CreateEventW(nullptr, TRUE, FALSE, nullptr));
                if (!event)
                {
                    error = L"CreateEvent failed: " + std::to_wstring(GetLastError());
                    return false;
                }
                OVERLAPPED overlapped{};
                overlapped.hEvent = event.get();
                DWORD read = 0;
                if (!ReadFile(pipe, destination, bytes, &read, &overlapped))
                {
                    const DWORD readError = GetLastError();
                    if (readError != ERROR_IO_PENDING)
                    {
                        error = L"pipe read failed: " + std::to_wstring(readError);
                        return false;
                    }
                    if (!CompleteOverlapped(pipe,
                                            overlapped,
                                            deadline,
                                            read,
                                            error))
                    {
                        return false;
                    }
                }
                if (read == 0)
                {
                    error = L"pipe read completed with zero bytes";
                    return false;
                }
                destination += read;
                bytes -= read;
            }
            return true;
        }

        bool VerifyServerProcess(HANDLE pipe, DWORD& serverPid, std::wstring& error)
        {
            ULONG pipeServerPid = 0;
            if (!GetNamedPipeServerProcessId(pipe, &pipeServerPid) ||
                pipeServerPid == 0)
            {
                error = L"GetNamedPipeServerProcessId failed: " +
                        std::to_wstring(GetLastError());
                return false;
            }

            unique_service_handle scm(
                OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
            if (!scm)
            {
                error = L"OpenSCManager failed: " + std::to_wstring(GetLastError());
                return false;
            }
            unique_service_handle service(
                OpenServiceW(static_cast<SC_HANDLE>(scm.get()),
                             kServiceName,
                             SERVICE_QUERY_STATUS));
            if (!service)
            {
                error = L"OpenService failed: " + std::to_wstring(GetLastError());
                return false;
            }

            SERVICE_STATUS_PROCESS status{};
            DWORD bytesNeeded = 0;
            if (!QueryServiceStatusEx(static_cast<SC_HANDLE>(service.get()),
                                      SC_STATUS_PROCESS_INFO,
                                      reinterpret_cast<BYTE*>(&status),
                                      sizeof(status),
                                      &bytesNeeded))
            {
                error = L"QueryServiceStatusEx failed: " +
                        std::to_wstring(GetLastError());
                return false;
            }
            if (status.dwCurrentState != SERVICE_RUNNING ||
                status.dwProcessId == 0 ||
                status.dwProcessId != pipeServerPid)
            {
                error = L"pipe server PID does not match the running broker service";
                return false;
            }
            serverPid = status.dwProcessId;
            return true;
        }

        bool WaitForServerDisconnect(HANDLE pipe,
                                     const Deadline& deadline,
                                     std::wstring& error)
        {
            while (RemainingMilliseconds(deadline) > 0)
            {
                DWORD available = 0;
                if (!PeekNamedPipe(pipe, nullptr, 0, nullptr, &available, nullptr))
                {
                    const DWORD peekError = GetLastError();
                    if (peekError == ERROR_BROKEN_PIPE ||
                        peekError == ERROR_PIPE_NOT_CONNECTED ||
                        peekError == ERROR_NO_DATA)
                    {
                        return true;
                    }
                    error = L"pipe disconnect check failed: " +
                            std::to_wstring(peekError);
                    return false;
                }
                if (available != 0)
                {
                    error = L"unexpected data after response ACK";
                    return false;
                }
                Sleep(5);
            }
            error = L"server did not disconnect before the I/O deadline";
            return false;
        }
    }

    bool ConnectToBroker(HANDLE& pipe, DWORD& verifiedServerPid, std::wstring& error)
    {
        pipe = INVALID_HANDLE_VALUE;
        verifiedServerPid = 0;
        for (int attempt = 0; attempt < 4; ++attempt)
        {
            pipe = CreateFileW(kPipeName,
                               FILE_GENERIC_READ | FILE_WRITE_DATA,
                               0,
                               nullptr,
                               OPEN_EXISTING,
                               FILE_FLAG_OVERLAPPED | SECURITY_SQOS_PRESENT |
                                   SECURITY_IMPERSONATION,
                               nullptr);
            if (pipe != INVALID_HANDLE_VALUE)
            {
                if (VerifyServerProcess(pipe, verifiedServerPid, error))
                {
                    return true;
                }
                CloseHandle(pipe);
                pipe = INVALID_HANDLE_VALUE;
                return false;
            }
            const DWORD lastError = GetLastError();
            if (lastError != ERROR_PIPE_BUSY && lastError != ERROR_FILE_NOT_FOUND)
            {
                error = L"CreateFile failed: " + std::to_wstring(lastError);
                return false;
            }
            WaitNamedPipeW(kPipeName, 1000);
        }
        error = L"broker pipe unavailable: " + std::to_wstring(GetLastError());
        return false;
    }

    bool SendRequest(HANDLE pipe,
                     const RequestHeader& request,
                     const std::vector<BYTE>& payload,
                     ClientResponse& response,
                     std::wstring& error,
                     DWORD responseReadDelayMs)
    {
        response = {};
        const Deadline deadline =
            std::chrono::steady_clock::now() + std::chrono::milliseconds(kIoTimeoutMs);
        if (!WriteAll(pipe, &request, sizeof(request), deadline, error) ||
            (!payload.empty() &&
             !WriteAll(pipe,
                       payload.data(),
                       static_cast<DWORD>(payload.size()),
                       deadline,
                       error)))
        {
            return false;
        }

        if (responseReadDelayMs > 0)
        {
            Sleep(responseReadDelayMs);
        }

        if (!ReadAll(pipe, &response.header, sizeof(response.header), deadline, error))
        {
            return false;
        }
        if (response.header.magic != kResponseMagic ||
            response.header.headerBytes != sizeof(ResponseHeader) ||
            response.header.payloadBytes > kMaxPayloadBytes)
        {
            error = L"invalid response frame";
            return false;
        }

        response.payload.resize(response.header.payloadBytes);
        if (!response.payload.empty() &&
            !ReadAll(pipe,
                     response.payload.data(),
                     static_cast<DWORD>(response.payload.size()),
                     deadline,
                     error))
        {
            response.payload.clear();
            return false;
        }
        const BYTE ack = kResponseConsumedAck;
        return WriteAll(pipe, &ack, sizeof(ack), deadline, error) &&
               WaitForServerDisconnect(pipe, deadline, error);
    }

    const wchar_t* StatusName(Status status)
    {
        switch (status)
        {
        case Status::Ok: return L"Ok";
        case Status::BadRequest: return L"BadRequest";
        case Status::UnsupportedMajor: return L"UnsupportedMajor";
        case Status::UnsupportedMinor: return L"UnsupportedMinor";
        case Status::UnknownOpcode: return L"UnknownOpcode";
        case Status::PayloadTooLarge: return L"PayloadTooLarge";
        case Status::AuthRejected: return L"AuthRejected";
        case Status::TargetDenied: return L"TargetDenied";
        case Status::Busy: return L"Busy";
        case Status::NotFound: return L"NotFound";
        case Status::IoError: return L"IoError";
        case Status::Timeout: return L"Timeout";
        }
        return L"UnknownStatus";
    }
}
