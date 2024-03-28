#include "pch.h"

#include "FileLocksmith.h"

#include "../FileLocksmithLib/Constants.h"

#define BUFSIZE 4096 * 4

namespace FileLocksmith::Interop
{
    public ref struct ProcessResult
    {
        System::String^ name;
        System::UInt32 pid;
        System::String^ user;
        array<System::String^>^ files;
    };

    System::String^ from_wstring_view(std::wstring_view str)
    {
        return gcnew System::String(str.data(), 0, static_cast<int>(str.size()));
    }

    std::wstring from_system_string(System::String^ str)
    {
        // TODO use some built-in method
        auto chars = str->ToCharArray();

        std::wstring result(chars->Length, 0);
        for (int i = 0; i < chars->Length; i++)
        {
            result[i] = chars[i];
        }

        return result;
    }

    std::wstring paths_file()
    {
#pragma warning(suppress : 4691) // Weird warning about System::String from referenced library not being the one expected (?!)
        std::wstring path = from_system_string(interop::Constants::AppDataPath());
        path += L"\\";
        path += constants::nonlocalizable::PowerToyName;
        path += L"\\";
        path += constants::nonlocalizable::LastRunPath;
        return path;
    }

    std::wstring executable_path()
    {
        return pid_to_full_path(GetCurrentProcessId());
    }

    public ref struct NativeMethods
    {
        static array<ProcessResult ^> ^ FindProcessesRecursive(array<System::String^>^ paths)
        {
            const int n = paths->Length;

            std::vector<std::wstring> paths_cpp(n);
            for (int i = 0; i < n; i++)
            {
                paths_cpp[i] = from_system_string(paths[i]);
            }

            auto result_cpp = find_processes_recursive(paths_cpp);
            const auto result_size = static_cast<int>(result_cpp.size());

            auto result = gcnew array<ProcessResult ^>(result_size);
            for (int i = 0; i < result_size; i++)
            {
                auto item = gcnew ProcessResult;

                item->name = from_wstring_view(result_cpp[i].name);
                item->pid = result_cpp[i].pid;
                item->user = from_wstring_view(result_cpp[i].user);

                const int n_files = static_cast<int>(result_cpp[i].files.size());
                item->files = gcnew array<System::String ^>(n_files);
                for (int j = 0; j < n_files; j++)
                {
                    item->files[j] = from_wstring_view(result_cpp[i].files[j]);
                }

                result[i] = item;
            }

            return result;
        }

        static System::String^ PidToFullPath(System::UInt32 pid)
        {
            auto path_cpp = pid_to_full_path(pid);
            return from_wstring_view(path_cpp);
        }

        static array<System::String ^>^ ReadPathsFromPipe(System::String^ pipeName)
        {
            HANDLE hStdin = INVALID_HANDLE_VALUE;

            if (pipeName->Length > 0)
            {
                std::wstring pipe = from_system_string(pipeName);

                if (pipe.size() > 0)
                {
                    while (1)
                    {
                        hStdin = CreateFile(
                            pipe.c_str(), // pipe name
                            GENERIC_READ | GENERIC_WRITE, // read and write
                            0, // no sharing
                            NULL, // default security attributes
                            OPEN_EXISTING, // opens existing pipe
                            0, // default attributes
                            NULL); // no template file

                        // Break if the pipe handle is valid.
                        if (hStdin != INVALID_HANDLE_VALUE)
                            break;

                        // Exit if an error other than ERROR_PIPE_BUSY occurs.
                        auto error = GetLastError();
                        if (error != ERROR_PIPE_BUSY)
                        {
                            break;
                        }

                        if (!WaitNamedPipe(pipe.c_str(), 3))
                        {
                            printf("Could not open pipe: 20 second wait timed out.");
                        }
                    }
                }
            }
            else
            {
                hStdin = GetStdHandle(STD_INPUT_HANDLE);
            }

            if (hStdin == INVALID_HANDLE_VALUE)
            {
                ExitProcess(1);
            }

            BOOL bSuccess;
            WCHAR chBuf[BUFSIZE];
            DWORD dwRead;

            std::vector<std::wstring> result_cpp;

            for (;;)
            {
                // Read from standard input and stop on error or no data.
                bSuccess = ReadFile(hStdin, chBuf, BUFSIZE * sizeof(wchar_t), &dwRead, NULL);

                if (!bSuccess || dwRead == 0)
                    break;

                std::wstring inputBatch{ chBuf, dwRead / sizeof(wchar_t) };

                std::wstringstream ss(inputBatch);
                std::wstring item;
                wchar_t delimiter = '?';
                while (std::getline(ss, item, delimiter))
                {
                    result_cpp.push_back(item);
                }

                if (!bSuccess)
                    break;
            }
            CloseHandle(hStdin);

            auto result = gcnew array<System::String ^>(static_cast<int>(result_cpp.size()));
            for (int i = 0; i < result->Length; i++)
            {
                result[i] = from_wstring_view(result_cpp[i]);
            }

            return result;
        }

        static System::Boolean StartAsElevated(array<System::String ^> ^ paths)
        {
            std::ofstream stream(paths_file());
            const WCHAR newline = L'\n';
            for (int i = 0; i < paths->Length; i++)
            {
                auto path_cpp = from_system_string(paths[i]);
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
        static System::Boolean SetDebugPrivilege()
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
                        (PTOKEN_PRIVILEGES)NULL,
                        (PDWORD)NULL))
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
        static System::Boolean IsProcessElevated()
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
    };
}
