#include "pch.h"
#include "Constants.h"
#include "Constants.g.cpp"
#include "shared_constants.h"
#include <ShlObj.h>

namespace winrt::interop::implementation
{
    hstring Constants::AppDataPath()
    {
        PWSTR local_app_path;
        winrt::check_hresult(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, NULL, &local_app_path));
        winrt::hstring result{ local_app_path };
        CoTaskMemFree(local_app_path);
        result = result + L"\\" + CommonSharedConstants::APPDATA_PATH;
        return result;
    }
    hstring Constants::PowerLauncherSharedEvent()
    {
        return CommonSharedConstants::POWER_LAUNCHER_SHARED_EVENT;
    }
}
