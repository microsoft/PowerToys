#include "pch.h"

#include "IPC.h"

constexpr DWORD DefaultPipeBufferSize = 8192;
constexpr DWORD DefaultPipeTimeoutMillis = 200;

namespace ipc
{
    Writer::Writer()
    {
        start();
    }

    Writer::~Writer()
    {
        finish();
    }

    HRESULT Writer::start()
    {
        SECURITY_ATTRIBUTES sa;
        sa.nLength = sizeof(SECURITY_ATTRIBUTES);
        sa.lpSecurityDescriptor = NULL;
        sa.bInheritHandle = TRUE;

        if (!CreatePipe(&m_read_pipe, &m_write_pipe, &sa, 0))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        return S_OK;
    }

    HRESULT Writer::add_path(LPCWSTR path)
    {
        int length = lstrlenW(path);
        DWORD written;
        if (!WriteFile(m_write_pipe, path, length * sizeof(WCHAR), &written, NULL))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        if (written != length * sizeof(WCHAR))
        {
            return E_FAIL;
        }

        WCHAR line_break = L'\n';
        if (!WriteFile(m_write_pipe, &line_break, sizeof(WCHAR), &written, NULL))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        if (written != sizeof(WCHAR))
        {
            return E_FAIL;
        }

        return S_OK;
    }

    void Writer::finish()
    {
        add_path(L"");

        if (m_write_pipe)
        {
            CloseHandle(m_write_pipe);
            m_write_pipe = NULL;
        }

        if (m_read_pipe)
        {
            CloseHandle(m_read_pipe);
            m_read_pipe = NULL;
        }
    }

    HANDLE Writer::get_read_handle()
    {
        HANDLE result = m_read_pipe;
        m_read_pipe = NULL;
        return result;
    }
}
