#pragma once

#include "ProjectTelemetry.h"

#define TraceLoggingWriteWrapper(provider, eventName, ...)   \
    if (IsDataDiagnosticsEnabled())                          \
    {                                                        \
        TraceLoggingWrite(provider, eventName, __VA_ARGS__); \
    }

namespace telemetry
{

constexpr inline const wchar_t* DataDiagnosticsRegKey = L"Software\\Classes\\PowerToys";
constexpr inline const wchar_t* DataDiagnosticsRegValueName = L"AllowDataDiagnostics";

class TraceBase
{
public:
    static void RegisterProvider()
    {
        TraceLoggingRegister(g_hProvider);
    }

    static void UnregisterProvider()
    {
        TraceLoggingUnregister(g_hProvider);
    }

    static bool IsDataDiagnosticsEnabled()
    {
        HKEY key{};
        if (RegOpenKeyExW(HKEY_CURRENT_USER,
                          DataDiagnosticsRegKey,
                          0,
                          KEY_READ,
                          &key) != ERROR_SUCCESS)
        {
            return false;
        }

        DWORD isDataDiagnosticsEnabled = 0;
        DWORD size = sizeof(isDataDiagnosticsEnabled);

        if (RegGetValueW(
                HKEY_CURRENT_USER,
                DataDiagnosticsRegKey,
                DataDiagnosticsRegValueName,
                RRF_RT_REG_DWORD,
                nullptr,
                &isDataDiagnosticsEnabled,
                &size) != ERROR_SUCCESS)
        {
            RegCloseKey(key);
            return false;
        }
        RegCloseKey(key);

        return isDataDiagnosticsEnabled;
    }
};

} // namespace telemetry