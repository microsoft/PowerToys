#pragma once

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <Windows.h>

#include <thread>
#include <optional>
#include <string>
#include <functional>

class FileWatcher
{
    DWORD m_refreshPeriod;
    std::wstring m_path;
    std::optional<FILETIME> m_lastWrite;
    std::function<void()> m_callback;
    HANDLE m_abortEvent;
    std::thread m_thread;
    
    std::optional<FILETIME> MyFileTime();
    void Run();
public:
    FileWatcher(const std::wstring& path, std::function<void()> callback, DWORD refreshPeriod = 1000);
    ~FileWatcher();
};
