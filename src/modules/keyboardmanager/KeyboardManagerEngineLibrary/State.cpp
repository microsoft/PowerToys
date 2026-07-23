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

// Function to get the iterator of an "Alone" single key remap given the source key. Returns nullopt if it isn't remapped
std::optional<SingleKeyRemapTable::iterator> State::GetSingleKeyAloneRemap(const DWORD& originalKey)
{
    auto it = aloneSingleKeyReMap.find(originalKey);
    if (it != aloneSingleKeyReMap.end())
    {
        return it;
    }

    return std::nullopt;
}

void State::SetAlonePending(const DWORD key)
{
    aloneCombinationKeys.erase(key);
    alonePendingKeys.insert(key);
}

bool State::IsAlonePending(const DWORD key) const
{
    return alonePendingKeys.find(key) != alonePendingKeys.end();
}

void State::SetAloneCombination(const DWORD key)
{
    alonePendingKeys.erase(key);
    aloneCombinationKeys.insert(key);
}

bool State::IsAloneCombination(const DWORD key) const
{
    return aloneCombinationKeys.find(key) != aloneCombinationKeys.end();
}

void State::ClearAloneKeyState(const DWORD key)
{
    alonePendingKeys.erase(key);
    aloneCombinationKeys.erase(key);
}

void State::ClearAllAloneKeyState()
{
    alonePendingKeys.clear();
    aloneCombinationKeys.clear();
}

std::vector<DWORD> State::GetPendingAloneKeys() const
{
    return std::vector<DWORD>(alonePendingKeys.begin(), alonePendingKeys.end());
}

bool State::HasPendingAloneKeys() const
{
    return !alonePendingKeys.empty();
}

bool State::HasOtherHeldAloneKey(const DWORD except) const
{
    for (const DWORD key : alonePendingKeys)
    {
        if (key != except)
        {
            return true;
        }
    }

    for (const DWORD key : aloneCombinationKeys)
    {
        if (key != except)
        {
            return true;
        }
    }

    return false;
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

void State::SetSingleKeyRemapInjectionFailed(const DWORD sourceKey, const bool failed)
{
    if (failed)
    {
        singleKeyRemapInjectionFailedKeys.insert(sourceKey);
    }
    else
    {
        singleKeyRemapInjectionFailedKeys.erase(sourceKey);
    }
}

bool State::ConsumeSingleKeyRemapInjectionFailed(const DWORD sourceKey)
{
    return singleKeyRemapInjectionFailedKeys.erase(sourceKey) > 0;
}
