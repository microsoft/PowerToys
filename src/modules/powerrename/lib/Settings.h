#pragma once

#include "json.h"

#include <string>
#include <mutex>

class CSettings
{
public:
    static const int MAX_INPUT_STRING_LEN = 1024;

    CSettings();

    bool GetEnabled() const;
    void SetEnabled(bool enabled);

    bool GetShowIconOnMenu() const;
    void SetShowIconOnMenu(bool show);

    bool GetExtendedContextMenuOnly() const;
    void SetExtendedContextMenuOnly(bool extendedOnly);

    bool GetPersistState() const;
    void SetPersistState(bool persistState);

    bool GetMRUEnabled() const;
    void SetMRUEnabled(bool MRUEenabled);

    long GetMaxMRUSize() const;
    void SetMaxMRUSize(long maxMRUSize);

    long GetFlags() const;
    void SetFlags(long flags);

    const std::wstring& GetSearchText() const;
    void SetSearchText(const std::wstring& text);

    const std::wstring& GetReplaceText() const;
    void SetReplaceText(const std::wstring& text);

    void LoadPowerRenameData();
    void SavePowerRenameData() const;

private:
    struct Settings
    {
        bool enabled{ true };
        bool showIconOnMenu{ true };
        bool extendedContextMenuOnly{ false }; // Disabled by default.
        bool persistState{ true };
        bool MRUEnabled{ true };
        long maxMRUSize{ 10 };
        long flags{ 0 };
        std::wstring searchText;
        std::wstring replaceText;
    };

    json::JsonObject GetPersistPowerRenameJSON();

    void MigrateSettingsFromRegistry();
    void ParseJsonSettings(const json::JsonObject& jsonSettings);

    Settings settings;
    std::wstring jsonFilePath;

    mutable std::recursive_mutex dataLock;
};

CSettings& CSettingsInstance();

HRESULT CRenameMRUSearch_CreateInstance(_Outptr_ IUnknown** ppUnk);
HRESULT CRenameMRUReplace_CreateInstance(_Outptr_ IUnknown** ppUnk);