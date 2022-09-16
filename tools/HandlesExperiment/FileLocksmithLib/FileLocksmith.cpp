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

static bool is_directory(const std::wstring path)
{
    DWORD attributes = GetFileAttributesW(path.c_str());
    return attributes != INVALID_FILE_ATTRIBUTES && attributes & FILE_ATTRIBUTE_DIRECTORY;
}

std::vector<ProcessResult> find_processes_recursive(const std::vector<std::wstring>& paths)
{
    NtdllExtensions nt_ext;

    // TODO use a trie!
    std::set<std::wstring> kernel_paths_files;
    std::set<std::wstring> kernel_paths_dirs;

    for (const auto& path : paths)
    {
        auto kernel_path = nt_ext.path_to_kernel_name(path.c_str());
        if (!kernel_path.empty())
        {
            (is_directory(path) ? kernel_paths_dirs : kernel_paths_files).insert(std::move(kernel_path));
        }
    }

    std::map<DWORD, uint64_t> pid_counts;

    auto kernel_paths_contain = [&](const std::wstring& path)
    {
        // Normal equivalence
        if (kernel_paths_files.contains(path) || kernel_paths_dirs.contains(path))
        {
            return true;
        }

        // Subfolder or file
        return std::ranges::any_of(kernel_paths_dirs, [&](const std::wstring& dir)
            {
                return path.starts_with(dir + L"\\");
            });
    };

    for (auto handle_info : nt_ext.handles())
    {
        if (handle_info.type_name == L"File" && kernel_paths_contain(handle_info.kernel_file_name))
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