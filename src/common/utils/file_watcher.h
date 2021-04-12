#include <functional>
#include <vector>
#include <thread>
#include <string>
#include <windows.h>

class file_watcher
{
    HANDLE watcher_exit_event = nullptr;

public:
    file_watcher()
    {
    }

    file_watcher(std::function<void(DWORD)> callback, const std::wstring& directoryPath, const std::vector<std::wstring>& files)
    {
        watcher_exit_event = CreateEventW(nullptr, true, false, nullptr);
        if (!watcher_exit_event)
        {
            callback(GetLastError());
            return;
        }

        // We copy this handle because we want to capture it
        // We can not relay on capturing 'this' because we allow move constructor
        auto watcher_exit_local_event = watcher_exit_event;
        std::thread([=]() {
            HANDLE h_directory = CreateFileW(
                directoryPath.c_str(),
                GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                NULL,
                OPEN_ALWAYS,
                FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OVERLAPPED,
                NULL);

            if (h_directory == INVALID_HANDLE_VALUE)
            {
                callback(GetLastError());
                return;
            }

            FILE_NOTIFY_INFORMATION info[10];
            auto wait_changes_event = CreateEventW(nullptr, false, false, nullptr);
            if (!wait_changes_event)
            {
                callback(GetLastError());
                return;
            }

            HANDLE wait_handles[2] = {
                wait_changes_event, watcher_exit_local_event
            };

            while (true)
            {
                memset(info, 0, sizeof(info));
                OVERLAPPED overlapped;
                memset(&overlapped, 0, sizeof(OVERLAPPED));
                overlapped.hEvent = wait_changes_event;

                auto ret = ReadDirectoryChangesW(
                    h_directory,
                    &info,
                    sizeof(info),
                    false,
                    FILE_NOTIFY_CHANGE_SIZE | FILE_NOTIFY_CHANGE_LAST_WRITE,
                    nullptr,
                    &overlapped,
                    nullptr);

                if (ret == 0)
                {
                    callback(GetLastError());
                    return;
                }

                auto waitRes = WaitForMultipleObjects(2, wait_handles, false, INFINITE);
                if (waitRes == WAIT_FAILED)
                {
                    callback(GetLastError());
                    continue;
                }

                if (waitRes == WAIT_OBJECT_0 + 1)
                {
                    break;
                }

                if (waitRes == WAIT_OBJECT_0)
                {
                    DWORD bytes_returned = 0;
                    if (GetOverlappedResult(overlapped.hEvent, &overlapped, &bytes_returned, false) == false)
                    {
                        callback(GetLastError());
                        return;
                    }

                    if (bytes_returned == 0)
                    {
                        // It is possible that buffer is to small to contain changes
                        // So we assume there was a change as we can not say for sure
                        callback(GetLastError());
                        continue;
                    }

                    bool changed = false;
                    FILE_NOTIFY_INFORMATION* entry = info;
                    while (true)
                    {
                        for (auto file : files)
                        {
                            if (lstrcmpW(file.c_str(), entry->FileName) == 0)
                            {
                                changed = true;
                                break;
                            }
                        }

                        if (changed || entry->NextEntryOffset == 0)
                        {
                            break;
                        }

                        entry = (FILE_NOTIFY_INFORMATION*)((UCHAR*)entry + entry->NextEntryOffset);
                    }

                    if (changed)
                    {
                        callback(ERROR_SUCCESS);
                    }
                }
            }
        }).detach();
    }

    file_watcher(file_watcher&) = delete;
    file_watcher& operator=(file_watcher&) = delete;

    file_watcher(file_watcher&& a) noexcept
    {
        this->watcher_exit_event = a.watcher_exit_event;
        a.watcher_exit_event = nullptr;
    }

    file_watcher& operator=(file_watcher&& a) noexcept
    {
        this->watcher_exit_event = a.watcher_exit_event;
        a.watcher_exit_event = nullptr;
        return *this;
    }

    ~file_watcher()
    {
        if (watcher_exit_event)
        {
            SetEvent(watcher_exit_event);
        }
    }
};
