#include "pch.h"

#include <iostream>

int wmain(int argc, WCHAR** argv)
{
    NtdllExtensions nt_ext;
    auto paths_to_check = ipc::read_paths_from_stdin();

    std::set<std::wstring> kernel_paths;

    for (auto path : paths_to_check)
    {
        auto kernel_path = nt_ext.path_to_kernel_name(path.c_str());
        if (!kernel_path.empty())
        {
            kernel_paths.insert(std::move(kernel_path));
        }
    }

    std::set<DWORD> pids;

    for (auto handle_info : nt_ext.handles())
    {
        if (handle_info.type_name == L"File" && kernel_paths.contains(handle_info.kernel_file_name))
        {
            pids.insert(handle_info.pid);
        }
    }

    std::vector<NtdllExtensions::ProcessInfo> result;

    for (auto process_info : nt_ext.processes())
    {
        if (pids.contains(process_info.pid))
        {
            result.push_back(process_info);
        }
    }

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
