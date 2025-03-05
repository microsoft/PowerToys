#pragma once

#include <keyboardmanager/common/Helpers.h>
#include <keyboardmanager/common/Input.h>
#include <keyboardmanager/common/MappingConfiguration.h>

struct KeyNamePair
{
    int keyCode;
    wchar_t keyName[64];
};

struct KeyboardMapping
{
    int originalKey;
    int targetKey;
    bool isShortcut;
};

struct ShortcutMapping
{
    wchar_t* originalKeys;
    wchar_t* targetKeys;
    wchar_t* targetApp;
    int operationType;
    wchar_t* targetText;
    wchar_t* programPath;
    wchar_t* programArgs;
    wchar_t* uriToOpen;
};

extern "C"
{
    __declspec(dllexport) void* CreateMappingConfiguration();
    __declspec(dllexport) void DestroyMappingConfiguration(void* config);
    __declspec(dllexport) bool LoadMappingSettings(void* config);
    __declspec(dllexport) bool SaveMappingSettings(void* config);

    __declspec(dllexport) int GetSingleKeyRemapCount(void* config);
    __declspec(dllexport) bool GetSingleKeyRemap(void* config, int index, KeyboardMapping* mapping);

    __declspec(dllexport) int GetShortcutRemapCount(void* config);
    __declspec(dllexport) bool GetShortcutRemap(void* config, int index, ShortcutMapping* mapping);

    __declspec(dllexport) bool AddSingleKeyRemap(void* config, int originalKey, int targetKey);
    __declspec(dllexport) bool AddShortcutRemap(void* config,
                                                const wchar_t* originalKeys,
                                                const wchar_t* targetKeys,
                                                const wchar_t* targetApp);

    __declspec(dllexport) void GetKeyDisplayName(int keyCode, wchar_t* keyName, int maxCount);
    __declspec(dllexport) void FreeString(wchar_t* str);
}
extern "C" __declspec(dllexport) bool CheckIfRemappingsAreValid();
extern "C" __declspec(dllexport) int GetKeyboardKeysList(bool isShortcut, KeyNamePair* keyList, int maxCount);
