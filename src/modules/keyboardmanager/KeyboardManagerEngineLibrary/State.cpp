#include "pch.h"
#include "State.h"
#include <optional>

// Function to get the iterator of a single key remap given the source key. Returns nullopt if it isn't remapped
std::optional<SingleKeyRemapTable::iterator> State::GetSingleKeyRemap(const DWORD& originalKey)
{
    auto it = singleKeyReMap.find(originalKey);
    if (it != singleKeyReMap.end())
    {
        return it;
    }

    return std::nullopt;
}

std::optional<std::wstring> State::GetSingleKeyToTextRemapEvent(const DWORD originalKey) const
{
    if (auto it = singleKeyToTextReMap.find(originalKey); it != end(singleKeyToTextReMap))
    {
        return std::get<std::wstring>(it->second);
    }
    else
    {
        return std::nullopt;
    }
}

bool State::CheckShortcutRemapInvoked(const std::optional<std::wstring>& appName)
{
    // Assumes appName exists in the app-specific remap table
    ShortcutRemapTable& currentRemapTable = appName ? appSpecificShortcutReMap[*appName] : osLevelShortcutReMap;
    for (auto& it : currentRemapTable)
    {
        if (it.second.isShortcutInvoked)
        {
            return true;
        }
    }

    return false;
}

// Function to get the source and target of a shortcut remap given the source shortcut. Returns nullopt if it isn't remapped
ShortcutRemapTable& State::GetShortcutRemapTable(const std::optional<std::wstring>& appName)
{
    if (appName)
    {
        auto itTable = appSpecificShortcutReMap.find(*appName);
        if (itTable != appSpecificShortcutReMap.end())
        {
            return itTable->second;
        }
    }

    return osLevelShortcutReMap;
}

std::vector<Shortcut>& State::GetSortedShortcutRemapVector(const std::optional<std::wstring>& appName)
{
    // Assumes appName exists in the app-specific remap table
    return appName ? appSpecificShortcutReMapSortedKeys[*appName] : osLevelShortcutReMapSortedKeys;
}

// Sets the activated target application in app-specific shortcut
void State::SetActivatedApp(const std::wstring& appName)
{
    activatedAppSpecificShortcutTarget = appName;
}

// Gets the activated target application in app-specific shortcut
std::wstring State::GetActivatedApp()
{
    return activatedAppSpecificShortcutTarget;
}
