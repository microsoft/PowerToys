#include "pch.h"
#include "two_way_pipe_message_ipc_impl.h"

#include <algorithm>
#include <iterator>
#include <system_error>

constexpr DWORD BUFSIZE = 1024;
constexpr DWORD PipeClientAccess = FILE_READ_DATA |
                                   FILE_READ_ATTRIBUTES |
                                   READ_CONTROL |
                                   FILE_WRITE_DATA |
                                   FILE_WRITE_ATTRIBUTES |
                                   SYNCHRONIZE;
constexpr DWORD PipeWaitIntervalMs = 100;

namespace
{
HANDLE duplicate_pipe_security_token(HANDLE token)
{
    if (!token)
    {
        return nullptr;
    }

    HANDLE duplicate = nullptr;
    if (!DuplicateHandle(GetCurrentProcess(),
                         token,
                         GetCurrentProcess(),
                         &duplicate,
                         0,
                         FALSE,
                         DUPLICATE_SAME_ACCESS))
    {
        throw std::system_error(GetLastError(), std::system_category());
    }
    return duplicate;
}
}

#ifdef TWO_WAY_PIPE_MESSAGE_IPC_TESTS
namespace
{
    std::atomic_int thread_start_failure_after{ -1 };
    std::atomic<HANDLE> wait_named_pipe_entered_event{ nullptr };
    std::atomic<HANDLE> handler_completed_event{ nullptr };
    std::atomic<HANDLE> handler_allow_return_event{ nullptr };
    std::atomic<HANDLE> before_replacement_listener_event{ nullptr };
    std::atomic<HANDLE> allow_replacement_listener_event{ nullptr };
    std::atomic<HANDLE> after_replacement_listener_event{ nullptr };
    std::atomic<HANDLE> allow_after_replacement_listener_event{ nullptr };
    std::atomic_int handler_thread_start_failure_after{ -1 };
    std::atomic<HANDLE> handler_thread_start_attempt_event{ nullptr };
    std::atomic<HANDLE> output_write_pending_event{ nullptr };

    void inject_thread_start_failure()
    {
        int remaining = thread_start_failure_after.load();
        while (remaining >= 0)
        {
            if (remaining == 0)
            {
                throw std::system_error(std::make_error_code(std::errc::resource_unavailable_try_again));
            }
            if (thread_start_failure_after.compare_exchange_weak(remaining, remaining - 1))
            {
                return;
            }
        }
    }

    void inject_handler_thread_start_failure()
    {
        int remaining = handler_thread_start_failure_after.load();
        while (remaining >= 0)
        {
            if (remaining == 0)
            {
                if (handler_thread_start_failure_after.compare_exchange_weak(remaining, -1))
                {
                    if (const HANDLE attempt_event = handler_thread_start_attempt_event.load())
                    {
                        SetEvent(attempt_event);
                    }
                    throw std::system_error(std::make_error_code(std::errc::resource_unavailable_try_again));
                }
                continue;
            }
            if (handler_thread_start_failure_after.compare_exchange_weak(remaining, remaining - 1))
            {
                return;
            }
        }
    }
}

namespace two_way_pipe_message_ipc_test
{
    void FailThreadStartAfter(int successful_starts)
    {
        thread_start_failure_after.store(successful_starts);
    }

    void SetWaitNamedPipeEnteredEvent(HANDLE event)
    {
        wait_named_pipe_entered_event.store(event);
    }

    void SetHandlerCompletionEvents(HANDLE completed_event, HANDLE allow_return_event)
    {
        handler_completed_event.store(completed_event);
        handler_allow_return_event.store(allow_return_event);
    }

    void SetBeforeReplacementListenerEvents(HANDLE reached_event, HANDLE allow_creation_event)
    {
        before_replacement_listener_event.store(reached_event);
        allow_replacement_listener_event.store(allow_creation_event);
    }

    void SetAfterReplacementListenerEvents(HANDLE reached_event, HANDLE allow_continue_event)
    {
        after_replacement_listener_event.store(reached_event);
        allow_after_replacement_listener_event.store(allow_continue_event);
    }

