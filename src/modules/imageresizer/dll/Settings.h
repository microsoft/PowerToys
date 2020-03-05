#pragma once

class CSettings
{
public:
    static bool GetEnabled();
    static bool SetEnabled(_In_ bool enabled);

private:
    static bool GetRegBoolValue(_In_ PCWSTR valueName, _In_ bool defaultValue);
    static bool SetRegBoolValue(_In_ PCWSTR valueName, _In_ bool value);
    static bool SetRegDWORDValue(_In_ PCWSTR valueName, _In_ DWORD value);
    static DWORD GetRegDWORDValue(_In_ PCWSTR valueName, _In_ DWORD defaultValue);
    static bool SetRegStringValue(_In_ PCWSTR valueName, _In_ PCWSTR value);
    static bool GetRegStringValue(_In_ PCWSTR valueName, __out_ecount(cchBuf) PWSTR value, DWORD cchBuf);
};