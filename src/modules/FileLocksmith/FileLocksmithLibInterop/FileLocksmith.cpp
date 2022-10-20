#include "pch.h"

#include "FileLocksmith.h"
#include "NtdllExtensions.h"

static bool is_directory(const std::wstring path)
{
    DWORD attributes = GetFileAttributesW(path.c_str());
    return attributes != INVALID_FILE_ATTRIBUTES && attributes & FILE_ATTRIBUTE_DIRECTORY;
}

// C++20 method
static bool starts_with(std::wstring_view whole, std::wstring_view part)
{
    return whole.size() >= part.size() && whole.substr(0, part.size()) == part;
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

    std::map<DWORD, std::set<std::wstring>> pid_files;

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
            if (starts_with(kernel_name, dir_kernel_name + L"\\"))
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
                pid_files[handle_info.pid].insert(std::move(path));
            }
        }
    }

    // Check all modules used by processes
    auto processes = nt_ext.processes();

    for (const auto& process : processes)
    {
        for (const auto& path : process.modules)
        {
            auto kernel_name = nt_ext.path_to_kernel_name(path.c_str());

            auto found_path = kernel_paths_contain(kernel_name);
            if (!found_path.empty())
            {
                pid_files[process.pid].insert(std::move(found_path));
            }
        }
    }

    std::vector<ProcessResult> result;

    for (const auto& process_info : processes)
    {
        if (auto it = pid_files.find(process_info.pid); it != pid_files.end())
        {
            result.push_back(ProcessResult
                {
                    process_info.name,
                    process_info.pid,
                    std::vector(it->second.begin(), it->second.end())
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

constexpr size_t LongMaxPathSize = 65536;

std::wstring pid_to_full_path(DWORD pid)
{
    HANDLE process = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, pid);

    std::wstring result(LongMaxPathSize, L'\0');

    // Returns zero on failure, so it's okay to resize to zero.
    auto length = GetModuleFileNameExW(process, NULL, result.data(), (DWORD)result.size());
    result.resize(length);

    CloseHandle(process);
    return result;
}
