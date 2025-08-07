#include "pch.h"
#include "centralized_hotkeys.h"

#include <map>
#include <common/logger/logger.h>
#include <common/utils/winapi_error.h>
#include <common/SettingsAPI/settings_objects.h>

namespace CentralizedHotkeys
{
    std::map<Shortcut, std::vector<Action>> actions;
    std::map<Shortcut, int> ids;
    HWND runnerWindow;

    std::wstring ToWstring(const Shortcut& shortcut)
    {
        std::wstring res = L"";
        if (shortcut.modifiersMask & MOD_SHIFT)
        {
            res += L"shift+";
        }

        if (shortcut.modifiersMask & MOD_CONTROL)
        {
            res += L"ctrl+";
        }

        if (shortcut.modifiersMask & MOD_WIN)
        {
            res += L"win+";
        }

        if (shortcut.modifiersMask & MOD_ALT)
        {
            res += L"alt+";
        }

        res += PowerToysSettings::HotkeyObject::key_from_code(shortcut.vkCode);

        return res;
    }

    bool AddHotkeyAction(Shortcut shortcut, Action action)
    {
        if (!actions[shortcut].empty())
        {
            // It will only work if previous one is rewritten
            Logger::warn(L"{} shortcut is already registered", ToWstring(shortcut));
        }

        actions[shortcut].push_back(action);
        // Register hotkey if it is the first shortcut
        if (actions[shortcut].size() == 1)
        {
            if (ids.find(shortcut) == ids.end())
            {
                static int nextId = 0;
                ids[shortcut] = nextId++;
            }

            if (!RegisterHotKey(runnerWindow, ids[shortcut], shortcut.modifiersMask, shortcut.vkCode))
            {
                Logger::warn(L"Failed to add {} shortcut. {}", ToWstring(shortcut), get_last_error_or_default(GetLastError()));
                return false;
            }

            Logger::trace(L"{} shortcut registered", ToWstring(shortcut));
            return true;
        }

        return true;
    }

    void UnregisterHotkeysForModule(std::wstring moduleName)
    {
        for (auto it = actions.begin(); it != actions.end(); it++)
        {
            auto val = std::find_if(it->second.begin(), it->second.end(), [moduleName](Action a) { return a.moduleName == moduleName; });
            if (val != it->second.end())
            {
                it->second.erase(val);

                if (it->second.empty())
                {
                    if (!UnregisterHotKey(runnerWindow, ids[it->first]))
                    {
                        Logger::warn(L"Failed to unregister {} shortcut. {}", ToWstring(it->first), get_last_error_or_default(GetLastError()));
                    }
                    else
                    {
                        Logger::trace(L"{} shortcut unregistered", ToWstring(it->first));
                    }
                }
            }
        }
    }

    void PopulateHotkey(Shortcut shortcut)
    {
        if (!actions.empty())
        {
            try
            {
                actions[shortcut].begin()->action(shortcut.modifiersMask, shortcut.vkCode);
            }
            catch(std::exception& ex)
            {
                Logger::error("Failed to execute hotkey's action. {}", ex.what());
            }
            catch(...)
            {
                Logger::error(L"Failed to execute hotkey's action");
            }
        }
    }

    void RegisterWindow(HWND hwnd)
    {
        runnerWindow = hwnd;
    }
}
