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
                    auto secondKeyOfChord = it.GetObjectW().GetNamedNumber(KeyboardManagerConstants::ShortcutSecondKeyOfChordSettingName, 0);

                    auto tempShortcut = Shortcut(newRemapKeys.c_str());
                    tempShortcut.operationType = Shortcut::OperationType::RunProgram;
                    tempShortcut.runProgramFilePath = runProgramFilePath;
                    tempShortcut.runProgramArgs = runProgramArgs;
                    tempShortcut.runProgramStartInDir = runProgramStartInDir;
                    tempShortcut.elevationLevel = static_cast<Shortcut::ElevationLevel>(runProgramElevationLevel);
                    tempShortcut.alreadyRunningAction = static_cast<Shortcut::ProgramAlreadyRunningAction>(runProgramAlreadyRunningAction);
                    tempShortcut.startWindowType = static_cast<Shortcut::StartWindowType>(runProgramStartWindowType);

                    AddAppSpecificShortcut(targetApp.c_str(), Shortcut(originalKeys.c_str(), static_cast<DWORD>(secondKeyOfChord)), tempShortcut);
                }
                else if (operationType == 2)
                {
                    auto secondKeyOfChord = it.GetObjectW().GetNamedNumber(KeyboardManagerConstants::ShortcutSecondKeyOfChordSettingName, 0);
                    auto tempShortcut = Shortcut(newRemapKeys.c_str());
                    tempShortcut.operationType = Shortcut::OperationType::OpenURI;
                    tempShortcut.uriToOpen = it.GetObjectW().GetNamedString(KeyboardManagerConstants::ShortcutOpenURI, L"");

                    AddAppSpecificShortcut(targetApp.c_str(), Shortcut(originalKeys.c_str(), static_cast<DWORD>(secondKeyOfChord)), tempShortcut);
                }

                if (!newRemapKeys.empty())
                {
                    // If remapped to a shortcut
                    if (std::wstring(newRemapKeys).find(L";") != std::string::npos)
                    {
                        AddAppSpecificShortcut(targetApp.c_str(), Shortcut(originalKeys.c_str()), Shortcut(newRemapKeys.c_str()));
                    }

                    // If remapped to a key
                    else
                    {
                        AddAppSpecificShortcut(targetApp.c_str(), Shortcut(originalKeys.c_str()), std::stoul(newRemapKeys.c_str()));
                    }
                }
                else
                {
                    AddAppSpecificShortcut(targetApp.c_str(), Shortcut(originalKeys.c_str()), newRemapText.c_str());
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
                            auto secondKeyOfChord = it.GetObjectW().GetNamedNumber(KeyboardManagerConstants::ShortcutSecondKeyOfChordSettingName, 0);

                            auto runProgramAlreadyRunningAction = it.GetObjectW().GetNamedNumber(KeyboardManagerConstants::RunProgramAlreadyRunningAction, 0);

                            auto tempShortcut = Shortcut(newRemapKeys.c_str());
                            tempShortcut.operationType = Shortcut::OperationType::RunProgram;
                            tempShortcut.runProgramFilePath = runProgramFilePath;
                            tempShortcut.runProgramArgs = runProgramArgs;
                            tempShortcut.runProgramStartInDir = runProgramStartInDir;
                            tempShortcut.elevationLevel = static_cast<Shortcut::ElevationLevel>(runProgramElevationLevel);
                            tempShortcut.alreadyRunningAction = static_cast<Shortcut::ProgramAlreadyRunningAction>(runProgramAlreadyRunningAction);
                            tempShortcut.startWindowType = static_cast<Shortcut::StartWindowType>(runProgramStartWindowType);

                            AddOSLevelShortcut(Shortcut(originalKeys.c_str(), static_cast<DWORD>(secondKeyOfChord)), tempShortcut);
                        }
                        else if (operationType == 2)
                        {
                            auto secondKeyOfChord = it.GetObjectW().GetNamedNumber(KeyboardManagerConstants::ShortcutSecondKeyOfChordSettingName, 0);
                            auto tempShortcut = Shortcut(newRemapKeys.c_str());
                            tempShortcut.operationType = Shortcut::OperationType::OpenURI;
                            tempShortcut.uriToOpen = it.GetObjectW().GetNamedString(KeyboardManagerConstants::ShortcutOpenURI, L"");

                            AddOSLevelShortcut(Shortcut(originalKeys.c_str(), static_cast<DWORD>(secondKeyOfChord)), tempShortcut);
                        }
                        else if (!newRemapKeys.empty())
                        {
                            // If remapped to a shortcut
                            if (std::wstring(newRemapKeys).find(L";") != std::string::npos)
                            {
                                AddOSLevelShortcut(Shortcut(originalKeys.c_str()), Shortcut(newRemapKeys.c_str()));
                            }
                            // If remapped to a key
                            else
                            {
                                AddOSLevelShortcut(Shortcut(originalKeys.c_str()), std::stoul(newRemapKeys.c_str()));
                            }
                        }
                        else
                        {
                            AddOSLevelShortcut(Shortcut(originalKeys.c_str()), newRemapText.c_str());
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
                keys.SetNamedValue(KeyboardManagerConstants::RunProgramElevationLevelSettingName, json::value(static_cast<unsigned int>(targetShortcut.elevationLevel)));
                keys.SetNamedValue(KeyboardManagerConstants::ShortcutSecondKeyOfChordSettingName, json::value(static_cast<unsigned int>(it.first.secondKey)));

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
                keys.SetNamedValue(KeyboardManagerConstants::RunProgramElevationLevelSettingName, json::value(static_cast<unsigned int>(targetShortcut.elevationLevel)));
                keys.SetNamedValue(KeyboardManagerConstants::ShortcutSecondKeyOfChordSettingName, json::value(static_cast<unsigned int>(it.first.secondKey)));
                keys.SetNamedValue(KeyboardManagerConstants::ShortcutOperationType, json::value(static_cast<unsigned int>(targetShortcut.operationType)));

                keys.SetNamedValue(KeyboardManagerConstants::ShortcutOpenURI, json::value(targetShortcut.uriToOpen));

                // we need to add this dummy data for backwards compatibility
                keys.SetNamedValue(KeyboardManagerConstants::NewTextSettingName, json::value(L"*Unsupported*"));
            }
            else
            {
                keys.SetNamedValue(KeyboardManagerConstants::ShortcutOperationType, json::value(static_cast<unsigned int>(targetShortcut.operationType)));
                keys.SetNamedValue(KeyboardManagerConstants::ShortcutSecondKeyOfChordSettingName, json::value(static_cast<unsigned int>(it.first.secondKey)));
                keys.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(targetShortcut.ToHstringVK()));
            }
        }
        // For shortcut to text remapping
        else if (it.second.targetShortcut.index() == 2)
        {
            remapToText = true;
            keys.SetNamedValue(KeyboardManagerConstants::NewTextSettingName, json::value(std::get<std::wstring>(it.second.targetShortcut)));
            keys.SetNamedValue(KeyboardManagerConstants::ShortcutSecondKeyOfChordSettingName, json::value(static_cast<unsigned int>(it.first.secondKey)));
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
                    keys.SetNamedValue(KeyboardManagerConstants::RunProgramElevationLevelSettingName, json::value(static_cast<unsigned int>(targetShortcut.elevationLevel)));
                    keys.SetNamedValue(KeyboardManagerConstants::ShortcutSecondKeyOfChordSettingName, json::value(static_cast<unsigned int>(itKeys.first.secondKey)));

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
                    keys.SetNamedValue(KeyboardManagerConstants::RunProgramElevationLevelSettingName, json::value(static_cast<unsigned int>(targetShortcut.elevationLevel)));
                    keys.SetNamedValue(KeyboardManagerConstants::ShortcutSecondKeyOfChordSettingName, json::value(static_cast<unsigned int>(itKeys.first.secondKey)));
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
