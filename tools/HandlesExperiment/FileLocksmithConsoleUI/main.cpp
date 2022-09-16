#include "pch.h"

#include <iostream>

int wmain(int argc, WCHAR** argv)
{
    auto paths_to_check = ipc::read_paths_from_stdin();

    auto result = find_processes_nonrecursive(paths_to_check);

    if (result.empty())
    {
        std::wcout << L"No processes are using these files.\n";
    }
    else
    {
        for (auto process_info : result)
        {
            std::wcout << L"[" << process_info.pid << L"] " << process_info.name << L'\n';
        }
    }

    Sleep(INFINITE);
}
