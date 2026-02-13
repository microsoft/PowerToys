#pragma once

#include <keyboardmanager/common/Helpers.h>
#include <keyboardmanager/common/Input.h>
#include <keyboardmanager/common/MappingConfiguration.h>

struct KeyNamePair
{
    int keyCode;
    wchar_t keyName[64];
};

struct SingleKeyMapping
{
    int originalKey;
    wchar_t* targetKey;
    bool isShortcut;
};

struct KeyboardTextMapping
{
    int originalKey;
    wchar_t* targetText;
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

struct MouseButtonMapping
{
    int originalButton;      // MouseButton enum value (0-6)
    wchar_t* targetKeys;     // Target key/shortcut string
    wchar_t* targetApp;      // Empty for global, app name for app-specific
    int targetType;          // 0=Key, 1=Shortcut, 2=Text, 3=RunProgram, 4=OpenUri
    wchar_t* targetText;     // For text mappings
    wchar_t* programPath;    // For RunProgram
    wchar_t* programArgs;    // For RunProgram
    wchar_t* uriToOpen;      // For OpenUri
};

struct KeyToMouseMapping
{
    int originalKey;         // Original key code (DWORD)
    int targetMouseButton;   // MouseButton enum value (0-6)
    wchar_t* targetApp;      // Empty for global, app name for app-specific
};

extern "C"
{
    __declspec(dllexport) void* CreateMappingConfiguration();
    __declspec(dllexport) void DestroyMappingConfiguration(void* config);
    __declspec(dllexport) bool LoadMappingSettings(void* config);
    __declspec(dllexport) bool SaveMappingSettings(void* config);

    __declspec(dllexport) int GetSingleKeyRemapCount(void* config);
    __declspec(dllexport) bool GetSingleKeyRemap(void* config, int index, SingleKeyMapping* mapping);

    __declspec(dllexport) int GetSingleKeyToTextRemapCount(void* config);
    __declspec(dllexport) bool GetSingleKeyToTextRemap(void* config, int index, KeyboardTextMapping* mapping);

    __declspec(dllexport) int GetShortcutRemapCountByType(void* config, int operationType);
    __declspec(dllexport) bool GetShortcutRemapByType(void* config, int operationType, int index, ShortcutMapping* mapping);
   
    __declspec(dllexport) int GetShortcutRemapCount(void* config);
    __declspec(dllexport) bool GetShortcutRemap(void* config, int index, ShortcutMapping* mapping);

    __declspec(dllexport) bool AddSingleKeyRemap(void* config, int originalKey, int targetKey);
    __declspec(dllexport) bool AddSingleKeyToTextRemap(void* config, int originalKey, const wchar_t* text);
    __declspec(dllexport) bool AddSingleKeyToShortcutRemap(void* config,
                                                           int originalKey,
                                                           const wchar_t* targetKeys);
    __declspec(dllexport) bool AddShortcutRemap(void* config,
                                                const wchar_t* originalKeys,
                                                const wchar_t* targetKeys,
                                                const wchar_t* targetApp,
                                                int operationType,
                                                const wchar_t* appPathOrUri = nullptr,
                                                const wchar_t* args = nullptr,
                                                const wchar_t* startDirectory = nullptr,
                                                int elevation = 0,
                                                int ifRunningAction = 0,
                                                int visibility = 0);

    __declspec(dllexport) void GetKeyDisplayName(int keyCode, wchar_t* keyName, int maxCount);
    __declspec(dllexport) int GetKeyCodeFromName(const wchar_t* keyName);
    __declspec(dllexport) void FreeString(wchar_t* str);
    __declspec(dllexport) int GetKeyType(int keyCode);

    __declspec(dllexport) bool IsShortcutIllegal(const wchar_t* shortcutKeys);
    __declspec(dllexport) bool AreShortcutsEqual(const wchar_t* lShort, const wchar_t* rShort);

    __declspec(dllexport) bool DeleteSingleKeyRemap(void* config, int originalKey);
    __declspec(dllexport) bool DeleteSingleKeyToTextRemap(void* config, int originalKey);
    __declspec(dllexport) bool DeleteShortcutRemap(void* config, const wchar_t* originalKeys, const wchar_t* targetApp);

    // Mouse Button Remap Functions
    __declspec(dllexport) int GetMouseButtonRemapCount(void* config);
    __declspec(dllexport) bool GetMouseButtonRemap(void* config, int index, MouseButtonMapping* mapping);
    __declspec(dllexport) bool AddMouseButtonRemap(void* config, int originalButton, const wchar_t* targetKeys, const wchar_t* targetApp, int targetType, const wchar_t* targetText, const wchar_t* programPath, const wchar_t* programArgs, const wchar_t* uriToOpen);
    __declspec(dllexport) bool DeleteMouseButtonRemap(void* config, int originalButton, const wchar_t* targetApp);

    // Key to Mouse Remap Functions
    __declspec(dllexport) int GetKeyToMouseRemapCount(void* config);
    __declspec(dllexport) bool GetKeyToMouseRemap(void* config, int index, KeyToMouseMapping* mapping);
    __declspec(dllexport) bool AddKeyToMouseRemap(void* config, int originalKey, int targetMouseButton, const wchar_t* targetApp);
    __declspec(dllexport) bool DeleteKeyToMouseRemap(void* config, int originalKey, const wchar_t* targetApp);

    // Mouse Button Utility Functions
    __declspec(dllexport) void GetMouseButtonName(int buttonCode, wchar_t* buttonName, int maxCount);
    __declspec(dllexport) int GetMouseButtonFromName(const wchar_t* buttonName);
}
extern "C" __declspec(dllexport) int GetKeyboardKeysList(bool isShortcut, KeyNamePair* keyList, int maxCount);
