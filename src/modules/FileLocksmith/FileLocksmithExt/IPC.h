#pragma once

#include "pch.h"

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

    private:
        std::ofstream m_stream;
    };
}
