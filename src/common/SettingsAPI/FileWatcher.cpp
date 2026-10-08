#include "pch.h"
#include "FileWatcher.h"
#include <utils/winapi_error.h>

namespace
{
    wil::unique_hfile openForReadCallback(const std::wstring& path)
    {
        constexpr DWORD retryDelays[] = { 10, 20, 40, 80, 160 };
        for (size_t attempt = 0;; ++attempt)
        {
            // Match std::ifstream sharing. A rename notification can arrive while
            // the publisher still holds DELETE access to the replacement file.
            wil::unique_hfile file(CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, 0, nullptr));
            if (file)
            {
                return file;
            }

            const auto error = GetLastError();
            if ((error != ERROR_SHARING_VIOLATION && error != ERROR_ACCESS_DENIED && error != ERROR_FILE_NOT_FOUND) || attempt == ARRAYSIZE(retryDelays))
            {
                return {};
            }
            Sleep(retryDelays[attempt]);
        }
    }
}

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
    FileWatcher(path, callback, false)
{
}

FileWatcher::FileWatcher(const std::wstring& path, std::function<void()> callback, bool watchFileReplacement) :
    m_path(path),
    m_callback(callback)
{
    std::filesystem::path fsPath(path);
    m_file_name = fsPath.filename();
    std::transform(m_file_name.begin(), m_file_name.end(), m_file_name.begin(), ::towlower);
    auto events = wil::FolderChangeEvents::LastWriteTime;
    if (watchFileReplacement)
    {
        events = events | wil::FolderChangeEvents::FileName;
        m_lastWrite = MyFileTime();
    }

    m_folder_change_reader = wil::make_folder_change_reader_nothrow(
        fsPath.parent_path().c_str(),
        false,
        events,
        [this, watchFileReplacement](wil::FolderChangeEvent event, PCWSTR fileName) {
            if (watchFileReplacement &&
                (event == wil::FolderChangeEvent::Removed || event == wil::FolderChangeEvent::RenameOldName))
            {
                return;
            }

            const bool changesLost = watchFileReplacement && event == wil::FolderChangeEvent::ChangesLost;
            if (!changesLost)
            {
                if (!fileName)
                {
                    return;
                }

                std::wstring lowerFileName(fileName);
                std::transform(lowerFileName.begin(), lowerFileName.end(), lowerFileName.begin(), ::towlower);
                if (m_file_name.compare(lowerFileName) != 0)
                {
                    return;
                }
            }

            std::optional<FILETIME> lastWrite;
            wil::unique_hfile readGuard;
            if (watchFileReplacement)
            {
                readGuard = openForReadCallback(m_path);
                FILETIME time{};
                if (!readGuard || !GetFileTime(readGuard.get(), nullptr, nullptr, &time))
                {
                    return;
                }
                lastWrite = time;
            }
            else
            {
                lastWrite = MyFileTime();
            }

            if (watchFileReplacement && lastWrite.has_value() &&
                (!m_lastWrite.has_value() || changesLost || event == wil::FolderChangeEvent::Added || event == wil::FolderChangeEvent::RenameNewName))
            {
                m_lastWrite = lastWrite;
                m_callback();
            }
            else if (!m_lastWrite.has_value())
            {
                m_lastWrite = lastWrite;
            }
            else if (lastWrite.has_value() &&
                     (m_lastWrite->dwHighDateTime != lastWrite->dwHighDateTime || m_lastWrite->dwLowDateTime != lastWrite->dwLowDateTime))
            {
                m_lastWrite = lastWrite;
                m_callback();
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
