#include "pch.h"
#include "MappingConfiguration.h"

#include <common/SettingsAPI/settings_objects.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/logger/logger.h>

#include "KeyboardManagerConstants.h"
#include "Shortcut.h"
#include "RemapShortcut.h"
#include "Helpers.h"

// Function to clear the OS Level shortcut remapping table
void MappingConfiguration::ClearOSLevelShortcuts()
{
    osLevelShortcutReMap.clear();
    osLevelShortcutReMapSortedKeys.clear();
}

// Function to clear the Keys remapping table.
void MappingConfiguration::ClearSingleKeyRemaps()
{
    singleKeyReMap.clear();
    scanMap.clear();
}

// Function to clear the Keys remapping table.
void MappingConfiguration::ClearSingleKeyToTextRemaps()
{
    singleKeyToTextReMap.clear();
}

// Function to clear the App specific shortcut remapping table
void MappingConfiguration::ClearAppSpecificShortcuts()
{
    appSpecificShortcutReMap.clear();
    appSpecificShortcutReMapSortedKeys.clear();
}

// Function to clear the mouse button remapping table
void MappingConfiguration::ClearMouseButtonRemaps()
{
    mouseButtonReMap.clear();
}

// Function to clear the key to mouse remapping table
void MappingConfiguration::ClearKeyToMouseRemaps()
{
    keyToMouseReMap.clear();
}

// Function to clear the app-specific mouse button remapping table
void MappingConfiguration::ClearAppSpecificMouseButtonRemaps()
{
    appSpecificMouseButtonReMap.clear();
}

// Function to clear the app-specific key to mouse remapping table
void MappingConfiguration::ClearAppSpecificKeyToMouseRemaps()
{
    appSpecificKeyToMouseReMap.clear();
}

// Function to add a new OS level shortcut remapping
bool MappingConfiguration::AddOSLevelShortcut(const Shortcut& originalSC, const KeyShortcutTextUnion& newSC)
{
    // Check if the shortcut is already remapped
    auto it = osLevelShortcutReMap.find(originalSC);
    if (it != osLevelShortcutReMap.end())
    {
        return false;
    }

    osLevelShortcutReMap[originalSC] = RemapShortcut(newSC);
    osLevelShortcutReMapSortedKeys.push_back(originalSC);
    Helpers::SortShortcutVectorBasedOnSize(osLevelShortcutReMapSortedKeys);

    return true;
}

// Function to add a new single key to key/shortcut remapping
bool MappingConfiguration::AddSingleKeyRemap(const DWORD& originalKey, const KeyShortcutTextUnion& newRemapKey)
{
    // Check if the key is already remapped
    auto it = singleKeyReMap.find(originalKey);
    if (it != singleKeyReMap.end())
    {
        return false;
    }

    singleKeyReMap[originalKey] = newRemapKey;
    if (Helpers::IsNumpadKeyThatIsAffectedByShift(originalKey))
    {
        // Numpad keys might get altered by shift being pressed. We need to save their scancode instead to try and detect that they were unpressed when they are mapped to shift.
        auto scanCode = MapVirtualKey(originalKey, MAPVK_VK_TO_VSC);
        if (scanCode != 0)
        {
            scanMap[MapVirtualKey(originalKey, MAPVK_VK_TO_VSC)] = originalKey;
        }
    }
    return true;
}

bool MappingConfiguration::AddSingleKeyToTextRemap(const DWORD originalKey, const std::wstring& text)
{
    if (auto it = singleKeyToTextReMap.find(originalKey); it != end(singleKeyToTextReMap))
    {
        return false;
    }
    else
    {
        singleKeyToTextReMap[originalKey] = text;
        return true;
    }
}

// Function to add a new mouse button remapping
bool MappingConfiguration::AddMouseButtonRemap(const MouseButton& originalButton, const KeyShortcutTextUnion& newRemapTarget)
{
    // Check if the button is already remapped
    auto it = mouseButtonReMap.find(originalButton);
    if (it != mouseButtonReMap.end())
    {
        return false;
    }

    mouseButtonReMap[originalButton] = newRemapTarget;
    return true;
}

// Function to add a new key to mouse remapping
bool MappingConfiguration::AddKeyToMouseRemap(const DWORD& originalKey, const MouseButton& targetButton)
{
    // Check if the key is already remapped
    auto it = keyToMouseReMap.find(originalKey);
    if (it != keyToMouseReMap.end())
    {
        return false;
    }

    keyToMouseReMap[originalKey] = targetButton;
    return true;
}

// Function to add a new app-specific mouse button remapping
bool MappingConfiguration::AddAppSpecificMouseButtonRemap(const std::wstring& app, const MouseButton& originalButton, const KeyShortcutTextUnion& newRemapTarget)
{
    // Convert app name to lower case
    std::wstring process_name;
    process_name.resize(app.length());
    std::transform(app.begin(), app.end(), process_name.begin(), towlower);

    // Check if there are any app specific mouse remaps for this app
    auto appIt = appSpecificMouseButtonReMap.find(process_name);
    if (appIt != appSpecificMouseButtonReMap.end())
    {
        // Check if the mouse button is already remapped
        auto buttonIt = appSpecificMouseButtonReMap[process_name].find(originalButton);
        if (buttonIt != appSpecificMouseButtonReMap[process_name].end())
        {
            return false;
        }
    }

    appSpecificMouseButtonReMap[process_name][originalButton] = newRemapTarget;
    return true;
}

// Function to add a new app-specific key to mouse remapping
bool MappingConfiguration::AddAppSpecificKeyToMouseRemap(const std::wstring& app, const DWORD& originalKey, const MouseButton& targetButton)
{
    // Convert app name to lower case
    std::wstring process_name;
    process_name.resize(app.length());
    std::transform(app.begin(), app.end(), process_name.begin(), towlower);

    // Check if there are any app specific key to mouse remaps for this app
    auto appIt = appSpecificKeyToMouseReMap.find(process_name);
    if (appIt != appSpecificKeyToMouseReMap.end())
    {
        // Check if the key is already remapped
        auto keyIt = appSpecificKeyToMouseReMap[process_name].find(originalKey);
        if (keyIt != appSpecificKeyToMouseReMap[process_name].end())
        {
            return false;
        }
    }

    appSpecificKeyToMouseReMap[process_name][originalKey] = targetButton;
    return true;
}

