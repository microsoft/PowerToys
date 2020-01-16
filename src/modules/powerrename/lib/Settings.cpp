
#include "stdafx.h"
#include <commctrl.h>
#include "Settings.h"
#include "PowerRenameInterfaces.h"
#include "resource.h"
#include <common.h>

const wchar_t* c_rootRegPath = GET_RESOURCE_STRING(IDS_ROOT_PATH).c_str();
const wchar_t* c_mruSearchRegPath = GET_RESOURCE_STRING(IDS_SEARCH_PATH).c_str();
const wchar_t* c_mruReplaceRegPath = GET_RESOURCE_STRING(IDS_REPLACE_PATH).c_str();
const wchar_t* c_enabled = GET_RESOURCE_STRING(IDS_ENABLED).c_str();
const wchar_t* c_showIconOnMenu = GET_RESOURCE_STRING(IDS_SHOW_ICON).c_str();
const wchar_t* c_extendedContextMenuOnly = GET_RESOURCE_STRING(IDS_CONTEXT_MENU).c_str();
const wchar_t* c_persistState = GET_RESOURCE_STRING(IDS_PERSIST_STATE).c_str();
const wchar_t* c_maxMRUSize = GET_RESOURCE_STRING(IDS_MAX_MRU_SIZE).c_str();
const wchar_t* c_flags = GET_RESOURCE_STRING(IDS_FLAGS).c_str();
const wchar_t* c_searchText = GET_RESOURCE_STRING(IDS_SEARCH_TEXT).c_str();
const wchar_t* c_replaceText = GET_RESOURCE_STRING(IDS_REPLACE_TEXT).c_str();
const wchar_t* c_mruEnabled = GET_RESOURCE_STRING(IDS_MRU_ENABLED).c_str();

const bool c_enabledDefault = true;
const bool c_showIconOnMenuDefault = true;
const bool c_extendedContextMenuOnlyDefaut = false;
const bool c_persistStateDefault = true;
const bool c_mruEnabledDefault = true;

const DWORD c_maxMRUSizeDefault = 10;
const DWORD c_flagsDefault = 0;

bool CSettings::GetEnabled()
{
    return GetRegBoolValue(c_enabled, c_enabledDefault);
}

bool CSettings::SetEnabled(_In_ bool enabled)
{
    return SetRegBoolValue(c_enabled, enabled);
}

bool CSettings::GetShowIconOnMenu()
{
    return GetRegBoolValue(c_showIconOnMenu, c_showIconOnMenuDefault);
}

bool CSettings::SetShowIconOnMenu(_In_ bool show)
{
    return SetRegBoolValue(c_showIconOnMenu, show);
}

bool CSettings::GetExtendedContextMenuOnly()
{
    return GetRegBoolValue(c_extendedContextMenuOnly, c_extendedContextMenuOnlyDefaut);
}

bool CSettings::SetExtendedContextMenuOnly(_In_ bool extendedOnly)
{
    return SetRegBoolValue(c_extendedContextMenuOnly, extendedOnly);
}

bool CSettings::GetPersistState()
{
    return GetRegBoolValue(c_persistState, c_persistStateDefault);
}

bool CSettings::SetPersistState(_In_ bool persistState)
{
    return SetRegBoolValue(c_persistState, persistState);
}

bool CSettings::GetMRUEnabled()
{
    return GetRegBoolValue(c_mruEnabled, c_mruEnabledDefault);
}

bool CSettings::SetMRUEnabled(_In_ bool enabled)
{
    return SetRegBoolValue(c_mruEnabled, enabled);
}

DWORD CSettings::GetMaxMRUSize()
{
    return GetRegDWORDValue(c_maxMRUSize, c_maxMRUSizeDefault);
}

bool CSettings::SetMaxMRUSize(_In_ DWORD maxMRUSize)
{
    return SetRegDWORDValue(c_maxMRUSize, maxMRUSize);
}

DWORD CSettings::GetFlags()
{
    return GetRegDWORDValue(c_flags, c_flagsDefault);
}

bool CSettings::SetFlags(_In_ DWORD flags)
{
    return SetRegDWORDValue(c_flags, flags);
}

bool CSettings::GetSearchText(__out_ecount(cchBuf) PWSTR text, DWORD cchBuf)
{
    return GetRegStringValue(c_searchText, text, cchBuf);
}

bool CSettings::SetSearchText(_In_ PCWSTR text)
{
    return SetRegStringValue(c_searchText, text);
}

