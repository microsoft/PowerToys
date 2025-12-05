#pragma once

#include "pch.h"

#include <fstream>
#include <thread>
#include <string>

namespace ipc
{
    class Writer
    {
    public:
        Writer();
        ~Writer();
        HRESULT start();
        HRESULT add_path(LPCWSTR path);
        void finish();
        HANDLE get_read_handle();
        std::wstring get_pipe_name() const { return m_pipe_name; }

    private:
        HRESULT create_pipe_server();
        void start_pipe_server_thread();
        
        std::ofstream m_stream; // Keep for backwards compatibility
        HANDLE m_pipe_handle;
        std::wstring m_pipe_name;
        std::thread m_pipe_thread;
        bool m_use_pipes;
    };
}
