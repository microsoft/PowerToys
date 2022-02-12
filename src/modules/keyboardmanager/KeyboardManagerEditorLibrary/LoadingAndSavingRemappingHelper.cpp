#include "pch.h"
#include "LoadingAndSavingRemappingHelper.h"

#include <set>
#include <variant>
#include <common/interop/shared_constants.h>
#include <keyboardmanager/common/MappingConfiguration.h>

#include "KeyboardManagerState.h"
#include "keyboardmanager/KeyboardManagerEditorLibrary/trace.h"
#include "EditorHelpers.h"
#include "RemapBuffer.h"
#include "ShortcutErrorType.h"

namespace LoadingAndSavingRemappingHelper
{
    // Function to check if the set of remappings in the buffer are valid
    ShortcutErrorType CheckIfRemappingsAreValid(const RemapBuffer& remappings)
    {
        const auto n = remappings.size();
        for (int i = 0; i < n; ++i)
        {
            const auto& remap1 = remappings.at(i);
            const auto& [originalKey1, remappedKey1] = remap1.mapping;
            if (!IsValidSingleKeyOrShortcutOrText(originalKey1) || !IsValidSingleKeyOrShortcutOrText(remappedKey1))
            {
                return ShortcutErrorType::RemapUnsuccessful;
            }

            for (int j = i + 1; j < n; ++j)
            {
                const auto& remap2 = remappings.at(j);
                const auto& originalKey2 = remap2.mapping.at(0);
                if ((originalKey1 == originalKey2) &&
                    (remap1.appName == remap2.appName) &&
                    (remap1.condition == remap2.condition))
                {
                    return ShortcutErrorType::RemapUnsuccessful;
                }
            }
        }

        return ShortcutErrorType::NoError;
    }