bool CSettings::GetReplaceText(__out_ecount(cchBuf) PWSTR text, DWORD cchBuf)
{
    return GetRegStringValue(c_replaceText, text, cchBuf);
}

bool CSettings::SetReplaceText(_In_ PCWSTR text)
{
    return SetRegStringValue(c_replaceText, text);
}

bool CSettings::SetRegBoolValue(_In_ PCWSTR valueName, _In_ bool value)
{
    DWORD dwValue = value ? 1 : 0;
    return SetRegDWORDValue(valueName, dwValue);
}

bool CSettings::GetRegBoolValue(_In_ PCWSTR valueName, _In_ bool defaultValue)
{
    DWORD value = GetRegDWORDValue(valueName, (defaultValue == 0) ? false : true);
    return (value == 0) ? false : true;
}

bool CSettings::SetRegDWORDValue(_In_ PCWSTR valueName, _In_ DWORD value)
{
    return (SUCCEEDED(HRESULT_FROM_WIN32(SHSetValue(HKEY_CURRENT_USER, c_rootRegPath, valueName, REG_DWORD, &value, sizeof(value)))));
}

DWORD CSettings::GetRegDWORDValue(_In_ PCWSTR valueName, _In_ DWORD defaultValue)
{
    DWORD retVal = defaultValue;
    DWORD type = REG_DWORD;
    DWORD dwEnabled = 0;
    DWORD cb = sizeof(dwEnabled);
    if (SHGetValue(HKEY_CURRENT_USER, c_rootRegPath, valueName, &type, &dwEnabled, &cb) == ERROR_SUCCESS)
    {
        retVal = dwEnabled;
    }

    return retVal;
}

bool CSettings::SetRegStringValue(_In_ PCWSTR valueName, _In_ PCWSTR value)
{
    ULONG cb = (DWORD)((wcslen(value) + 1) * sizeof(*value));
    return (SUCCEEDED(HRESULT_FROM_WIN32(SHSetValue(HKEY_CURRENT_USER, c_rootRegPath, valueName, REG_SZ, (const BYTE*)value, cb))));
}

bool CSettings::GetRegStringValue(_In_ PCWSTR valueName, __out_ecount(cchBuf) PWSTR value, DWORD cchBuf)
{
    if (cchBuf > 0)
    {
        value[0] = L'\0';
    }

    DWORD type = REG_SZ;
    ULONG cb = cchBuf * sizeof(*value);
    return (SUCCEEDED(HRESULT_FROM_WIN32(SHGetValue(HKEY_CURRENT_USER, c_rootRegPath, valueName, &type, value, &cb) == ERROR_SUCCESS)));
}


typedef int (CALLBACK* MRUCMPPROC)(LPCWSTR, LPCWSTR);

typedef struct {
    DWORD      cbSize;
    UINT       uMax;
    UINT       fFlags;
    HKEY       hKey;
    LPCTSTR    lpszSubKey;
    MRUCMPPROC lpfnCompare;
} MRUINFO;

typedef HANDLE (*CreateMRUListFn)(MRUINFO* pmi);
typedef int (*AddMRUStringFn)(HANDLE hMRU, LPCWSTR data);
typedef int (*EnumMRUListFn)(HANDLE hMRU, int nItem, void* lpData, UINT uLen);
typedef int (*FreeMRUListFn)(HANDLE hMRU);

class CRenameMRU :
    public IEnumString,
    public IPowerRenameMRU
{
public:
    // IUnknown
    IFACEMETHODIMP_(ULONG) AddRef();
    IFACEMETHODIMP_(ULONG) Release();
    IFACEMETHODIMP QueryInterface(_In_ REFIID riid, _Outptr_ void** ppv);

    // IEnumString
    IFACEMETHODIMP Next(__in ULONG celt, __out_ecount_part(celt, *pceltFetched) LPOLESTR* rgelt, __out_opt ULONG* pceltFetched);
    IFACEMETHODIMP Skip(__in ULONG) { return E_NOTIMPL; }
    IFACEMETHODIMP Reset();
    IFACEMETHODIMP Clone(__deref_out IEnumString** ppenum) { *ppenum = nullptr;  return E_NOTIMPL; }

    // IPowerRenameMRU
    IFACEMETHODIMP AddMRUString(_In_ PCWSTR entry);

    static HRESULT CreateInstance(_In_ PCWSTR regPathMRU, _In_ ULONG maxMRUSize, _Outptr_ IUnknown** ppUnk);

private:
    CRenameMRU();
    ~CRenameMRU();

    HRESULT _Initialize(_In_ PCWSTR regPathMRU, __in ULONG maxMRUSize);
    HRESULT _CreateMRUList(_In_ MRUINFO* pmi);
    int _AddMRUString(_In_ PCWSTR data);
    int _EnumMRUList(_In_ int nItem, _Out_ void* lpData, _In_ UINT uLen);
    void _FreeMRUList();

    long   m_refCount = 0;
    HKEY   m_hKey = NULL;
    ULONG  m_maxMRUSize = 0;
    ULONG  m_mruIndex = 0;
    ULONG  m_mruSize = 0;
    HANDLE m_mruHandle = NULL;
    HMODULE m_hComctl32Dll = NULL;
    PWSTR  m_regPath = nullptr;
};

