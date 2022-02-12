#include "pch.h"
#include "State.h"

inline KeyShortcutUnion GetRemappedKey(const SingleKeyRemapTable& table, DWORD originalKey)
{
    if (const auto it = table.find(originalKey); it != table.end())
    {
        return it->second;
    }
    
    return KeyboardManagerConstants::VK_NULL;
}

// Function to get key events for a single key remap given the source key event.
SingleKeyRemapResult State::GetSingleKeyRemapResult(DWORD originalKeyCode, bool isKeyDown)
{
    const auto universalRemapKey = GetRemappedKey(alwaysSingleKeyRemapTable, originalKeyCode);
    const auto combinationRemapKey = GetRemappedKey(combinationSingleKeyRemapTable, originalKeyCode);
    const auto aloneRemapKey = GetRemappedKey(aloneSingleKeyRemapTable, originalKeyCode);
    const auto isUniversalRemapKeyValid = IsValidSingleKeyOrShortcut(universalRemapKey);
    const auto isCombinationRemapKeyValid = IsValidSingleKeyOrShortcut(combinationRemapKey);
    const auto isAloneRemapKeyValid = IsValidSingleKeyOrShortcut(aloneRemapKey);
    const auto originalKey = KeyShortcutUnion{ originalKeyCode };
    const auto& effectiveCombinationRemapKey =
        isCombinationRemapKeyValid ? combinationRemapKey :
        isUniversalRemapKeyValid ? universalRemapKey :
        originalKey;
    const auto& effectiveAloneRemapKey =
        isAloneRemapKeyValid ? aloneRemapKey :
        isUniversalRemapKeyValid ? universalRemapKey :
        originalKey;
    const auto isDualRemap = (isCombinationRemapKeyValid || isAloneRemapKeyValid) && (effectiveCombinationRemapKey != effectiveAloneRemapKey);

    auto keyEvents = std::vector<RemappedKeyEvent>{};

    // Another key is pressed while a dual-remapped key is pressed.
    if (isKeyDown && (pendingPressedKeyCode != KeyboardManagerConstants::VK_NULL) && (pendingPressedKeyCode != originalKeyCode))
    {
        keyEvents.insert(keyEvents.end(), pendingCombinationRemappedKeyEvents.cbegin(), pendingCombinationRemappedKeyEvents.cend());
        pendingCombinationRemappedKeyEvents.clear();
        pendingPressedKeyCode = KeyboardManagerConstants::VK_NULL;
    }

    if (isDualRemap)
    {
        if (isKeyDown)
        {
            // Delay the key press of a dual-mapped key.
            // We cannot tell which remap to use until the next key event.
            pendingPressedKeyCode = originalKeyCode;
            pendingCombinationRemappedKeyEvents = {
                { effectiveCombinationRemapKey, true },
            };
        }
        else // Key Up
        {
            if (pendingPressedKeyCode == originalKeyCode)
            {
                // No key has been pressed while the dual-mapped key is pressed and released.
                // Inject a key press and release using the alone remap.
                pendingPressedKeyCode = KeyboardManagerConstants::VK_NULL;
                pendingCombinationRemappedKeyEvents.clear();
                keyEvents.emplace_back(effectiveAloneRemapKey, true /*isKeyDown*/);
                keyEvents.emplace_back(effectiveAloneRemapKey, false /*isKeyDown*/);
            }
            else
            {
                // One or more keys have been pressed while the dual-mapped key is pressed and released.
                // Inject a key release using the combination remap.
                // A remapped key press should have been injected when another key was pressed or released.
                keyEvents.emplace_back(effectiveCombinationRemapKey, false /*isKeyDown*/);
            }
        }

        return { true, keyEvents };
    }
    else if (isUniversalRemapKeyValid)
    {
        keyEvents.emplace_back(universalRemapKey, isKeyDown);
        return { true /*shouldEatOriginalKey*/, keyEvents };
    }
    else
    {
        return { false /*shouldEatOriginalKey*/, keyEvents };
    }
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
