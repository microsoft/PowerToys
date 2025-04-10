#include "pch.h"
#include "KeyboardManagerEditorLibraryWrapper.h"
#include <algorithm>
#include <cstring>
#include <vector>
#include <string>
#include <memory>

#include <common/utils/logger_helper.h>
#include <keyboardmanager/KeyboardManagerEditor/KeyboardManagerEditor.h>
#include <common/interop/keyboard_layout.h>

extern "C"
{
    void* CreateMappingConfiguration()
    {
        return new MappingConfiguration();
    }

    void DestroyMappingConfiguration(void* config)
    {
        delete static_cast<MappingConfiguration*>(config);
    }

    bool LoadMappingSettings(void* config)
    {
        return static_cast<MappingConfiguration*>(config)->LoadSettings();
    }

    bool SaveMappingSettings(void* config)
    {
        return static_cast<MappingConfiguration*>(config)->SaveSettingsToFile();
    }

    wchar_t* AllocateAndCopyString(const std::wstring& str)
    {
        size_t len = str.length();
        wchar_t* buffer = new wchar_t[len + 1];
        wcscpy_s(buffer, len + 1, str.c_str());
        return buffer;
    }

    int GetSingleKeyRemapCount(void* config)
    {
        auto mapping = static_cast<MappingConfiguration*>(config);
        return static_cast<int>(mapping->singleKeyReMap.size());
    }

    bool GetSingleKeyRemap(void* config, int index, SingleKeyMapping* mapping)
    {
        auto mappingConfig = static_cast<MappingConfiguration*>(config);

        std::vector<std::pair<DWORD, KeyShortcutTextUnion>> allMappings;

        for (const auto& kv : mappingConfig->singleKeyReMap)
        {
            allMappings.push_back(kv);
        }

        if (index < 0 || index >= allMappings.size())
        {
            return false;
        }

        const auto& kv = allMappings[index];
        mapping->originalKey = static_cast<int>(kv.first);

        // Remap to single key
        if (kv.second.index() == 0)
        {
            mapping->targetKey = AllocateAndCopyString(std::to_wstring(std::get<DWORD>(kv.second)));
            mapping->isShortcut = false;
        }
        // Remap to shortcut
        else if (kv.second.index() == 1)
        {
            mapping->targetKey = AllocateAndCopyString(std::get<Shortcut>(kv.second).ToHstringVK().c_str());
            mapping->isShortcut = true;
        }
        else
        {
            mapping->targetKey = AllocateAndCopyString(L"");
            mapping->isShortcut = false;
        }

        return true;
    }

    int GetSingleKeyToTextRemapCount(void* config)
    {
        auto mapping = static_cast<MappingConfiguration*>(config);
        return static_cast<int>(mapping->singleKeyToTextReMap.size());
    }

    bool GetSingleKeyToTextRemap(void* config, int index, KeyboardTextMapping* mapping)
    {
        auto mappingConfig = static_cast<MappingConfiguration*>(config);

        if (index < 0 || index >= mappingConfig->singleKeyToTextReMap.size())
        {
            return false;
        }

        auto it = mappingConfig->singleKeyToTextReMap.begin();
        std::advance(it, index);

        mapping->originalKey = static_cast<int>(it->first);
        std::wstring text = std::get<std::wstring>(it->second);
        mapping->targetText = AllocateAndCopyString(text);

        return true;
    }

    int GetShortcutRemapCountByType(void* config, int operationType)
    {
        auto mapping = static_cast<MappingConfiguration*>(config);
        int count = 0;

        for (const auto& kv : mapping->osLevelShortcutReMap)
        {
            bool shouldCount = false;


            if (operationType == 0)
            {
                if ((kv.second.targetShortcut.index() == 0) ||
                    (kv.second.targetShortcut.index() == 1 &&
                     std::get<Shortcut>(kv.second.targetShortcut).operationType == Shortcut::OperationType::RemapShortcut))
                {
                    shouldCount = true;
                }
            }
            else if (operationType == 1)
            {

                if (kv.second.targetShortcut.index() == 1 &&
                    std::get<Shortcut>(kv.second.targetShortcut).operationType == Shortcut::OperationType::RunProgram)
                {
                    shouldCount = true;
                }
            }
            else if (operationType == 2)
            {
                if (kv.second.targetShortcut.index() == 1 &&
                    std::get<Shortcut>(kv.second.targetShortcut).operationType == Shortcut::OperationType::OpenURI)
                {
                    shouldCount = true;
                }
            }
            else if (operationType == 3)
            {
                if (kv.second.targetShortcut.index() == 2)
                {
                    shouldCount = true;
                }
            }

            if (shouldCount)
            {
                count++;
            }
        }

        for (const auto& appMap : mapping->appSpecificShortcutReMap)
        {
            for (const auto& shortcutKv : appMap.second)
            {
                bool shouldCount = false;

                if (operationType == 0)
                {
                    if ((shortcutKv.second.targetShortcut.index() == 0) ||
                        (shortcutKv.second.targetShortcut.index() == 1 &&
                         std::get<Shortcut>(shortcutKv.second.targetShortcut).operationType == Shortcut::OperationType::RemapShortcut))
                    {
                        shouldCount = true;
                    }
                }
                else if (operationType == 1)
                {
                    if (shortcutKv.second.targetShortcut.index() == 1 &&
                        std::get<Shortcut>(shortcutKv.second.targetShortcut).operationType == Shortcut::OperationType::RunProgram)
                    {
                        shouldCount = true;
                    }
                }
                else if (operationType == 2)
                {
                    if (shortcutKv.second.targetShortcut.index() == 1 &&
                        std::get<Shortcut>(shortcutKv.second.targetShortcut).operationType == Shortcut::OperationType::OpenURI)
                    {
                        shouldCount = true;
                    }
                }
                else if (operationType == 3)
                {
                    if (shortcutKv.second.targetShortcut.index() == 2)
                    {
                        shouldCount = true;
                    }
                }

                if (shouldCount)
                {
                    count++;
                }
            }
        }

        return count;
    }

bool GetShortcutRemapByType(void* config, int operationType, int index, ShortcutMapping* mapping)
    {
        auto mappingConfig = static_cast<MappingConfiguration*>(config);

        std::vector<std::tuple<Shortcut, KeyShortcutTextUnion, std::wstring>> filteredMappings;

        for (const auto& kv : mappingConfig->osLevelShortcutReMap)
        {
            bool shouldAdd = false;

            if (operationType == 0) // RemapShortcut
            {
                if ((kv.second.targetShortcut.index() == 0) ||
                    (kv.second.targetShortcut.index() == 1 &&
                     std::get<Shortcut>(kv.second.targetShortcut).operationType == Shortcut::OperationType::RemapShortcut))
                {
                    shouldAdd = true;
                }
            }
            else if (operationType == 1) // RunProgram
            {
                if (kv.second.targetShortcut.index() == 1 &&
                    std::get<Shortcut>(kv.second.targetShortcut).operationType == Shortcut::OperationType::RunProgram)
                {
                    shouldAdd = true;
                }
            }
            else if (operationType == 2) // OpenURI
            {
                if (kv.second.targetShortcut.index() == 1 &&
                    std::get<Shortcut>(kv.second.targetShortcut).operationType == Shortcut::OperationType::OpenURI)
                {
                    shouldAdd = true;
                }
            }
            else if (operationType == 3)
            {
                if (kv.second.targetShortcut.index() == 2)
                {
                    shouldAdd = true;
                }
            }

            if (shouldAdd)
            {
                filteredMappings.push_back(std::make_tuple(kv.first, kv.second.targetShortcut, L""));
            }
        }

        for (const auto& appKv : mappingConfig->appSpecificShortcutReMap)
        {
            for (const auto& shortcutKv : appKv.second)
            {
                bool shouldAdd = false;

                if (operationType == 0) // RemapShortcut
                {
                    if ((shortcutKv.second.targetShortcut.index() == 0) ||
                        (shortcutKv.second.targetShortcut.index() == 1 &&
                         std::get<Shortcut>(shortcutKv.second.targetShortcut).operationType == Shortcut::OperationType::RemapShortcut))
                    {
                        shouldAdd = true;
                    }
                }
                else if (operationType == 1) // RunProgram
                {
                    if (shortcutKv.second.targetShortcut.index() == 1 &&
                        std::get<Shortcut>(shortcutKv.second.targetShortcut).operationType == Shortcut::OperationType::RunProgram)
                    {
                        shouldAdd = true;
                    }
                }
                else if (operationType == 2) // OpenURI
                {
                    if (shortcutKv.second.targetShortcut.index() == 1 &&
                        std::get<Shortcut>(shortcutKv.second.targetShortcut).operationType == Shortcut::OperationType::OpenURI)
                    {
                        shouldAdd = true;
                    }
                }
                else if (operationType == 3)
                {
                    if (shortcutKv.second.targetShortcut.index() == 2)
                    {
                        shouldAdd = true;
                    }
                }

                if (shouldAdd)
                {
                    filteredMappings.push_back(std::make_tuple(
                        shortcutKv.first, shortcutKv.second.targetShortcut, appKv.first));
                }
            }
        }

        if (index < 0 || index >= filteredMappings.size())
        {
            return false;
        }

        const auto& [origShortcut, targetShortcutUnion, app] = filteredMappings[index];

        std::wstring origKeysStr = origShortcut.ToHstringVK().c_str();
        mapping->originalKeys = AllocateAndCopyString(origKeysStr);
        mapping->targetApp = AllocateAndCopyString(app);

        if (targetShortcutUnion.index() == 0)
        {
            DWORD targetKey = std::get<DWORD>(targetShortcutUnion);
            mapping->targetKeys = AllocateAndCopyString(std::to_wstring(targetKey));
            mapping->operationType = 0;
            mapping->targetText = AllocateAndCopyString(L"");
            mapping->programPath = AllocateAndCopyString(L"");
            mapping->programArgs = AllocateAndCopyString(L"");
            mapping->uriToOpen = AllocateAndCopyString(L"");
        }
        else if (targetShortcutUnion.index() == 1)
        {
            Shortcut targetShortcut = std::get<Shortcut>(targetShortcutUnion);
            std::wstring targetKeysStr = targetShortcut.ToHstringVK().c_str();

            mapping->operationType = static_cast<int>(targetShortcut.operationType);

            if (targetShortcut.operationType == Shortcut::OperationType::RunProgram)
            {
                mapping->targetKeys = AllocateAndCopyString(targetKeysStr);
                mapping->targetText = AllocateAndCopyString(L"");
                mapping->programPath = AllocateAndCopyString(targetShortcut.runProgramFilePath);
                mapping->programArgs = AllocateAndCopyString(targetShortcut.runProgramArgs);
                mapping->uriToOpen = AllocateAndCopyString(L"");
            }
            else if (targetShortcut.operationType == Shortcut::OperationType::OpenURI)
            {
                mapping->targetKeys = AllocateAndCopyString(targetKeysStr);
                mapping->targetText = AllocateAndCopyString(L"");
                mapping->programPath = AllocateAndCopyString(L"");
                mapping->programArgs = AllocateAndCopyString(L"");
                mapping->uriToOpen = AllocateAndCopyString(targetShortcut.uriToOpen);
            }
            else
            {
                mapping->targetKeys = AllocateAndCopyString(targetKeysStr);
                mapping->targetText = AllocateAndCopyString(L"");
                mapping->programPath = AllocateAndCopyString(L"");
                mapping->programArgs = AllocateAndCopyString(L"");
                mapping->uriToOpen = AllocateAndCopyString(L"");
            }
        }
        else if (targetShortcutUnion.index() == 2)
        {
            std::wstring text = std::get<std::wstring>(targetShortcutUnion);
            mapping->targetKeys = AllocateAndCopyString(L"");
            mapping->operationType = 0;
            mapping->targetText = AllocateAndCopyString(text);
            mapping->programPath = AllocateAndCopyString(L"");
            mapping->programArgs = AllocateAndCopyString(L"");
            mapping->uriToOpen = AllocateAndCopyString(L"");
        }

        return true;
    }

    int GetShortcutRemapCount(void* config)
    {
        auto mapping = static_cast<MappingConfiguration*>(config);
        int count = static_cast<int>(mapping->osLevelShortcutReMap.size());

        for (const auto& appMap : mapping->appSpecificShortcutReMap)
        {
            count += static_cast<int>(appMap.second.size());
        }

        return count;
    }

    bool GetShortcutRemap(void* config, int index, ShortcutMapping* mapping)
    {
        auto mappingConfig = static_cast<MappingConfiguration*>(config);

        std::vector<std::tuple<Shortcut, KeyShortcutTextUnion, std::wstring>> allMappings;

        for (const auto& kv : mappingConfig->osLevelShortcutReMap)
        {
            allMappings.push_back(std::make_tuple(kv.first, kv.second.targetShortcut, L""));
        }

        for (const auto& appKv : mappingConfig->appSpecificShortcutReMap)
        {
            for (const auto& shortcutKv : appKv.second)
            {
                allMappings.push_back(std::make_tuple(
                    shortcutKv.first, shortcutKv.second.targetShortcut, appKv.first));
            }
        }

        if (index < 0 || index >= allMappings.size())
        {
            return false;
        }

        const auto& [origShortcut, targetShortcutUnion, app] = allMappings[index];

        std::wstring origKeysStr = origShortcut.ToHstringVK().c_str();
        mapping->originalKeys = AllocateAndCopyString(origKeysStr);

        mapping->targetApp = AllocateAndCopyString(app);

        if (targetShortcutUnion.index() == 0)
        {
            DWORD targetKey = std::get<DWORD>(targetShortcutUnion);
            mapping->targetKeys = AllocateAndCopyString(std::to_wstring(targetKey));
            mapping->operationType = 0;
            mapping->targetText = AllocateAndCopyString(L"");
            mapping->programPath = AllocateAndCopyString(L"");
            mapping->programArgs = AllocateAndCopyString(L"");
            mapping->uriToOpen = AllocateAndCopyString(L"");
        }
        else if (targetShortcutUnion.index() == 1)
        {
            Shortcut targetShortcut = std::get<Shortcut>(targetShortcutUnion);
            std::wstring targetKeysStr = targetShortcut.ToHstringVK().c_str();

            mapping->operationType = static_cast<int>(targetShortcut.operationType);

            if (targetShortcut.operationType == Shortcut::OperationType::RunProgram)
            {
                mapping->targetKeys = AllocateAndCopyString(targetKeysStr);
                mapping->targetText = AllocateAndCopyString(L"");
                mapping->programPath = AllocateAndCopyString(targetShortcut.runProgramFilePath);
                mapping->programArgs = AllocateAndCopyString(targetShortcut.runProgramArgs);
                mapping->uriToOpen = AllocateAndCopyString(L"");
            }
            else if (targetShortcut.operationType == Shortcut::OperationType::OpenURI)
            {
                mapping->targetKeys = AllocateAndCopyString(targetKeysStr);
                mapping->targetText = AllocateAndCopyString(L"");
                mapping->programPath = AllocateAndCopyString(L"");
                mapping->programArgs = AllocateAndCopyString(L"");
                mapping->uriToOpen = AllocateAndCopyString(targetShortcut.uriToOpen);
            }
            else
            {
                mapping->targetKeys = AllocateAndCopyString(targetKeysStr);
                mapping->targetText = AllocateAndCopyString(L"");
                mapping->programPath = AllocateAndCopyString(L"");
                mapping->programArgs = AllocateAndCopyString(L"");
                mapping->uriToOpen = AllocateAndCopyString(L"");
            }
        }
        else if (targetShortcutUnion.index() == 2)
        {
            std::wstring text = std::get<std::wstring>(targetShortcutUnion);
            mapping->targetKeys = AllocateAndCopyString(L"");
            mapping->operationType = 0;
            mapping->targetText = AllocateAndCopyString(text);
            mapping->programPath = AllocateAndCopyString(L"");
            mapping->programArgs = AllocateAndCopyString(L"");
            mapping->uriToOpen = AllocateAndCopyString(L"");
        }

        return true;
    }

    void FreeString(wchar_t* str)
    {
        delete[] str;
    }

    bool AddSingleKeyRemap(void* config, int originalKey, int targetKey)
    {
        auto mappingConfig = static_cast<MappingConfiguration*>(config);
        return mappingConfig->AddSingleKeyRemap(static_cast<DWORD>(originalKey), static_cast<DWORD>(targetKey));
    }

    bool AddSingleKeyToTextRemap(void* config, int originalKey, const wchar_t* text)
    {
        auto mappingConfig = static_cast<MappingConfiguration*>(config);

        if (text == nullptr)
        {
            return false;
        }

        return mappingConfig->AddSingleKeyToTextRemap(static_cast<DWORD>(originalKey), text);
    }

    bool AddSingleKeyToShortcutRemap(void* config, int originalKey, const wchar_t* targetKeys)
    {
        auto mappingConfig = static_cast<MappingConfiguration*>(config);

        if (!targetKeys)
        {
            return false;
        }

        Shortcut targetShortcut(targetKeys);

        return mappingConfig->AddSingleKeyRemap(static_cast<DWORD>(originalKey), targetShortcut);
    }

    bool AddShortcutRemap(void* config,
                          const wchar_t* originalKeys,
                          const wchar_t* targetKeys,
                          const wchar_t* targetApp)
    {
        auto mappingConfig = static_cast<MappingConfiguration*>(config);

        Shortcut origShortcut(originalKeys);
        Shortcut targetShortcut(targetKeys);

        std::wstring app(targetApp ? targetApp : L"");

        if (app.empty())
        {
            return mappingConfig->AddOSLevelShortcut(origShortcut, targetShortcut);
        }
        else
        {
            return mappingConfig->AddAppSpecificShortcut(app, origShortcut, targetShortcut);
        }
    }

    void GetKeyDisplayName(int keyCode, wchar_t* keyName, int maxCount)
    {
        if (keyName == nullptr || maxCount <= 0)
        {
            return;
        }
        LayoutMap layoutMap;
        std::wstring name = layoutMap.GetKeyName(static_cast<DWORD>(keyCode));
        wcsncpy_s(keyName, maxCount, name.c_str(), _TRUNCATE);
    }

    int GetKeyCodeFromName(const wchar_t* keyName)
    {
        if (keyName == nullptr)
        {
            return 0;
        }

        LayoutMap layoutMap;
        std::wstring name(keyName);
        return static_cast<int>(layoutMap.GetKeyFromName(name));
    }

    // Function to delete a single key remapping
    bool DeleteSingleKeyRemap(void* config, int originalKey)
    {
        auto mappingConfig = static_cast<MappingConfiguration*>(config);

        // Find and delete the single key remapping
        auto it = mappingConfig->singleKeyReMap.find(static_cast<DWORD>(originalKey));
        if (it != mappingConfig->singleKeyReMap.end())
        {
            mappingConfig->singleKeyReMap.erase(it);
            return true;
        }

        return false;
    }

    // Function to delete a shortcut remapping
    bool DeleteShortcutRemap(void* config, const wchar_t* originalKeys, const wchar_t* targetApp)
    {
        auto mappingConfig = static_cast<MappingConfiguration*>(config);

        if (originalKeys == nullptr)
        {
            return false;
        }

        std::wstring appName = targetApp ? targetApp : L"";
        Shortcut shortcut(originalKeys);

        // Determine the type of remapping to delete based on the app name
        if (appName.empty())
        {
            // Delete OS level shortcut mapping
            auto it = mappingConfig->osLevelShortcutReMap.find(shortcut);
            if (it != mappingConfig->osLevelShortcutReMap.end())
            {
                mappingConfig->osLevelShortcutReMap.erase(it);
                return true;
            }
        }
        else
        {
            // Delete app-specific shortcut mapping
            auto appIt = mappingConfig->appSpecificShortcutReMap.find(appName);
            if (appIt != mappingConfig->appSpecificShortcutReMap.end())
            {
                auto shortcutIt = appIt->second.find(shortcut);
                if (shortcutIt != appIt->second.end())
                {
                    appIt->second.erase(shortcutIt);

                    // If the app-specific mapping is empty, remove the app entry
                    if (appIt->second.empty())
                    {
                        mappingConfig->appSpecificShortcutReMap.erase(appIt);
                    }

                    return true;
                }
            }
        }

        return false;
    }
}

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