CRenameMRU::CRenameMRU() :
    m_refCount(1)
{}

CRenameMRU::~CRenameMRU()
{
    if (m_hKey)
    {
        RegCloseKey(m_hKey);
    }

    _FreeMRUList();

    if (m_hComctl32Dll)
    {
        FreeLibrary(m_hComctl32Dll);
    }

    CoTaskMemFree(m_regPath);
}

HRESULT CRenameMRU::CreateInstance(_In_ PCWSTR regPathMRU, _In_ ULONG maxMRUSize, _Outptr_ IUnknown** ppUnk)
{
    *ppUnk = nullptr;
    HRESULT hr = (regPathMRU && maxMRUSize > 0) ? S_OK : E_FAIL;
    if (SUCCEEDED(hr))
    {
        CRenameMRU* renameMRU = new CRenameMRU();
        hr = renameMRU ? S_OK : E_OUTOFMEMORY;
        if (SUCCEEDED(hr))
        {
            hr = renameMRU->_Initialize(regPathMRU, maxMRUSize);
            if (SUCCEEDED(hr))
            {
                hr = renameMRU->QueryInterface(IID_PPV_ARGS(ppUnk));
            }

            renameMRU->Release();
        }
    }

    return hr;
}

// IUnknown
IFACEMETHODIMP_(ULONG) CRenameMRU::AddRef()
{
    return InterlockedIncrement(&m_refCount);
}

IFACEMETHODIMP_(ULONG) CRenameMRU::Release()
{
    long refCount = InterlockedDecrement(&m_refCount);

    if (refCount == 0)
    {
        delete this;
    }
    return refCount;
}

IFACEMETHODIMP CRenameMRU::QueryInterface(_In_ REFIID riid, _Outptr_ void** ppv)
{
    static const QITAB qit[] = {
        QITABENT(CRenameMRU, IEnumString),
        QITABENT(CRenameMRU, IPowerRenameMRU),
        { 0 }
    };
    return QISearch(this, qit, riid, ppv);
}

HRESULT CRenameMRU::_Initialize(_In_ PCWSTR regPathMRU, __in ULONG maxMRUSize)
{
    m_maxMRUSize = maxMRUSize;

    wchar_t regPath[MAX_PATH] = { 0 };
    HRESULT hr = StringCchPrintf(regPath, ARRAYSIZE(regPath), L"%s\\%s", c_rootRegPath, regPathMRU);
    if (SUCCEEDED(hr))
    {
        hr = SHStrDup(regPathMRU, &m_regPath);

        if (SUCCEEDED(hr))
        {
            MRUINFO mi = {
                sizeof(MRUINFO),
                maxMRUSize,
                0,
                HKEY_CURRENT_USER,
                regPath,
                nullptr
            };

            hr = _CreateMRUList(&mi);
            if (SUCCEEDED(hr))
            {
                m_mruSize = _EnumMRUList(-1, NULL, 0);
            }
            else
            {
                hr = E_FAIL;
            }
        }
    }

    return hr;
}

// IEnumString
IFACEMETHODIMP CRenameMRU::Reset()
{
    m_mruIndex = 0;
    return S_OK;
}

#define MAX_ENTRY_STRING 1024