    void FailHandlerThreadStartAfter(int successful_starts)
    {
        handler_thread_start_failure_after.store(successful_starts);
    }

    void SetHandlerThreadStartAttemptEvent(HANDLE event)
    {
        handler_thread_start_attempt_event.store(event);
    }

    void SetOutputWritePendingEvent(HANDLE event)
    {
        output_write_pending_event.store(event);
    }

    void ResetFaultInjection()
    {
        thread_start_failure_after.store(-1);
        wait_named_pipe_entered_event.store(nullptr);
        handler_completed_event.store(nullptr);
        handler_allow_return_event.store(nullptr);
        before_replacement_listener_event.store(nullptr);
        allow_replacement_listener_event.store(nullptr);
        after_replacement_listener_event.store(nullptr);
        allow_after_replacement_listener_event.store(nullptr);
        handler_thread_start_failure_after.store(-1);
        handler_thread_start_attempt_event.store(nullptr);
        output_write_pending_event.store(nullptr);
    }
}
#else
namespace
{
    void inject_thread_start_failure()
    {
    }

    void inject_handler_thread_start_failure()
    {
    }
}
#endif

TwoWayPipeMessageIPC::TwoWayPipeMessageIPC(
    std::wstring _input_pipe_name,
    std::wstring _output_pipe_name,
    callback_function p_func) :
    impl(new TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl(
        _input_pipe_name,
        _output_pipe_name,
        p_func))
{
}

TwoWayPipeMessageIPC::~TwoWayPipeMessageIPC()
{
    impl->end();
    delete impl;
}

void TwoWayPipeMessageIPC::send(std::wstring msg)
{
    impl->send(msg);
}

void TwoWayPipeMessageIPC::start(HANDLE _restricted_pipe_token)
{
    impl->start(_restricted_pipe_token);
}

void TwoWayPipeMessageIPC::start(HANDLE _restricted_pipe_token, const interop_auth::CallerPolicy& caller_policy)
{
    impl->start(_restricted_pipe_token, caller_policy);
}

void TwoWayPipeMessageIPC::end()
{
    impl->end();
}

TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::TwoWayPipeMessageIPCImpl(
    std::wstring _input_pipe_name,
    std::wstring _output_pipe_name,
    callback_function p_func)
{
    input_pipe_name = _input_pipe_name;
    output_pipe_name = _output_pipe_name;
    dispatch_inc_message_function = p_func;
}

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::send(std::wstring msg)
{
    output_queue.queue_message(msg);
}

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::start(HANDLE _restricted_pipe_token)
{
    std::scoped_lock lock(lifecycle_mutex);
    if (lifecycle_state != LifecycleState::NotStarted)
    {
        return;
    }

    // Legacy overload = no caller authentication: explicitly clear any previously-set policy so this
    // path can never inherit a policy from a prior parameterized start on the same instance.
    caller_policy = {};
    pipe_security_token = duplicate_pipe_security_token(_restricted_pipe_token);
    closed.store(false);
    lifecycle_state = LifecycleState::Starting;
    try
    {
        start_threads(pipe_security_token);
        lifecycle_state = LifecycleState::Running;
    }
    catch (...)
    {
        closed.store(true);
        stop_started_threads();
        lifecycle_state = LifecycleState::Stopped;
        lifecycle_stopped.notify_all();
        throw;
    }
}

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::start(HANDLE _restricted_pipe_token, const interop_auth::CallerPolicy& _caller_policy)
{
    std::scoped_lock lock(lifecycle_mutex);
    if (lifecycle_state != LifecycleState::NotStarted)
    {
        return;
    }

    // Start threads inline (do not chain into the legacy overload, which would clear the policy).
    caller_policy = _caller_policy;
    pipe_security_token = duplicate_pipe_security_token(_restricted_pipe_token);
    closed.store(false);
    lifecycle_state = LifecycleState::Starting;
    try
    {
        start_threads(pipe_security_token);
        lifecycle_state = LifecycleState::Running;
    }
    catch (...)
    {
        closed.store(true);
        stop_started_threads();
        lifecycle_state = LifecycleState::Stopped;
        lifecycle_stopped.notify_all();
        throw;
    }
}

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::end()
{
    {
        std::unique_lock lock(lifecycle_mutex);
        if (lifecycle_state == LifecycleState::NotStarted || lifecycle_state == LifecycleState::Stopped)
        {
            lifecycle_state = LifecycleState::Stopped;
            return;
        }
        if (lifecycle_state == LifecycleState::Stopping)
        {
            lifecycle_stopped.wait(lock, [this] {
                return lifecycle_state == LifecycleState::Stopped;
            });
            return;
        }

        lifecycle_state = LifecycleState::Stopping;
        closed.store(true);
    }

    stop_started_threads();

    {
        std::scoped_lock lock(lifecycle_mutex);
        lifecycle_state = LifecycleState::Stopped;
    }
    lifecycle_stopped.notify_all();
}

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::start_threads(HANDLE token)
{
    inject_thread_start_failure();
    output_queue_thread = std::thread(&TwoWayPipeMessageIPCImpl::consume_output_queue_thread, this);
    inject_thread_start_failure();
    input_queue_thread = std::thread(&TwoWayPipeMessageIPCImpl::consume_input_queue_thread, this);
    inject_thread_start_failure();
    input_pipe_thread = std::thread(&TwoWayPipeMessageIPCImpl::start_named_pipe_server, this, token);
}

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::stop_started_threads()
{
    input_queue.interrupt();
    if (input_queue_thread.joinable())
    {
        input_queue_thread.join();
    }
    output_queue.interrupt();
    cancel_active_output_io();
    if (output_queue_thread.joinable())
    {
        output_queue_thread.join();
    }
    {
        std::scoped_lock lock(pipe_connect_handle_mutex);
        if (current_connect_pipe_handle != NULL)
        {
            // Cancels the pipe currently waiting for a connection.
            CancelIoEx(current_connect_pipe_handle, NULL);
        }
    }
    if (input_pipe_thread.joinable())
    {
        input_pipe_thread.join();
    }
    cancel_and_wait_for_connection_handlers();
    if (pipe_security_token)
    {
        CloseHandle(pipe_security_token);
        pipe_security_token = nullptr;
    }
}

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::cancel_active_output_io()
{
    std::scoped_lock lock(output_pipe_mutex);
    if (active_output_pipe_handle != INVALID_HANDLE_VALUE)
    {
        CancelIoEx(active_output_pipe_handle, nullptr);
    }
}

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::send_pipe_message(std::wstring message)
{
    // Adapted from https://learn.microsoft.com/windows/win32/ipc/named-pipe-client
    const wchar_t* message_send = message.c_str();
    const wchar_t* lpszPipename = output_pipe_name.c_str();
    OwnedPipeHandle output_pipe;

    // Try to open a named pipe; wait for it, if necessary.

    while (!closed.load())
    {
        output_pipe.reset(CreateFile(
            lpszPipename, // pipe name
            PipeClientAccess,
            0, // no sharing
            NULL, // default security attributes
            OPEN_EXISTING, // opens existing pipe
            two_way_pipe_message_ipc::ClientOpenFlags,
            NULL)); // no template file

        // Break if the pipe handle is valid.

        if (output_pipe.valid())
            break;

        // Exit if an error other than ERROR_PIPE_BUSY occurs.
        DWORD curr_error = 0;
        if ((curr_error = GetLastError()) != ERROR_PIPE_BUSY)
        {
            return;
        }

        // Use short waits so end() can promptly join the output thread instead of waiting for a
        // long unavailable-pipe timeout.
#ifdef TWO_WAY_PIPE_MESSAGE_IPC_TESTS
        if (const HANDLE event = wait_named_pipe_entered_event.load())
        {
            SetEvent(event);
        }
#endif
        if (!WaitNamedPipe(lpszPipename, PipeWaitIntervalMs) && GetLastError() != ERROR_SEM_TIMEOUT)
        {
            return;
        }
    }
    if (closed.load() || !output_pipe.valid())
    {
        return;
    }

    const HANDLE output_pipe_handle = output_pipe.get();
    const auto clear_active_output_pipe = [&]() {
        std::scoped_lock lock(output_pipe_mutex);
        if (active_output_pipe_handle == output_pipe_handle)
        {
            active_output_pipe_handle = INVALID_HANDLE_VALUE;
        }
    };

    DWORD dwMode = PIPE_READMODE_MESSAGE;
    if (!SetNamedPipeHandleState(
        output_pipe_handle,
        &dwMode, // new pipe mode
        NULL, // don't set maximum bytes
        NULL)) // don't set maximum time
    {
        clear_active_output_pipe();
        return;
    }

    HANDLE write_complete_event = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (!write_complete_event)
    {
        return;
    }

    OVERLAPPED write_overlapped{};
    write_overlapped.hEvent = write_complete_event;
    DWORD bytes_written = 0;
    const DWORD bytes_to_write = (lstrlen(message_send)) * sizeof(WCHAR);
    BOOL write_succeeded = FALSE;
    {
        // Begin the overlapped write while holding the same mutex end() uses for
        // cancellation. This closes the check-to-write race where shutdown could
        // otherwise cancel before the write was issued.
        std::scoped_lock lock(output_pipe_mutex);
        if (closed.load())
        {
            CloseHandle(write_complete_event);
            return;
        }
        active_output_pipe_handle = output_pipe_handle;
        write_succeeded = WriteFile(output_pipe_handle,
                                    message_send,
                                    bytes_to_write,
                                    &bytes_written,
                                    &write_overlapped);
    }
    if (!write_succeeded)
    {
        if (GetLastError() != ERROR_IO_PENDING)
        {
            CloseHandle(write_complete_event);
            clear_active_output_pipe();
            return;
        }
#ifdef TWO_WAY_PIPE_MESSAGE_IPC_TESTS
        if (const HANDLE pending_event = output_write_pending_event.load())
        {
            SetEvent(pending_event);
        }
#endif
        GetOverlappedResult(output_pipe_handle, &write_overlapped, &bytes_written, TRUE);
    }

    CloseHandle(write_complete_event);
    clear_active_output_pipe();
}

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::consume_output_queue_thread()
{
    while (!closed.load())
    {
        std::wstring message = output_queue.pop_message();
        if (message.length() == 0)
        {
            break;
        }
        send_pipe_message(message);
    }
}

