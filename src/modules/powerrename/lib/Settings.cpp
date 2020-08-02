#include "pch.h"
#include "Settings.h"
#include "PowerRenameInterfaces.h"
#include "settings_helpers.h"

#include <filesystem>
#include <commctrl.h>
#include <algorithm>
#include <fstream>

namespace
{
    const wchar_t c_powerRenameDataFilePath[] = L"\\power-rename-settings.json";
    const wchar_t c_powerRenameUIFlagsFilePath[] = L"\\power-rename-ui-flags";
    const wchar_t c_searchMRUListFilePath[] = L"\\search-mru.json";
    const wchar_t c_replaceMRUListFilePath[] = L"\\replace-mru.json";

    const wchar_t c_rootRegPath[] = L"Software\\Microsoft\\PowerRename";
    const wchar_t c_mruSearchRegPath[] = L"\\SearchMRU";
    const wchar_t c_mruReplaceRegPath[] = L"\\ReplaceMRU";

    const wchar_t c_enabled[] = L"Enabled";
    const wchar_t c_showIconOnMenu[] = L"ShowIcon";
    const wchar_t c_extendedContextMenuOnly[] = L"ExtendedContextMenuOnly";
    const wchar_t c_persistState[] = L"PersistState";
    const wchar_t c_maxMRUSize[] = L"MaxMRUSize";
    const wchar_t c_flags[] = L"Flags";
    const wchar_t c_searchText[] = L"SearchText";
    const wchar_t c_replaceText[] = L"ReplaceText";
    const wchar_t c_mruEnabled[] = L"MRUEnabled";
    const wchar_t c_mruList[] = L"MRUList";
    const wchar_t c_insertionIdx[] = L"InsertionIdx";

    unsigned int GetRegNumber(const std::wstring& valueName, unsigned int defaultValue)
    {
        DWORD type = REG_DWORD;
        DWORD data = 0;
        DWORD size = sizeof(DWORD);
        if (SHGetValue(HKEY_CURRENT_USER, c_rootRegPath, valueName.c_str(), &type, &data, &size) == ERROR_SUCCESS)
        {
            return data;
        }
        return defaultValue;
    }

    void SetRegNumber(const std::wstring& valueName, unsigned int value)
    {
        SHSetValue(HKEY_CURRENT_USER, c_rootRegPath, valueName.c_str(), REG_DWORD, &value, sizeof(value));
    }

    bool GetRegBoolean(const std::wstring& valueName, bool defaultValue)
    {
        DWORD value = GetRegNumber(valueName.c_str(), defaultValue ? 1 : 0);
        return (value == 0) ? false : true;
    }

    void SetRegBoolean(const std::wstring& valueName, bool value)
    {
        SetRegNumber(valueName, value ? 1 : 0);
    }

    std::wstring GetRegString(const std::wstring& valueName,const std::wstring& subPath)
    {
        wchar_t value[CSettings::MAX_INPUT_STRING_LEN];
        value[0] = L'\0';
        DWORD type = REG_SZ;
        DWORD size = CSettings::MAX_INPUT_STRING_LEN * sizeof(wchar_t);
        std::wstring completePath = std::wstring(c_rootRegPath) + subPath;
        if (SHGetValue(HKEY_CURRENT_USER, completePath.c_str(), valueName.c_str(), &type, value, &size) == ERROR_SUCCESS)
        {
            return std::wstring(value);
        }
        return std::wstring{};
    }

    bool LastModifiedTime(const std::wstring& filePath, FILETIME* lpFileTime)
    {
        WIN32_FILE_ATTRIBUTE_DATA attr{};
        if (GetFileAttributesExW(filePath.c_str(), GetFileExInfoStandard, &attr))
        {
            *lpFileTime = attr.ftLastWriteTime;
            return true;
        }
        return false;
    }
}

class MRUListHandler
{
public:
    MRUListHandler(unsigned int size, const std::wstring& filePath, const std::wstring& regPath) :
        pushIdx(0),
        nextIdx(1),
        size(size),
        jsonFilePath(PTSettingsHelper::get_module_save_folder_location(L"PowerRename") + filePath),
        registryFilePath(regPath)
    {
        items.resize(size);
        Load();
    }

