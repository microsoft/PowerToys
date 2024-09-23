#include "pch.h"
#include "ProcessResult.h"

namespace winrt::PowerToys::FileLocksmithLib::Interop::implementation
{
    ProcessResult::ProcessResult(hstring const& name, uint32_t pid, hstring const& user, array_view<hstring const> files)
    {
        _name = name;
        _pid = pid;
        _user = user;
        _files = { files.begin(), files.end() };
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
    com_array<hstring> ProcessResult::files()
    {
        return winrt::com_array<hstring>{ _files.begin(), _files.end() };
    }
}
