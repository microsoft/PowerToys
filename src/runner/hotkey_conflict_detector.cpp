#include "pch.h"
#include "hotkey_conflict_detector.h"
#include <common/SettingsAPI/settings_helpers.h>
#include <windows.h>
#include <unordered_map>
#include <cwchar>

namespace HotkeyConflictDetector
{
    Hotkey ShortcutToHotkey(const CentralizedHotkeys::Shortcut& shortcut)
    {
        Hotkey hotkey;

        hotkey.win = (shortcut.modifiersMask & MOD_WIN) != 0;
        hotkey.ctrl = (shortcut.modifiersMask & MOD_CONTROL) != 0;
        hotkey.shift = (shortcut.modifiersMask & MOD_SHIFT) != 0;
        hotkey.alt = (shortcut.modifiersMask & MOD_ALT) != 0;

        hotkey.key = shortcut.vkCode > 255 ? 0 : static_cast<unsigned char>(shortcut.vkCode);

        return hotkey;
    }

    HotkeyConflictManager* HotkeyConflictManager::instance = nullptr;
    std::mutex HotkeyConflictManager::instanceMutex;

    HotkeyConflictManager& HotkeyConflictManager::GetInstance()
    {
        std::lock_guard<std::mutex> lock(instanceMutex);
        if (instance == nullptr)
        {
            instance = new HotkeyConflictManager();
        }
        return *instance;
    }

    HotkeyConflictType HotkeyConflictManager::HasConflict(Hotkey const& _hotkey, const wchar_t* _moduleName, const int _hotkeyID)
    {
        if (disabledHotkeys.find(_moduleName) != disabledHotkeys.end())
        {
            return HotkeyConflictType::NoConflict;
        }

        uint16_t handle = GetHotkeyHandle(_hotkey);

        if (handle == 0)
        {
            return HotkeyConflictType::NoConflict;
        }

        // The order is important, first to check sys conflict and then inapp conflict
        if (sysConflictHotkeyMap.find(handle) != sysConflictHotkeyMap.end())
        {
            return HotkeyConflictType::SystemConflict;
        }
        
        if (inAppConflictHotkeyMap.find(handle) != inAppConflictHotkeyMap.end())
        {
            return HotkeyConflictType::InAppConflict;
        }

        auto it = hotkeyMap.find(handle);

        if (it == hotkeyMap.end())
        {
            return HasConflictWithSystemHotkey(_hotkey) ?
                HotkeyConflictType::SystemConflict :
                HotkeyConflictType::NoConflict;
        }

        if (wcscmp(it->second.moduleName.c_str(), _moduleName) == 0 && it->second.hotkeyID == _hotkeyID)
        {
            // A shortcut matching its own assignment is not considered a conflict.
            return HotkeyConflictType::NoConflict;
        }

        return HotkeyConflictType::InAppConflict;
    }

    HotkeyConflictType HotkeyConflictManager::HasConflict(Hotkey const& _hotkey)
    {
        uint16_t handle = GetHotkeyHandle(_hotkey);

        if (handle == 0)
        {
            return HotkeyConflictType::NoConflict;
        }

        // The order is important, first to check sys conflict and then inapp conflict
        if (sysConflictHotkeyMap.find(handle) != sysConflictHotkeyMap.end())
        {
            return HotkeyConflictType::SystemConflict;
        }

        if (inAppConflictHotkeyMap.find(handle) != inAppConflictHotkeyMap.end())
        {
            return HotkeyConflictType::InAppConflict;
        }

        auto it = hotkeyMap.find(handle);

        if (it == hotkeyMap.end())
        {
            return HasConflictWithSystemHotkey(_hotkey) ?
                       HotkeyConflictType::SystemConflict :
                       HotkeyConflictType::NoConflict;
        }

        return HotkeyConflictType::InAppConflict;
    }

    // This function should only be called when a conflict has already been identified. 
    // It returns a list of all conflicting shortcuts.
    std::vector<HotkeyConflictInfo> HotkeyConflictManager::GetAllConflicts(Hotkey const& _hotkey)
    {
        std::vector<HotkeyConflictInfo> conflicts;
        uint16_t handle = GetHotkeyHandle(_hotkey);

        // Check in-app conflicts first
        auto inAppIt = inAppConflictHotkeyMap.find(handle);
        if (inAppIt != inAppConflictHotkeyMap.end())
        {
            // Add all in-app conflicts
            for (const auto& conflict : inAppIt->second)
            {
                conflicts.push_back(conflict);
            }

            return conflicts;
        }

        // Check system conflicts
        auto sysIt = sysConflictHotkeyMap.find(handle);
        if (sysIt != sysConflictHotkeyMap.end())
        {
            HotkeyConflictInfo systemConflict;
            systemConflict.hotkey = _hotkey;
            systemConflict.moduleName = L"System";
            systemConflict.hotkeyID = 0;

            conflicts.push_back(systemConflict);

            return conflicts;
        }

        // Check if there's a successfully registered hotkey that would conflict
        auto registeredIt = hotkeyMap.find(handle);
        if (registeredIt != hotkeyMap.end())
        {
            conflicts.push_back(registeredIt->second);

            return conflicts;
        }

        // If all the above conditions are ruled out, a system-level conflict is the only remaining explanation.
        HotkeyConflictInfo systemConflict;
        systemConflict.hotkey = _hotkey;
        systemConflict.moduleName = L"System";
        systemConflict.hotkeyID = 0;
        conflicts.push_back(systemConflict);

        return conflicts;
    }

