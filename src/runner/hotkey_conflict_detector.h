#pragma once
#include "pch.h"
#include <unordered_map>
#include <unordered_set>
#include <string>

#include "../modules/interface/powertoy_module_interface.h"
#include "centralized_hotkeys.h"
#include "common/utils/json.h"

namespace HotkeyConflictDetector
{
    using Hotkey = PowertoyModuleIface::Hotkey;
    using HotkeyEx = PowertoyModuleIface::HotkeyEx;
    using Shortcut = CentralizedHotkeys::Shortcut;

    struct HotkeyConflictInfo
    {
        Hotkey hotkey;
        std::wstring moduleName;
        int hotkeyID = 0;

        inline bool operator==(const HotkeyConflictInfo& other) const  
        {  
           return hotkey == other.hotkey &&  
                  moduleName == other.moduleName &&  
                  hotkeyID == other.hotkeyID;  
        }
    };

    Hotkey ShortcutToHotkey(const CentralizedHotkeys::Shortcut& shortcut);

    enum HotkeyConflictType
    {
        NoConflict = 0,
        SystemConflict = 1,
        InAppConflict = 2,
    };

    class HotkeyConflictManager
    {
    public:
        static HotkeyConflictManager& GetInstance();

        HotkeyConflictType HasConflict(const Hotkey& hotkey, const wchar_t* moduleName, const int hotkeyID);
        HotkeyConflictType HotkeyConflictManager::HasConflict(Hotkey const& _hotkey);
        std::vector<HotkeyConflictInfo> HotkeyConflictManager::GetAllConflicts(Hotkey const& hotkey);
        bool AddHotkey(const Hotkey& hotkey, const wchar_t* moduleName, const int hotkeyID, bool isEnabled);
        std::vector<HotkeyConflictInfo> RemoveHotkeyByModule(const std::wstring& moduleName);
        
        void EnableHotkeyByModule(const std::wstring& moduleName);
        void DisableHotkeyByModule(const std::wstring& moduleName);

        json::JsonObject GetHotkeyConflictsAsJson();

    private:
        static std::mutex instanceMutex;
        static HotkeyConflictManager* instance;

        std::mutex hotkeyMutex;
        // Hotkey in hotkeyMap means the hotkey has been registered successfully
        std::unordered_map<uint16_t, HotkeyConflictInfo> hotkeyMap;
        // Hotkey in sysConflictHotkeyMap means the hotkey has conflict with system defined hotkeys
        std::unordered_map<uint16_t, std::unordered_set<HotkeyConflictInfo>> sysConflictHotkeyMap;
        // Hotkey in inAppConflictHotkeyMap means the hotkey has conflict with other modules
        std::unordered_map<uint16_t, std::unordered_set<HotkeyConflictInfo>> inAppConflictHotkeyMap;

        std::unordered_map<std::wstring, std::vector<HotkeyConflictInfo>> disabledHotkeys;

        uint16_t GetHotkeyHandle(const Hotkey&);
        bool HasConflictWithSystemHotkey(const Hotkey&);

        HotkeyConflictManager() = default;
    };
};

namespace std
{
    template<>
    struct hash<HotkeyConflictDetector::HotkeyConflictInfo>
    {
        size_t operator()(const HotkeyConflictDetector::HotkeyConflictInfo& info) const
        {

            size_t hotkeyHash =
                (info.hotkey.win ? 1ULL : 0ULL) |
                ((info.hotkey.ctrl ? 1ULL : 0ULL) << 1) |
                ((info.hotkey.shift ? 1ULL : 0ULL) << 2) |
                ((info.hotkey.alt ? 1ULL : 0ULL) << 3) |
                (static_cast<size_t>(info.hotkey.key) << 4);

            size_t moduleHash = std::hash<std::wstring>{}(info.moduleName);
            size_t idHash = std::hash<int>{}(info.hotkeyID);

            return hotkeyHash ^ 
                ((moduleHash << 1) | (moduleHash >> (sizeof(size_t) * 8 - 1))) ^    // rotate left 1 bit
                ((idHash << 2) | (idHash >> (sizeof(size_t) * 8 - 2)));         // rotate left 2 bits
        }
    };
}
