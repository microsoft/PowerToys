#include "pch.h"

#include <winrt/Windows.Data.Json.h>

extern "C"
{
    __declspec(dllexport) void UpdateNewPlusRegistrationWin10(bool enabled)
    {
        if (enabled)
        {
#if defined(ENABLE_REGISTRATION) || defined(NDEBUG)
            NewPlusRuntimeRegistration::EnsureRegisteredWin10();
            Logger::info(L"New+ context menu registered");
#endif
        }
        else
        {
#if defined(ENABLE_REGISTRATION) || defined(NDEBUG)
            NewPlusRuntimeRegistration::Unregister();
            Logger::info(L"New+ context menu unregistered");
#endif
        }
    }
}