// Function to add a new App specific shortcut remapping
bool MappingConfiguration::AddAppSpecificShortcut(const std::wstring& app, const Shortcut& originalSC, const KeyShortcutTextUnion& newSC)
{
    // Convert app name to lower case
    std::wstring process_name;
    process_name.resize(app.length());
    std::transform(app.begin(), app.end(), process_name.begin(), towlower);

    // Check if there are any app specific shortcuts for this app
    auto appIt = appSpecificShortcutReMap.find(process_name);
    if (appIt != appSpecificShortcutReMap.end())
    {
        // Check if the shortcut is already remapped
        auto shortcutIt = appSpecificShortcutReMap[process_name].find(originalSC);
        if (shortcutIt != appSpecificShortcutReMap[process_name].end())
        {
            return false;
        }
    }
    else
    {
        appSpecificShortcutReMapSortedKeys[process_name] = std::vector<Shortcut>();
    }

    appSpecificShortcutReMap[process_name][originalSC] = RemapShortcut(newSC);
    appSpecificShortcutReMapSortedKeys[process_name].push_back(originalSC);
    Helpers::SortShortcutVectorBasedOnSize(appSpecificShortcutReMapSortedKeys[process_name]);
    return true;
}

bool MappingConfiguration::LoadSingleKeyRemaps(const json::JsonObject& jsonData)
{
    bool result = true;

    try
    {
        auto remapKeysData = jsonData.GetNamedObject(KeyboardManagerConstants::RemapKeysSettingName);
        ClearSingleKeyRemaps();

        if (remapKeysData)
        {
            auto inProcessRemapKeys = remapKeysData.GetNamedArray(KeyboardManagerConstants::InProcessRemapKeysSettingName);
            for (const auto& it : inProcessRemapKeys)
            {
                try
                {
                    auto originalKey = it.GetObjectW().GetNamedString(KeyboardManagerConstants::OriginalKeysSettingName);
                    auto newRemapKey = it.GetObjectW().GetNamedString(KeyboardManagerConstants::NewRemapKeysSettingName);

                    // If remapped to a shortcut
                    if (std::wstring(newRemapKey).find(L";") != std::string::npos)
                    {
                        AddSingleKeyRemap(std::stoul(originalKey.c_str()), Shortcut(newRemapKey.c_str()));
                    }

                    // If remapped to a key
                    else
                    {
                        AddSingleKeyRemap(std::stoul(originalKey.c_str()), std::stoul(newRemapKey.c_str()));
                    }
                }
                catch (...)
                {
                    Logger::error(L"Improper Key Data JSON. Try the next remap.");
                    result = false;
                }
            }
        }
    }
    catch (...)
    {
        Logger::error(L"Improper JSON format for single key remaps. Skip to next remap type");
        result = false;
    }

    return result;
}

bool MappingConfiguration::LoadSingleKeyToTextRemaps(const json::JsonObject& jsonData)
{
    bool result = true;

    try
    {
        auto remapKeysData = jsonData.GetNamedObject(KeyboardManagerConstants::RemapKeysToTextSettingName);
        ClearSingleKeyToTextRemaps();

        if (!remapKeysData)
        {
            return result;
        }

        auto inProcessRemapKeys = remapKeysData.GetNamedArray(KeyboardManagerConstants::InProcessRemapKeysSettingName);
        for (const auto& it : inProcessRemapKeys)
        {
            try
            {
                auto originalKey = it.GetObjectW().GetNamedString(KeyboardManagerConstants::OriginalKeysSettingName);
                auto newText = it.GetObjectW().GetNamedString(KeyboardManagerConstants::NewTextSettingName);

                // undo dummy data for backwards compatibility
                if (newText == L"*Unsupported*")
                {
                    newText == L"";
                }

                AddSingleKeyToTextRemap(std::stoul(originalKey.c_str()), newText.c_str());
            }
            catch (...)
            {
                Logger::error(L"Improper Key Data JSON. Try the next remap.");
                result = false;
            }
        }
    }
    catch (...)
    {
        Logger::error(L"Improper JSON format for single key to text remaps. Skip to next remap type");
        result = false;
    }

    return result;
}

bool MappingConfiguration::LoadAppSpecificShortcutRemaps(const json::JsonObject& remapShortcutsData)
{
    bool result = true;

    try
    {
        auto appSpecificRemapShortcuts = remapShortcutsData.GetNamedArray(KeyboardManagerConstants::AppSpecificRemapShortcutsSettingName);
        for (const auto& it : appSpecificRemapShortcuts)
        {
            try
            {
                auto originalKeys = it.GetObjectW().GetNamedString(KeyboardManagerConstants::OriginalKeysSettingName);
                auto newRemapKeys = it.GetObjectW().GetNamedString(KeyboardManagerConstants::NewRemapKeysSettingName, {});
                auto newRemapText = it.GetObjectW().GetNamedString(KeyboardManagerConstants::NewTextSettingName, {});
                auto targetApp = it.GetObjectW().GetNamedString(KeyboardManagerConstants::TargetAppSettingName);
                auto operationType = it.GetObjectW().GetNamedNumber(KeyboardManagerConstants::ShortcutOperationType, 0);
                auto exactMatch = it.GetObjectW().GetNamedBoolean(KeyboardManagerConstants::ShortcutExactMatch, false);
                auto originalShortcut = Shortcut(originalKeys.c_str());
                originalShortcut.exactMatch = exactMatch;
                // undo dummy data for backwards compatibility
                if (newRemapText == L"*Unsupported*")
                {
                    newRemapText == L"";
                }

                // check Shortcut::OperationType
                if (operationType == 1)
                {
                    auto runProgramFilePath = it.GetObjectW().GetNamedString(KeyboardManagerConstants::RunProgramFilePathSettingName, L"");
                    auto runProgramArgs = it.GetObjectW().GetNamedString(KeyboardManagerConstants::RunProgramArgsSettingName, L"");
                    auto runProgramStartInDir = it.GetObjectW().GetNamedString(KeyboardManagerConstants::RunProgramStartInDirSettingName, L"");
                    auto runProgramElevationLevel = it.GetObjectW().GetNamedNumber(KeyboardManagerConstants::RunProgramElevationLevelSettingName, 0);
                    auto runProgramAlreadyRunningAction = it.GetObjectW().GetNamedNumber(KeyboardManagerConstants::RunProgramAlreadyRunningAction, 0);
                    auto runProgramStartWindowType = it.GetObjectW().GetNamedNumber(KeyboardManagerConstants::RunProgramStartWindowType, 0);

                    auto tempShortcut = Shortcut(newRemapKeys.c_str());
                    tempShortcut.operationType = Shortcut::OperationType::RunProgram;
                    tempShortcut.runProgramFilePath = runProgramFilePath;
                    tempShortcut.runProgramArgs = runProgramArgs;
                    tempShortcut.runProgramStartInDir = runProgramStartInDir;
                    tempShortcut.elevationLevel = static_cast<Shortcut::ElevationLevel>(runProgramElevationLevel);
                    tempShortcut.alreadyRunningAction = static_cast<Shortcut::ProgramAlreadyRunningAction>(runProgramAlreadyRunningAction);
                    tempShortcut.startWindowType = static_cast<Shortcut::StartWindowType>(runProgramStartWindowType);

                    AddAppSpecificShortcut(targetApp.c_str(), originalShortcut, tempShortcut);
                }
                else if (operationType == 2)
                {
                    auto tempShortcut = Shortcut(newRemapKeys.c_str());
                    tempShortcut.operationType = Shortcut::OperationType::OpenURI;
                    tempShortcut.uriToOpen = it.GetObjectW().GetNamedString(KeyboardManagerConstants::ShortcutOpenURI, L"");

                    AddAppSpecificShortcut(targetApp.c_str(), originalShortcut, tempShortcut);
                }

                if (!newRemapKeys.empty())
                {
                    // If remapped to a shortcut
                    if (std::wstring(newRemapKeys).find(L";") != std::string::npos)
                    {
                        AddAppSpecificShortcut(targetApp.c_str(), originalShortcut, Shortcut(newRemapKeys.c_str()));
                    }

                    // If remapped to a key
                    else
                    {
                        AddAppSpecificShortcut(targetApp.c_str(), originalShortcut, std::stoul(newRemapKeys.c_str()));
                    }
                }
                else
                {
                    AddAppSpecificShortcut(targetApp.c_str(), originalShortcut, newRemapText.c_str());
                }
            }
            catch (...)
            {
                Logger::error(L"Improper Key Data JSON. Try the next shortcut.");
                result = false;
            }
        }
    }
    catch (...)
    {
        Logger::error(L"Improper JSON format for os level shortcut remaps. Skip to next remap type");
        result = false;
    }

    return result;
}

