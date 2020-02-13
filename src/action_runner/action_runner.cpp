#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <shellapi.h>

#include <string_view>

#include <common/msi_to_msix_upgrade_lib/msi_to_msix_upgrade.h>

void uninstall_msi_action()
{
    const auto package_path = get_msi_package_path();
    if (package_path.empty())
    {
        return;
    }
    uninstall_msi_version(package_path);
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
        uninstall_msi_action();
        return 0;
    }

    return 0;
}