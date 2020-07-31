#include "pch.h"
#include "LoadingAndSavingRemappingHelper.h"
#include <set>
#include "../common/shared_constants.h"

namespace LoadingAndSavingRemappingHelper
{
    // Function to check if the set of remappings in the buffer are valid
    KeyboardManagerHelper::ErrorType CheckIfRemappingsAreValid(const std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>>& remappings)
    {
        KeyboardManagerHelper::ErrorType isSuccess = KeyboardManagerHelper::ErrorType::NoError;
        std::map<std::wstring, std::set<std::variant<DWORD, Shortcut>>> ogKeys;
        for (int i = 0; i < remappings.size(); i++)
        {
            std::variant<DWORD, Shortcut> ogKey = remappings[i].first[0];
            std::variant<DWORD, Shortcut> newKey = remappings[i].first[1];
            std::wstring appName = remappings[i].second;

            bool ogKeyValidity = (ogKey.index() == 0 && std::get<DWORD>(ogKey) != NULL) || (ogKey.index() == 1 && std::get<Shortcut>(ogKey).IsValidShortcut());
            bool newKeyValidity = (newKey.index() == 0 && std::get<DWORD>(newKey) != NULL) || (newKey.index() == 1 && std::get<Shortcut>(newKey).IsValidShortcut());

            // Add new set for a new target app name
            if (ogKeys.find(appName) == ogKeys.end())
            {
                ogKeys[appName] = std::set<std::variant<DWORD, Shortcut>>();
            }

            if (ogKeyValidity && newKeyValidity && ogKeys[appName].find(ogKey) == ogKeys[appName].end())
            {
                ogKeys[appName].insert(ogKey);
            }
            else if (ogKeyValidity && newKeyValidity && ogKeys[appName].find(ogKey) != ogKeys[appName].end())
            {
                isSuccess = KeyboardManagerHelper::ErrorType::RemapUnsuccessful;
            }
            else
            {
                isSuccess = KeyboardManagerHelper::ErrorType::RemapUnsuccessful;
            }
        }
        return isSuccess;
    }

    // Function to return the set of keys that have been orphaned from the remap buffer
    std::vector<DWORD> GetOrphanedKeys(std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>>& remappings)
    {
        std::set<DWORD> ogKeys;
        std::set<DWORD> newKeys;

        for (int i = 0; i < remappings.size(); i++)
        {
            DWORD ogKey = std::get<DWORD>(remappings[i].first[0]);
            std::variant<DWORD, Shortcut> newKey = remappings[i].first[1];

            if (ogKey != NULL && ((newKey.index() == 0 && std::get<DWORD>(newKey) != 0) || (newKey.index() == 1 && std::get<Shortcut>(newKey).IsValidShortcut())))
            {
                ogKeys.insert(ogKey);

                // newKey should be added only if the target is a key
                if (remappings[i].first[1].index() == 0)
                {
                    newKeys.insert(std::get<DWORD>(newKey));
                }
            }
        }

        for (auto& k : newKeys)
        {
            ogKeys.erase(k);
        }

        return std::vector(ogKeys.begin(), ogKeys.end());
    }

    // Function to combine remappings if the L and R version of the modifier is mapped to the same key
    void CombineRemappings(std::unordered_map<DWORD, std::variant<DWORD, Shortcut>>& table, DWORD leftKey, DWORD rightKey, DWORD combinedKey)
    {
        if (table.find(leftKey) != table.end() && table.find(rightKey) != table.end())
        {
            // If they are mapped to the same key, delete those entries and set the common version
            if (table[leftKey] == table[rightKey])
            {
                table[combinedKey] = table[leftKey];
                table.erase(leftKey);
                table.erase(rightKey);
            }
        }
    }

    // Function to pre process the remap table before loading it into the UI
    void PreProcessRemapTable(std::unordered_map<DWORD, std::variant<DWORD, Shortcut>>& table)
    {
        // Pre process the table to combine L and R versions of Ctrl/Alt/Shift/Win that are mapped to the same key
        CombineRemappings(table, VK_LCONTROL, VK_RCONTROL, VK_CONTROL);
        CombineRemappings(table, VK_LMENU, VK_RMENU, VK_MENU);
        CombineRemappings(table, VK_LSHIFT, VK_RSHIFT, VK_SHIFT);
        CombineRemappings(table, VK_LWIN, VK_RWIN, CommonSharedConstants::VK_WIN_BOTH);
    }
}