BOOL TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::GetLogonSID(HANDLE hToken, PSID* ppsid)
{
    // From https://learn.microsoft.com/previous-versions/aa446670(v=vs.85)
    BOOL bSuccess = FALSE;
    DWORD dwIndex;
    DWORD dwLength = 0;
    PTOKEN_GROUPS ptg = NULL;

    // Verify the parameter passed in is not NULL.
    if (NULL == ppsid)
        goto Cleanup;
    *ppsid = nullptr;

    // Get required buffer size and allocate the TOKEN_GROUPS buffer.

    if (!GetTokenInformation(
            hToken, // handle to the access token
            TokenGroups, // get information about the token's groups
            static_cast<LPVOID>(ptg), // pointer to TOKEN_GROUPS buffer
            0, // size of buffer
            &dwLength // receives required buffer size
            ))
    {
        if (GetLastError() != ERROR_INSUFFICIENT_BUFFER)
            goto Cleanup;

        ptg = static_cast<PTOKEN_GROUPS>(HeapAlloc(GetProcessHeap(),
                                       HEAP_ZERO_MEMORY,
                                       dwLength));

        if (ptg == NULL)
            goto Cleanup;
    }

    // Get the token group information from the access token.

    if (!GetTokenInformation(
            hToken, // handle to the access token
            TokenGroups, // get information about the token's groups
            static_cast<LPVOID>(ptg), // pointer to TOKEN_GROUPS buffer
            dwLength, // size of buffer
            &dwLength // receives required buffer size
            ))
    {
        goto Cleanup;
    }

    // Loop through the groups to find the logon SID.

    for (dwIndex = 0; dwIndex < ptg->GroupCount; dwIndex++)
        if ((ptg->Groups[dwIndex].Attributes & SE_GROUP_LOGON_ID) == SE_GROUP_LOGON_ID)
        {
            // Found the logon SID; make a copy of it.

            dwLength = GetLengthSid(ptg->Groups[dwIndex].Sid);
            *ppsid = static_cast<PSID>(HeapAlloc(GetProcessHeap(),
                                     HEAP_ZERO_MEMORY,
                                     dwLength));
            if (*ppsid == NULL)
                goto Cleanup;
            if (!CopySid(dwLength, *ppsid, ptg->Groups[dwIndex].Sid))
            {
                HeapFree(GetProcessHeap(), 0, static_cast<LPVOID>(*ppsid));
                *ppsid = nullptr;
                goto Cleanup;
            }
            bSuccess = TRUE;
            break;
        }

Cleanup:

    // Free the buffer for the token groups.

    if (ptg != NULL)
        HeapFree(GetProcessHeap(), 0, static_cast<LPVOID>(ptg));

    return bSuccess;
}

