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
    std::wstring m_path;
    std::wstring m_file_name;
    std::optional<FILETIME> m_lastWrite;
    std::function<void()> m_callback;
    wil::unique_folder_change_reader_nothrow m_folder_change_reader;

    std::optional<FILETIME> MyFileTime();
public:
    FileWatcher(const std::wstring& path, std::function<void()> callback);
    ~FileWatcher();
};
