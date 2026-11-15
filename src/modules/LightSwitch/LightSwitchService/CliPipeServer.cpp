// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "CliPipeServer.h"
#include "LocalizedStrings.h"
#include "CliIdentity.h"

#include <array>
#include <future>
#include <mutex>
#include <thread>
#include <utility>
#include <sddl.h>
#include <roapi.h>
#include <wil/resource.h>

using namespace winrt::Windows::Data::Json;

namespace light_switch_cli
{
    namespace
    {
        std::wstring SidString(PSID sid)
        {
            wil::unique_hlocal_string text;
            winrt::check_bool(ConvertSidToStringSidW(sid, text.put()));
            return text.get();
        }

        DWORD RemainingTime(ULONGLONG deadline) noexcept
        {
            const auto now = GetTickCount64();
            return now >= deadline ? 0 : static_cast<DWORD>(deadline - now);
        }

        enum class IoResult
        {
            Completed,
            Closed,
            Stopped,
            TimedOut,
            Failed,
        };
    }

    std::wstring GetPipeName()
    {
        DWORD sessionId = 0;
        winrt::check_bool(ProcessIdToSessionId(GetCurrentProcessId(), &sessionId));
        return L"\\\\.\\pipe\\PowerToys_LightSwitch_Cli_" + std::to_wstring(sessionId);
    }

    class CliPipeServer::Impl
    {
    public:
        Impl(Handler handler, Options options) : m_handler(std::move(handler)), m_options(std::move(options)) {}

        bool Start()
        {
            std::lock_guard lock(m_lifecycleMutex);
            if (m_thread.joinable())
            {
                return true;
            }

            DWORD error = ERROR_GEN_FAILURE;
            try
            {
                if (!m_handler || m_options.readTimeoutMs == 0 || m_options.writeTimeoutMs == 0)
                {
                    SetLastError(ERROR_INVALID_PARAMETER);
                    return false;
                }
                if (m_options.pipeName.empty())
                {
                    m_options.pipeName = GetPipeName();
                }

                wil::unique_handle token;
                winrt::check_bool(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, token.put()));
                m_identity = ReadTokenIdentity(token.get());
                m_stopEvent.reset(CreateEventW(nullptr, TRUE, FALSE, nullptr));
                winrt::check_bool(m_stopEvent.get() != nullptr);

                // Restrict clients to this logon. A medium label also permits the normal CLI
                // when PowerToys is elevated. GR/GW includes the pipe-instance right, so keep
                // the single, first instance open until Stop; never release/reacquire its name.
                const auto descriptorText = L"O:" + SidString(m_identity.userSid.data()) +
                                            L"D:P(A;;GRGW;;;" + SidString(m_identity.logonSid.data()) + L")S:(ML;;NW;;;ME)";
                wil::unique_hlocal_security_descriptor descriptor;
                winrt::check_bool(ConvertStringSecurityDescriptorToSecurityDescriptorW(
                    descriptorText.c_str(), SDDL_REVISION_1, descriptor.put(), nullptr));
                SECURITY_ATTRIBUTES attributes{ sizeof(attributes), descriptor.get(), FALSE };
                m_pipe.reset(CreateNamedPipeW(
                    m_options.pipeName.c_str(),
                    PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED | FILE_FLAG_FIRST_PIPE_INSTANCE,
                    PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
                    1,
                    static_cast<DWORD>((MaxMessageCharacters + 1) * sizeof(wchar_t)),
                    static_cast<DWORD>((MaxMessageCharacters + 1) * sizeof(wchar_t)),
                    0,
                    &attributes));
                winrt::check_bool(m_pipe.get() != INVALID_HANDLE_VALUE);

                std::promise<HRESULT> ready;
                auto initialized = ready.get_future();
                m_thread = std::thread([this, ready = std::move(ready)]() mutable {
                    const auto result = RoInitialize(RO_INIT_MULTITHREADED);
                    ready.set_value(result);
                    if (SUCCEEDED(result))
                    {
                        Run();
                        RoUninitialize();
                    }
                });
                const auto result = initialized.get();
                if (SUCCEEDED(result))
                {
                    return true;
                }
                error = static_cast<DWORD>(result);
                m_thread.join();
            }
            catch (const winrt::hresult_error& exception)
            {
                const HRESULT code = exception.code();
                error = HRESULT_FACILITY(code) == FACILITY_WIN32 ? HRESULT_CODE(code) : static_cast<DWORD>(code);
            }
            catch (...)
            {
                error = ERROR_GEN_FAILURE;
            }
            if (m_thread.joinable())
            {
                SetEvent(m_stopEvent.get());
                CancelIoEx(m_pipe.get(), nullptr);
                m_thread.join();
            }
            m_pipe.reset();
            m_stopEvent.reset();
            SetLastError(error);
            return false;
        }

        void Stop() noexcept
        {
            std::lock_guard lock(m_lifecycleMutex);
            if (m_thread.joinable())
            {
                SetEvent(m_stopEvent.get());
                CancelIoEx(m_pipe.get(), nullptr);
                // The worker drains cancelled OVERLAPPED operations and finishes any active
                // handler before these handles or the handler's captured state can be freed.
                m_thread.join();
            }
            m_pipe.reset();
            m_stopEvent.reset();
        }

