#include "pch.h"
#include "MappingConfiguration.h"

#include <cmath>
#include <cstdint>
#include <fstream>
#include <limits>
#include <unordered_set>
#include <utility>

#include <common/SettingsAPI/settings_objects.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/interop/shared_constants.h>
#include <common/logger/logger.h>

#include "KeyboardManagerConstants.h"
#include "Shortcut.h"
#include "RemapShortcut.h"
#include "Helpers.h"

namespace
{
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

    constexpr bool IsCanonicalGuid(const std::wstring_view value)
    {
        if (value.size() != 36)
        {
            return false;
        }

        for (size_t index = 0; index < value.size(); ++index)
        {
            if (index == 8 || index == 13 || index == 18 || index == 23)
            {
                if (value[index] != L'-')
                {
                    return false;
                }
            }
            else if (!((value[index] >= L'0' && value[index] <= L'9') ||
                       (value[index] >= L'a' && value[index] <= L'f')))
            {
                return false;
            }
        }

        return true;
    }

    constexpr bool IsValidSourceText(const std::wstring_view text)
    {
        if (text.empty() || text.size() > KeyboardManagerConstants::MaxTextExpansionSourceLength || text.find(L'\0') != std::wstring_view::npos)
        {
            return false;
        }

        for (size_t index = 0; index < text.size(); ++index)
        {
            const auto codeUnit = static_cast<uint16_t>(text[index]);
            if (IsHighSurrogate(text[index]))
            {
                if (++index >= text.size() || !IsLowSurrogate(text[index]))
                {
                    return false;
                }
            }
            else if (IsLowSurrogate(text[index]) || codeUnit < 0x20 || (codeUnit >= 0x7F && codeUnit <= 0x9F))
            {
                return false;
            }
        }

        return true;
    }

    constexpr bool IsValidReplacementText(const std::wstring_view text)
    {
        if (text.empty() || text.size() > KeyboardManagerConstants::MaxTextExpansionReplacementLength || text.find(L'\0') != std::wstring_view::npos)
        {
            return false;
        }

        for (size_t index = 0; index < text.size(); ++index)
        {
            const auto codeUnit = static_cast<uint16_t>(text[index]);
            if (IsHighSurrogate(text[index]))
            {
                if (++index >= text.size() || !IsLowSurrogate(text[index]))
                {
                    return false;
                }
            }
            else if (IsLowSurrogate(text[index]) ||
                     (codeUnit < 0x20 && codeUnit != L'\r' && codeUnit != L'\n') ||
                     (codeUnit >= 0x7F && codeUnit <= 0x9F))
            {
                return false;
            }
        }

        return true;
    }

    bool IsValidActivation(const Shortcut& activation)
    {
        if (activation.actionKey == 0 || activation.HasChord())
        {
            return false;
        }

        auto normalizedActivation = activation;
        for (const auto key : normalizedActivation.GetKeyCodes())
        {
            if (key == 0 || (key > 0xFF && key != CommonSharedConstants::VK_WIN_BOTH))
            {
                return false;
            }
        }

        return true;
    }

