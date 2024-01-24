#include "pch.h"
#include "FileWatcher.h"
#include <utils/winapi_error.h>

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

FileWatcher::FileWatcher(const std::wstring& path, std::function<void()> callback) :
    m_path(path),
    m_callback(callback)
{
    std::filesystem::path fsPath(path);
    m_file_name = fsPath.filename();
    std::transform(m_file_name.begin(), m_file_name.end(), m_file_name.begin(), ::towlower);
    m_folder_change_reader = wil::make_folder_change_reader_nothrow(
        fsPath.parent_path().c_str(),
        false,
        wil::FolderChangeEvents::LastWriteTime,
        [this](wil::FolderChangeEvent, PCWSTR fileName) {
            std::wstring lowerFileName(fileName);
            std::transform(lowerFileName.begin(), lowerFileName.end(), lowerFileName.begin(), ::towlower);

            if (m_file_name.compare(fileName) == 0)
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
            }
        });

    if (!m_folder_change_reader)
    {
        Logger::error(L"Failed to start folder change reader for path {}. {}", path, get_last_error_or_default(GetLastError()));
    }
}

FileWatcher::~FileWatcher()
{
    m_folder_change_reader.reset();
}
