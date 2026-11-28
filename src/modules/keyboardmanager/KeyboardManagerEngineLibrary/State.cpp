#include "pch.h"
#include "State.h"
#include <algorithm>
#include <climits>
#include <optional>
#include <keyboardmanager/common/Helpers.h>

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

void State::StageSequenceTable()
{
    // Build the finished table before taking the lock, and free a table that was staged but never
    // published after releasing it.
    std::shared_ptr<const SingleKeyToSequenceRemapTable> staged = std::make_shared<const SingleKeyToSequenceRemapTable>(singleKeyToSequenceReMap);
    {
        std::lock_guard lock{ stagedSequenceTableMutex };
        stagedSequenceTable.swap(staged);
        sequenceTableStaged = true;
    }
}

bool State::PublishStagedSequenceTable()
{
    // The hook gets here at every key event: nothing staged is the usual case, and costs this read.
    if (!sequenceTableStaged)
    {
        return false;
    }

    std::shared_ptr<const SingleKeyToSequenceRemapTable> staged;
    {
        std::lock_guard lock{ stagedSequenceTableMutex };
        staged.swap(stagedSequenceTable);
        sequenceTableStaged = false;
    }

    if (!staged)
    {
        return false;
    }

    // The previous table is freed here, or by a hook call that still holds it (see GetLiveSequenceTable).
    // The press records are kept as they are: the hook tracks every press whether or not there are
    // key-sequence remaps, so a key that is held now has its record, and a remap for it waits for
    // its next press.
    liveSequenceTable = std::move(staged);
    return true;
}

void State::StartSequencePressTracking(KeyboardManagerInput::InputInterface& ii)
{
    PublishStagedSequenceTable();

    // No event was seen while there was no hook. Keys that are down now already reached the
    // system (no hook of ours could suppress them), so the key state tells which ones they are.
    sequencePresses.Reset();
    sequencePresses.SeedForwardedPresses(ii);
}

std::shared_ptr<const SingleKeyToSequenceRemapTable> State::GetLiveSequenceTable() const
{
    return liveSequenceTable;
}

bool State::HasLiveSequenceRemaps() const noexcept
{
    return !liveSequenceTable->empty();
}

SequencePressTracker& State::SequencePresses() noexcept
{
    return sequencePresses;
}

namespace
{
    uint64_t KeyIdOf(const KBDLLHOOKSTRUCT& key) noexcept
    {
        return SequencePressTracker::MakeKeyId(key.scanCode, (key.flags & LLKHF_EXTENDED) != 0, key.vkCode);
    }

    // The virtual key without the numpad-origin marker the hook procedure adds to it.
    DWORD PlainVirtualKey(const KBDLLHOOKSTRUCT& key) noexcept
    {
        return Helpers::ClearKeyNumpadOrigin(key.vkCode);
    }
}

uint64_t SequencePressTracker::MakeKeyId(const DWORD scanCode, const bool extended, const DWORD vkCode) noexcept
{
    if (scanCode != 0)
    {
        // Bit 32 keeps the scan-code ids apart from the virtual-key ids below.
        return (uint64_t{ 1 } << 32) | (extended ? 0x10000u : 0u) | (scanCode & 0xFFFFu);
    }

    // No scan code: fall back to the virtual key as the hook reports it (with its numpad-origin marker).
    return vkCode;
}

DWORD SequencePressTracker::SlowedRepeatLimit(KeyboardManagerInput::InputInterface& ii)
{
    // Twice the longest gap, as for the ordinary settings. Capped far below the wrap-around range.
    constexpr DWORD largestRepeatMs = 0x10000000;
    const DWORD repeatMs = ii.GetFilterKeysRepeatMs();
    return repeatMs < largestRepeatMs ? 2 * repeatMs : 2 * largestRepeatMs;
}

