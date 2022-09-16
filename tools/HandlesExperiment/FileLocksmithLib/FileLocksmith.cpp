#include "pch.h"

#include "FileLocksmith.h"
#include "NtdllExtensions.h"

std::vector<ProcessResult> find_processes_nonrecursive(const std::vector<std::wstring>& paths)
{
    NtdllExtensions nt_ext;
    std::set<std::wstring> kernel_paths;

    for (const auto& path : paths)
    {
        auto kernel_path = nt_ext.path_to_kernel_name(path.c_str());
        if (!kernel_path.empty())
        {
            kernel_paths.insert(std::move(kernel_path));
        }
    }

    std::map<DWORD, uint64_t> pid_counts;

    for (auto handle_info : nt_ext.handles())
    {
        if (handle_info.type_name == L"File" && kernel_paths.contains(handle_info.kernel_file_name))
        {
            pid_counts[handle_info.pid]++;
        }
    }

    std::vector<ProcessResult> result;

    for (auto process_info : nt_ext.processes())
    {
        if (auto it = pid_counts.find(process_info.pid); it != pid_counts.end())
        {
            result.push_back(ProcessResult
                {
                    .name = process_info.name,
                    .pid = process_info.pid,
                    .num_files = it->second
                });
        }
    }

    return result;
}

