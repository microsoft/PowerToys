#pragma once

#include <keyboardmanager/common/Helpers.h>

#include "ShortcutErrorType.h"

class MappingConfiguration;

namespace LoadingAndSavingRemappingHelper
{
    // Function to check if the set of remappings in the buffer are valid
    ShortcutErrorType CheckIfRemappingsAreValid(const RemapBuffer& remappings);

    // Function to return the set of keys that have been orphaned from the remap buffer
    std::vector<DWORD> GetOrphanedKeys(const RemapBuffer& remappings);

    // Function to combine remappings if the L and R version of the modifier is mapped to the same key
    void CombineRemappings(std::unordered_map<DWORD, KeyShortcutTextUnion>& table, DWORD leftKey, DWORD rightKey, DWORD combinedKey);

    // Function to pre process the remap table before loading it into the UI
    void PreProcessRemapTable(std::unordered_map<DWORD, KeyShortcutTextUnion>& table);

    // Function to apply the single key remappings from the buffer to the KeyboardManagerState variable
    void ApplySingleKeyRemappings(MappingConfiguration& mappingConfiguration, const RemapBuffer& remappings, bool isTelemetryRequired);

    // Function to apply the shortcut remappings from the buffer to the KeyboardManagerState variable
    void ApplyShortcutRemappings(MappingConfiguration& mappingConfiguration, const RemapBuffer& remappings, bool isTelemetryRequired);
}
