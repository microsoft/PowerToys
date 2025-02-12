#include "pch.h"
#include "KeyboardManagerEditorLibraryWrapper.h"

#include <common/utils/logger_helper.h>
#include <keyboardmanager/KeyboardManagerEditor/KeyboardManagerEditor.h>

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
