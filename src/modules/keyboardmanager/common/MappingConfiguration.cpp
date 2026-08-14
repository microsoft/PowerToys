#include "pch.h"
#include "MappingConfiguration.h"

#include <cstdint>
#include <fstream>

#include <common/SettingsAPI/settings_objects.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/logger/logger.h>

#include "KeyboardManagerConstants.h"
#include "Shortcut.h"
#include "RemapShortcut.h"
#include "Helpers.h"

namespace
{
    constexpr bool IsValidTextReplacementTriggerKey(const DWORD triggerKey)
    {
        return triggerKey == VK_TAB || triggerKey == VK_RETURN || triggerKey == VK_SPACE;
    }

    constexpr bool IsHighSurrogate(const wchar_t value)
    {
        const auto codeUnit = static_cast<uint16_t>(value);
        return codeUnit >= 0xD800 && codeUnit <= 0xDBFF;
    }

    constexpr bool IsLowSurrogate(const wchar_t value)
    {
        const auto codeUnit = static_cast<uint16_t>(value);
        return codeUnit >= 0xDC00 && codeUnit <= 0xDFFF;
    }

    constexpr bool IsValidTextReplacementTrigger(const std::wstring_view trigger)
    {
        for (size_t index = 0; index < trigger.size(); ++index)
        {
            const auto codeUnit = static_cast<uint16_t>(trigger[index]);
            if (IsHighSurrogate(trigger[index]))
            {
                if (++index >= trigger.size() || !IsLowSurrogate(trigger[index]))
                {
                    return false;
                }
            }
            else if (IsLowSurrogate(trigger[index]) || codeUnit < 0x20 || (codeUnit >= 0x7F && codeUnit <= 0x9F))
            {
                return false;
            }
        }

        return true;
    }

    constexpr bool HasSpaceDelimiterPrefixConflict(
        const std::wstring_view shorterTrigger,
        const DWORD shorterTriggerKey,
        const std::wstring_view longerTrigger)
    {
        return shorterTriggerKey == VK_SPACE &&
               longerTrigger.size() > shorterTrigger.size() &&
               longerTrigger.compare(0, shorterTrigger.size(), shorterTrigger) == 0 &&
               longerTrigger[shorterTrigger.size()] == L' ';
    }

    bool IsValidTextReplacement(
        const TextReplacementTable& replacements,
        std::wstring_view trigger,
        std::wstring_view text,
        const DWORD triggerKey,
        std::wstring_view excludedTrigger = {})
    {
        if (trigger.empty() ||
            text.empty() ||
            trigger.find(L'\0') != std::wstring_view::npos ||
            text.find(L'\0') != std::wstring_view::npos ||
            !IsValidTextReplacementTrigger(trigger) ||
            trigger.size() > KeyboardManagerConstants::MaxTextReplacementTriggerLength ||
            text.size() > KeyboardManagerConstants::MaxTextReplacementTextLength ||
            !IsValidTextReplacementTriggerKey(triggerKey))
        {
            return false;
        }

        const auto excludedReplacement = excludedTrigger.empty() ? replacements.end() : replacements.find(excludedTrigger);
        for (auto replacement = replacements.begin(); replacement != replacements.end(); ++replacement)
        {
            if (replacement == excludedReplacement)
            {
                continue;
            }

            if (replacement->first == trigger ||
                HasSpaceDelimiterPrefixConflict(replacement->first, replacement->second.triggerKey, trigger) ||
                HasSpaceDelimiterPrefixConflict(trigger, triggerKey, replacement->first))
            {
                return false;
            }
        }

        return true;
    }