    bool IsValidTextExpansionRule(const TextExpansionRule& rule)
    {
        return IsCanonicalGuid(rule.id) &&
               IsValidSourceText(rule.sourceText) &&
               IsValidActivation(rule.activation) &&
               IsValidReplacementText(rule.replacementText);
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

    bool SignalSettingsReload()
    {
        const HANDLE event = CreateEvent(nullptr, false, false, KeyboardManagerConstants::SettingsEventName.c_str());
        if (!event)
        {
            return false;
        }

        const bool signaled = SetEvent(event) != FALSE;
        CloseHandle(event);
        return signaled;
    }

    std::wstring GetSettingsFilePath(const std::wstring& configurationName)
    {
        return PTSettingsHelper::get_module_save_folder_location(KeyboardManagerConstants::ModuleName) + L"\\" + configurationName + L".json";
    }
}

MappingConfiguration::MappingConfiguration(SettingsWriter settingsWriter, SettingsReloadNotifier settingsReloadNotifier, SettingsPathProvider settingsPathProvider) :
    settingsWriter(settingsWriter ? std::move(settingsWriter) : SettingsWriter{ WriteJsonAtomically }),
    settingsReloadNotifier(settingsReloadNotifier ? std::move(settingsReloadNotifier) : SettingsReloadNotifier{ SignalSettingsReload }),
    settingsPathProvider(settingsPathProvider ? std::move(settingsPathProvider) : SettingsPathProvider{ GetSettingsFilePath })
{
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

void MappingConfiguration::ClearTextExpansions()
{
    textExpansions.clear();
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

bool MappingConfiguration::AddTextExpansion(const TextExpansionRule& rule)
{
    if (!IsValidTextExpansionRule(rule))
    {
        return false;
    }

    const auto existing = std::find_if(textExpansions.begin(), textExpansions.end(), [&](const TextExpansionRule& candidate) {
        return candidate.id == rule.id;
    });
    if (existing != textExpansions.end())
    {
        return false;
    }

    textExpansions.push_back(rule);
    return true;
}

bool MappingConfiguration::UpdateTextExpansion(
    const std::wstring_view id,
    const std::wstring& sourceText,
    const Shortcut& activation,
    const std::wstring& replacementText,
    const bool enabled)
{
    const auto existing = std::find_if(textExpansions.begin(), textExpansions.end(), [&](const TextExpansionRule& candidate) {
        return candidate.id == id;
    });
    if (existing == textExpansions.end())
    {
        return false;
    }

    TextExpansionRule updated{ existing->id, sourceText, activation, replacementText, enabled };
    if (!IsValidTextExpansionRule(updated))
    {
        return false;
    }

    *existing = std::move(updated);
    return true;
}

bool MappingConfiguration::DeleteTextExpansion(const std::wstring_view id)
{
    const auto existing = std::find_if(textExpansions.begin(), textExpansions.end(), [&](const TextExpansionRule& candidate) {
        return candidate.id == id;
    });
    if (existing == textExpansions.end())
    {
        return false;
    }

    textExpansions.erase(existing);
    return true;
}

bool MappingConfiguration::SetTextExpansionEnabled(const std::wstring_view id, const bool enabled)
{
    const auto existing = std::find_if(textExpansions.begin(), textExpansions.end(), [&](const TextExpansionRule& candidate) {
        return candidate.id == id;
    });
    if (existing == textExpansions.end())
    {
        return false;
    }

    existing->enabled = enabled;
    return true;
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

bool MappingConfiguration::LoadTextExpansions(const json::JsonObject& jsonData)
{
    ClearTextExpansions();

    if (!jsonData.HasKey(KeyboardManagerConstants::TextReplacementsSettingName))
    {
        return true;
    }

    if (!json::has(jsonData, KeyboardManagerConstants::TextReplacementsSettingName, json::JsonValueType::Object))
    {
        Logger::error(L"Improper JSON format for text expansions.");
        return false;
    }

    const auto textReplacementsData = jsonData.GetNamedObject(KeyboardManagerConstants::TextReplacementsSettingName);
    if (!json::has(textReplacementsData, KeyboardManagerConstants::InProcessRemapKeysSettingName, json::JsonValueType::Array))
    {
        Logger::error(L"Text expansions section is missing the inProcess array.");
        return false;
    }

    bool result = true;
    const auto inProcessTextExpansions = textReplacementsData.GetNamedArray(KeyboardManagerConstants::InProcessRemapKeysSettingName);
    for (uint32_t index = 0; index < inProcessTextExpansions.Size(); ++index)
    {
        try
        {
            const auto value = inProcessTextExpansions.GetAt(index);
            if (value.ValueType() != json::JsonValueType::Object)
            {
                result = false;
                continue;
            }

            const auto object = value.GetObjectW();
            if (!json::has(object, KeyboardManagerConstants::TextExpansionIdSettingName, json::JsonValueType::String) ||
                !json::has(object, KeyboardManagerConstants::TextExpansionSourceTextSettingName, json::JsonValueType::String) ||
                !json::has(object, KeyboardManagerConstants::TextExpansionActivationKeysSettingName, json::JsonValueType::Array) ||
                !json::has(object, KeyboardManagerConstants::TextExpansionReplacementTextSettingName, json::JsonValueType::String) ||
                !json::has(object, KeyboardManagerConstants::TextExpansionEnabledSettingName, json::JsonValueType::Boolean))
            {
                result = false;
                continue;
            }

            const auto idValue = object.GetNamedString(KeyboardManagerConstants::TextExpansionIdSettingName);
            const auto sourceValue = object.GetNamedString(KeyboardManagerConstants::TextExpansionSourceTextSettingName);
            const auto replacementValue = object.GetNamedString(KeyboardManagerConstants::TextExpansionReplacementTextSettingName);
            const auto activationValues = object.GetNamedArray(KeyboardManagerConstants::TextExpansionActivationKeysSettingName);

            std::vector<int32_t> activationKeys;
            activationKeys.reserve(activationValues.Size());
            std::unordered_set<uint32_t> uniqueKeys;
            bool validActivationArray = activationValues.Size() > 0;
            for (uint32_t keyIndex = 0; keyIndex < activationValues.Size() && validActivationArray; ++keyIndex)
            {
                const auto keyValue = activationValues.GetAt(keyIndex);
                if (keyValue.ValueType() != json::JsonValueType::Number)
                {
                    validActivationArray = false;
                    break;
                }

                const double numericKey = keyValue.GetNumber();
                if (!std::isfinite(numericKey) || numericKey != std::trunc(numericKey) || numericKey <= 0 ||
                    numericKey > static_cast<double>((std::numeric_limits<int32_t>::max)()))
                {
                    validActivationArray = false;
                    break;
                }

                const auto key = static_cast<uint32_t>(numericKey);
                if ((key > 0xFF && key != CommonSharedConstants::VK_WIN_BOTH) || !uniqueKeys.insert(key).second)
                {
                    validActivationArray = false;
                    break;
                }

                activationKeys.push_back(static_cast<int32_t>(key));
            }

            if (!validActivationArray)
            {
                result = false;
                continue;
            }

            Shortcut activation{ activationKeys };
            if (!IsValidActivation(activation) || activation.GetKeyCodes().size() != activationKeys.size())
            {
                result = false;
                continue;
            }

            TextExpansionRule rule{
                std::wstring{ idValue.c_str(), idValue.size() },
                std::wstring{ sourceValue.c_str(), sourceValue.size() },
                std::move(activation),
                std::wstring{ replacementValue.c_str(), replacementValue.size() },
                object.GetNamedBoolean(KeyboardManagerConstants::TextExpansionEnabledSettingName),
            };
            if (!AddTextExpansion(rule))
            {
                result = false;
            }
        }
        catch (...)
        {
            Logger::error(L"Improper text expansion JSON at index {}. Try the next entry.", index);
            result = false;
        }
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
    return LoadSettingsWithResult() == MappingConfigurationLoadResult::Success;
}

MappingConfigurationLoadResult MappingConfiguration::LoadSettingsFromJson(const json::JsonObject& configFile)
{
    MappingConfiguration candidate{ settingsWriter, settingsReloadNotifier, settingsPathProvider };

    bool result = candidate.LoadSingleKeyRemaps(configFile);
    candidate.ClearOSLevelShortcuts();
    candidate.ClearAppSpecificShortcuts();
    result = candidate.LoadShortcutRemaps(configFile, KeyboardManagerConstants::RemapShortcutsSettingName) && result;
    result = candidate.LoadShortcutRemaps(configFile, KeyboardManagerConstants::RemapShortcutsToTextSettingName) && result;
    result = candidate.LoadSingleKeyToTextRemaps(configFile) && result;
    result = candidate.LoadTextExpansions(configFile) && result;
    if (!result)
    {
        return MappingConfigurationLoadResult::Partial;
    }

    singleKeyReMap = std::move(candidate.singleKeyReMap);
    scanMap = std::move(candidate.scanMap);
    singleKeyToTextReMap = std::move(candidate.singleKeyToTextReMap);
    textExpansions = std::move(candidate.textExpansions);
    osLevelShortcutReMap = std::move(candidate.osLevelShortcutReMap);
    osLevelShortcutReMapSortedKeys = std::move(candidate.osLevelShortcutReMapSortedKeys);
    appSpecificShortcutReMap = std::move(candidate.appSpecificShortcutReMap);
    appSpecificShortcutReMapSortedKeys = std::move(candidate.appSpecificShortcutReMapSortedKeys);

    return MappingConfigurationLoadResult::Success;
}

MappingConfigurationLoadResult MappingConfiguration::LoadSettingsFromFile(
    const std::wstring& configurationName,
    const std::wstring& filePath)
{
    const DWORD attributes = GetFileAttributesW(filePath.c_str());
    if (attributes == INVALID_FILE_ATTRIBUTES)
    {
        const DWORD error = GetLastError();
        if (error != ERROR_FILE_NOT_FOUND && error != ERROR_PATH_NOT_FOUND)
        {
            return MappingConfigurationLoadResult::Failure;
        }

        // A newly selected profile has no JSON file until its first save.
        ClearSingleKeyRemaps();
        ClearSingleKeyToTextRemaps();
        ClearTextExpansions();
        ClearOSLevelShortcuts();
        ClearAppSpecificShortcuts();
        currentConfig = configurationName;
        return MappingConfigurationLoadResult::Success;
    }

    if ((attributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
    {
        return MappingConfigurationLoadResult::Failure;
    }

    const auto configFile = json::from_file(filePath);
    if (!configFile)
    {
        return MappingConfigurationLoadResult::Failure;
    }

    const auto result = LoadSettingsFromJson(*configFile);
    if (result == MappingConfigurationLoadResult::Success)
    {
        currentConfig = configurationName;
    }
    return result;
}

MappingConfigurationLoadResult MappingConfiguration::LoadSettingsWithResult()
{
    Logger::trace(L"SettingsHelper::LoadSettings()");
    try
    {
        PowerToysSettings::PowerToyValues settings = PowerToysSettings::PowerToyValues::load_from_settings_file(KeyboardManagerConstants::ModuleName);
        auto current_config = settings.get_string_value(KeyboardManagerConstants::ActiveConfigurationSettingName);

        if (!current_config)
        {
            return MappingConfigurationLoadResult::Failure;
        }

        return LoadSettingsFromFile(*current_config, settingsPathProvider(*current_config));
    }
    catch (...)
    {
        Logger::error(L"SettingsHelper::LoadSettings() failed");
    }

    return MappingConfigurationLoadResult::Failure;
}

// Save the updated configuration.
bool MappingConfiguration::SaveSettingsToFile()
{
    return SaveSettingsToFileWithResult().settingsCommitted;
}

MappingConfigurationSaveResult MappingConfiguration::SaveSettingsToFileWithResult() try
{
    bool result = true;
    json::JsonObject configJson;
    json::JsonObject remapShortcuts;
    json::JsonObject remapShortcutsToText;

    json::JsonObject remapKeys;
    json::JsonObject remapKeysToText;
    json::JsonObject textReplacements;

    json::JsonArray inProcessRemapKeysArray;
    json::JsonArray inProcessRemapKeysToTextArray;
    json::JsonArray inProcessTextExpansionsArray;

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

    std::unordered_set<std::wstring> textExpansionIds;
    for (const auto& rule : textExpansions)
    {
        if (!IsValidTextExpansionRule(rule) || !textExpansionIds.insert(rule.id).second)
        {
            Logger::error(L"Refusing to save an invalid text expansion profile.");
            return {};
        }

        json::JsonObject entry;
        entry.SetNamedValue(KeyboardManagerConstants::TextExpansionIdSettingName, json::value(rule.id));
        entry.SetNamedValue(KeyboardManagerConstants::TextExpansionSourceTextSettingName, json::value(rule.sourceText));

        json::JsonArray activationKeys;
        auto activation = rule.activation;
        for (const auto key : activation.GetKeyCodes())
        {
            activationKeys.Append(json::value(key));
        }
        entry.SetNamedValue(KeyboardManagerConstants::TextExpansionActivationKeysSettingName, activationKeys);
        entry.SetNamedValue(KeyboardManagerConstants::TextExpansionReplacementTextSettingName, json::value(rule.replacementText));
        entry.SetNamedValue(KeyboardManagerConstants::TextExpansionEnabledSettingName, json::value(rule.enabled));
        inProcessTextExpansionsArray.Append(entry);
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
    textReplacements.SetNamedValue(KeyboardManagerConstants::InProcessRemapKeysSettingName, inProcessTextExpansionsArray);
    configJson.SetNamedValue(KeyboardManagerConstants::RemapKeysSettingName, remapKeys);
    configJson.SetNamedValue(KeyboardManagerConstants::RemapKeysToTextSettingName, remapKeysToText);
    configJson.SetNamedValue(KeyboardManagerConstants::TextReplacementsSettingName, textReplacements);
    configJson.SetNamedValue(KeyboardManagerConstants::RemapShortcutsSettingName, remapShortcuts);
    configJson.SetNamedValue(KeyboardManagerConstants::RemapShortcutsToTextSettingName, remapShortcutsToText);

    try
    {
        if (!settingsWriter(settingsPathProvider(currentConfig), configJson))
        {
            result = false;
        }
    }
    catch (...)
    {
        result = false;
    }

    if (!result)
    {
        Logger::error(L"Failed to save the settings");
        return {};
    }

    bool reloadNotified = false;
    try
    {
        reloadNotified = settingsReloadNotifier();
        if (reloadNotified)
        {
            Logger::trace(L"Signaled {} event", KeyboardManagerConstants::SettingsEventName);
        }
        else
        {
            Logger::error(L"Failed to signal {} event", KeyboardManagerConstants::SettingsEventName);
        }
    }
    catch (...)
    {
        Logger::error(L"Failed to signal {} event", KeyboardManagerConstants::SettingsEventName);
    }

    return { true, reloadNotified };
}
catch (...)
{
    Logger::error(L"Failed to serialize or save the settings");
    return {};
}