std::optional<SequencePressTracker::Disposition> SequencePressTracker::FindPress(const KBDLLHOOKSTRUCT& key, KeyboardManagerInput::InputInterface& ii)
{
    const uint64_t id = KeyIdOf(key);
    if (const auto it = m_presses.find(id); it != m_presses.end())
    {
        // The time is a tick count that wraps around, so the age is the unsigned difference. A
        // difference in the upper half of the range is a time stamp a little older than the last
        // one (events are not always stamped in the order they arrive), not a gap of weeks.
        const DWORD age = key.time - it->second.lastEventTime;
        if (age <= StalePressAfterMs || age > static_cast<DWORD>(INT_MAX) || age <= SlowedRepeatLimit(ii))
        {
            it->second.lastEventTime = key.time;
            return it->second.disposition;
        }

        // Too late for an auto-repeat: the key-up of the recorded press never arrived, and this is
        // a new press.
        m_presses.erase(it);
    }

    if (!m_seededKeys.empty())
    {
        // A seeded key that the system no longer reports as down was released without an event the
        // hook tracks (for example it was down because Keyboard Manager itself had sent it). The hook
        // runs before the key state changes, so a held key that repeats is still reported as down
        // here, and a key that is pressed again is not.
        std::erase_if(m_seededKeys, [&ii](const DWORD vk) { return !ii.GetVirtualKeyState(static_cast<int>(vk)); });

        if (m_seededKeys.erase(PlainVirtualKey(key)) > 0)
        {
            // First event of a key that was down when tracking started: from here on it is known by
            // its own identity.
            m_presses.insert_or_assign(id, Press{ Disposition::Forwarded, key.time, key.vkCode });
            return Disposition::Forwarded;
        }
    }

    return std::nullopt;
}

void SequencePressTracker::RecordPress(const KBDLLHOOKSTRUCT& key, const Disposition disposition)
{
    m_presses.insert_or_assign(KeyIdOf(key), Press{ disposition, key.time, key.vkCode });
}

std::optional<SequencePressTracker::Disposition> SequencePressTracker::ReleasePress(const KBDLLHOOKSTRUCT& key)
{
    const bool seeded = !m_seededKeys.empty() && m_seededKeys.erase(PlainVirtualKey(key)) > 0;

    if (const auto it = m_presses.find(KeyIdOf(key)); it != m_presses.end())
    {
        const Disposition disposition = it->second.disposition;
        m_presses.erase(it);
        return disposition;
    }

    if (seeded)
    {
        return Disposition::Forwarded;
    }

    // No press with this scan code. The key may have been pressed under another one (Print Screen
    // changes its scan code while Alt is down), or this key-up has none: end the press that began
    // with the same virtual key.
    const auto sameVirtualKey = std::find_if(m_presses.begin(), m_presses.end(), [&key](const auto& press) {
        return press.second.virtualKey == key.vkCode;
    });
    if (sameVirtualKey != m_presses.end())
    {
        const Disposition disposition = sameVirtualKey->second.disposition;
        m_presses.erase(sameVirtualKey);
        return disposition;
    }

    return std::nullopt;
}

void SequencePressTracker::SeedForwardedPresses(KeyboardManagerInput::InputInterface& ii)
{
    for (DWORD vk = 1; vk < 0xFF; vk++)
    {
        // Skip the mouse buttons, and the Shift/Ctrl/Alt codes that do not say which side is down:
        // the hook reports the left and right keys, which are checked on their own.
        if (vk == VK_LBUTTON || vk == VK_RBUTTON || vk == VK_MBUTTON || vk == VK_XBUTTON1 || vk == VK_XBUTTON2 ||
            vk == VK_SHIFT || vk == VK_CONTROL || vk == VK_MENU)
        {
            continue;
        }

        if (ii.GetVirtualKeyState(static_cast<int>(vk)))
        {
            m_seededKeys.insert(vk);
        }
    }
}

void SequencePressTracker::Reset() noexcept
{
    m_presses.clear();
    m_seededKeys.clear();
}

bool SequencePressTracker::Empty() const noexcept
{
    return m_presses.empty() && m_seededKeys.empty();
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
