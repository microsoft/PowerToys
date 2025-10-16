#include "pch.h"
#include "process_path.h"

#include <chrono>

std::wstring get_process_path(DWORD pid) noexcept
{
    wil::unique_handle process{ OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, TRUE, pid) };
    std::wstring name;
    if (process)
    {
        name.resize(MAX_PATH);
        DWORD nameLength = static_cast<DWORD>(name.length());
        if (QueryFullProcessImageNameW(process.get(), 0, name.data(), &nameLength) == 0)
        {
            nameLength = 0;
        }
        name.resize(nameLength);
    }
    return name;
}

std::wstring get_process_path(HWND window) noexcept
{
    const static std::wstring appFrameHost = L"ApplicationFrameHost.exe";

    DWORD pid{};
    GetWindowThreadProcessId(window, &pid);
    auto name = get_process_path(pid);

    if (name.length() >= appFrameHost.length() &&
        name.compare(name.length() - appFrameHost.length(), appFrameHost.length(), appFrameHost) == 0)
    {
        DWORD newPid = pid;

        EnumChildWindows(
            window,
            [](HWND hwnd, LPARAM param) -> BOOL {
                auto newPidPtr = reinterpret_cast<DWORD*>(param);
                DWORD childPid;
                GetWindowThreadProcessId(hwnd, &childPid);
                if (childPid != *newPidPtr)
                {
                    *newPidPtr = childPid;
                    return FALSE;
                }
                return TRUE;
            },
            reinterpret_cast<LPARAM>(&newPid));

        if (newPid != pid)
        {
            return get_process_path(newPid);
        }
    }

    return name;
}

std::wstring get_process_path_waiting_uwp(HWND window)
{
    const static std::wstring appFrameHost = L"ApplicationFrameHost.exe";

    int attempt = 0;
    auto processPath = get_process_path(window);

    while (++attempt < 30 && processPath.length() >= appFrameHost.length() &&
           processPath.compare(processPath.length() - appFrameHost.length(), appFrameHost.length(), appFrameHost) == 0)
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(5));
        processPath = get_process_path(window);
    }

    return processPath;
}

std::wstring get_module_filename(HMODULE mod)
{
    wchar_t buffer[MAX_PATH + 1];
    DWORD actualLength = GetModuleFileNameW(mod, buffer, MAX_PATH);
    if (GetLastError() == ERROR_INSUFFICIENT_BUFFER)
    {
        const DWORD longPathLength = 0xFFFF;
        std::wstring longFilename(longPathLength, L'\0');
        actualLength = GetModuleFileNameW(mod, longFilename.data(), longPathLength);
        return longFilename.substr(0, actualLength);
    }
    return { buffer, actualLength };
}

std::wstring get_module_folderpath(HMODULE mod, bool removeFilename)
{
    wchar_t buffer[MAX_PATH + 1];
    DWORD actualLength = GetModuleFileNameW(mod, buffer, MAX_PATH);
    if (GetLastError() == ERROR_INSUFFICIENT_BUFFER)
    {
        const DWORD longPathLength = 0xFFFF;
        std::wstring longFilename(longPathLength, L'\0');
        actualLength = GetModuleFileNameW(mod, longFilename.data(), longPathLength);
        PathRemoveFileSpecW(longFilename.data());
        longFilename.resize(std::wcslen(longFilename.data()));
        longFilename.shrink_to_fit();
        return longFilename;
    }

    if (removeFilename)
    {
        PathRemoveFileSpecW(buffer);
    }
    return { buffer, static_cast<uint64_t>(lstrlenW(buffer)) };
}
