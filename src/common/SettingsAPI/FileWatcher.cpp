#include "pch.h"
#include "FileWatcher.h"

std::optional<FILETIME> FileWatcher::MyFileTime()
{
    HANDLE hFile = CreateFileW(m_path.c_str(), FILE_READ_ATTRIBUTES, FILE_SHARE_DELETE | FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, 0, nullptr);
    std::optional<FILETIME> result;
    if (hFile != INVALID_HANDLE_VALUE)
    {
        FILETIME lastWrite;
        if (GetFileTime(hFile, nullptr, nullptr, &lastWrite))
        {
            result = lastWrite;
        }

        CloseHandle(hFile);
    }

    return result;
}

void FileWatcher::Run()
{
    while (1)
    {
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

        if (WaitForSingleObject(m_abortEvent, m_refreshPeriod) == WAIT_OBJECT_0)
        {
            return;
        }
    }
}

FileWatcher::FileWatcher(const std::wstring& path, std::function<void()> callback, DWORD refreshPeriod) :
    m_refreshPeriod(refreshPeriod),
    m_path(path),
    m_callback(callback)
{
    m_abortEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (m_abortEvent)
    {
        m_thread = std::thread([this]() { Run(); });
    }
}

FileWatcher::~FileWatcher()
{
    if (m_abortEvent)
    {
        SetEvent(m_abortEvent);
        m_thread.join();
        CloseHandle(m_abortEvent);
    }
}
