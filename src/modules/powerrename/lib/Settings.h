#pragma once

#include "json.h"

#include <string>
#include <mutex>

class CSettings
{
public:
    static const int MAX_INPUT_STRING_LEN = 1024;

    CSettings();

    inline bool GetEnabled() const
    {
        return settings.enabled;
    }

    inline void SetEnabled(bool enabled)
    {
        settings.enabled = enabled;
    }

    inline bool GetShowIconOnMenu() const
    {
        return settings.showIconOnMenu;
    }

    inline void SetShowIconOnMenu(bool show)
    {
        settings.showIconOnMenu = show;
    }

    inline bool GetExtendedContextMenuOnly() const
    {
        return settings.extendedContextMenuOnly;
    }

    inline void SetExtendedContextMenuOnly(bool extendedOnly)
    {
        settings.extendedContextMenuOnly = extendedOnly;
    }

    inline bool GetPersistState() const
    {
        return settings.persistState;
    }

    inline void SetPersistState(bool persistState)
    {
        settings.persistState = persistState;
    }

    inline bool GetMRUEnabled() const
    {
        return settings.MRUEnabled;
    }

    inline void SetMRUEnabled(bool MRUEnabled)
    {
        settings.MRUEnabled = MRUEnabled;
    }

    inline long GetMaxMRUSize() const
    {
        return settings.maxMRUSize;
    }

    inline void SetMaxMRUSize(long maxMRUSize)
    {
        settings.maxMRUSize = maxMRUSize;
    }

    inline long GetFlags() const
    {
        return settings.flags;
    }

    inline void SetFlags(long flags)
    {
        settings.flags = flags;
    }

    inline const std::wstring& GetSearchText() const
    {
        return settings.searchText;
    }

    inline void SetSearchText(const std::wstring& text)
    {
        settings.searchText = text;
    }

    inline const std::wstring& GetReplaceText() const
    {
        return settings.replaceText;
    }

    inline void SetReplaceText(const std::wstring& text)
    {
        settings.replaceText = text;
    }

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
        std::wstring searchText{};
        std::wstring replaceText{};
    };

    void MigrateSettingsFromRegistry();
    void ParseJsonSettings();

    Settings settings;
    std::wstring jsonFilePath;
};

CSettings& CSettingsInstance();

HRESULT CRenameMRUSearch_CreateInstance(_Outptr_ IUnknown** ppUnk);
HRESULT CRenameMRUReplace_CreateInstance(_Outptr_ IUnknown** ppUnk);