    // Function to return the set of keys that have been orphaned from the remap buffer
    std::vector<DWORD> GetOrphanedKeys(const RemapBuffer& remappings)
    {
        std::set<DWORD> ogKeys;
        std::set<DWORD> newKeys;

        for (const auto& remapping : remappings)
        {
            const DWORD ogKey = std::get<DWORD>(remapping.mapping[0]);
            const KeyShortcutTextUnion& newKey = remapping.mapping[1];

            if (IsValidSingleKey(ogKey) && IsValidSingleKeyOrShortcutOrText(newKey))
            {
                ogKeys.insert(ogKey);

                // newKey should be added only if the target is a key
                if (newKey.index() == 0)
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
    void CombineRemappings(SingleKeyRemapTable& table, DWORD leftKey, DWORD rightKey, DWORD combinedKey)
    {
        if (table.find(leftKey) != table.end() && table.find(rightKey) != table.end())
        {
            // If they are mapped to the same key, delete those entries and set the common version
            if (table[leftKey] == table[rightKey])
            {
                if (std::holds_alternative<DWORD>(table[leftKey]) && std::get<DWORD>(table[leftKey]) == combinedKey)
                {
                    // Avoid mapping a key to itself when the combined key is equal to the resulting mapping.
                    return;
                }
                table[combinedKey] = table[leftKey];
                table.erase(leftKey);
                table.erase(rightKey);
            }
        }
    }

    // Function to pre process the remap table before loading it into the UI
    void PreProcessRemapTable(SingleKeyRemapTable& table)
    {
        // Pre process the table to combine L and R versions of Ctrl/Alt/Shift/Win that are mapped to the same key
        CombineRemappings(table, VK_LCONTROL, VK_RCONTROL, VK_CONTROL);
        CombineRemappings(table, VK_LMENU, VK_RMENU, VK_MENU);
        CombineRemappings(table, VK_LSHIFT, VK_RSHIFT, VK_SHIFT);
        CombineRemappings(table, VK_LWIN, VK_RWIN, CommonSharedConstants::VK_WIN_BOTH);
    }

    // Function to apply the single key remappings from the buffer to the KeyboardManagerState variable
    void ApplySingleKeyRemappings(MappingConfiguration& mappingConfiguration, const RemapBuffer& remappings, bool isTelemetryRequired)
    {
        // Clear existing Key Remaps
        mappingConfiguration.ClearSingleKeyRemaps();
        mappingConfiguration.ClearSingleKeyToTextRemaps();
        DWORD successfulKeyToKeyRemapCount = 0;
        DWORD successfulKeyToShortcutRemapCount = 0;
        DWORD successfulKeyToTextRemapCount = 0;
        for (const auto& remapping : remappings)
        {
            const DWORD originalKey = std::get<DWORD>(remapping.mapping[0]);
            const KeyShortcutTextUnion newKey = remapping.mapping[1];
            const RemapCondition condition = remapping.condition;

            if (IsValidSingleKey(originalKey) && IsValidSingleKeyOrShortcutOrText(newKey))
            {
                // If Ctrl/Alt/Shift are added, add their L and R versions instead to the same key
                bool result = false;
                std::vector<DWORD> originalKeysWithModifiers;
                if (originalKey == VK_CONTROL)
                {
                    originalKeysWithModifiers.push_back(VK_LCONTROL);
                    originalKeysWithModifiers.push_back(VK_RCONTROL);
                }
                else if (originalKey == VK_MENU)
                {
                    originalKeysWithModifiers.push_back(VK_LMENU);
                    originalKeysWithModifiers.push_back(VK_RMENU);
                }
                else if (originalKey == VK_SHIFT)
                {
                    originalKeysWithModifiers.push_back(VK_LSHIFT);
                    originalKeysWithModifiers.push_back(VK_RSHIFT);
                }
                else if (originalKey == CommonSharedConstants::VK_WIN_BOTH)
                {
                    originalKeysWithModifiers.push_back(VK_LWIN);
                    originalKeysWithModifiers.push_back(VK_RWIN);
                }
                else
                {
                    originalKeysWithModifiers.push_back(originalKey);
                }

                for (const DWORD key : originalKeysWithModifiers)
                {
                    const bool mappedToText = newKey.index() == 2;
                    result = mappedToText
                        ? mappingConfiguration.AddSingleKeyToTextRemap(key, std::get<std::wstring>(newKey))
                        : mappingConfiguration.AddSingleKeyRemap(key, newKey, condition) && result;
                }

                if (result)
                {
                    if (newKey.index() == 0)
                    {
                        ++successfulKeyToKeyRemapCount;
                    }
                    else if (newKey.index() == 1)
                    {
                        ++successfulKeyToShortcutRemapCount;
                    }
                    else if (newKey.index() == 2)
                    {
                        ++successfulKeyToTextRemapCount;
                    }
                }
            }
        }

        // If telemetry is to be logged, log the key remap counts
        if (isTelemetryRequired)
        {
            Trace::KeyRemapCount(successfulKeyToKeyRemapCount, successfulKeyToShortcutRemapCount, successfulKeyToTextRemapCount);
        }
    }

    // Function to apply the shortcut remappings from the buffer to the KeyboardManagerState variable
    void ApplyShortcutRemappings(MappingConfiguration& mappingConfiguration, const RemapBuffer& remappings, bool isTelemetryRequired)
    {
        // Clear existing shortcuts
        mappingConfiguration.ClearOSLevelShortcuts();
        mappingConfiguration.ClearAppSpecificShortcuts();
        DWORD successfulOSLevelShortcutToShortcutRemapCount = 0;
        DWORD successfulOSLevelShortcutToKeyRemapCount = 0;
        DWORD successfulAppSpecificShortcutToShortcutRemapCount = 0;
        DWORD successfulAppSpecificShortcutToKeyRemapCount = 0;

        // Save the shortcuts that are valid and report if any of them were invalid
        for (int i = 0; i < remappings.size(); i++)
        {
            Shortcut originalShortcut = std::get<Shortcut>(remappings[i].mapping[0]);
            const KeyShortcutTextUnion& newShortcut = remappings[i].mapping[1];
            if (IsValidShortcut(originalShortcut) && IsValidSingleKeyOrShortcutOrText(newShortcut))
            {
                if (remappings[i].appName == L"")
                {
                    bool result = mappingConfiguration.AddOSLevelShortcut(originalShortcut, newShortcut);
                    if (result)
                    {
                        if (newShortcut.index() == 0)
                        {
                            successfulOSLevelShortcutToKeyRemapCount += 1;
                        }
                        else
                        {
                            successfulOSLevelShortcutToShortcutRemapCount += 1;
                        }
                    }
                }
                else
                {
                    bool result = mappingConfiguration.AddAppSpecificShortcut(remappings[i].appName, originalShortcut, newShortcut);
                    if (result)
                    {
                        if (newShortcut.index() == 0)
                        {
                            successfulAppSpecificShortcutToKeyRemapCount += 1;
                        }
                        else
                        {
                            successfulAppSpecificShortcutToShortcutRemapCount += 1;
                        }
                    }
                }
            }
        }

        // If telemetry is to be logged, log the shortcut remap counts
        if (isTelemetryRequired)
        {
            Trace::OSLevelShortcutRemapCount(successfulOSLevelShortcutToShortcutRemapCount, successfulOSLevelShortcutToKeyRemapCount);
            Trace::AppSpecificShortcutRemapCount(successfulAppSpecificShortcutToShortcutRemapCount, successfulAppSpecificShortcutToKeyRemapCount);
        }
    }
}