    bool HotkeyConflictManager::AddHotkey(Hotkey const& _hotkey, const wchar_t* _moduleName, const int _hotkeyID, bool isEnabled)
    {
        if (!isEnabled)
        {
            disabledHotkeys[_moduleName].push_back({ _hotkey, _moduleName, _hotkeyID });
            return true;
        }

        uint16_t handle = GetHotkeyHandle(_hotkey);

        if (handle == 0)
        {
            return false;
        }

        HotkeyConflictType conflictType = HasConflict(_hotkey, _moduleName, _hotkeyID);
        if (conflictType != HotkeyConflictType::NoConflict)
        {
            if (conflictType == HotkeyConflictType::InAppConflict)
            {
                auto hotkeyFound = hotkeyMap.find(handle);
                inAppConflictHotkeyMap[handle].insert({ _hotkey, _moduleName, _hotkeyID });

                if (hotkeyFound != hotkeyMap.end())
                {
                    inAppConflictHotkeyMap[handle].insert(hotkeyFound->second);
                    hotkeyMap.erase(hotkeyFound);
                }
            }
            else
            {
                sysConflictHotkeyMap[handle].insert({ _hotkey, _moduleName, _hotkeyID });
            }
            return false;
        }

        HotkeyConflictInfo hotkeyInfo;
        hotkeyInfo.moduleName = _moduleName;
        hotkeyInfo.hotkeyID = _hotkeyID;
        hotkeyInfo.hotkey = _hotkey;
        hotkeyMap[handle] = hotkeyInfo;

        return true;
    }

    std::vector<HotkeyConflictInfo> HotkeyConflictManager::RemoveHotkeyByModule(const std::wstring& moduleName)
    {
        std::vector<HotkeyConflictInfo> removedHotkeys;

        if (disabledHotkeys.find(moduleName) != disabledHotkeys.end())
        {
            disabledHotkeys.erase(moduleName);
        }

        std::lock_guard<std::mutex> lock(hotkeyMutex);
        bool foundRecord = false;

        for (auto it = sysConflictHotkeyMap.begin(); it != sysConflictHotkeyMap.end();)
        {
            auto& conflictSet = it->second;
            for (auto setIt = conflictSet.begin(); setIt != conflictSet.end();)
            {
                if (setIt->moduleName == moduleName)
                {
                    removedHotkeys.push_back(*setIt);
                    setIt = conflictSet.erase(setIt);
                    foundRecord = true;
                }
                else
                {
                    ++setIt;
                }
            }
            if (conflictSet.empty())
            {
                it = sysConflictHotkeyMap.erase(it);
            }
            else
            {
                ++it;
            }
        }

        for (auto it = inAppConflictHotkeyMap.begin(); it != inAppConflictHotkeyMap.end();)
        {
            auto& conflictSet = it->second;
            uint16_t handle = it->first;

            for (auto setIt = conflictSet.begin(); setIt != conflictSet.end();)
            {
                if (setIt->moduleName == moduleName)
                {
                    removedHotkeys.push_back(*setIt);
                    setIt = conflictSet.erase(setIt);
                    foundRecord = true;
                }
                else
                {
                    ++setIt;
                }
            }

            if (conflictSet.empty())
            {
                it = inAppConflictHotkeyMap.erase(it);
            }
            else if (conflictSet.size() == 1)
            {
                // Move the only remaining conflict to main map
                const auto& onlyConflict = *conflictSet.begin();
                hotkeyMap[handle] = onlyConflict;
                it = inAppConflictHotkeyMap.erase(it);
            }
            else
            {
                ++it;
            }
        }

        for (auto it = hotkeyMap.begin(); it != hotkeyMap.end();)
        {
            if (it->second.moduleName == moduleName)
            {
                uint16_t handle = it->first;
                removedHotkeys.push_back(it->second);
                it = hotkeyMap.erase(it);
                foundRecord = true;

                auto inAppIt = inAppConflictHotkeyMap.find(handle);
                if (inAppIt != inAppConflictHotkeyMap.end() && inAppIt->second.size() == 1)
                {
                    // Move the only in-app conflict to main map
                    const auto& onlyConflict = *inAppIt->second.begin();
                    hotkeyMap[handle] = onlyConflict;
                    inAppConflictHotkeyMap.erase(inAppIt);
                }
            }
            else
            {
                ++it;
            }
        }

        return removedHotkeys;
    }

    void HotkeyConflictManager::EnableHotkeyByModule(const std::wstring& moduleName)
    {
        if (disabledHotkeys.find(moduleName) == disabledHotkeys.end())
        {
            return; // No disabled hotkeys for this module
        }

        auto hotkeys = disabledHotkeys[moduleName];
        disabledHotkeys.erase(moduleName);

        for (const auto& hotkeyInfo : hotkeys)
        {
            // Re-add the hotkey as enabled
            AddHotkey(hotkeyInfo.hotkey, moduleName.c_str(), hotkeyInfo.hotkeyID, true);
        }   
    }

