#pragma once
#include "ProcessResult.g.h"

namespace winrt::PowerToys::FileLocksmithLib::Interop::implementation
{
    struct ProcessResult : ProcessResultT<ProcessResult>
    {
        ProcessResult() = default;

        ProcessResult(hstring const& name, uint32_t pid, hstring const& user, array_view<hstring const> files);
        hstring name();
        uint32_t pid();
        hstring user();
        com_array<hstring> files();

    private:
        hstring _name;
        uint32_t _pid;
        hstring _user;
        com_array<hstring> _files;
    };
}
namespace winrt::PowerToys::FileLocksmithLib::Interop::factory_implementation
{
    struct ProcessResult : ProcessResultT<ProcessResult, implementation::ProcessResult>
    {
    };
}