VOID TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::FreeLogonSID(PSID* ppsid)
{
    // From https://learn.microsoft.com/previous-versions/aa446670(v=vs.85)
    HeapFree(GetProcessHeap(), 0, static_cast<LPVOID>(*ppsid));
}

bool TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::create_pipe_security_attributes(HANDLE token, PipeSecurityAttributes& security_attributes)
{
    HANDLE process_token = nullptr;
    EXPLICIT_ACCESS entries[3]{};
    bool success = false;
    DWORD administrators_sid_size = 0;
    DWORD local_system_sid_size = 0;
    TOKEN_ELEVATION elevation{};
    DWORD elevation_size = 0;
    PSID server_sid = nullptr;
    TRUSTEE_TYPE server_trustee_type = TRUSTEE_IS_GROUP;
    auto set_entry = [](EXPLICIT_ACCESS& entry, DWORD access, PSID sid, TRUSTEE_TYPE trustee_type) {
        entry.grfAccessPermissions = access;
        entry.grfAccessMode = SET_ACCESS;
        entry.grfInheritance = NO_INHERITANCE;
        entry.Trustee.TrusteeForm = TRUSTEE_IS_SID;
        entry.Trustee.TrusteeType = trustee_type;
        entry.Trustee.ptstrName = static_cast<LPTSTR>(sid);
    };

    if (!token)
    {
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &process_token))
        {
            return false;
        }
        token = process_token;
    }

    if (!GetLogonSID(token, &security_attributes.logon_sid))
    {
        goto Cleanup;
    }

    administrators_sid_size = ARRAYSIZE(security_attributes.administrators_sid);
    local_system_sid_size = ARRAYSIZE(security_attributes.local_system_sid);
    if (!CreateWellKnownSid(WinBuiltinAdministratorsSid,
                            nullptr,
                            security_attributes.administrators_sid,
                            &administrators_sid_size) ||
        !CreateWellKnownSid(WinLocalSystemSid,
                            nullptr,
                            security_attributes.local_system_sid,
                            &local_system_sid_size))
    {
        goto Cleanup;
    }

    if (!GetTokenInformation(token, TokenElevation, &elevation, sizeof(elevation), &elevation_size))
    {
        goto Cleanup;
    }

    server_sid = security_attributes.administrators_sid;
    if (!elevation.TokenIsElevated)
    {
        // A non-elevated server has no identity distinct from its same-user clients. Retain its
        // existing multi-instance behavior with an explicit DACL; elevated Runner servers use the
        // Administrators-owned path below, which is the security boundary this transport needs.
        DWORD token_user_size = 0;
        GetTokenInformation(token, TokenUser, nullptr, 0, &token_user_size);
        auto* token_user = static_cast<TOKEN_USER*>(HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, token_user_size));
        if (!token_user ||
            !GetTokenInformation(token, TokenUser, token_user, token_user_size, &token_user_size))
        {
            if (token_user)
            {
                HeapFree(GetProcessHeap(), 0, token_user);
            }
            goto Cleanup;
        }

        const DWORD server_sid_size = GetLengthSid(token_user->User.Sid);
        const bool copied = server_sid_size <= ARRAYSIZE(security_attributes.server_sid) &&
                            CopySid(server_sid_size, security_attributes.server_sid, token_user->User.Sid) == TRUE;
        HeapFree(GetProcessHeap(), 0, token_user);
        if (!copied)
        {
            goto Cleanup;
        }

        server_sid = security_attributes.server_sid;
        server_trustee_type = TRUSTEE_IS_USER;
    }

    // The elevated server identity can change the DACL or create later instances. The
    // medium-integrity client receives the exact data/attribute rights it needs, never default or
    // creator-owner rights.
    set_entry(entries[0],
              FILE_ALL_ACCESS,
              server_sid,
              server_trustee_type);
    set_entry(entries[1],
              FILE_ALL_ACCESS,
              security_attributes.local_system_sid,
              TRUSTEE_IS_USER);
    set_entry(entries[2],
              PipeClientAccess,
              security_attributes.logon_sid,
              TRUSTEE_IS_USER);

    if (SetEntriesInAcl(ARRAYSIZE(entries), entries, nullptr, &security_attributes.dacl) != ERROR_SUCCESS)
    {
        goto Cleanup;
    }

    if (!InitializeSecurityDescriptor(&security_attributes.security_descriptor, SECURITY_DESCRIPTOR_REVISION) ||
        !SetSecurityDescriptorOwner(&security_attributes.security_descriptor,
                                    server_sid,
                                    FALSE) ||
        !SetSecurityDescriptorGroup(&security_attributes.security_descriptor,
                                    server_sid,
                                    FALSE) ||
        !SetSecurityDescriptorDacl(&security_attributes.security_descriptor,
                                   TRUE,
                                   security_attributes.dacl,
                                   FALSE))
    {
        goto Cleanup;
    }

    security_attributes.attributes.nLength = sizeof(security_attributes.attributes);
    security_attributes.attributes.lpSecurityDescriptor = &security_attributes.security_descriptor;
    security_attributes.attributes.bInheritHandle = FALSE;
    success = true;