bool MappingConfiguration::LoadMouseButtonRemaps(const json::JsonObject& jsonData)
{
    bool result = true;

    try
    {
        auto remapMouseData = jsonData.GetNamedObject(KeyboardManagerConstants::RemapMouseButtonsSettingName);
        ClearMouseButtonRemaps();
        ClearAppSpecificMouseButtonRemaps();

        if (!remapMouseData)
        {
            return result;
        }

        // Try to load global remaps first (new format), fall back to inProcess (old format) for backward compatibility
        json::JsonArray globalRemaps;
        try
        {
            globalRemaps = remapMouseData.GetNamedArray(KeyboardManagerConstants::GlobalMouseRemapsSettingName);
        }
        catch (...)
        {
            // Fall back to old format
            try
            {
                globalRemaps = remapMouseData.GetNamedArray(KeyboardManagerConstants::InProcessRemapKeysSettingName);
            }
            catch (...)
            {
                // No global remaps found
            }
        }

        for (const auto& it : globalRemaps)
        {
            try
            {
                auto originalButton = it.GetObjectW().GetNamedString(KeyboardManagerConstants::OriginalMouseButtonSettingName);
                auto newRemapKeys = it.GetObjectW().GetNamedString(KeyboardManagerConstants::NewRemapKeysSettingName, {});
                auto unicodeText = it.GetObjectW().GetNamedString(KeyboardManagerConstants::NewTextSettingName, {});
                auto runProgramFilePath = it.GetObjectW().GetNamedString(KeyboardManagerConstants::RunProgramFilePathSettingName, {});
                auto openUri = it.GetObjectW().GetNamedString(KeyboardManagerConstants::ShortcutOpenURI, {});

                auto mouseButton = MouseButtonHelpers::MouseButtonFromString(originalButton.c_str());
                if (!mouseButton.has_value())
                {
                    Logger::error(L"Invalid mouse button name: {}", originalButton.c_str());
                    continue;
                }

                // Priority: Text > Run Program > Open URI > Key/Shortcut
                if (!unicodeText.empty())
                {
                    // Remapped to text
                    AddMouseButtonRemap(*mouseButton, std::wstring(unicodeText));
                }
                else if (!runProgramFilePath.empty())
                {
                    // Remapped to run program
                    Shortcut runProgramShortcut;
                    runProgramShortcut.runProgramFilePath = runProgramFilePath.c_str();
                    runProgramShortcut.runProgramArgs = it.GetObjectW().GetNamedString(KeyboardManagerConstants::RunProgramArgsSettingName, {}).c_str();
                    runProgramShortcut.runProgramStartInDir = it.GetObjectW().GetNamedString(KeyboardManagerConstants::RunProgramStartInDirSettingName, {}).c_str();

                    auto runProgramElevationLevel = it.GetObjectW().GetNamedNumber(KeyboardManagerConstants::RunProgramElevationLevelSettingName, 0);
                    auto runProgramAlreadyRunningAction = it.GetObjectW().GetNamedNumber(KeyboardManagerConstants::RunProgramAlreadyRunningAction, 0);
                    auto runProgramStartWindowType = it.GetObjectW().GetNamedNumber(KeyboardManagerConstants::RunProgramStartWindowType, 0);

                    runProgramShortcut.elevationLevel = static_cast<Shortcut::ElevationLevel>(runProgramElevationLevel);
                    runProgramShortcut.alreadyRunningAction = static_cast<Shortcut::ProgramAlreadyRunningAction>(runProgramAlreadyRunningAction);
                    runProgramShortcut.startWindowType = static_cast<Shortcut::StartWindowType>(runProgramStartWindowType);

                    runProgramShortcut.operationType = Shortcut::OperationType::RunProgram;
                    AddMouseButtonRemap(*mouseButton, runProgramShortcut);
                }
                else if (!openUri.empty())
                {
                    // Remapped to open URI
                    Shortcut openUriShortcut;
                    openUriShortcut.uriToOpen = openUri.c_str();
                    openUriShortcut.operationType = Shortcut::OperationType::OpenURI;
                    AddMouseButtonRemap(*mouseButton, openUriShortcut);
                }
                else if (!newRemapKeys.empty())
                {
                    // If remapped to a shortcut
                    if (std::wstring(newRemapKeys).find(L";") != std::string::npos)
                    {
                        AddMouseButtonRemap(*mouseButton, Shortcut(newRemapKeys.c_str()));
                    }
                    // If remapped to a key
                    else
                    {
                        AddMouseButtonRemap(*mouseButton, std::stoul(newRemapKeys.c_str()));
                    }
                }
            }
            catch (...)
            {
                Logger::error(L"Improper Mouse Button Data JSON. Try the next remap.");
                result = false;
            }
        }

        // Load app-specific mouse button remaps
        result = result && LoadAppSpecificMouseButtonRemaps(remapMouseData);
    }
    catch (...)
    {
        Logger::error(L"Improper JSON format for mouse button remaps. Skip to next remap type");
        result = false;
    }

    return result;
}

