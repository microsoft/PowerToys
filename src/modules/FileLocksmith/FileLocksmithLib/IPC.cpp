#include "pch.h"

#include "IPC.h"
#include "Constants.h"

#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/logger_helper.h>
#include <thread>
#include <sstream>
#include <rpc.h>

constexpr DWORD DefaultPipeBufferSize = 8192;
constexpr DWORD DefaultPipeTimeoutMillis = 200;

namespace ipc
{
    Writer::Writer() : m_pipe_handle(INVALID_HANDLE_VALUE), m_use_pipes(true)
    {
        start();
    }

    Writer::~Writer()
    {
        finish();
    }

    HRESULT Writer::start()
    {
        // Try to use pipes first, fall back to file-based IPC if needed
        if (m_use_pipes)
        {
            // Generate unique pipe name similar to PowerRename
            UUID temp_uuid;
            wchar_t* uuid_chars = nullptr;
            
            if (UuidCreate(&temp_uuid) == RPC_S_OK &&
                UuidToString(&temp_uuid, reinterpret_cast<RPC_WSTR*>(&uuid_chars)) == RPC_S_OK)
            {
                m_pipe_name = L"\\\\.\\pipe\\powertoys_filelocksmith_input_";
                m_pipe_name += uuid_chars;
                RpcStringFree(reinterpret_cast<RPC_WSTR*>(&uuid_chars));
                
                HRESULT hr = create_pipe_server();
                if (SUCCEEDED(hr))
                {
                    return hr;
                }
            }
            
            // If pipe creation failed, fall back to file-based IPC
            m_use_pipes = false;
        }
        
        // File-based IPC fallback
        std::wstring path = PTSettingsHelper::get_module_save_folder_location(constants::nonlocalizable::PowerToyName);
        path += L"\\";
        path += constants::nonlocalizable::LastRunPath;

        try
        {
            m_stream = std::ofstream(path);
            return S_OK;
        }
        catch (...)
        {
            return E_FAIL;
        }
    }

    HRESULT Writer::create_pipe_server()
    {
        m_pipe_handle = CreateNamedPipe(
            m_pipe_name.c_str(),
            PIPE_ACCESS_DUPLEX | WRITE_DAC,
            PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
            PIPE_UNLIMITED_INSTANCES,
            DefaultPipeBufferSize,
            DefaultPipeBufferSize,
            DefaultPipeTimeoutMillis,
            NULL);

        if (m_pipe_handle == NULL || m_pipe_handle == INVALID_HANDLE_VALUE)
        {
            return E_FAIL;
        }

        start_pipe_server_thread();
        return S_OK;
    }

    void Writer::start_pipe_server_thread()
    {
        m_pipe_thread = std::thread([this]() {
            // This call blocks until a client process connects to the pipe
            BOOL connected = ConnectNamedPipe(m_pipe_handle, NULL);
            if (!connected && GetLastError() != ERROR_PIPE_CONNECTED)
            {
                CloseHandle(m_pipe_handle);
                m_pipe_handle = INVALID_HANDLE_VALUE;
            }
        });
    }

    HRESULT Writer::add_path(LPCWSTR path)
    {
        if (m_use_pipes && m_pipe_handle != INVALID_HANDLE_VALUE)
        {
            // Wait for pipe connection to be established
            if (m_pipe_thread.joinable())
            {
                m_pipe_thread.join();
            }

            if (m_pipe_handle != INVALID_HANDLE_VALUE)
            {
                DWORD path_length_bytes = static_cast<DWORD>((wcslen(path) + 1) * sizeof(WCHAR)); // +1 for delimiter
                WCHAR delimited_path[MAX_PATH + 1];
                wcscpy_s(delimited_path, MAX_PATH, path);
                wcscat_s(delimited_path, MAX_PATH, L"?"); // Use '?' as delimiter like PowerRename
                
                DWORD bytes_written;
                BOOL result = WriteFile(m_pipe_handle, delimited_path, path_length_bytes, &bytes_written, NULL);
                return result ? S_OK : E_FAIL;
            }
        }
        
        // File-based IPC fallback
        int length = lstrlenW(path);
        if (!m_stream.write(reinterpret_cast<const char*>(path), length * sizeof(WCHAR)))
        {
            return E_FAIL;
        }

        WCHAR line_break = L'\n';
        if (!m_stream.write(reinterpret_cast<const char*>(&line_break), sizeof(WCHAR)))
        {
            return E_FAIL;
        }

        return S_OK;
    }

    void Writer::finish()
    {
        if (m_use_pipes)
        {
            if (m_pipe_thread.joinable())
            {
                m_pipe_thread.join();
            }
            
            if (m_pipe_handle != INVALID_HANDLE_VALUE)
            {
                CloseHandle(m_pipe_handle);
                m_pipe_handle = INVALID_HANDLE_VALUE;
            }
        }
        else
        {
            // File-based IPC
            add_path(L"");
            m_stream.close();
        }
    }
}
