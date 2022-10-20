#include "pch.h"

#include "FileLocksmith.h"

#include "../FileLocksmithExt/Constants.h"

namespace FileLocksmith::Interop
{
    public ref struct ProcessResult
    {
        System::String^ name;
        System::UInt32 pid;
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

        static System::Boolean KillProcess(System::UInt32 pid)
        {
            HANDLE process = OpenProcess(PROCESS_TERMINATE, FALSE, pid);
            if (!process || !TerminateProcess(process, 1))
            {
                return false;
            }

            CloseHandle(process);
            return true;
        }

        static System::Boolean WaitForProcess(System::UInt32 pid)
        {
            HANDLE process = OpenProcess(SYNCHRONIZE, FALSE, pid);

            if (!process)
            {
                return false;
            }

            auto wait_result = WaitForSingleObject(process, INFINITE);
            CloseHandle(process);

            return wait_result == WAIT_OBJECT_0;
        }

        static System::String^ PidToUser(System::UInt32 pid)
        {
            auto user_cpp = pid_to_user(pid);
            return from_wstring_view(user_cpp);
        }

        static System::String^ PidToFullPath(System::UInt32 pid)
        {
            auto path_cpp = pid_to_full_path(pid);
            return from_wstring_view(path_cpp);
        }

        static array<System::String^>^ ReadPathsFromFile()
        {
            std::ifstream stream(paths_file());

            std::vector<std::wstring> result_cpp;
            std::wstring line;

            bool finished = false;

            while (!finished)
            {
                WCHAR ch;
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

            SHELLEXECUTEINFOW exec_info;
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
    };
}