bool MappingConfiguration::LoadAppSpecificMouseButtonRemaps(const json::JsonObject& remapMouseData)
{
    bool result = true;

    try
    {
        auto appSpecificRemaps = remapMouseData.GetNamedArray(KeyboardManagerConstants::AppSpecificMouseRemapsSettingName);
        for (const auto& it : appSpecificRemaps)
        {
            try
            {
                auto originalButton = it.GetObjectW().GetNamedString(KeyboardManagerConstants::OriginalMouseButtonSettingName);
                auto targetApp = it.GetObjectW().GetNamedString(KeyboardManagerConstants::TargetAppSettingName);
                auto newRemapKeys = it.GetObjectW().GetNamedString(KeyboardManagerConstants::NewRemapKeysSettingName, {});
                auto unicodeText = it.GetObjectW().GetNamedString(KeyboardManagerConstants::NewTextSettingName, {});
                auto runProgramFilePath = it.GetObjectW().GetNamedString(KeyboardManagerConstants::RunProgramFilePathSettingName, {});
                auto openUri = it.GetObjectW().GetNamedString(KeyboardManagerConstants::ShortcutOpenURI, {});

                auto mouseButton = MouseButtonHelpers::MouseButtonFromString(originalButton.c_str());
                if (!mouseButton.has_value())
                {
                    Logger::error(L"Invalid mouse button name: {}", originalButton.c_str());
                    continue;
                }

                // Priority: Text > Run Program > Open URI > Key/Shortcut
                if (!unicodeText.empty())
                {
                    AddAppSpecificMouseButtonRemap(targetApp.c_str(), *mouseButton, std::wstring(unicodeText));
                }
                else if (!runProgramFilePath.empty())
                {
                    Shortcut runProgramShortcut;
                    runProgramShortcut.runProgramFilePath = runProgramFilePath.c_str();
                    runProgramShortcut.runProgramArgs = it.GetObjectW().GetNamedString(KeyboardManagerConstants::RunProgramArgsSettingName, {}).c_str();
                    runProgramShortcut.runProgramStartInDir = it.GetObjectW().GetNamedString(KeyboardManagerConstants::RunProgramStartInDirSettingName, {}).c_str();

                    auto runProgramElevationLevel = it.GetObjectW().GetNamedNumber(KeyboardManagerConstants::RunProgramElevationLevelSettingName, 0);
                    auto runProgramAlreadyRunningAction = it.GetObjectW().GetNamedNumber(KeyboardManagerConstants::RunProgramAlreadyRunningAction, 0);
                    auto runProgramStartWindowType = it.GetObjectW().GetNamedNumber(KeyboardManagerConstants::RunProgramStartWindowType, 0);

                    runProgramShortcut.elevationLevel = static_cast<Shortcut::ElevationLevel>(runProgramElevationLevel);
                    runProgramShortcut.alreadyRunningAction = static_cast<Shortcut::ProgramAlreadyRunningAction>(runProgramAlreadyRunningAction);
                    runProgramShortcut.startWindowType = static_cast<Shortcut::StartWindowType>(runProgramStartWindowType);

                    runProgramShortcut.operationType = Shortcut::OperationType::RunProgram;
                    AddAppSpecificMouseButtonRemap(targetApp.c_str(), *mouseButton, runProgramShortcut);
                }
                else if (!openUri.empty())
                {
                    Shortcut openUriShortcut;
                    openUriShortcut.uriToOpen = openUri.c_str();
                    openUriShortcut.operationType = Shortcut::OperationType::OpenURI;
                    AddAppSpecificMouseButtonRemap(targetApp.c_str(), *mouseButton, openUriShortcut);
                }
                else if (!newRemapKeys.empty())
                {
                    if (std::wstring(newRemapKeys).find(L";") != std::string::npos)
                    {
                        AddAppSpecificMouseButtonRemap(targetApp.c_str(), *mouseButton, Shortcut(newRemapKeys.c_str()));
                    }
                    else
                    {
                        AddAppSpecificMouseButtonRemap(targetApp.c_str(), *mouseButton, std::stoul(newRemapKeys.c_str()));
                    }
                }
            }
            catch (...)
            {
                Logger::error(L"Improper App-Specific Mouse Button Data JSON. Try the next remap.");
                result = false;
            }
        }
    }
    catch (...)
    {
        // No app-specific mouse button remaps found, that's ok
    }

    return result;
}

bool MappingConfiguration::LoadKeyToMouseRemaps(const json::JsonObject& jsonData)
{
    bool result = true;

    try
    {
        auto remapKeyToMouseData = jsonData.GetNamedObject(KeyboardManagerConstants::RemapKeysToMouseSettingName);
        ClearKeyToMouseRemaps();
        ClearAppSpecificKeyToMouseRemaps();

        if (!remapKeyToMouseData)
        {
            return result;
        }

        // Try to load global remaps first (new format), fall back to inProcess (old format) for backward compatibility
        json::JsonArray globalRemaps;
        try
        {
            globalRemaps = remapKeyToMouseData.GetNamedArray(KeyboardManagerConstants::GlobalMouseRemapsSettingName);
        }
        catch (...)
        {
            // Fall back to old format
            try
            {
                globalRemaps = remapKeyToMouseData.GetNamedArray(KeyboardManagerConstants::InProcessRemapKeysSettingName);
            }
            catch (...)
            {
                // No global remaps found
            }
        }

        Logger::info(L"LoadKeyToMouseRemaps: Found {} remaps", globalRemaps.Size());
        for (const auto& it : globalRemaps)
        {
            try
            {
                auto originalKey = it.GetObjectW().GetNamedString(KeyboardManagerConstants::OriginalKeysSettingName);
                auto targetMouseButton = it.GetObjectW().GetNamedString(KeyboardManagerConstants::TargetMouseButtonSettingName);

                Logger::info(L"LoadKeyToMouseRemaps: Loading key {} -> {}", originalKey.c_str(), targetMouseButton.c_str());

                DWORD originalKeyCode = std::stoul(originalKey.c_str());
                auto mouseButton = MouseButtonHelpers::MouseButtonFromString(targetMouseButton.c_str());

                if (!mouseButton.has_value())
                {
                    Logger::error(L"Invalid target mouse button name: {}", targetMouseButton.c_str());
                    continue;
                }

                bool added = AddKeyToMouseRemap(originalKeyCode, *mouseButton);
                Logger::info(L"LoadKeyToMouseRemaps: Added key {} -> mouse button {}: {}", originalKeyCode, static_cast<int>(*mouseButton), added);
            }
            catch (...)
            {
                Logger::error(L"Improper Key to Mouse Data JSON. Try the next remap.");
                result = false;
            }
        }

        // Load app-specific key to mouse remaps
        result = result && LoadAppSpecificKeyToMouseRemaps(remapKeyToMouseData);
    }
    catch (...)
    {
        Logger::error(L"Improper JSON format for key to mouse remaps. Skip to next remap type");
        result = false;
    }

    return result;
}

