#include "pch.h"

#include "FileLocksmith.h"
#include "NtdllExtensions.h"

static bool is_directory(const std::wstring path)
{
    DWORD attributes = GetFileAttributesW(path.c_str());
    return attributes != INVALID_FILE_ATTRIBUTES && attributes & FILE_ATTRIBUTE_DIRECTORY;
}

std::vector<ProcessResult> find_processes_recursive(const std::vector<std::wstring>& paths)
{
    NtdllExtensions nt_ext;

    // TODO use a trie!

    // This maps kernel names of files within `paths` to their normal paths.
    std::map<std::wstring, std::wstring> kernel_names_files;

    // This maps kernel names of directories within `paths` to their normal paths.
    std::map<std::wstring, std::wstring> kernel_names_dirs;

    for (const auto& path : paths)
    {
        auto kernel_path = nt_ext.path_to_kernel_name(path.c_str());
        if (!kernel_path.empty())
        {
            (is_directory(path) ? kernel_names_dirs : kernel_names_files)[kernel_path] = path;
        }
    }

    std::map<DWORD, std::vector<std::wstring>> pid_files;

    // Returns a normal path of the file specified by kernel_name, if it matches
    // the search criteria. Otherwise, return an empty string.
    auto kernel_paths_contain = [&](const std::wstring& kernel_name) -> std::wstring
    {
        // Normal equivalence
        if (auto it = kernel_names_files.find(kernel_name); it != kernel_names_files.end())
        {
            return it->second;
        }

        if (auto it = kernel_names_dirs.find(kernel_name); it != kernel_names_dirs.end())
        {
            return it->second;
        }

        for (const auto& [dir_kernel_name, dir_path] : kernel_names_dirs)
        {
            if (kernel_name.starts_with(dir_kernel_name + L"\\"))
            {
                return dir_path + kernel_name.substr(dir_kernel_name.size());
            }
        }

        return {};
    };

    for (const auto& handle_info : nt_ext.handles())
    {
        if (handle_info.type_name == L"File")
        {
            auto path = kernel_paths_contain(handle_info.kernel_file_name);
            if (!path.empty())
            {
                pid_files[handle_info.pid].push_back(std::move(path));
            }
        }
    }

    std::vector<ProcessResult> result;

    for (const auto& process_info : nt_ext.processes())
    {
        if (auto it = pid_files.find(process_info.pid); it != pid_files.end())
        {
            result.push_back(ProcessResult
                {
                    .name = process_info.name,
                    .pid = process_info.pid,
                    .files = it->second
                });
        }
    }

    return result;
}

std::wstring pid_to_user(DWORD pid)
{
    HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);

    if (process == NULL)
    {
        return {};
    }

    std::wstring user = L"";
    std::wstring domain = L"";

    HANDLE token = NULL;

    if (OpenProcessToken(process, TOKEN_QUERY, &token))
    {
        DWORD token_size = 0;
        GetTokenInformation(token, TokenUser, NULL, 0, &token_size);

        if (token_size > 0)
        {
            std::vector<BYTE> token_buffer(token_size);
            GetTokenInformation(token, TokenUser, token_buffer.data(), token_size, &token_size);
            TOKEN_USER* user_ptr = (TOKEN_USER*)token_buffer.data();
            PSID psid = user_ptr->User.Sid;
            DWORD user_size = 0;
            DWORD domain_size = 0;
            SID_NAME_USE sid_name;
            LookupAccountSidW(NULL, psid, NULL, &user_size, NULL, &domain_size, &sid_name);
            user.resize(user_size + 1);
            domain.resize(domain_size + 1);
            LookupAccountSidW(NULL, psid, user.data(), &user_size, domain.data(), &domain_size, &sid_name);
            user[user_size] = L'\0';
            domain[domain_size] = L'\0';
        }

        CloseHandle(token);
    }

    CloseHandle(process);

    return user;
}
