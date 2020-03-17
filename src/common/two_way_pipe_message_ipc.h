#pragma once
#include <Windows.h>
#include "async_message_queue.h"
#include <WinSafer.h>
#include <Sddl.h>
#include <accctrl.h>
#include <aclapi.h>
#include <list>

class TwoWayPipeMessageIPC
{
public:
    typedef void (*callback_function)(const std::wstring&);
    void send(std::wstring msg)
    {
        output_queue.queue_message(msg);
    }
    TwoWayPipeMessageIPC(std::wstring _input_pipe_name, std::wstring _output_pipe_name, callback_function p_func)
    {
        input_pipe_name = _input_pipe_name;
        output_pipe_name = _output_pipe_name;
        dispatch_inc_message_function = p_func;
    }
    void start(HANDLE _restricted_pipe_token)
    {
        output_queue_thread = std::thread(&TwoWayPipeMessageIPC::consume_output_queue_thread, this);
        input_queue_thread = std::thread(&TwoWayPipeMessageIPC::consume_input_queue_thread, this);
        input_pipe_thread = std::thread(&TwoWayPipeMessageIPC::start_named_pipe_server, this, _restricted_pipe_token);
    }

    void end()
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

private:
    AsyncMessageQueue input_queue;
    AsyncMessageQueue output_queue;
    std::wstring output_pipe_name;
    std::wstring input_pipe_name;
    std::thread input_queue_thread;
    std::thread output_queue_thread;
    std::thread input_pipe_thread;
    std::mutex pipe_connect_handle_mutex; // For manipulating the current_connect_pipe
    std::wstring outgoing_message; // Store the updated json settings.  

    HANDLE current_connect_pipe_handle = NULL;
    bool closed = false;
    TwoWayPipeMessageIPC::callback_function dispatch_inc_message_function;
    const DWORD BUFSIZE = 1024;

    void send_pipe_message(std::wstring message)
    {
        // Adapted from https://docs.microsoft.com/en-us/windows/win32/ipc/named-pipe-client
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
    void consume_output_queue_thread()
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
                                 
    BOOL GetLogonSID(HANDLE hToken, PSID* ppsid)
    {
        // From https://docs.microsoft.com/en-us/previous-versions/aa446670(v=vs.85)
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

            ptg = (PTOKEN_GROUPS)HeapAlloc(GetProcessHeap(),
                                           HEAP_ZERO_MEMORY,
                                           dwLength);

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
                *ppsid = (PSID)HeapAlloc(GetProcessHeap(),
                                         HEAP_ZERO_MEMORY,
                                         dwLength);
                if (*ppsid == NULL)
                    goto Cleanup;
                if (!CopySid(dwLength, *ppsid, ptg->Groups[dwIndex].Sid))
                {
                    HeapFree(GetProcessHeap(), 0, (LPVOID)*ppsid);
                    goto Cleanup;
                }
                break;
            }

        bSuccess = TRUE;

    Cleanup:

        // Free the buffer for the token groups.

        if (ptg != NULL)
            HeapFree(GetProcessHeap(), 0, (LPVOID)ptg);

        return bSuccess;
    }

    VOID FreeLogonSID(PSID* ppsid)
    {
        // From https://docs.microsoft.com/en-us/previous-versions/aa446670(v=vs.85)
        HeapFree(GetProcessHeap(), 0, (LPVOID)*ppsid);
    }

    int change_pipe_security_allow_restricted_token(HANDLE handle, HANDLE token)
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
        ea.Trustee.ptstrName = (LPTSTR)user_restricted;

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
        LocalFree((HLOCAL)new_dacl);
    Lclean_sd:
        LocalFree((HLOCAL)sd);
    Lclean_sid:
        FreeLogonSID(&user_restricted);
    Ldone:
        return error;
    }

    HANDLE create_medium_integrity_token()
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

    void handle_pipe_connection(HANDLE input_pipe_handle)
    {
        //Adapted from https://docs.microsoft.com/en-us/windows/win32/ipc/multithreaded-pipe-server
        HANDLE hHeap = GetProcessHeap();
        uint8_t* pchRequest = (uint8_t*)HeapAlloc(hHeap, 0, BUFSIZE * sizeof(uint8_t));

        DWORD cbBytesRead = 0, cbReplyBytes = 0, cbWritten = 0;
        BOOL fSuccess = FALSE;

        // Do some extra error checking since the app will keep running even if this thread fails.
        std::list<std::vector<uint8_t>> message_parts;

        if (input_pipe_handle == NULL)
        {
            if (pchRequest != NULL)
                HeapFree(hHeap, 0, pchRequest);
            return;
        }

        if (pchRequest == NULL)
        {
            return;
        }

        // Loop until done reading
        do
        {
            // Read client requests from the pipe. This simplistic code only allows messages
            // up to BUFSIZE characters in length.
            ZeroMemory(pchRequest, BUFSIZE * sizeof(uint8_t));
            fSuccess = ReadFile(
                input_pipe_handle, // handle to pipe
                pchRequest, // buffer to receive data
                BUFSIZE * sizeof(uint8_t), // size of buffer
                &cbBytesRead, // number of bytes read
                NULL); // not overlapped I/O

            if (!fSuccess && GetLastError() != ERROR_MORE_DATA)
            {
                break;
            }
            std::vector<uint8_t> part_vector;
            part_vector.reserve(cbBytesRead);
            std::copy(pchRequest, pchRequest + cbBytesRead, std::back_inserter(part_vector));
            message_parts.push_back(part_vector);
        } while (!fSuccess);

        if (fSuccess)
        {
            // Reconstruct the total_message.
            std::vector<uint8_t> reconstructed_message;
            size_t total_size = 0;
            for (auto& part_vector : message_parts)
            {
                total_size += part_vector.size();
            }
            reconstructed_message.reserve(total_size);
            for (auto& part_vector : message_parts)
            {
                std::move(part_vector.begin(), part_vector.end(), std::back_inserter(reconstructed_message));
            }
            std::wstring unicode_msg;
            unicode_msg.assign(reinterpret_cast<std::wstring::const_pointer>(reconstructed_message.data()), reconstructed_message.size() / sizeof(std::wstring::value_type));
            input_queue.queue_message(unicode_msg);
        }

        // Flush the pipe to allow the client to read the pipe's contents
        // before disconnecting. Then disconnect the pipe, and close the
        // handle to this pipe instance.

        FlushFileBuffers(input_pipe_handle);
        DisconnectNamedPipe(input_pipe_handle);
        CloseHandle(input_pipe_handle);

        HeapFree(hHeap, 0, pchRequest);

        printf("InstanceThread exitting.\n");
    }

    void start_named_pipe_server(HANDLE token)
    {
        // Adapted from https://docs.microsoft.com/en-us/windows/win32/ipc/multithreaded-pipe-server
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
                    int err = change_pipe_security_allow_restricted_token(connect_pipe_handle, token);
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
                std::thread(&TwoWayPipeMessageIPC::handle_pipe_connection, this, connect_pipe_handle).detach();
            }
            else
            {
                // Client could not connect.
                CloseHandle(connect_pipe_handle);
            }
        }
    }

    void consume_input_queue_thread()
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
};