bool MappingConfiguration::LoadAppSpecificKeyToMouseRemaps(const json::JsonObject& remapKeyToMouseData)
{
    bool result = true;

    try
    {
        auto appSpecificRemaps = remapKeyToMouseData.GetNamedArray(KeyboardManagerConstants::AppSpecificMouseRemapsSettingName);
        for (const auto& it : appSpecificRemaps)
        {
            try
            {
                auto originalKey = it.GetObjectW().GetNamedString(KeyboardManagerConstants::OriginalKeysSettingName);
                auto targetApp = it.GetObjectW().GetNamedString(KeyboardManagerConstants::TargetAppSettingName);
                auto targetMouseButton = it.GetObjectW().GetNamedString(KeyboardManagerConstants::TargetMouseButtonSettingName);

                DWORD originalKeyCode = std::stoul(originalKey.c_str());
                auto mouseButton = MouseButtonHelpers::MouseButtonFromString(targetMouseButton.c_str());

                if (!mouseButton.has_value())
                {
                    Logger::error(L"Invalid target mouse button name: {}", targetMouseButton.c_str());
                    continue;
                }

                AddAppSpecificKeyToMouseRemap(targetApp.c_str(), originalKeyCode, *mouseButton);
            }
            catch (...)
            {
                Logger::error(L"Improper App-Specific Key to Mouse Data JSON. Try the next remap.");
                result = false;
            }
        }
    }
    catch (...)
    {
        // No app-specific key to mouse remaps found, that's ok
    }

    return result;
}

bool MappingConfiguration::LoadShortcutRemaps(const json::JsonObject& jsonData, const std::wstring& objectName)
{
    bool result = true;

    try
    {
        auto remapShortcutsData = jsonData.GetNamedObject(objectName);
        if (remapShortcutsData)
        {
            // Load os level shortcut remaps
            try
            {
                auto globalRemapShortcuts = remapShortcutsData.GetNamedArray(KeyboardManagerConstants::GlobalRemapShortcutsSettingName);
                for (const auto& it : globalRemapShortcuts)
                {
                    try
                    {
                        auto originalKeys = it.GetObjectW().GetNamedString(KeyboardManagerConstants::OriginalKeysSettingName);
                        auto newRemapKeys = it.GetObjectW().GetNamedString(KeyboardManagerConstants::NewRemapKeysSettingName, {});
                        auto newRemapText = it.GetObjectW().GetNamedString(KeyboardManagerConstants::NewTextSettingName, {});
                        auto operationType = it.GetObjectW().GetNamedNumber(KeyboardManagerConstants::ShortcutOperationType, 0);

                        auto originalShortcut = Shortcut(originalKeys.c_str());
                        originalShortcut.exactMatch = it.GetObjectW().GetNamedBoolean(KeyboardManagerConstants::ShortcutExactMatch, false);
                        // undo dummy data for backwards compatibility
                        if (newRemapText == L"*Unsupported*")
                        {
                            newRemapText == L"";
                        }

                        // check Shortcut::OperationType
                        if (operationType == 1)
                        {
                            auto runProgramFilePath = it.GetObjectW().GetNamedString(KeyboardManagerConstants::RunProgramFilePathSettingName, L"");
                            auto runProgramArgs = it.GetObjectW().GetNamedString(KeyboardManagerConstants::RunProgramArgsSettingName, L"");
                            auto runProgramStartInDir = it.GetObjectW().GetNamedString(KeyboardManagerConstants::RunProgramStartInDirSettingName, L"");
                            auto runProgramElevationLevel = it.GetObjectW().GetNamedNumber(KeyboardManagerConstants::RunProgramElevationLevelSettingName, 0);
                            auto runProgramStartWindowType = it.GetObjectW().GetNamedNumber(KeyboardManagerConstants::RunProgramStartWindowType, 0);
                            auto runProgramAlreadyRunningAction = it.GetObjectW().GetNamedNumber(KeyboardManagerConstants::RunProgramAlreadyRunningAction, 0);

                            auto tempShortcut = Shortcut(newRemapKeys.c_str());
                            tempShortcut.operationType = Shortcut::OperationType::RunProgram;
                            tempShortcut.runProgramFilePath = runProgramFilePath;
                            tempShortcut.runProgramArgs = runProgramArgs;
                            tempShortcut.runProgramStartInDir = runProgramStartInDir;
                            tempShortcut.elevationLevel = static_cast<Shortcut::ElevationLevel>(runProgramElevationLevel);
                            tempShortcut.alreadyRunningAction = static_cast<Shortcut::ProgramAlreadyRunningAction>(runProgramAlreadyRunningAction);
                            tempShortcut.startWindowType = static_cast<Shortcut::StartWindowType>(runProgramStartWindowType);

                            AddOSLevelShortcut(originalShortcut, tempShortcut);
                        }
                        else if (operationType == 2)
                        {
                            auto tempShortcut = Shortcut(newRemapKeys.c_str());
                            tempShortcut.operationType = Shortcut::OperationType::OpenURI;
                            tempShortcut.uriToOpen = it.GetObjectW().GetNamedString(KeyboardManagerConstants::ShortcutOpenURI, L"");

                            AddOSLevelShortcut(originalShortcut, tempShortcut);
                        }
                        else if (!newRemapKeys.empty())
                        {
                            // If remapped to a shortcut
                            if (std::wstring(newRemapKeys).find(L";") != std::string::npos)
                            {
                                AddOSLevelShortcut(originalShortcut, Shortcut(newRemapKeys.c_str()));
                            }
                            // If remapped to a key
                            else
                            {
                                AddOSLevelShortcut(originalShortcut, std::stoul(newRemapKeys.c_str()));
                            }
                        }
                        else
                        {
                            AddOSLevelShortcut(originalShortcut, newRemapText.c_str());
                        }
                    }
                    catch (...)
                    {
                        Logger::error(L"Improper Key Data JSON. Try the next shortcut.");
                        result = false;
                    }
                }
            }
            catch (...)
            {
                Logger::error(L"Improper JSON format for os level shortcut remaps. Skip to next remap type");
                result = false;
            }

            // Load app specific shortcut remaps
            result = result && LoadAppSpecificShortcutRemaps(remapShortcutsData);
        }
    }
    catch (...)
    {
        Logger::error(L"Improper JSON format for shortcut remaps. Skip to next remap type");
        result = false;
    }

    return result;
}