IFACEMETHODIMP CRenameMRU::Next(__in ULONG celt, __out_ecount_part(celt, *pceltFetched) LPOLESTR* rgelt, __out_opt ULONG* pceltFetched)
{
    HRESULT hr = S_OK;
    WCHAR mruEntry[MAX_ENTRY_STRING];
    mruEntry[0] = L'\0';

    if (pceltFetched)
    {
        *pceltFetched = 0;
    }

    if (!celt)
    {
        return S_OK;
    }

    if (!rgelt)
    {
        return S_FALSE;
    }

    hr = S_FALSE;
    if (m_mruIndex <= m_mruSize && _EnumMRUList(m_mruIndex++, (void*)mruEntry, ARRAYSIZE(mruEntry)) > 0)
    {
        hr = SHStrDup(mruEntry, rgelt);
        if (SUCCEEDED(hr) && pceltFetched != nullptr)
        {
            *pceltFetched = 1;
        }
    }

    return hr;
}

IFACEMETHODIMP CRenameMRU::AddMRUString(_In_ PCWSTR entry)
{
    return (_AddMRUString(entry) < 0) ? E_FAIL : S_OK;
}

HRESULT CRenameMRU::_CreateMRUList(_In_ MRUINFO* pmi)
{
    if (m_mruHandle != NULL)
    {
        _FreeMRUList();
    }

    if (m_hComctl32Dll == NULL)
    {
        m_hComctl32Dll = LoadLibraryEx(L"comctl32.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
    }

    if (m_hComctl32Dll != nullptr)
    {
        CreateMRUListFn pfnCreateMRUList = reinterpret_cast<CreateMRUListFn>(GetProcAddress(m_hComctl32Dll, (LPCSTR)MAKEINTRESOURCE(400)));
        if (pfnCreateMRUList != nullptr)
        {
            m_mruHandle = pfnCreateMRUList(pmi);
        }
    }

    return (m_mruHandle != NULL) ? S_OK : E_FAIL;
}

int CRenameMRU::_AddMRUString(_In_ PCWSTR data)
{
    int retVal = -1;
    if (m_mruHandle != NULL)
    {
        if (m_hComctl32Dll == NULL)
        {
            m_hComctl32Dll = LoadLibraryEx(L"comctl32.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
        }

        if (m_hComctl32Dll != nullptr)
        {
            AddMRUStringFn pfnAddMRUString = reinterpret_cast<AddMRUStringFn>(GetProcAddress(m_hComctl32Dll, (LPCSTR)MAKEINTRESOURCE(401)));
            if (pfnAddMRUString != nullptr)
            {
                retVal = pfnAddMRUString(m_mruHandle, data);
            }
        }
    }

    return retVal;
}

int CRenameMRU::_EnumMRUList(_In_ int nItem, _Out_ void* lpData, _In_ UINT uLen)
{
    int retVal = -1;
    if (m_mruHandle != NULL)
    {
        if (m_hComctl32Dll == NULL)
        {
            m_hComctl32Dll = LoadLibraryEx(L"comctl32.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
        }

        if (m_hComctl32Dll != nullptr)
        {
            EnumMRUListFn pfnEnumMRUList = reinterpret_cast<EnumMRUListFn>(GetProcAddress(m_hComctl32Dll, (LPCSTR)MAKEINTRESOURCE(403)));
            if (pfnEnumMRUList != nullptr)
            {
                retVal = pfnEnumMRUList(m_mruHandle, nItem, lpData, uLen);
            }
        }
    }

    return retVal;
}

void CRenameMRU::_FreeMRUList()
{
    if (m_mruHandle != NULL)
    {
        if (m_hComctl32Dll == NULL)
        {
            m_hComctl32Dll = LoadLibraryEx(L"comctl32.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
        }

        if (m_hComctl32Dll != nullptr)
        {
            FreeMRUListFn pfnFreeMRUList = reinterpret_cast<FreeMRUListFn>(GetProcAddress(m_hComctl32Dll, (LPCSTR)MAKEINTRESOURCE(152)));
            if (pfnFreeMRUList != nullptr)
            {
                pfnFreeMRUList(m_mruHandle);
            }
            
        }
        m_mruHandle = NULL;
    }
}

HRESULT CRenameMRUSearch_CreateInstance(_Outptr_ IUnknown** ppUnk)
{
    return CRenameMRU::CreateInstance(c_mruSearchRegPath, CSettings::GetMaxMRUSize(), ppUnk);
}

HRESULT CRenameMRUReplace_CreateInstance(_Outptr_ IUnknown** ppUnk)
{
    return CRenameMRU::CreateInstance(c_mruReplaceRegPath, CSettings::GetMaxMRUSize(), ppUnk);
}
