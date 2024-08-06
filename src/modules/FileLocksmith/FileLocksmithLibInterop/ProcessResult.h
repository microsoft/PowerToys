#pragma once
#include "ProcessResult.g.h"

namespace winrt::PowerToys::FileLocksmithLib::Interop::implementation
{
    struct ProcessResult : ProcessResultT<ProcessResult>
    {
        ProcessResult(hstring const& name, uint32_t pid, hstring const& user, winrt::Windows::Foundation::Collections::IVector<hstring> const& files);
        hstring name();
        uint32_t pid();
        hstring user();
        winrt::Windows::Foundation::Collections::IVector<hstring> files();

    private:
        hstring _name;
        uint32_t _pid;
        hstring _user;
        winrt::Windows::Foundation::Collections::IVector<hstring> _files;
    };
}
namespace winrt::PowerToys::FileLocksmithLib::Interop::factory_implementation
{
    struct ProcessResult : ProcessResultT<ProcessResult, implementation::ProcessResult>
    {
    };
}