    void Push(const std::wstring& data);
    bool Next(std::wstring& data);

    void Reset();

private:
    void Load();
    void Save();
    void MigrateFromRegistry();
    json::JsonArray Serialize();
    void ParseJson();

    bool Exists(const std::wstring& data);

    std::vector<std::wstring> items;
    unsigned int pushIdx;
    unsigned int nextIdx;
    unsigned int size;
    const std::wstring jsonFilePath;
    const std::wstring registryFilePath;
};


void MRUListHandler::Push(const std::wstring& data)
{
    if (Exists(data))
    {
        // TODO: Already existing item should be put on top of MRU list.
        return;
    }
    items[pushIdx] = data;
    pushIdx = (pushIdx + 1) % size;
    Save();
}

bool MRUListHandler::Next(std::wstring& data)
{
    if (nextIdx == size + 1)
    {
        Reset();
        return false;
    }
    // Go backwards to consume latest items first.
    unsigned int idx = (pushIdx + size - nextIdx) % size;
    if (items[idx].empty())
    {
        Reset();
        return false;
    }
    data = items[idx];
    ++nextIdx;
    return true;
}

void MRUListHandler::Reset()
{
    nextIdx = 1;
}

void MRUListHandler::Load()
{
    if (!std::filesystem::exists(jsonFilePath))
    {
        MigrateFromRegistry();

        Save();
    }
    else
    {
        ParseJson();
    }
}

void MRUListHandler::Save()
{
    json::JsonObject jsonData;

    jsonData.SetNamedValue(c_maxMRUSize, json::value(size));
    jsonData.SetNamedValue(c_insertionIdx, json::value(pushIdx));
    jsonData.SetNamedValue(c_mruList, Serialize());

    json::to_file(jsonFilePath, jsonData);
}

json::JsonArray MRUListHandler::Serialize()
{
    json::JsonArray searchMRU{};

    std::wstring data{};
    for (const std::wstring& item : items)
    {
        searchMRU.Append(json::value(item));
    }

    return searchMRU;
}

void MRUListHandler::MigrateFromRegistry()
{
    std::wstring searchListKeys = GetRegString(c_mruList, registryFilePath);
    std::sort(std::begin(searchListKeys), std::end(searchListKeys));
    for (const wchar_t& key : searchListKeys)
    {
        Push(GetRegString(std::wstring(1, key), registryFilePath));
    }
}

void MRUListHandler::ParseJson()
{
    auto json = json::from_file(jsonFilePath);
    if (json)
    {
        const json::JsonObject& jsonObject = json.value();
        try
        {
            unsigned int oldSize{ size };
            if (json::has(jsonObject, c_maxMRUSize, json::JsonValueType::Number))
            {
                oldSize = (unsigned int)jsonObject.GetNamedNumber(c_maxMRUSize);
            }
            unsigned int oldPushIdx{ 0 };
            if (json::has(jsonObject, c_insertionIdx, json::JsonValueType::Number))
            {
                oldPushIdx = (unsigned int)jsonObject.GetNamedNumber(c_insertionIdx);
                if (oldPushIdx < 0 || oldPushIdx >= oldSize)
                {
                    oldPushIdx = 0;
                }
            }
            if (json::has(jsonObject, c_mruList, json::JsonValueType::Array))
            {
                auto jsonArray = jsonObject.GetNamedArray(c_mruList);
                if (oldSize == size)
                {
                    for (uint32_t i = 0; i < jsonArray.Size(); ++i)
                    {
                        items[i] = std::wstring(jsonArray.GetStringAt(i));
                    }
                    pushIdx = oldPushIdx;
                }
                else
                {
                    std::vector<std::wstring> temp;
                    for (unsigned int i = 0; i < min(jsonArray.Size(), size); ++i)
                    {
                        int idx = (oldPushIdx + oldSize - (i + 1)) % oldSize;
                        temp.push_back(std::wstring(jsonArray.GetStringAt(idx)));
                    }
                    if (size > oldSize)
                    {
                        std::reverse(std::begin(temp), std::end(temp));
                        pushIdx = (unsigned int)temp.size();
                        temp.resize(size);
                    }
                    else
                    {
                        temp.resize(size);
                        std::reverse(std::begin(temp), std::end(temp));
                    }
                    items = std::move(temp);
                    Save();
                }
            }
        }
        catch (const winrt::hresult_error&) { }
    }
}

