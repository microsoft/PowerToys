#include "pch.h"

namespace FileLocksmith::Interop
{
    ref struct ProcessResult
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

    ref struct NativeMethods
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

            auto result = gcnew array<ProcessResult ^>(n);
            for (int i = 0; i < n; i++)
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
    };
}