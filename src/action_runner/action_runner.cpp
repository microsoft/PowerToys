#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <shellapi.h>

#include <string_view>

#include <common/msi_to_msix_upgrade_lib/msi_to_msix_upgrade.h>

#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Storage.h>

int uninstall_msi_action()
{
    const auto package_path = get_msi_package_path();
    if (package_path.empty())
    {
        return 0;
    }
    if (!uninstall_msi_version(package_path))
    {
        return -1;
    }

    // Launch PowerToys again, since it's been terminated by the MSI uninstaller
    std::wstring runner_path{ winrt::Windows::ApplicationModel::Package::Current().InstalledLocation().Path() };
    runner_path += L"\\PowerToys.exe";
    SHELLEXECUTEINFOW sei{ sizeof(sei) };
    sei.fMask = { SEE_MASK_FLAG_NO_UI | SEE_MASK_NOASYNC };
    sei.lpFile = runner_path.c_str();
    sei.nShow = SW_SHOWNORMAL;
    ShellExecuteExW(&sei);

    return 0;
}

int WINAPI WinMain(HINSTANCE, HINSTANCE, LPSTR, int)
{
    int nArgs = 0;
    LPWSTR* args = CommandLineToArgvW(GetCommandLineW(), &nArgs);
    if (!args || nArgs < 2)
    {
        return 1;
    }
    std::wstring_view action{ args[1] };

    if (action == L"-uninstall_msi")
    {
        return uninstall_msi_action();
    }

    return 0;
}