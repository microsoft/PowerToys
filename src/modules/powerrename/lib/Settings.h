#pragma once

class CSettings
{
public:
    static bool GetEnabled();
    static bool SetEnabled(_In_ bool enabled);

    static bool GetShowIconOnMenu();
    static bool SetShowIconOnMenu(_In_ bool show);

    static bool GetExtendedContextMenuOnly();
    static bool SetExtendedContextMenuOnly(_In_ bool extendedOnly);

    static bool GetPersistState();
    static bool SetPersistState(_In_ bool extendedOnly);

    static bool GetMRUEnabled();
    static bool SetMRUEnabled(_In_ bool enabled);

    static DWORD GetMaxMRUSize();
    static bool SetMaxMRUSize(_In_ DWORD maxMRUSize);

    static DWORD GetFlags();
    static bool SetFlags(_In_ DWORD flags);

    static bool GetSearchText(__out_ecount(cchBuf) PWSTR text, DWORD cchBuf);
    static bool SetSearchText(_In_ PCWSTR text);

    static bool GetReplaceText(__out_ecount(cchBuf) PWSTR text, DWORD cchBuf);
    static bool SetReplaceText(_In_ PCWSTR text);

private:
    static bool GetRegBoolValue(_In_ PCWSTR valueName, _In_ bool defaultValue);
    static bool SetRegBoolValue(_In_ PCWSTR valueName, _In_ bool value);
    static bool SetRegDWORDValue(_In_ PCWSTR valueName, _In_ DWORD value);
    static DWORD GetRegDWORDValue(_In_ PCWSTR valueName, _In_ DWORD defaultValue);
    static bool SetRegStringValue(_In_ PCWSTR valueName, _In_ PCWSTR value);
    static bool GetRegStringValue(_In_ PCWSTR valueName, __out_ecount(cchBuf) PWSTR value, DWORD cchBuf);
};

HRESULT CRenameMRUSearch_CreateInstance(_Outptr_ IUnknown** ppUnk);
HRESULT CRenameMRUReplace_CreateInstance(_Outptr_ IUnknown** ppUnk);