bool MRUListHandler::Exists(const std::wstring& data)
{
    return std::find(std::begin(items), std::end(items), data) != std::end(items);
}

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

    static HRESULT CreateInstance(_In_ const std::wstring& filePath, _In_ const std::wstring& regPath, _Outptr_ IUnknown** ppUnk);

private:
    CRenameMRU(int size, const std::wstring& filePath, const std::wstring& regPath);

    std::unique_ptr<MRUListHandler> mruList;
    unsigned int refCount = 0;
};

CRenameMRU::CRenameMRU(int size, const std::wstring& filePath, const std::wstring& regPath) :
    refCount(1)
{
    mruList = std::make_unique<MRUListHandler>(size, filePath, regPath);
}

HRESULT CRenameMRU::CreateInstance(_In_ const std::wstring& filePath, _In_ const std::wstring& regPath, _Outptr_ IUnknown** ppUnk)
{
    *ppUnk = nullptr;
    unsigned int maxMRUSize = CSettingsInstance().GetMaxMRUSize();
    HRESULT hr = maxMRUSize > 0 ? S_OK : E_FAIL;
    if (SUCCEEDED(hr))
    {
        CRenameMRU* renameMRU = new CRenameMRU(maxMRUSize, filePath, regPath);
        hr = renameMRU ? S_OK : E_OUTOFMEMORY;
        if (SUCCEEDED(hr))
        {
            renameMRU->QueryInterface(IID_PPV_ARGS(ppUnk));
            renameMRU->Release();
        }
    }

    return hr;
}

IFACEMETHODIMP_(ULONG) CRenameMRU::AddRef()
{
    return InterlockedIncrement(&refCount);
}

