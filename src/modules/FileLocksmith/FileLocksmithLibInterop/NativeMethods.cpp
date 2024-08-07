#include "pch.h"
#include "NativeMethods.h"
#include "FileLocksmith.h"
#include "../FileLocksmithLib/Constants.h"

namespace winrt::PowerToys::FileLocksmithLib::Interop::implementation
{

#pragma region HelperMethods
    std::wstring executable_path()
    {
        return pid_to_full_path(GetCurrentProcessId());
    }
    std::wstring paths_file()
    {
        std::wstring path{ PowerToys::Interop::Constants::AppDataPath() };
        path += L"\\";
        path += constants::nonlocalizable::PowerToyName;
        path += L"\\";
        path += constants::nonlocalizable::LastRunPath;
        return path;
    }

#pragma endregion

    com_array<winrt::PowerToys::FileLocksmithLib::Interop::ProcessResult> NativeMethods::FindProcessesRecursive(array_view<hstring const> paths)
    {
        std::vector<std::wstring> paths_cpp{ paths.begin(), paths.end() };

        auto result_cpp = find_processes_recursive(paths_cpp);
        const auto result_size = static_cast<int>(result_cpp.size());

        std::vector<ProcessResult> result;

        if (result_size == 0)
        {
            return com_array<ProcessResult>();
        }

        for (int i = 0; i < result_size; i++)
        {
            result.push_back(ProcessResult
            {
                hstring { result_cpp[i].name },
                result_cpp[i].pid,
                hstring{ result_cpp[i].user },
                    winrt::com_array<hstring>
                {
                    result_cpp[i].files.begin(), result_cpp[i].files.end()
                }
            });
        }

        return com_array<ProcessResult>{ result.begin(), result.end() };
    }

    hstring NativeMethods::PidToFullPath(uint32_t pid)
    {
        return hstring{ pid_to_full_path(pid) };
    }

    com_array<hstring> NativeMethods::ReadPathsFromFile()
    {
        std::ifstream stream(paths_file());

        std::vector<std::wstring> result_cpp;
        std::wstring line;

        bool finished = false;

        while (!finished)
        {
            WCHAR ch{};
            // We have to read data like this
            if (!stream.read(reinterpret_cast<char*>(&ch), 2))
            {
                finished = true;
            }
            else if (ch == L'\n')
            {
                if (line.empty())
                {
                    finished = true;
                }
                else
                {
                    result_cpp.push_back(line);
                    line = {};
                }
            }
            else
            {
                line += ch;
            }
        }
        return com_array<hstring>{ result_cpp.begin(), result_cpp.end() };
    }
 
    bool NativeMethods::StartAsElevated(array_view<hstring const> paths)
    {
        std::ofstream stream(paths_file());
        const WCHAR newline = L'\n';

        for (uint32_t i = 0; i < paths.size(); i++)
        {
            std::wstring path_cpp{ paths[i] };
            stream.write(reinterpret_cast<const char*>(path_cpp.c_str()), path_cpp.size() * sizeof(WCHAR));
            stream.write(reinterpret_cast<const char*>(&newline), sizeof(WCHAR));
        }

        stream.write(reinterpret_cast<const char*>(&newline), sizeof(WCHAR));

        if (!stream)
        {
            return false;
        }

        stream.close();

        auto exec_path = executable_path();

        SHELLEXECUTEINFOW exec_info{};
        exec_info.cbSize = sizeof(exec_info);
        exec_info.fMask = SEE_MASK_NOCLOSEPROCESS;
        exec_info.hwnd = NULL;
        exec_info.lpVerb = L"runas";
        exec_info.lpFile = exec_path.c_str();
        exec_info.lpParameters = L"--elevated";
        exec_info.lpDirectory = NULL;
        exec_info.nShow = SW_SHOW;
        exec_info.hInstApp = NULL;

        if (ShellExecuteExW(&exec_info))
        {
            CloseHandle(exec_info.hProcess);
            return true;
        }

        return false;
    }

    /* Adapted from "https://learn.microsoft.com/windows/win32/secauthz/enabling-and-disabling-privileges-in-c--" */
    bool NativeMethods::SetDebugPrivilege()
    {
        HANDLE hToken;
        TOKEN_PRIVILEGES tp{};
        LUID luid;

        if (OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES, &hToken) != 0)
        {
            if (!LookupPrivilegeValue(
                    NULL, // lookup privilege on local system
                    SE_DEBUG_NAME, // privilege to lookup
                    &luid)) // receives LUID of privilege
            {
                CloseHandle(hToken);
                return false;
            }
            tp.PrivilegeCount = 1;
            tp.Privileges[0].Luid = luid;
            tp.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;

            if (!AdjustTokenPrivileges(
                    hToken,
                    FALSE,
                    &tp,
                    sizeof(TOKEN_PRIVILEGES),
                    NULL,
                    NULL))
            {
                CloseHandle(hToken);
                return false;
            }

            if (GetLastError() == ERROR_NOT_ALL_ASSIGNED)
            {
                CloseHandle(hToken);
                return false;
            }

            CloseHandle(hToken);
            return true;
        }
        return false;
    }
 
    // adapted from common/utils/elevation.h. No need to bring all dependencies to this project, though.
    // TODO: Make elevation.h lighter so that this function can be used without bringing dependencies like spdlog in.
    bool NativeMethods::IsProcessElevated()
    {
        HANDLE token = nullptr;
        bool elevated = false;
        if (OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token))
        {
            TOKEN_ELEVATION elevation{};
            DWORD size;
            if (GetTokenInformation(token, TokenElevation, &elevation, sizeof(elevation), &size))
            {
                elevated = (elevation.TokenIsElevated != 0);
            }
        }
        if (token)
        {
            CloseHandle(token);
        }
        return elevated;
    }
}