    private:
        bool IsStopped() const noexcept
        {
            return WaitForSingleObject(m_stopEvent.get(), 0) == WAIT_OBJECT_0;
        }

        template<typename BeginOperation>
        IoResult PerformIo(BeginOperation beginOperation, DWORD timeoutMs, DWORD& transferred)
        {
            transferred = 0;
            if (IsStopped())
            {
                return IoResult::Stopped;
            }
            wil::unique_handle completed(CreateEventW(nullptr, TRUE, FALSE, nullptr));
            if (!completed.get())
            {
                return IoResult::Failed;
            }
            OVERLAPPED operation{};
            operation.hEvent = completed.get();
            if (beginOperation(operation))
            {
                return GetOverlappedResult(m_pipe.get(), &operation, &transferred, FALSE) ? IoResult::Completed : IoResult::Failed;
            }

            const auto error = GetLastError();
            if (error == ERROR_PIPE_CONNECTED)
            {
                return IoResult::Completed;
            }
            if (error != ERROR_IO_PENDING)
            {
                return error == ERROR_BROKEN_PIPE || error == ERROR_NO_DATA || error == ERROR_PIPE_NOT_CONNECTED ? IoResult::Closed : IoResult::Failed;
            }

            const HANDLE waits[]{ m_stopEvent.get(), completed.get() };
            const auto wait = WaitForMultipleObjects(2, waits, FALSE, timeoutMs);
            if (wait == WAIT_OBJECT_0 + 1)
            {
                if (GetOverlappedResult(m_pipe.get(), &operation, &transferred, FALSE))
                {
                    return IoResult::Completed;
                }
                const auto completionError = GetLastError();
                return completionError == ERROR_BROKEN_PIPE || completionError == ERROR_NO_DATA || completionError == ERROR_PIPE_NOT_CONNECTED ? IoResult::Closed : IoResult::Failed;
            }

            CancelIoEx(m_pipe.get(), &operation);
            // Even if completion raced cancellation, the kernel no longer references this
            // OVERLAPPED or its caller-owned buffer once GetOverlappedResult has returned.
            GetOverlappedResult(m_pipe.get(), &operation, &transferred, TRUE);
            if (wait == WAIT_OBJECT_0)
            {
                return IoResult::Stopped;
            }
            return wait == WAIT_TIMEOUT ? IoResult::TimedOut : IoResult::Failed;
        }

        bool AuthenticateClient()
        {
            // ReadRequest has consumed a bounded frame, so the pipe can identify its
            // sender. Identification-level impersonation needs no SeImpersonatePrivilege
            // and avoids opening an elevated client's process or primary token.
            if (!ImpersonateNamedPipeClient(m_pipe.get()))
            {
                return false;
            }
            wil::unique_handle token;
            {
                const auto revert = wil::scope_exit([]() {
                    // Continuing with a client's thread token would compromise later requests.
                    FAIL_FAST_IF_WIN32_BOOL_FALSE(RevertToSelf());
                });
                if (!OpenThreadToken(GetCurrentThread(), TOKEN_QUERY, TRUE, token.put()))
                {
                    return false;
                }
            }
            try
            {
                return IsSameLogon(m_identity, ReadTokenIdentity(token.get()));
            }
            catch (...)
            {
                return false;
            }
        }

        std::wstring ReadRequest()
        {
            std::wstring message;
            std::optional<BYTE> firstByte;
            const auto deadline = GetTickCount64() + m_options.readTimeoutMs;
            while (!IsStopped())
            {
                std::array<BYTE, 4096> bytes{};
                DWORD count = 0;
                const auto result = PerformIo(
                    [&](OVERLAPPED& operation) { return ReadFile(m_pipe.get(), bytes.data(), static_cast<DWORD>(bytes.size()), nullptr, &operation); },
                    RemainingTime(deadline),
                    count);
                if (result == IoResult::TimedOut)
                {
                    throw RequestError(L"TIMEOUT", GET_RESOURCE_STRING(IDS_REQUEST_READ_TIMEOUT));
                }
                if (result == IoResult::Stopped)
                {
                    throw RequestError(L"SERVICE_UNAVAILABLE", GET_RESOURCE_STRING(IDS_SERVICE_STOPPING));
                }
                if (result != IoResult::Completed || count == 0)
                {
                    throw RequestError(L"PROTOCOL_ERROR", GET_RESOURCE_STRING(IDS_REQUEST_NEWLINE_REQUIRED));
                }

                for (DWORD index = 0; index < count; ++index)
                {
                    if (!firstByte)
                    {
                        firstByte = bytes[index];
                        continue;
                    }
                    const auto value = static_cast<wchar_t>(*firstByte | (static_cast<unsigned int>(bytes[index]) << 8));
                    firstByte.reset();
                    if (value == L'\n')
                    {
                        if (index + 1 != count)
                        {
                            throw RequestError(L"PROTOCOL_ERROR", GET_RESOURCE_STRING(IDS_REQUEST_SINGLE_REQUIRED));
                        }
                        return message;
                    }
                    if (message.size() == MaxMessageCharacters)
                    {
                        throw RequestError(L"PROTOCOL_ERROR", GET_RESOURCE_STRING(IDS_REQUEST_TOO_LONG));
                    }
                    message.push_back(value);
                }
            }
            throw RequestError(L"SERVICE_UNAVAILABLE", GET_RESOURCE_STRING(IDS_SERVICE_STOPPING));
        }