IFACEMETHODIMP_(ULONG) CRenameMRU::Release()
{
    unsigned int cnt = InterlockedDecrement(&refCount);

    if (cnt == 0)
    {
        delete this;
    }
    return cnt;
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

IFACEMETHODIMP CRenameMRU::Next(__in ULONG celt, __out_ecount_part(celt, *pceltFetched) LPOLESTR* rgelt, __out_opt ULONG* pceltFetched)
{
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

    HRESULT hr = S_FALSE;
    if (std::wstring data{}; mruList->Next(data))
    {
        hr = SHStrDup(data.c_str(), rgelt);
        if (SUCCEEDED(hr) && pceltFetched != nullptr)
        {
            *pceltFetched = 1;
        }
    }

    return hr;
}

IFACEMETHODIMP CRenameMRU::Reset()
{
    mruList->Reset();
    return S_OK;
}

IFACEMETHODIMP CRenameMRU::AddMRUString(_In_ PCWSTR entry)
{
    mruList->Push(entry);
    return S_OK;
}

CSettings::CSettings()
{
    std::wstring result = PTSettingsHelper::get_module_save_folder_location(L"PowerRename");
    jsonFilePath = result + std::wstring(c_powerRenameDataFilePath);
    UIFlagsFilePath = result + std::wstring(c_powerRenameUIFlagsFilePath);
    Load();
}

void CSettings::Save()
{
    json::JsonObject jsonData;

    jsonData.SetNamedValue(c_enabled,                 json::value(settings.enabled));
    jsonData.SetNamedValue(c_showIconOnMenu,          json::value(settings.showIconOnMenu));
    jsonData.SetNamedValue(c_extendedContextMenuOnly, json::value(settings.extendedContextMenuOnly));
    jsonData.SetNamedValue(c_persistState,            json::value(settings.persistState));
    jsonData.SetNamedValue(c_mruEnabled,              json::value(settings.MRUEnabled));
    jsonData.SetNamedValue(c_maxMRUSize,              json::value(settings.maxMRUSize));
    jsonData.SetNamedValue(c_searchText,              json::value(settings.searchText));
    jsonData.SetNamedValue(c_replaceText,             json::value(settings.replaceText));

    json::to_file(jsonFilePath, jsonData);
    GetSystemTimeAsFileTime(&lastLoadedTime);
}

void CSettings::Load()
{
    if (!std::filesystem::exists(jsonFilePath))
    {
        MigrateFromRegistry();

        Save();
        WriteFlags();
    }
    else
    {
        ParseJson();
        ReadFlags();
    }
}

void CSettings::Reload()
{
    // Load json settings from data file if it is modified in the meantime.
    FILETIME lastModifiedTime{};
    if (LastModifiedTime(jsonFilePath, &lastModifiedTime) &&
        CompareFileTime(&lastModifiedTime, &lastLoadedTime) == 1)
    {
        Load();
    }
}

void CSettings::MigrateFromRegistry()
{
    settings.enabled                 = GetRegBoolean(c_enabled, true);
    settings.showIconOnMenu          = GetRegBoolean(c_showIconOnMenu, true);
    settings.extendedContextMenuOnly = GetRegBoolean(c_extendedContextMenuOnly, false); // Disabled by default.
    settings.persistState            = GetRegBoolean(c_persistState, true);
    settings.MRUEnabled              = GetRegBoolean(c_mruEnabled, true);
    settings.maxMRUSize              = GetRegNumber(c_maxMRUSize, 10);
    settings.flags                   = GetRegNumber(c_flags, 0);
    settings.searchText              = GetRegString(c_searchText, L"");
    settings.replaceText             = GetRegString(c_replaceText, L"");
}

void CSettings::ParseJson()
{
    auto json = json::from_file(jsonFilePath);
    if (json)
    {
        const json::JsonObject& jsonSettings = json.value();
        try
        {
            if (json::has(jsonSettings, c_enabled, json::JsonValueType::Boolean))
            {
                settings.enabled = jsonSettings.GetNamedBoolean(c_enabled);
            }
            if (json::has(jsonSettings, c_showIconOnMenu, json::JsonValueType::Boolean))
            {
                settings.showIconOnMenu = jsonSettings.GetNamedBoolean(c_showIconOnMenu);
            }
            if (json::has(jsonSettings, c_extendedContextMenuOnly, json::JsonValueType::Boolean))
            {
                settings.extendedContextMenuOnly = jsonSettings.GetNamedBoolean(c_extendedContextMenuOnly);
            }
            if (json::has(jsonSettings, c_persistState, json::JsonValueType::Boolean))
            {
                settings.persistState = jsonSettings.GetNamedBoolean(c_persistState);
            }
            if (json::has(jsonSettings, c_mruEnabled, json::JsonValueType::Boolean))
            {
                settings.MRUEnabled = jsonSettings.GetNamedBoolean(c_mruEnabled);
            }
            if (json::has(jsonSettings, c_maxMRUSize, json::JsonValueType::Number))
            {
                settings.maxMRUSize = (unsigned int)jsonSettings.GetNamedNumber(c_maxMRUSize);
            }
            if (json::has(jsonSettings, c_searchText, json::JsonValueType::String))
            {
                settings.searchText = jsonSettings.GetNamedString(c_searchText);
            }
            if (json::has(jsonSettings, c_replaceText, json::JsonValueType::String))
            {
                settings.replaceText = jsonSettings.GetNamedString(c_replaceText);
            }
        }
        catch (const winrt::hresult_error&) { }
    }
    GetSystemTimeAsFileTime(&lastLoadedTime);
}

void CSettings::ReadFlags()
{
    std::ifstream file(UIFlagsFilePath, std::ios::binary);
    if (file.is_open())
    {
        file >> settings.flags;
    }
}

void CSettings::WriteFlags()
{
    std::ofstream file(UIFlagsFilePath, std::ios::binary);
    if (file.is_open())
    {
        file << settings.flags;
    }
}

CSettings& CSettingsInstance()
{
    static CSettings instance;
    return instance;
}

HRESULT CRenameMRUSearch_CreateInstance(_Outptr_ IUnknown** ppUnk)
{
    return CRenameMRU::CreateInstance(c_searchMRUListFilePath, c_mruSearchRegPath, ppUnk);
}

HRESULT CRenameMRUReplace_CreateInstance(_Outptr_ IUnknown** ppUnk)
{
    return CRenameMRU::CreateInstance(c_replaceMRUListFilePath, c_mruReplaceRegPath, ppUnk);
}