    void HotkeyConflictManager::DisableHotkeyByModule(const std::wstring& moduleName)
    {
        auto hotkeys = RemoveHotkeyByModule(moduleName);
        disabledHotkeys[moduleName] = hotkeys;
    }

    bool HotkeyConflictManager::HasConflictWithSystemHotkey(const Hotkey& hotkey)
    {
        // Convert PowerToys Hotkey format to Win32 RegisterHotKey format
        UINT modifiers = 0;
        if (hotkey.win)
        {
            modifiers |= MOD_WIN;
        }
        if (hotkey.ctrl)
        {
            modifiers |= MOD_CONTROL;
        }
        if (hotkey.alt)
        {
            modifiers |= MOD_ALT;
        }
        if (hotkey.shift)
        {
            modifiers |= MOD_SHIFT;
        }

        // No modifiers or no key is not a valid hotkey
        if (modifiers == 0 || hotkey.key == 0)
        {
            return false;
        }

        // Use a unique ID for this test registration
        const int hotkeyId = 0x0FFF; // Arbitrary ID for temporary registration

        // Try to register the hotkey with Windows, using nullptr instead of a window handle
        if (!RegisterHotKey(nullptr, hotkeyId, modifiers, hotkey.key))
        {
            // If registration fails with ERROR_HOTKEY_ALREADY_REGISTERED, it means the hotkey
            // is already in use by the system or another application
            if (GetLastError() == ERROR_HOTKEY_ALREADY_REGISTERED)
            {
                return true;
            }
        }
        else
        {
            // If registration succeeds, unregister it immediately
            UnregisterHotKey(nullptr, hotkeyId);
        }

        return false;
    }

    json::JsonObject HotkeyConflictManager::GetHotkeyConflictsAsJson()
    {
        std::lock_guard<std::mutex> lock(hotkeyMutex);

        using namespace json;
        JsonObject root;

        // Serialize hotkey to a unique string format for grouping
        auto serializeHotkey = [](const Hotkey& hotkey) -> JsonObject {
            JsonObject obj;
            obj.Insert(L"win", value(hotkey.win));
            obj.Insert(L"ctrl", value(hotkey.ctrl));
            obj.Insert(L"shift", value(hotkey.shift));
            obj.Insert(L"alt", value(hotkey.alt));
            obj.Insert(L"key", value(static_cast<int>(hotkey.key)));
            return obj;
        };

        // New format: Group conflicts by hotkey
        JsonArray inAppConflictsArray;
        JsonArray sysConflictsArray;

        // Process in-app conflicts - only include hotkeys that are actually in conflict
        for (const auto& [handle, conflicts] : inAppConflictHotkeyMap)
        {
            if (!conflicts.empty())
            {
                JsonObject conflictGroup;

                // All entries have the same hotkey, so use the first one for the key
                conflictGroup.Insert(L"hotkey", serializeHotkey(conflicts.begin()->hotkey));

                // Create an array of module info without repeating the hotkey
                JsonArray modules;
                for (const auto& info : conflicts)
                {
                    JsonObject moduleInfo;
                    moduleInfo.Insert(L"moduleName", value(info.moduleName));
                    moduleInfo.Insert(L"hotkeyID", value(info.hotkeyID));
                    modules.Append(moduleInfo);
                }

                conflictGroup.Insert(L"modules", modules);
                inAppConflictsArray.Append(conflictGroup);
            }
        }

        // Process system conflicts - only include hotkeys that are actually in conflict
        for (const auto& [handle, conflicts] : sysConflictHotkeyMap)
        {
            if (!conflicts.empty())
            {
                JsonObject conflictGroup;

                // All entries have the same hotkey, so use the first one for the key
                conflictGroup.Insert(L"hotkey", serializeHotkey(conflicts.begin()->hotkey));

                // Create an array of module info without repeating the hotkey
                JsonArray modules;
                for (const auto& info : conflicts)
                {
                    JsonObject moduleInfo;
                    moduleInfo.Insert(L"moduleName", value(info.moduleName));
                    moduleInfo.Insert(L"hotkeyID", value(info.hotkeyID));
                    modules.Append(moduleInfo);
                }

                conflictGroup.Insert(L"modules", modules);
                sysConflictsArray.Append(conflictGroup);
            }
        }

        // Add the grouped conflicts to the root object
        root.Insert(L"inAppConflicts", inAppConflictsArray);
        root.Insert(L"sysConflicts", sysConflictsArray);

        return root;
    }

    uint16_t HotkeyConflictManager::GetHotkeyHandle(const Hotkey& hotkey)
    {
        uint16_t handle = hotkey.key;
        handle |= hotkey.win << 8;
        handle |= hotkey.ctrl << 9;
        handle |= hotkey.shift << 10;
        handle |= hotkey.alt << 11;
        return handle;
    }
}