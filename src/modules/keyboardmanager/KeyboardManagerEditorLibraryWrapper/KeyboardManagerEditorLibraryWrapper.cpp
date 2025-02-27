#include "pch.h"
#include "KeyboardManagerEditorLibraryWrapper.h"
#include <algorithm>
#include <cstring>

#include <common/utils/logger_helper.h>
#include <keyboardmanager/KeyboardManagerEditor/KeyboardManagerEditor.h>
#include <common/interop/keyboard_layout.h>

// Test function to call the remapping helper function

bool CheckIfRemappingsAreValid()
{
    RemapBuffer remapBuffer;

    // Mock valid key to key remappings
    remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x41, (DWORD)0x42 }), std::wstring() });
    remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x42, (DWORD)0x43 }), std::wstring() });

    auto result = LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer);

    return result == ShortcutErrorType::NoError;
}

// Get the list of keyboard keys in Editor
int GetKeyboardKeysList(bool isShortcut, KeyNamePair* keyList, int maxCount)
{
    if (keyList == nullptr || maxCount <= 0)
    {
        return 0;
    }

    LayoutMap layoutMap;
    auto keyNameList = layoutMap.GetKeyNameList(isShortcut);

    int count = (std::min)(static_cast<int>(keyNameList.size()), maxCount);

    // Transfer the key list to the output struct format
    for (int i = 0; i < count; ++i)
    {
        keyList[i].keyCode = static_cast<int>(keyNameList[i].first);
        wcsncpy_s(keyList[i].keyName, keyNameList[i].second.c_str(), _countof(keyList[i].keyName) - 1);
    }

    return count;
}