bool MappingConfiguration::LoadSettings()
{
    Logger::trace(L"SettingsHelper::LoadSettings()");
    try
    {
        PowerToysSettings::PowerToyValues settings = PowerToysSettings::PowerToyValues::load_from_settings_file(KeyboardManagerConstants::ModuleName);
        auto current_config = settings.get_string_value(KeyboardManagerConstants::ActiveConfigurationSettingName);

        if (!current_config)
        {
            return false;
        }

        currentConfig = *current_config;

        // Read the config file and load the remaps.
        auto configFile = json::from_file(PTSettingsHelper::get_module_save_folder_location(KeyboardManagerConstants::ModuleName) + L"\\" + *current_config + L".json");
        if (!configFile)
        {
            return false;
        }

        bool result = LoadSingleKeyRemaps(*configFile);
        ClearOSLevelShortcuts();
        ClearAppSpecificShortcuts();
        result = LoadShortcutRemaps(*configFile, KeyboardManagerConstants::RemapShortcutsSettingName) && result;
        result = LoadShortcutRemaps(*configFile, KeyboardManagerConstants::RemapShortcutsToTextSettingName) && result;
        result = LoadSingleKeyToTextRemaps(*configFile) && result;
        result = LoadMouseButtonRemaps(*configFile) && result;
        result = LoadKeyToMouseRemaps(*configFile) && result;

        return result;
    }
    catch (...)
    {
        Logger::error(L"SettingsHelper::LoadSettings() failed");
    }

    return false;
}

