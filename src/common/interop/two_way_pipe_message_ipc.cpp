#include "pch.h"
#include "two_way_pipe_message_ipc_impl.h"

#include <iterator>

constexpr DWORD BUFSIZE = 1024;

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
    output_queue_thread = std::thread(&TwoWayPipeMessageIPCImpl::consume_output_queue_thread, this);
    input_queue_thread = std::thread(&TwoWayPipeMessageIPCImpl::consume_input_queue_thread, this);
    input_pipe_thread = std::thread(&TwoWayPipeMessageIPCImpl::start_named_pipe_server, this, _restricted_pipe_token);
}

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::end()
{
    closed = true;
    input_queue.interrupt();
    input_queue_thread.join();
    output_queue.interrupt();
    output_queue_thread.join();
    pipe_connect_handle_mutex.lock();
    if (current_connect_pipe_handle != NULL)
    {
        //Cancels the Pipe currently waiting for a connection.
        CancelIoEx(current_connect_pipe_handle, NULL);
    }
    pipe_connect_handle_mutex.unlock();
    input_pipe_thread.join();
}

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::send_pipe_message(std::wstring message)
{
    // Adapted from https://learn.microsoft.com/windows/win32/ipc/named-pipe-client
    HANDLE output_pipe_handle;
    const wchar_t* message_send = message.c_str();
    BOOL fSuccess = FALSE;
    DWORD cbToWrite, cbWritten, dwMode;
    const wchar_t* lpszPipename = output_pipe_name.c_str();

    // Try to open a named pipe; wait for it, if necessary.

    while (1)
    {
        output_pipe_handle = CreateFile(
            lpszPipename, // pipe name
            GENERIC_READ | // read and write access
                GENERIC_WRITE,
            0, // no sharing
            NULL, // default security attributes
            OPEN_EXISTING, // opens existing pipe
            0, // default attributes
            NULL); // no template file

        // Break if the pipe handle is valid.

        if (output_pipe_handle != INVALID_HANDLE_VALUE)
            break;

        // Exit if an error other than ERROR_PIPE_BUSY occurs.
        DWORD curr_error = 0;
        if ((curr_error = GetLastError()) != ERROR_PIPE_BUSY)
        {
            return;
        }

        // All pipe instances are busy, so wait for 20 seconds.

        if (!WaitNamedPipe(lpszPipename, 20000))
        {
            return;
        }
    }
    dwMode = PIPE_READMODE_MESSAGE;
    fSuccess = SetNamedPipeHandleState(
        output_pipe_handle, // pipe handle
        &dwMode, // new pipe mode
        NULL, // don't set maximum bytes
        NULL); // don't set maximum time
    if (!fSuccess)
    {
        return;
    }

    // Send a message to the pipe server.

    cbToWrite = (lstrlen(message_send)) * sizeof(WCHAR); // no need to send final '\0'. Pipe is in message mode.

    fSuccess = WriteFile(
        output_pipe_handle, // pipe handle
        message_send, // message
        cbToWrite, // message length
        &cbWritten, // bytes written
        NULL); // not overlapped
    if (!fSuccess)
    {
        return;
    }
    CloseHandle(output_pipe_handle);
    return;
}

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::consume_output_queue_thread()
{
    while (!closed)
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

    // Get required buffer size and allocate the TOKEN_GROUPS buffer.

    if (!GetTokenInformation(
            hToken, // handle to the access token
            TokenGroups, // get information about the token's groups
            (LPVOID)ptg, // pointer to TOKEN_GROUPS buffer
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
            (LPVOID)ptg, // pointer to TOKEN_GROUPS buffer
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
                goto Cleanup;
            }
            break;
        }

    bSuccess = TRUE;

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

int TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::change_pipe_security_allow_restricted_token(HANDLE handle, HANDLE token)
{
    PACL old_dacl, new_dacl;
    PSECURITY_DESCRIPTOR sd;
    EXPLICIT_ACCESS ea;
    PSID user_restricted;
    int error;

    if (!GetLogonSID(token, &user_restricted))
    {
        error = 5; // No access error.
        goto Ldone;
    }

    if (GetSecurityInfo(handle,
                        SE_KERNEL_OBJECT,
                        DACL_SECURITY_INFORMATION,
                        NULL,
                        NULL,
                        &old_dacl,
                        NULL,
                        &sd))
    {
        error = GetLastError();
        goto Lclean_sid;
    }

    memset(&ea, 0, sizeof(EXPLICIT_ACCESS));
    ea.grfAccessPermissions |= GENERIC_READ | FILE_WRITE_ATTRIBUTES;
    ea.grfAccessPermissions |= GENERIC_WRITE | FILE_READ_ATTRIBUTES;
    ea.grfAccessPermissions |= SYNCHRONIZE;
    ea.grfAccessMode = SET_ACCESS;
    ea.grfInheritance = NO_INHERITANCE;
    ea.Trustee.TrusteeForm = TRUSTEE_IS_SID;
    ea.Trustee.TrusteeType = TRUSTEE_IS_USER;
    ea.Trustee.ptstrName = static_cast<LPTSTR>(user_restricted);

    if (SetEntriesInAcl(1, &ea, old_dacl, &new_dacl))
    {
        error = GetLastError();
        goto Lclean_sd;
    }

    if (SetSecurityInfo(handle,
                        SE_KERNEL_OBJECT,
                        DACL_SECURITY_INFORMATION,
                        NULL,
                        NULL,
                        new_dacl,
                        NULL))
    {
        error = GetLastError();
        goto Lclean_dacl;
    }

    error = 0;

Lclean_dacl:
    LocalFree(static_cast<HLOCAL>(new_dacl));
Lclean_sd:
    LocalFree(static_cast<HLOCAL>(sd));
Lclean_sid:
    FreeLogonSID(&user_restricted);
Ldone:
    return error;
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

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::handle_pipe_connection(HANDLE input_pipe_handle)
{
    if (!input_pipe_handle)
    {
        return;
    }
    constexpr DWORD readBlockBytes = BUFSIZE;
    std::wstring message;
    size_t iBlock = 0;
    message.reserve(BUFSIZE);
    bool ok;
    do
    {
        constexpr size_t charsPerBlock = readBlockBytes / sizeof(message[0]);
        message.resize(message.size() + charsPerBlock);
        DWORD bytesRead = 0;
        ok = ReadFile(
            input_pipe_handle,
            // read the message directly into the string block by block simultaneously resizing it
            message.data() + iBlock * charsPerBlock,
            readBlockBytes,
            &bytesRead,
            nullptr);

        if (!ok && GetLastError() != ERROR_MORE_DATA)
        {
            break;
        }
        iBlock++;
    } while (!ok);
    // trim the message's buffer
    const auto nullCharPos = message.find_last_not_of(L'\0');
    if (nullCharPos != std::wstring::npos)
    {
        message.resize(nullCharPos + 1);
    }

    input_queue.queue_message(std::move(message));

    // Flush the pipe to allow the client to read the pipe's contents
    // before disconnecting. Then disconnect the pipe, and close the
    // handle to this pipe instance.

    FlushFileBuffers(input_pipe_handle);
    DisconnectNamedPipe(input_pipe_handle);
    CloseHandle(input_pipe_handle);
}

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::start_named_pipe_server(HANDLE token)
{
    // Adapted from https://learn.microsoft.com/windows/win32/ipc/multithreaded-pipe-server
    const wchar_t* pipe_name = input_pipe_name.c_str();
    BOOL connected = FALSE;
    HANDLE connect_pipe_handle = INVALID_HANDLE_VALUE;
    while (!closed)
    {
        {
            std::unique_lock lock(pipe_connect_handle_mutex);
            connect_pipe_handle = CreateNamedPipe(
                pipe_name,
                PIPE_ACCESS_DUPLEX |
                    WRITE_DAC,
                PIPE_TYPE_MESSAGE |
                    PIPE_READMODE_MESSAGE |
                    PIPE_WAIT,
                PIPE_UNLIMITED_INSTANCES,
                BUFSIZE,
                BUFSIZE,
                0,
                NULL);

            if (connect_pipe_handle == INVALID_HANDLE_VALUE)
            {
                return;
            }

            if (token != NULL)
            {
                change_pipe_security_allow_restricted_token(connect_pipe_handle, token);
            }
            current_connect_pipe_handle = connect_pipe_handle;
        }
        connected = ConnectNamedPipe(connect_pipe_handle, NULL) ? TRUE : (GetLastError() == ERROR_PIPE_CONNECTED);
        {
            std::unique_lock lock(pipe_connect_handle_mutex);
            current_connect_pipe_handle = NULL;
        }
        if (connected)
        {
            std::thread(&TwoWayPipeMessageIPCImpl::handle_pipe_connection, this, connect_pipe_handle).detach();
        }
        else
        {
            // Client could not connect.
            CloseHandle(connect_pipe_handle);
        }
    }
}

void TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl::consume_input_queue_thread()
{
    while (!closed)
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