        void WaitForClientClose()
        {
            // DisconnectNamedPipe can discard unread output. Wait for the client to consume
            // its response and close instead of flushing synchronously (which could hang).
            // Any further input is discarded; it can never invoke the handler a second time.
            const auto deadline = GetTickCount64() + m_options.readTimeoutMs;
            while (!IsStopped() && RemainingTime(deadline) > 0)
            {
                std::array<BYTE, 256> ignored{};
                DWORD count = 0;
                const auto result = PerformIo(
                    [&](OVERLAPPED& operation) { return ReadFile(m_pipe.get(), ignored.data(), static_cast<DWORD>(ignored.size()), nullptr, &operation); },
                    RemainingTime(deadline),
                    count);
                if (result != IoResult::Completed || count == 0)
                {
                    return;
                }
            }
        }

        void WriteResponse(JsonObject response)
        {
            std::wstring text(response.Stringify());
            if (text.size() > MaxMessageCharacters)
            {
                text = MakeError(L"PROTOCOL_ERROR", GET_RESOURCE_STRING(IDS_RESPONSE_TOO_LONG)).Stringify();
            }
            text.push_back(L'\n');
            const auto size = static_cast<DWORD>(text.size() * sizeof(wchar_t));
            const auto deadline = GetTickCount64() + m_options.writeTimeoutMs;
            DWORD offset = 0;
            while (offset < size)
            {
                DWORD count = 0;
                const auto result = PerformIo(
                    [&](OVERLAPPED& operation) { return WriteFile(m_pipe.get(), reinterpret_cast<const BYTE*>(text.data()) + offset, size - offset, nullptr, &operation); },
                    RemainingTime(deadline),
                    count);
                if (result != IoResult::Completed || count == 0)
                {
                    return;
                }
                offset += count;
            }
            WaitForClientClose();
        }

        void HandleConnection()
        {
            JsonObject response;
            try
            {
                const auto message = ReadRequest();
                if (!AuthenticateClient())
                {
                    response = MakeError(L"SERVICE_UNAVAILABLE", GET_RESOURCE_STRING(IDS_CLIENT_LOGON_MISMATCH));
                }
                else
                {
                    const auto request = ParseRequest(message);
                    response = IsStopped() ? MakeError(L"SERVICE_UNAVAILABLE", GET_RESOURCE_STRING(IDS_SERVICE_STOPPING)) : m_handler(request);
                    if (!response)
                    {
                        response = MakeError(L"EXECUTION_FAILED", GET_RESOURCE_STRING(IDS_RESPONSE_MISSING));
                    }
                }
            }
            catch (const RequestError& error)
            {
                response = MakeError(error.Code(), error.Message());
            }
            catch (...)
            {
                response = MakeError(L"EXECUTION_FAILED", GET_RESOURCE_STRING(IDS_COMMAND_FAILED));
            }
            if (!IsStopped())
            {
                WriteResponse(response);
            }
        }

        void Run() noexcept
        {
            while (!IsStopped())
            {
                try
                {
                    DWORD ignored = 0;
                    const auto result = PerformIo(
                        [&](OVERLAPPED& operation) { return ConnectNamedPipe(m_pipe.get(), &operation); },
                        INFINITE,
                        ignored);
                    if (result == IoResult::Stopped)
                    {
                        break;
                    }
                    if (result == IoResult::Failed)
                    {
                        break;
                    }
                    if (result == IoResult::Completed)
                    {
                        HandleConnection();
                    }
                }
                catch (...)
                {
                    // Serialization and allocation failures also must not terminate the
                    // process or release the owned pipe name between connections.
                    try
                    {
                        if (!IsStopped())
                        {
                            WriteResponse(MakeError(L"EXECUTION_FAILED", GET_RESOURCE_STRING(IDS_REQUEST_FAILED)));
                        }
                    }
                    catch (...)
                    {
                    }
                }
                DisconnectNamedPipe(m_pipe.get());
            }
        }

        Handler m_handler;
        Options m_options;
        TokenIdentity m_identity;
        wil::unique_handle m_pipe;
        wil::unique_handle m_stopEvent;
        std::thread m_thread;
        std::mutex m_lifecycleMutex;
    };

    CliPipeServer::CliPipeServer(Handler handler) : CliPipeServer(std::move(handler), Options{}) {}
    CliPipeServer::CliPipeServer(Handler handler, Options options) : m_impl(std::make_unique<Impl>(std::move(handler), std::move(options))) {}
    CliPipeServer::~CliPipeServer()
    {
        Stop();
    }
    bool CliPipeServer::Start()
    {
        return m_impl->Start();
    }
    void CliPipeServer::Stop() noexcept
    {
        m_impl->Stop();
    }
}