// Save the updated configuration.
bool MappingConfiguration::SaveSettingsToFile()
{
    bool result = true;
    json::JsonObject configJson;
    json::JsonObject remapShortcuts;
    json::JsonObject remapShortcutsToText;

    json::JsonObject remapKeys;
    json::JsonObject remapKeysToText;

    json::JsonArray inProcessRemapKeysArray;
    json::JsonArray inProcessRemapKeysToTextArray;

    json::JsonArray appSpecificRemapShortcutsArray;
    json::JsonArray appSpecificRemapShortcutsToTextArray;

    json::JsonArray globalRemapShortcutsArray;
    json::JsonArray globalRemapShortcutsToTextArray;

    for (const auto& it : singleKeyReMap)
    {
        json::JsonObject keys;
        keys.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(winrt::to_hstring(static_cast<unsigned int>(it.first))));

        // For key to key remapping
        if (it.second.index() == 0)
        {
            keys.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(winrt::to_hstring((unsigned int)std::get<DWORD>(it.second))));
        }

        // For key to shortcut remapping
        else
        {
            keys.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(std::get<Shortcut>(it.second).ToHstringVK()));
        }

        inProcessRemapKeysArray.Append(keys);
    }

    for (const auto& [code, text] : singleKeyToTextReMap)
    {
        json::JsonObject keys;
        keys.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(winrt::to_hstring(static_cast<unsigned int>(code))));
        keys.SetNamedValue(KeyboardManagerConstants::NewTextSettingName, json::value(std::get<std::wstring>(text)));
        inProcessRemapKeysToTextArray.Append(keys);
    }

    for (const auto& it : osLevelShortcutReMap)
    {
        json::JsonObject keys;

        keys.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(it.first.ToHstringVK()));

        keys.SetNamedValue(KeyboardManagerConstants::ShortcutExactMatch, json::JsonValue::CreateBooleanValue(it.first.exactMatch));
        bool remapToText = false;

        // For shortcut to key remapping
        if (it.second.targetShortcut.index() == 0)
        {
            keys.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(winrt::to_hstring((unsigned int)std::get<DWORD>(it.second.targetShortcut))));
        }
        // For shortcut to shortcut remapping
        else if (it.second.targetShortcut.index() == 1)
        {
            auto targetShortcut = std::get<Shortcut>(it.second.targetShortcut);

            if (targetShortcut.operationType == Shortcut::OperationType::RunProgram)
            {
                keys.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(targetShortcut.ToHstringVK()));
                keys.SetNamedValue(KeyboardManagerConstants::RunProgramElevationLevelSettingName, json::value(static_cast<unsigned int>(targetShortcut.elevationLevel)));

                keys.SetNamedValue(KeyboardManagerConstants::ShortcutOperationType, json::value(static_cast<unsigned int>(targetShortcut.operationType)));
                keys.SetNamedValue(KeyboardManagerConstants::RunProgramAlreadyRunningAction, json::value(static_cast<unsigned int>(targetShortcut.alreadyRunningAction)));
                keys.SetNamedValue(KeyboardManagerConstants::RunProgramStartWindowType, json::value(static_cast<unsigned int>(targetShortcut.startWindowType)));

                keys.SetNamedValue(KeyboardManagerConstants::RunProgramFilePathSettingName, json::value(targetShortcut.runProgramFilePath));
                keys.SetNamedValue(KeyboardManagerConstants::RunProgramArgsSettingName, json::value(targetShortcut.runProgramArgs));
                keys.SetNamedValue(KeyboardManagerConstants::RunProgramStartInDirSettingName, json::value(targetShortcut.runProgramStartInDir));

                // we need to add this dummy data for backwards compatibility
                keys.SetNamedValue(KeyboardManagerConstants::NewTextSettingName, json::value(L"*Unsupported*"));
            }
            else if (targetShortcut.operationType == Shortcut::OperationType::OpenURI)
            {
                keys.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(targetShortcut.ToHstringVK()));
                keys.SetNamedValue(KeyboardManagerConstants::RunProgramElevationLevelSettingName, json::value(static_cast<unsigned int>(targetShortcut.elevationLevel)));
                keys.SetNamedValue(KeyboardManagerConstants::ShortcutOperationType, json::value(static_cast<unsigned int>(targetShortcut.operationType)));

                keys.SetNamedValue(KeyboardManagerConstants::ShortcutOpenURI, json::value(targetShortcut.uriToOpen));

                // we need to add this dummy data for backwards compatibility
                keys.SetNamedValue(KeyboardManagerConstants::NewTextSettingName, json::value(L"*Unsupported*"));
            }
            else
            {
                keys.SetNamedValue(KeyboardManagerConstants::ShortcutOperationType, json::value(static_cast<unsigned int>(targetShortcut.operationType)));
                keys.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(targetShortcut.ToHstringVK()));
            }
        }
        // For shortcut to text remapping
        else if (it.second.targetShortcut.index() == 2)
        {
            remapToText = true;
            keys.SetNamedValue(KeyboardManagerConstants::NewTextSettingName, json::value(std::get<std::wstring>(it.second.targetShortcut)));
        }

        if (!remapToText)
            globalRemapShortcutsArray.Append(keys);
        else
            globalRemapShortcutsToTextArray.Append(keys);
    }

    for (const auto& itApp : appSpecificShortcutReMap)
    {
        // Iterate over apps
        for (const auto& itKeys : itApp.second)
        {
            json::JsonObject keys;
            keys.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(itKeys.first.ToHstringVK()));
            keys.SetNamedValue(KeyboardManagerConstants::ShortcutExactMatch, json::JsonValue::CreateBooleanValue(itKeys.first.exactMatch));
            bool remapToText = false;

            // For shortcut to key remapping
            if (itKeys.second.targetShortcut.index() == 0)
            {
                keys.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(winrt::to_hstring((unsigned int)std::get<DWORD>(itKeys.second.targetShortcut))));
            }

            // For shortcut to shortcut remapping
            else if (itKeys.second.targetShortcut.index() == 1)
            {
                auto targetShortcut = std::get<Shortcut>(itKeys.second.targetShortcut);

                if (targetShortcut.operationType == Shortcut::OperationType::RunProgram)
                {
                    keys.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(targetShortcut.ToHstringVK()));
                    keys.SetNamedValue(KeyboardManagerConstants::RunProgramElevationLevelSettingName, json::value(static_cast<unsigned int>(targetShortcut.elevationLevel)));

                    keys.SetNamedValue(KeyboardManagerConstants::ShortcutOperationType, json::value(static_cast<unsigned int>(targetShortcut.operationType)));
                    keys.SetNamedValue(KeyboardManagerConstants::RunProgramAlreadyRunningAction, json::value(static_cast<unsigned int>(targetShortcut.alreadyRunningAction)));
                    keys.SetNamedValue(KeyboardManagerConstants::RunProgramStartWindowType, json::value(static_cast<unsigned int>(targetShortcut.startWindowType)));

                    keys.SetNamedValue(KeyboardManagerConstants::RunProgramFilePathSettingName, json::value(targetShortcut.runProgramFilePath));
                    keys.SetNamedValue(KeyboardManagerConstants::RunProgramArgsSettingName, json::value(targetShortcut.runProgramArgs));
                    keys.SetNamedValue(KeyboardManagerConstants::RunProgramStartInDirSettingName, json::value(targetShortcut.runProgramStartInDir));

                    // we need to add this dummy data for backwards compatibility
                    keys.SetNamedValue(KeyboardManagerConstants::NewTextSettingName, json::value(L"*Unsupported*"));
                }
                else if (targetShortcut.operationType == Shortcut::OperationType::OpenURI)
                {
                    keys.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(targetShortcut.ToHstringVK()));
                    keys.SetNamedValue(KeyboardManagerConstants::RunProgramElevationLevelSettingName, json::value(static_cast<unsigned int>(targetShortcut.elevationLevel)));
                    keys.SetNamedValue(KeyboardManagerConstants::ShortcutOperationType, json::value(static_cast<unsigned int>(targetShortcut.operationType)));

                    keys.SetNamedValue(KeyboardManagerConstants::ShortcutOpenURI, json::value(targetShortcut.uriToOpen));

                    // we need to add this dummy data for backwards compatibility
                    keys.SetNamedValue(KeyboardManagerConstants::NewTextSettingName, json::value(L"*Unsupported*"));
                }
                else
                {
                    keys.SetNamedValue(KeyboardManagerConstants::ShortcutOperationType, json::value(static_cast<unsigned int>(targetShortcut.operationType)));
                    keys.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(std::get<Shortcut>(itKeys.second.targetShortcut).ToHstringVK()));
                }
            }
            else if (itKeys.second.targetShortcut.index() == 2)
            {
                keys.SetNamedValue(KeyboardManagerConstants::NewTextSettingName, json::value(std::get<std::wstring>(itKeys.second.targetShortcut)));
                remapToText = true;
            }

            keys.SetNamedValue(KeyboardManagerConstants::TargetAppSettingName, json::value(itApp.first));

            if (!remapToText)
                appSpecificRemapShortcutsArray.Append(keys);
            else
                appSpecificRemapShortcutsToTextArray.Append(keys);
        }
    }

    remapShortcuts.SetNamedValue(KeyboardManagerConstants::GlobalRemapShortcutsSettingName, globalRemapShortcutsArray);
    remapShortcuts.SetNamedValue(KeyboardManagerConstants::AppSpecificRemapShortcutsSettingName, appSpecificRemapShortcutsArray);

    remapShortcutsToText.SetNamedValue(KeyboardManagerConstants::GlobalRemapShortcutsSettingName, globalRemapShortcutsToTextArray);
    remapShortcutsToText.SetNamedValue(KeyboardManagerConstants::AppSpecificRemapShortcutsSettingName, appSpecificRemapShortcutsToTextArray);

    remapKeys.SetNamedValue(KeyboardManagerConstants::InProcessRemapKeysSettingName, inProcessRemapKeysArray);
    remapKeysToText.SetNamedValue(KeyboardManagerConstants::InProcessRemapKeysSettingName, inProcessRemapKeysToTextArray);
    configJson.SetNamedValue(KeyboardManagerConstants::RemapKeysSettingName, remapKeys);
    configJson.SetNamedValue(KeyboardManagerConstants::RemapKeysToTextSettingName, remapKeysToText);
    configJson.SetNamedValue(KeyboardManagerConstants::RemapShortcutsSettingName, remapShortcuts);
    configJson.SetNamedValue(KeyboardManagerConstants::RemapShortcutsToTextSettingName, remapShortcutsToText);

    // Save mouse button remaps
    json::JsonObject remapMouseButtons;
    json::JsonArray globalMouseRemapsArray;
    json::JsonArray appSpecificMouseRemapsArray;

    for (const auto& it : mouseButtonReMap)
    {
        json::JsonObject mouseRemap;
        mouseRemap.SetNamedValue(KeyboardManagerConstants::OriginalMouseButtonSettingName, json::value(MouseButtonHelpers::MouseButtonToString(it.first)));

        // For mouse to key remapping
        if (it.second.index() == 0)
        {
            mouseRemap.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(winrt::to_hstring(static_cast<unsigned int>(std::get<DWORD>(it.second)))));
        }
        // For mouse to shortcut remapping
        else if (it.second.index() == 1)
        {
            auto targetShortcut = std::get<Shortcut>(it.second);
            if (targetShortcut.operationType == Shortcut::OperationType::RunProgram)
            {
                mouseRemap.SetNamedValue(KeyboardManagerConstants::RunProgramFilePathSettingName, json::value(targetShortcut.runProgramFilePath));
                mouseRemap.SetNamedValue(KeyboardManagerConstants::RunProgramArgsSettingName, json::value(targetShortcut.runProgramArgs));
                mouseRemap.SetNamedValue(KeyboardManagerConstants::RunProgramStartInDirSettingName, json::value(targetShortcut.runProgramStartInDir));
                mouseRemap.SetNamedValue(KeyboardManagerConstants::RunProgramElevationLevelSettingName, json::value(static_cast<unsigned int>(targetShortcut.elevationLevel)));
                mouseRemap.SetNamedValue(KeyboardManagerConstants::RunProgramAlreadyRunningAction, json::value(static_cast<unsigned int>(targetShortcut.alreadyRunningAction)));
                mouseRemap.SetNamedValue(KeyboardManagerConstants::RunProgramStartWindowType, json::value(static_cast<unsigned int>(targetShortcut.startWindowType)));
            }
            else if (targetShortcut.operationType == Shortcut::OperationType::OpenURI)
            {
                mouseRemap.SetNamedValue(KeyboardManagerConstants::ShortcutOpenURI, json::value(targetShortcut.uriToOpen));
            }
            else
            {
                mouseRemap.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(targetShortcut.ToHstringVK()));
            }
        }
        // For mouse to text remapping
        else if (it.second.index() == 2)
        {
            mouseRemap.SetNamedValue(KeyboardManagerConstants::NewTextSettingName, json::value(std::get<std::wstring>(it.second)));
        }

        globalMouseRemapsArray.Append(mouseRemap);
    }

    // Save app-specific mouse button remaps
    for (const auto& appIt : appSpecificMouseButtonReMap)
    {
        for (const auto& it : appIt.second)
        {
            json::JsonObject mouseRemap;
            mouseRemap.SetNamedValue(KeyboardManagerConstants::OriginalMouseButtonSettingName, json::value(MouseButtonHelpers::MouseButtonToString(it.first)));
            mouseRemap.SetNamedValue(KeyboardManagerConstants::TargetAppSettingName, json::value(appIt.first));

            if (it.second.index() == 0)
            {
                mouseRemap.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(winrt::to_hstring(static_cast<unsigned int>(std::get<DWORD>(it.second)))));
            }
            else if (it.second.index() == 1)
            {
                auto targetShortcut = std::get<Shortcut>(it.second);
                if (targetShortcut.operationType == Shortcut::OperationType::RunProgram)
                {
                    mouseRemap.SetNamedValue(KeyboardManagerConstants::RunProgramFilePathSettingName, json::value(targetShortcut.runProgramFilePath));
                    mouseRemap.SetNamedValue(KeyboardManagerConstants::RunProgramArgsSettingName, json::value(targetShortcut.runProgramArgs));
                    mouseRemap.SetNamedValue(KeyboardManagerConstants::RunProgramStartInDirSettingName, json::value(targetShortcut.runProgramStartInDir));
                    mouseRemap.SetNamedValue(KeyboardManagerConstants::RunProgramElevationLevelSettingName, json::value(static_cast<unsigned int>(targetShortcut.elevationLevel)));
                    mouseRemap.SetNamedValue(KeyboardManagerConstants::RunProgramAlreadyRunningAction, json::value(static_cast<unsigned int>(targetShortcut.alreadyRunningAction)));
                    mouseRemap.SetNamedValue(KeyboardManagerConstants::RunProgramStartWindowType, json::value(static_cast<unsigned int>(targetShortcut.startWindowType)));
                }
                else if (targetShortcut.operationType == Shortcut::OperationType::OpenURI)
                {
                    mouseRemap.SetNamedValue(KeyboardManagerConstants::ShortcutOpenURI, json::value(targetShortcut.uriToOpen));
                }
                else
                {
                    mouseRemap.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(targetShortcut.ToHstringVK()));
                }
            }
            else if (it.second.index() == 2)
            {
                mouseRemap.SetNamedValue(KeyboardManagerConstants::NewTextSettingName, json::value(std::get<std::wstring>(it.second)));
            }

            appSpecificMouseRemapsArray.Append(mouseRemap);
        }
    }

    remapMouseButtons.SetNamedValue(KeyboardManagerConstants::GlobalMouseRemapsSettingName, globalMouseRemapsArray);
    remapMouseButtons.SetNamedValue(KeyboardManagerConstants::AppSpecificMouseRemapsSettingName, appSpecificMouseRemapsArray);
    configJson.SetNamedValue(KeyboardManagerConstants::RemapMouseButtonsSettingName, remapMouseButtons);

    // Save key to mouse remaps
    json::JsonObject remapKeysToMouse;
    json::JsonArray globalKeysToMouseArray;
    json::JsonArray appSpecificKeysToMouseArray;

    for (const auto& it : keyToMouseReMap)
    {
        json::JsonObject keyToMouseRemap;
        keyToMouseRemap.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(winrt::to_hstring(static_cast<unsigned int>(it.first))));
        keyToMouseRemap.SetNamedValue(KeyboardManagerConstants::TargetMouseButtonSettingName, json::value(MouseButtonHelpers::MouseButtonToString(it.second)));
        globalKeysToMouseArray.Append(keyToMouseRemap);
    }

    for (const auto& appIt : appSpecificKeyToMouseReMap)
    {
        for (const auto& it : appIt.second)
        {
            json::JsonObject keyToMouseRemap;
            keyToMouseRemap.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(winrt::to_hstring(static_cast<unsigned int>(it.first))));
            keyToMouseRemap.SetNamedValue(KeyboardManagerConstants::TargetMouseButtonSettingName, json::value(MouseButtonHelpers::MouseButtonToString(it.second)));
            keyToMouseRemap.SetNamedValue(KeyboardManagerConstants::TargetAppSettingName, json::value(appIt.first));
            appSpecificKeysToMouseArray.Append(keyToMouseRemap);
        }
    }

    remapKeysToMouse.SetNamedValue(KeyboardManagerConstants::GlobalMouseRemapsSettingName, globalKeysToMouseArray);
    remapKeysToMouse.SetNamedValue(KeyboardManagerConstants::AppSpecificMouseRemapsSettingName, appSpecificKeysToMouseArray);
    configJson.SetNamedValue(KeyboardManagerConstants::RemapKeysToMouseSettingName, remapKeysToMouse);

    try
    {
        json::to_file((PTSettingsHelper::get_module_save_folder_location(KeyboardManagerConstants::ModuleName) + L"\\" + currentConfig + L".json"), configJson);
    }
    catch (...)
    {
        result = false;
        Logger::error(L"Failed to save the settings");
    }

    if (result)
    {
        auto hEvent = CreateEvent(nullptr, false, false, KeyboardManagerConstants::SettingsEventName.c_str());
        if (hEvent)
        {
            SetEvent(hEvent);
            Logger::trace(L"Signaled {} event", KeyboardManagerConstants::SettingsEventName);
        }
        else
        {
            Logger::error(L"Failed to signal {} event", KeyboardManagerConstants::SettingsEventName);
        }
    }

    return result;
}