Cleanup:
    if (process_token)
    {
        CloseHandle(process_token);
    }
    return success;
}

HANDLE TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::create_medium_integrity_token()
{
    HANDLE restricted_token_handle;
    SAFER_LEVEL_HANDLE level_handle = NULL;
    DWORD sid_size = SECURITY_MAX_SID_SIZE;
    BYTE medium_sid[SECURITY_MAX_SID_SIZE];
    if (!SaferCreateLevel(SAFER_SCOPEID_USER, SAFER_LEVELID_NORMALUSER, SAFER_LEVEL_OPEN, &level_handle, NULL))
    {
        return NULL;
    }
    if (!SaferComputeTokenFromLevel(level_handle, NULL, &restricted_token_handle, 0, NULL))
    {
        SaferCloseLevel(level_handle);
        return NULL;
    }
    SaferCloseLevel(level_handle);

    if (!CreateWellKnownSid(WinMediumLabelSid, nullptr, medium_sid, &sid_size))
    {
        CloseHandle(restricted_token_handle);
        return NULL;
    }

    TOKEN_MANDATORY_LABEL integrity_level = { 0 };
    integrity_level.Label.Attributes = SE_GROUP_INTEGRITY;
    integrity_level.Label.Sid = reinterpret_cast<PSID>(medium_sid);

    if (!SetTokenInformation(restricted_token_handle, TokenIntegrityLevel, &integrity_level, sizeof(integrity_level)))
    {
        CloseHandle(restricted_token_handle);
        return NULL;
    }

    return restricted_token_handle;
}

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::handle_pipe_connection(const std::shared_ptr<ConnectionHandler>& handler)
{
    const HANDLE input_pipe_handle = handler->pipe_handle.get();
    if (input_pipe_handle == INVALID_HANDLE_VALUE)
    {
        finish_connection_handler(handler);
        return;
    }

    bool accepted = !closed.load();

    // Authenticate the connecting client before reading/queuing anything. Fail-closed: an unauthenticated
    // caller gets no dispatch. When the policy is disabled (managed server / tests) this is a no-op.
    if (accepted && caller_policy.enabled)
    {
        const interop_auth::AuthResult auth = interop_auth::AuthenticateClient(input_pipe_handle, caller_policy, caller_cache);
        if (!auth.accepted)
        {
            accepted = false;
        }
    }

    if (accepted && !closed.load())
    {
        constexpr DWORD readBlockBytes = BUFSIZE;
        std::wstring message;
        size_t iBlock = 0;
        message.reserve(BUFSIZE);
        bool message_read = false;
        do
        {
            constexpr size_t charsPerBlock = readBlockBytes / sizeof(message[0]);
            message.resize(message.size() + charsPerBlock);
            DWORD bytesRead = 0;
            message_read = ReadFile(
                input_pipe_handle,
                // Read the message directly into the string block by block while resizing it.
                message.data() + iBlock * charsPerBlock,
                readBlockBytes,
                &bytesRead,
                nullptr);

            if (!message_read && GetLastError() != ERROR_MORE_DATA)
            {
                break;
            }
            iBlock++;
        } while (!message_read);

        if (message_read && !closed.load())
        {
            // Trim the message's buffer.
            const auto nullCharPos = message.find_last_not_of(L'\0');
            if (nullCharPos != std::wstring::npos)
            {
                message.resize(nullCharPos + 1);
            }

            input_queue.queue_message(std::move(message));

            // Flush the pipe to allow the client to read the pipe's contents before disconnecting.
            FlushFileBuffers(input_pipe_handle);
        }
    }
    finish_connection_handler(handler);
#ifdef TWO_WAY_PIPE_MESSAGE_IPC_TESTS
    if (const HANDLE completed_event = handler_completed_event.load())
    {
        SetEvent(completed_event);
        if (const HANDLE allow_return_event = handler_allow_return_event.load())
        {
            WaitForSingleObject(allow_return_event, 10'000);
        }
    }
#endif
}

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::finish_connection_handler(const std::shared_ptr<ConnectionHandler>& handler)
{
    HANDLE pipe_handle = INVALID_HANDLE_VALUE;
    {
        std::scoped_lock lock(connection_handlers_mutex);
        pipe_handle = handler->pipe_handle.get();
        if (pipe_handle != INVALID_HANDLE_VALUE)
        {
            DisconnectNamedPipe(pipe_handle);
        }
        handler->pipe_handle.reset();
    }

    {
        std::scoped_lock lock(connection_handlers_mutex);
        handler->completed = true;
    }
}

bool TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::start_connection_handler(OwnedPipeHandle&& pipe_handle)
{
    auto handler = std::make_shared<ConnectionHandler>(std::move(pipe_handle));
    {
        std::scoped_lock lock(connection_handlers_mutex);
        connection_handlers.emplace_back(handler);
    }

    try
    {
#ifdef TWO_WAY_PIPE_MESSAGE_IPC_TESTS
        inject_handler_thread_start_failure();
#endif
        handler->thread = std::thread(&TwoWayPipeMessageIPCImpl::handle_pipe_connection, this, handler);
        return true;
    }
    catch (...)
    {
        finish_connection_handler(handler);
        reap_finished_connection_handlers();
        return false;
    }
}

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::reap_finished_connection_handlers()
{
    std::vector<std::shared_ptr<ConnectionHandler>> completed_handlers;
    {
        std::scoped_lock lock(connection_handlers_mutex);
        for (auto it = connection_handlers.begin(); it != connection_handlers.end();)
        {
            if ((*it)->completed)
            {
                completed_handlers.emplace_back(*it);
                it = connection_handlers.erase(it);
            }
            else
            {
                ++it;
            }
        }
    }

    for (const auto& handler : completed_handlers)
    {
        if (handler->thread.joinable())
        {
            handler->thread.join();
        }
    }
}

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::cancel_and_wait_for_connection_handlers()
{
    std::vector<std::shared_ptr<ConnectionHandler>> handlers;
    {
        std::scoped_lock lock(connection_handlers_mutex);
        for (const auto& handler : connection_handlers)
        {
            if (handler->pipe_handle.valid())
            {
                CancelIoEx(handler->pipe_handle.get(), nullptr);
                DisconnectNamedPipe(handler->pipe_handle.get());
            }
        }
        handlers.swap(connection_handlers);
    }

    for (const auto& handler : handlers)
    {
        if (handler->thread.joinable())
        {
            handler->thread.join();
        }
    }
}

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::start_named_pipe_server(HANDLE token)
{
    // Adapted from https://learn.microsoft.com/windows/win32/ipc/multithreaded-pipe-server
    const wchar_t* pipe_name = input_pipe_name.c_str();
    // Create the first instance with FILE_FLAG_FIRST_PIPE_INSTANCE so that CreateNamedPipe
    // fails fast if a pipe with this name already exists (for example a leftover instance
    // from a previous run or another process), making this server the sole owner of the
    // pipe name instead of silently sharing it. The flag is only valid on the first
    // instance; subsequent instances must omit it.
    auto create_listener = [&](bool first_instance) {
#ifdef TWO_WAY_PIPE_MESSAGE_IPC_TESTS
        if (!first_instance)
        {
            if (const HANDLE reached_event = before_replacement_listener_event.load())
            {
                SetEvent(reached_event);
                if (const HANDLE allow_creation_event = allow_replacement_listener_event.load())
                {
                    WaitForSingleObject(allow_creation_event, 10'000);
                }
            }
        }
#endif
        DWORD open_mode = PIPE_ACCESS_DUPLEX | WRITE_DAC;
        if (first_instance)
        {
            open_mode |= FILE_FLAG_FIRST_PIPE_INSTANCE;
        }

        PipeSecurityAttributes security_attributes;
        if (!create_pipe_security_attributes(token, security_attributes))
        {
            return INVALID_HANDLE_VALUE;
        }

        return CreateNamedPipe(
            pipe_name,
            open_mode,
            PIPE_TYPE_MESSAGE |
                PIPE_READMODE_MESSAGE |
                PIPE_WAIT |
                PIPE_REJECT_REMOTE_CLIENTS,
            PIPE_UNLIMITED_INSTANCES,
            BUFSIZE,
            BUFSIZE,
            0,
            &security_attributes.attributes);
    };

    OwnedPipeHandle listener{ create_listener(true) };
    if (!listener.valid())
    {
        return;
    }

    while (!closed.load())
    {
        {
            std::unique_lock lock(pipe_connect_handle_mutex);
            if (closed.load())
            {
                break;
            }
            current_connect_pipe_handle = listener.get();
        }
        const BOOL connected = ConnectNamedPipe(listener.get(), NULL) ? TRUE : (GetLastError() == ERROR_PIPE_CONNECTED);
        {
            std::unique_lock lock(pipe_connect_handle_mutex);
            current_connect_pipe_handle = NULL;
        }

        if (!connected)
        {
            if (closed.load())
            {
                break;
            }
            DisconnectNamedPipe(listener.get());
            continue;
        }

        // Claim the replacement listener before giving the accepted instance to its handler. This
        // keeps at least one secured instance alive even if a rejected client closes immediately.
        OwnedPipeHandle replacement;
        while (!closed.load() && !replacement.valid())
        {
            replacement.reset(create_listener(false));
            if (!replacement.valid() && !closed.load())
            {
                Sleep(10);
            }
        }

#ifdef TWO_WAY_PIPE_MESSAGE_IPC_TESTS
        if (replacement.valid())
        {
            if (const HANDLE reached_event = after_replacement_listener_event.load())
            {
                SetEvent(reached_event);
                if (const HANDLE allow_continue_event = allow_after_replacement_listener_event.load())
                {
                    WaitForSingleObject(allow_continue_event, 10'000);
                }
            }
        }
#endif
        if (closed.load())
        {
            break;
        }

        start_connection_handler(std::move(listener));
        listener = std::move(replacement);
        reap_finished_connection_handlers();
    }

    if (listener.valid())
    {
        DisconnectNamedPipe(listener.get());
    }
    reap_finished_connection_handlers();
}

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::consume_input_queue_thread()
{
    while (!closed.load())
    {
        outgoing_message = L"";
        std::wstring message = input_queue.pop_message();
        if (message.length() == 0)
        {
            break;
        }

        // Check if callback method exists first before trying to call it.
        // otherwise just store the response message in a variable.
        if (dispatch_inc_message_function != nullptr)
        {
            dispatch_inc_message_function(message);
        }
        outgoing_message = message;
    }
}
