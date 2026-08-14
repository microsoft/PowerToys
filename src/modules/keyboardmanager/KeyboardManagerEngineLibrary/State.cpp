#include "pch.h"
#include "State.h"
#include <iterator>
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

const SingleKeyRemapPressState* State::GetSingleKeyRemapPressState(const DWORD sourceKey) const noexcept
{
    const auto press = singleKeyRemapPressStates.find(sourceKey);
    return press == singleKeyRemapPressStates.end() ? nullptr : &press->second;
}

SingleKeyRemapPressState* State::GetSingleKeyRemapPressState(const DWORD sourceKey) noexcept
{
    const auto press = singleKeyRemapPressStates.find(sourceKey);
    return press == singleKeyRemapPressStates.end() ? nullptr : &press->second;
}

void State::SetSingleKeyRemapPassthrough(const DWORD sourceKey)
{
    singleKeyRemapPressStates.insert_or_assign(sourceKey, SingleKeyRemapPressState{});
    singleKeyRemapActiveKeys.erase(sourceKey);
}

void State::SetSingleKeyRemapSuppressed(const DWORD sourceKey)
{
    SingleKeyRemapPressState press;
    press.owner = SingleKeyRemapPressOwner::Suppressed;
    singleKeyRemapPressStates.insert_or_assign(sourceKey, std::move(press));
    singleKeyRemapActiveKeys.erase(sourceKey);
}

void State::SetSingleKeyRemapTarget(const DWORD sourceKey, std::vector<INPUT> repeatEvents, std::vector<INPUT> releaseEvents)
{
    SingleKeyRemapPressState press;
    press.owner = SingleKeyRemapPressOwner::RemappedTarget;
    press.repeatEvents = std::move(repeatEvents);
    press.releaseEvents = std::move(releaseEvents);
    singleKeyRemapPressStates.insert_or_assign(sourceKey, std::move(press));
    singleKeyRemapActiveKeys.insert(sourceKey);
}

void State::SetSingleKeyRemapReleasePending(const DWORD sourceKey)
{
    if (auto* press = GetSingleKeyRemapPressState(sourceKey))
    {
        press->releasePending = true;
    }
}

void State::SetSingleKeyRemapSuppressedPhysicalPressHeld(const DWORD sourceKey, const bool held)
{
    if (auto* press = GetSingleKeyRemapPressState(sourceKey))
    {
        press->suppressedPhysicalPressHeld = held;
    }
}

void State::ClearSingleKeyRemapPressState(const DWORD sourceKey)
{
    singleKeyRemapPressStates.erase(sourceKey);
    singleKeyRemapActiveKeys.erase(sourceKey);
}

void State::ClearSingleKeyRemapPressStates()
{
    singleKeyRemapPressStates.clear();
    singleKeyRemapActiveKeys.clear();
}

bool State::HasSingleKeyRemapPressState(const DWORD sourceKey) const noexcept
{
    return singleKeyRemapPressStates.contains(sourceKey);
}

std::vector<DWORD> State::GetSingleKeyRemapReleasePendingKeys() const
{
    std::vector<DWORD> pendingKeys;
    for (const auto& [sourceKey, press] : singleKeyRemapPressStates)
    {
        if (press.owner == SingleKeyRemapPressOwner::RemappedTarget && press.releasePending)
        {
            pendingKeys.push_back(sourceKey);
        }
    }
    return pendingKeys;
}

void State::QueuePendingInputCleanup(std::vector<INPUT> cleanupEvents)
{
    if (cleanupEvents.empty())
    {
        return;
    }

    std::scoped_lock lock(pendingInputCleanupMutex);
    pendingInputCleanup.insert(
        pendingInputCleanup.end(),
        std::make_move_iterator(cleanupEvents.begin()),
        std::make_move_iterator(cleanupEvents.end()));
}

void State::PrependPendingInputCleanup(std::vector<INPUT> cleanupEvents)
{
    if (cleanupEvents.empty())
    {
        return;
    }

    std::scoped_lock lock(pendingInputCleanupMutex);
    pendingInputCleanup.insert(
        pendingInputCleanup.begin(),
        std::make_move_iterator(cleanupEvents.begin()),
        std::make_move_iterator(cleanupEvents.end()));
}

std::vector<INPUT> State::TakePendingInputCleanup()
{
    std::scoped_lock lock(pendingInputCleanupMutex);
    std::vector<INPUT> cleanupEvents = std::move(pendingInputCleanup);
    pendingInputCleanup.clear();
    return cleanupEvents;
}

void State::ClearPendingInputCleanup()
{
    std::scoped_lock lock(pendingInputCleanupMutex);
    pendingInputCleanup.clear();
}

bool State::HasPendingInputCleanup() const
{
    std::scoped_lock lock(pendingInputCleanupMutex);
    return !pendingInputCleanup.empty();
}
