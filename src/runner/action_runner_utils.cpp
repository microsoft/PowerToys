#include "pch.h"

#include "action_runner_utils.h"

#include <common/common.h>
#include <common/winstore.h>

SHELLEXECUTEINFOW launch_action_runner(const wchar_t* cmdline)
{
    std::wstring action_runner_path;
    if (winstore::running_as_packaged())
    {
        action_runner_path = winrt::Windows::ApplicationModel::Package::Current().InstalledLocation().Path();
    }
    else
    {
        action_runner_path = get_module_folderpath();
    }

    action_runner_path += L"\\action_runner.exe";
    SHELLEXECUTEINFOW sei{ sizeof(sei) };
    sei.fMask = { SEE_MASK_FLAG_NO_UI | SEE_MASK_NOASYNC | SEE_MASK_NOCLOSEPROCESS };
    sei.lpFile = action_runner_path.c_str();
    sei.nShow = SW_SHOWNORMAL;
    sei.lpParameters = cmdline;
    ShellExecuteExW(&sei);
    return sei;
}