    bool WriteJsonAtomically(const std::wstring& filePath, const json::JsonObject& value)
    {
        const std::wstring temporaryFilePath = filePath + L".tmp";
        try
        {
            const std::string serializedValue = winrt::to_string(value.Stringify());

            std::ofstream temporaryFile;
            temporaryFile.exceptions(std::ios::failbit | std::ios::badbit);
            temporaryFile.open(temporaryFilePath.c_str(), std::ios::binary | std::ios::trunc);
            temporaryFile.write(serializedValue.data(), static_cast<std::streamsize>(serializedValue.size()));
            temporaryFile.flush();
            temporaryFile.close();

            if (!MoveFileExW(
                    temporaryFilePath.c_str(),
                    filePath.c_str(),
                    MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
            {
                DeleteFileW(temporaryFilePath.c_str());
                return false;
            }

            return true;
        }
        catch (...)
        {
            DeleteFileW(temporaryFilePath.c_str());
            return false;
        }
    }

}

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

void MappingConfiguration::ClearTextReplacements()
{
    textReplacements.clear();
    maxTextReplacementTriggerLength = 0;
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

bool MappingConfiguration::AddTextReplacement(const std::wstring& trigger, const std::wstring& text, const DWORD triggerKey)
{
    if (!IsValidTextReplacement(textReplacements, trigger, text, triggerKey))
    {
        return false;
    }

    textReplacements.emplace(trigger, TextReplacementValue{ text, triggerKey });
    maxTextReplacementTriggerLength = (std::max)(maxTextReplacementTriggerLength, trigger.length());
    return true;
}

bool MappingConfiguration::DeleteTextReplacement(const std::wstring& trigger)
{
    if (textReplacements.erase(trigger) == 0)
    {
        return false;
    }

    RecalculateMaxTextReplacementTriggerLength();
    return true;
}

bool MappingConfiguration::UpdateTextReplacement(const std::wstring& oldTrigger, const std::wstring& newTrigger, const std::wstring& newText, const DWORD triggerKey)
{
    const auto oldReplacement = textReplacements.find(oldTrigger);
    if (oldReplacement == textReplacements.end() ||
        !IsValidTextReplacement(textReplacements, newTrigger, newText, triggerKey, oldTrigger))
    {
        return false;
    }

    if (oldTrigger == newTrigger)
    {
        oldReplacement->second = TextReplacementValue{ newText, triggerKey };
    }
    else
    {
        textReplacements.emplace(newTrigger, TextReplacementValue{ newText, triggerKey });
        textReplacements.erase(oldReplacement);
    }

    RecalculateMaxTextReplacementTriggerLength();
    return true;
}

void MappingConfiguration::RecalculateMaxTextReplacementTriggerLength()
{
    maxTextReplacementTriggerLength = 0;
    for (const auto& replacement : textReplacements)
    {
        maxTextReplacementTriggerLength = (std::max)(maxTextReplacementTriggerLength, replacement.first.length());
    }
}

// Function to add a new App specific shortcut remapping
bool MappingConfiguration::AddAppSpecificShortcut(const std::wstring& app, const Shortcut& originalSC, const KeyShortcutTextUnion& newSC)
{
    // Convert app name to lowercase
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

bool MappingConfiguration::LoadTextReplacements(const json::JsonObject& jsonData)
{
    bool result = true;

    try
    {
        ClearTextReplacements();

        auto textReplacementsData = jsonData.GetNamedObject(KeyboardManagerConstants::TextReplacementsSettingName, json::JsonObject{});
        if (!textReplacementsData.HasKey(KeyboardManagerConstants::InProcessRemapKeysSettingName))
        {
            return result;
        }

        auto inProcessTextReplacements = textReplacementsData.GetNamedArray(KeyboardManagerConstants::InProcessRemapKeysSettingName, json::JsonArray{});
        for (uint32_t index = 0; index < inProcessTextReplacements.Size(); ++index)
        {
            try
            {
                const auto replacement = inProcessTextReplacements.GetAt(index).GetObjectW();
                const auto triggerValue = replacement.GetNamedString(KeyboardManagerConstants::TriggerTextSettingName);
                const auto textValue = replacement.GetNamedString(KeyboardManagerConstants::NewTextSettingName);
                std::wstring trigger{ triggerValue.c_str(), triggerValue.size() };
                const std::wstring text{ textValue.c_str(), textValue.size() };
                DWORD triggerKey = VK_SPACE;
                if (replacement.HasKey(KeyboardManagerConstants::TextReplacementTriggerKeySettingName))
                {
                    const double triggerKeyValue = replacement.GetNamedNumber(KeyboardManagerConstants::TextReplacementTriggerKeySettingName);
                    if (triggerKeyValue != VK_TAB && triggerKeyValue != VK_RETURN && triggerKeyValue != VK_SPACE)
                    {
                        Logger::error(L"Invalid text replacement trigger key at index {}. Try the next replacement.", index);
                        result = false;
                        continue;
                    }

                    triggerKey = static_cast<DWORD>(triggerKeyValue);
                }
                else if (trigger.length() > 1 && trigger.back() == L' ')
                {
                    trigger.pop_back();
                }

                if (!AddTextReplacement(trigger, text, triggerKey))
                {
                    Logger::error(L"Invalid text replacement at index {}. Try the next replacement.", index);
                    result = false;
                }
            }
            catch (...)
            {
                Logger::error(L"Improper text replacement JSON at index {}. Try the next replacement.", index);
                result = false;
            }
        }
    }
    catch (...)
    {
        Logger::error(L"Improper JSON format for text replacements. Skip to next remap type");
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
        result = LoadTextReplacements(*configFile) && result;

        return result;
    }
    catch (...)
    {
        Logger::error(L"SettingsHelper::LoadSettings() failed");
    }

    return false;
}

// Save the updated configuration.
bool MappingConfiguration::SaveSettingsToFile() try
{
    bool result = true;
    json::JsonObject configJson;
    json::JsonObject remapShortcuts;
    json::JsonObject remapShortcutsToText;

    json::JsonObject remapKeys;
    json::JsonObject remapKeysToText;
    json::JsonObject textReplacementsJson;

    json::JsonArray inProcessRemapKeysArray;
    json::JsonArray inProcessRemapKeysToTextArray;
    json::JsonArray inProcessTextReplacementsArray;

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

    for (const auto& [trigger, value] : textReplacements)
    {
        json::JsonObject replacement;
        replacement.SetNamedValue(KeyboardManagerConstants::TriggerTextSettingName, json::value(trigger));
        replacement.SetNamedValue(KeyboardManagerConstants::NewTextSettingName, json::value(value.text));
        replacement.SetNamedValue(KeyboardManagerConstants::TextReplacementTriggerKeySettingName, json::value(value.triggerKey));
        inProcessTextReplacementsArray.Append(replacement);
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
    textReplacementsJson.SetNamedValue(KeyboardManagerConstants::InProcessRemapKeysSettingName, inProcessTextReplacementsArray);
    configJson.SetNamedValue(KeyboardManagerConstants::RemapKeysSettingName, remapKeys);
    configJson.SetNamedValue(KeyboardManagerConstants::RemapKeysToTextSettingName, remapKeysToText);
    configJson.SetNamedValue(KeyboardManagerConstants::TextReplacementsSettingName, textReplacementsJson);
    configJson.SetNamedValue(KeyboardManagerConstants::RemapShortcutsSettingName, remapShortcuts);
    configJson.SetNamedValue(KeyboardManagerConstants::RemapShortcutsToTextSettingName, remapShortcutsToText);

    try
    {
        const std::wstring settingsFilePath = PTSettingsHelper::get_module_save_folder_location(KeyboardManagerConstants::ModuleName) + L"\\" + currentConfig + L".json";
        if (!WriteJsonAtomically(settingsFilePath, configJson))
        {
            result = false;
            Logger::error(L"Failed to save the settings");
        }
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
            if (SetEvent(hEvent))
            {
                Logger::trace(L"Signaled {} event", KeyboardManagerConstants::SettingsEventName);
            }
            else
            {
                result = false;
                Logger::error(L"Failed to signal {} event", KeyboardManagerConstants::SettingsEventName);
            }

            CloseHandle(hEvent);
        }
        else
        {
            result = false;
            Logger::error(L"Failed to signal {} event", KeyboardManagerConstants::SettingsEventName);
        }
    }

    return result;
}
catch (...)
{
    Logger::error(L"Failed to serialize or save the settings");
    return false;
}
