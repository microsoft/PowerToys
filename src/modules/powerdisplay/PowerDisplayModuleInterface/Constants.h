#include <string>

namespace PowerDisplayConstants
{
    // Name of the powertoy module.
    inline const std::wstring ModuleKey = L"PowerDisplay";

    // Process synchronization constants
    inline const wchar_t* ProcessReadyEventName = L"Local\\PowerToys_PowerDisplay_Ready";
    constexpr DWORD ProcessReadyTimeoutMs = 5000;
    constexpr DWORD FallbackDelayMs = 500;
}