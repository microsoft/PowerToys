#include "pch.h"
#include "ProcessResult.h"

namespace winrt::PowerToys::FileLocksmithLib::Interop::implementation
{
    ProcessResult::ProcessResult(hstring const& name, uint32_t pid, hstring const& user, winrt::Windows::Foundation::Collections::IVector<hstring> const& files)
    {
        _name = name;
        _pid = pid;
        _user = user;
        _files = files;
    }
    hstring ProcessResult::name()
    {
        return _name;
    }
    uint32_t ProcessResult::pid()
    {
        return _pid;
    }
    hstring ProcessResult::user()
    {
        return _user;
    }
    winrt::Windows::Foundation::Collections::IVector<hstring> ProcessResult::files()
    {
        return _files;
    }
}
