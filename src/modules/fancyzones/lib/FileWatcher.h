#pragma once

#include "pch.h"

class FileWatcher
{
    DWORD m_refreshPeriod;
    std::wstring m_path;
    std::optional<FILETIME> m_lastWrite;
    std::function<void()> m_callback;
    std::thread m_thread;
    std::mutex m_mutex;
    std::atomic<bool> m_abort{ false };
    HANDLE m_abortEvent;

    std::optional<FILETIME> MyFileTime()
    {
        HANDLE hFile = CreateFileW(m_path.c_str(), 0, FILE_SHARE_DELETE | FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, 0, NULL);
        std::optional<FILETIME> result;
        if (hFile != INVALID_HANDLE_VALUE)
        {
            FILETIME lastWrite;
            if (GetFileTime(hFile, NULL, NULL, &lastWrite))
            {
                result = lastWrite;
            }

            CloseHandle(hFile);
        }

        return result;
    }

    void Run()
    {
        while (1)
        {
            {
                std::unique_lock lock(m_mutex);
                if (m_abort)
                {
                    return;
                }

                auto lastWrite = MyFileTime();
                if (!m_lastWrite.has_value())
                {
                    m_lastWrite = lastWrite;
                }
                else if (lastWrite.has_value())
                {
                    if (m_lastWrite->dwHighDateTime != lastWrite->dwHighDateTime ||
                        m_lastWrite->dwLowDateTime != lastWrite->dwLowDateTime)
                    {
                        m_lastWrite = lastWrite;
                        m_callback();
                    }
                }
            }

            if (WaitForSingleObject(m_abortEvent, m_refreshPeriod) == WAIT_OBJECT_0)
            {
                return;
            }
        }
    }

public:

    FileWatcher(const std::wstring& path, std::function<void()> callback, DWORD refreshPeriod = 1000) :
        m_path(path),
        m_callback(callback),
        m_refreshPeriod(refreshPeriod),
        m_thread([this]() { Run(); })
    {
        m_abortEvent = CreateEventW(NULL, TRUE, FALSE, NULL);
    }

    ~FileWatcher()
    {
        {
            std::unique_lock lock(m_mutex);
            m_abort = true;
            SetEvent(m_abortEvent);
        }

        m_thread.join();
        CloseHandle(m_abortEvent);
    }
};
