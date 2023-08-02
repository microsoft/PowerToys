#pragma once
#include <tlhelp32.h>

struct Process
{
    DWORD Pid;
    std::wstring Name;
};

inline Process CreateProcessFromProcessEntry(PROCESSENTRY32W const& entry)
{
    std::wstring processName(entry.szExeFile);
    auto pid = entry.th32ProcessID;
    return { pid, processName };
}

inline std::vector<Process> GetAllProcesses()
{
    std::vector<Process> result;
    wil::unique_handle snapshot(winrt::check_pointer(CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)));
    PROCESSENTRY32W entry = {};
    entry.dwSize = sizeof(entry);

    if (Process32FirstW(snapshot.get(), &entry))
    {
        do
        {
            auto process = CreateProcessFromProcessEntry(entry);
            result.push_back(process);
        } while (Process32NextW(snapshot.get(), &entry));
    }
    return result;
}
