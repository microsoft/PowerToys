#pragma once

#include "json.h"

class CSettings
{
public:
    static const int MAX_INPUT_STRING_LEN = 1024;

    CSettings();

    inline bool GetEnabled()
    {
        Reload();
        return settings.enabled;
    }

    inline void SetEnabled(bool enabled)
    {
        settings.enabled = enabled;
    }

    inline bool GetShowIconOnMenu()
    {
        Reload();
        return settings.showIconOnMenu;
    }

    inline void SetShowIconOnMenu(bool show)
    {
        settings.showIconOnMenu = show;
    }

    inline bool GetExtendedContextMenuOnly()
    {
        Reload();
        return settings.extendedContextMenuOnly;
    }

    inline void SetExtendedContextMenuOnly(bool extendedOnly)
    {
        settings.extendedContextMenuOnly = extendedOnly;
    }

    inline bool GetPersistState()
    {
        Reload();
        return settings.persistState;
    }

    inline void SetPersistState(bool persistState)
    {
        settings.persistState = persistState;
    }

    inline bool GetMRUEnabled()
    {
        Reload();
        return settings.MRUEnabled;
    }

    inline void SetMRUEnabled(bool MRUEnabled)
    {
        settings.MRUEnabled = MRUEnabled;
    }

    inline long GetMaxMRUSize()
    {
        Reload();
        return settings.maxMRUSize;
    }

    inline void SetMaxMRUSize(long maxMRUSize)
    {
        settings.maxMRUSize = maxMRUSize;
    }

    inline long GetFlags()
    {
        Reload();
        return settings.flags;
    }

    inline void SetFlags(long flags)
    {
        settings.flags = flags;
        Save();
    }

    inline const std::wstring& GetSearchText()
    {
        Reload();
        return settings.searchText;
    }

    inline void SetSearchText(const std::wstring& text)
    {
        settings.searchText = text;
        Save();
    }

    inline const std::wstring& GetReplaceText()
    {
        Reload();
        return settings.replaceText;
    }

    inline void SetReplaceText(const std::wstring& text)
    {
        settings.replaceText = text;
        Save();
    }

    void Save();

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
        std::wstring searchText{};
        std::wstring replaceText{};
    };

    void Load();
    void Reload();
    void MigrateFromRegistry();
    void ParseJson();

    Settings settings;
    std::wstring jsonFilePath;
    FILETIME lastLoadedTime;
};

CSettings& CSettingsInstance();

HRESULT CRenameMRUSearch_CreateInstance(_Outptr_ IUnknown** ppUnk);
HRESULT CRenameMRUReplace_CreateInstance(_Outptr_ IUnknown** ppUnk);