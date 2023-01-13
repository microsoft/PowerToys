#include "pch.h"
#include "start_visible.h"

bool is_start_visible()
{
    static const auto app_visibility = []() {
        winrt::com_ptr<IAppVisibility> result;
        CoCreateInstance(CLSID_AppVisibility,
                         nullptr,
                         CLSCTX_INPROC_SERVER,
                         __uuidof(result),
                         result.put_void());
        return result;
    }();

    if (!app_visibility)
    {
        return false;
    }

    BOOL visible;
    auto result = app_visibility->IsLauncherVisible(&visible);
    return SUCCEEDED(result) && visible;
}
