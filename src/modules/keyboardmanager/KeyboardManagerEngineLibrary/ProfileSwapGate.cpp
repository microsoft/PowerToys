#include "pch.h"
#include "ProfileSwapGate.h"

#include <algorithm>

#include <keyboardmanager/common/Helpers.h>

ProfileSwapGate::ProfileSwapGate(NowFunction now, AsyncKeyDownFunction asyncKeyDown) :
    m_now(std::move(now)), m_asyncKeyDown(std::move(asyncKeyDown))
{
}

void ProfileSwapGate::SetAppliedProfile(const std::wstring& profile)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    m_appliedProfile = profile;
}

std::wstring ProfileSwapGate::AppliedProfile() const
{
    std::lock_guard<std::mutex> lock(m_mutex);
    return m_appliedProfile;
}

ProfileSwapGate::OfferResult ProfileSwapGate::Offer(const MappingConfiguration::LoadOutcome outcome, std::unique_ptr<State> configuration, const ApplyInPlaceFunction& applyInPlace, const std::function<void(const std::wstring&)>& onStaged)
{
    // A transient failure says nothing about what the file holds: keep the live tables and leave any
    // pending stage alone (the caller arranges a retry).
    if (outcome == MappingConfiguration::LoadOutcome::Failed || !configuration)
    {
        return OfferResult::Discarded;
    }

    // Objects released below hold tables; destroy them after the lock is dropped.
    std::unique_ptr<State> replaced;
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        if (!m_staged && configuration->currentConfig == m_appliedProfile)
        {
            // Same profile, nothing pending: an edit of the live profile. Keep the existing behaviour
            // and apply it right away (in place) instead of waiting for an idle key-down.
            applyInPlace(*configuration);
            replaced = std::move(configuration);
        }
        else
        {
            // A profile change, or a reload that supersedes a pending one (including a switch back to
            // the applied profile, which must cancel the pending switch through the same gate).
            if (!m_staged)
            {
                m_stagedSince = m_now();
            }

            replaced = std::move(m_staged);
            m_staged = std::move(configuration);
            m_hasStage.store(true);
            if (onStaged)
            {
                onStaged(m_staged->currentConfig);
            }

            return OfferResult::Staged;
        }
    }

    return OfferResult::AppliedInPlace;
}

bool ProfileSwapGate::HasStage() const noexcept
{
    return m_hasStage.load();
}

std::wstring ProfileSwapGate::StagedProfile() const
{
    std::lock_guard<std::mutex> lock(m_mutex);
    return m_staged ? m_staged->currentConfig : std::wstring{};
}

uint32_t ProfileSwapGate::MakeKeyId(const DWORD scanCode, const bool extended, const DWORD vkCode) noexcept
{
    if (scanCode != 0)
    {
        return (extended ? 0x10000u : 0u) | (scanCode & 0xFFFFu);
    }

    return 0x80000000u | (vkCode & 0xFFFFu);
}

void ProfileSwapGate::PruneStaleKeys(const TimePoint now, const uint32_t keepId)
{
    for (auto it = m_heldKeys.begin(); it != m_heldKeys.end();)
    {
        const HeldKey& key = it->second;

        // Only a key whose key-down reached the system can be checked with GetAsyncKeyState; a key the
        // hook suppressed is assumed not to be reflected there, so it is never dropped as stale (a lost
        // key-up for such a key is bounded by the forced swap instead). Neither is a key for which an
        // injected key-up was seen while it was held. The key of the current event is kept: it just
        // produced an event, so it is not silent.
        const bool canDrop = it->first != keepId && !key.suppressed && !key.asyncStateUnreliable && now - key.lastEvent >= HeldKeyStaleTimeout && !m_asyncKeyDown(key.vkCode);
        it = canDrop ? m_heldKeys.erase(it) : std::next(it);
    }
}

void ProfileSwapGate::TryApplyStage(const TimePoint now, const HandleFunction& handle, const AdoptFunction& adopt)
{
    std::unique_ptr<State> staged;
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        if (!m_staged)
        {
            return;
        }

        bool forced = false;
        if (!m_heldKeys.empty())
        {
            if (now - m_stagedSince < ForcedSwapTimeout)
            {
                return;
            }

            for (const auto& [id, key] : m_heldKeys)
            {
                if (now - key.lastEvent < ForcedSwapTimeout)
                {
                    return;
                }
            }

            forced = true;
        }

        if (forced)
        {
            // Release, under the old profile, everything it did for the keys still believed held:
            // replay each key-up (most recently pressed first) through the old handlers, which injects
            // e.g. the remap target's key-up. Keys that are orphans already belong to an even older
            // profile and keep their recorded action.
            std::vector<HeldKey*> toRelease;
            for (auto& [id, key] : m_heldKeys)
            {
                if (!key.orphan)
                {
                    toRelease.push_back(&key);
                }
            }

            std::sort(toRelease.begin(), toRelease.end(), [](const HeldKey* a, const HeldKey* b) { return a->sequence > b->sequence; });

            for (HeldKey* key : toRelease)
            {
                KBDLLHOOKSTRUCT upData = key->downData;
                upData.flags |= LLKHF_UP;
                LowlevelKeyboardEvent upEvent{};
                upEvent.lParam = &upData;
                upEvent.wParam = key->downMessage == WM_SYSKEYDOWN ? WM_SYSKEYUP : WM_KEYUP;

                // What the old profile did with this key-up is what happens to the real one later.
                key->suppressed = handle(&upEvent) == 1;
                key->orphan = true;
            }
        }

        staged = std::move(m_staged);
        m_hasStage.store(false);
        m_appliedProfile = staged->currentConfig;
        adopt(std::move(staged));
    }
}

intptr_t ProfileSwapGate::OnPhysicalKeyEvent(LowlevelKeyboardEvent* event, const HandleFunction& handle, const AdoptFunction& adopt)
{
    const bool keyDown = event->wParam == WM_KEYDOWN || event->wParam == WM_SYSKEYDOWN;
    const TimePoint now = m_now();
    const DWORD rawVkCode = Helpers::ClearKeyNumpadOrigin(event->lParam->vkCode);
    const uint32_t id = MakeKeyId(event->lParam->scanCode, (event->lParam->flags & LLKHF_EXTENDED) != 0, rawVkCode);

    PruneStaleKeys(now, id);

    auto found = m_heldKeys.find(id);
    if (found != m_heldKeys.end() && found->second.orphan)
    {
        // Pressed under an earlier profile: never consult the current one. Its auto-repeats and its
        // key-up get the action recorded for it.
        const intptr_t result = found->second.suppressed ? 1 : 0;
        if (keyDown)
        {
            found->second.lastEvent = now;
        }
        else
        {
            m_heldKeys.erase(found);
        }

        return result;
    }

    // A key-down of a key that is not held is a fresh press (an auto-repeat is a key-down of a key
    // already in the set). Only a fresh press may apply a pending stage.
    if (keyDown && found == m_heldKeys.end() && m_hasStage.load())
    {
        TryApplyStage(now, handle, adopt);
    }

    // Copy before the handlers run: they may rewrite vkCode (numpad/Shift handling).
    const KBDLLHOOKSTRUCT eventData = *event->lParam;
    const intptr_t result = handle(event);

    if (keyDown)
    {
        auto [it, inserted] = m_heldKeys.try_emplace(id);
        if (inserted)
        {
            it->second.vkCode = rawVkCode;
            it->second.downData = eventData;
            it->second.downMessage = event->wParam;
            it->second.suppressed = result == 1;
            it->second.sequence = ++m_sequence;
        }

        it->second.lastEvent = now;
    }
    else
    {
        m_heldKeys.erase(id);
    }

    return result;
}

void ProfileSwapGate::OnInjectedKeyEvent(const LowlevelKeyboardEvent* event) noexcept
{
    if (m_heldKeys.empty() || (event->wParam != WM_KEYUP && event->wParam != WM_SYSKEYUP))
    {
        return;
    }

    const DWORD injectedVkCode = Helpers::ClearKeyNumpadOrigin(event->lParam->vkCode);
    const auto sameKey = [injectedVkCode](const DWORD heldVkCode) {
        if (heldVkCode == injectedVkCode)
        {
            return true;
        }

        // A generic modifier stands for both of its left/right keys.
        switch (injectedVkCode)
        {
        case VK_CONTROL:
            return heldVkCode == VK_LCONTROL || heldVkCode == VK_RCONTROL;
        case VK_MENU:
            return heldVkCode == VK_LMENU || heldVkCode == VK_RMENU;
        case VK_SHIFT:
            return heldVkCode == VK_LSHIFT || heldVkCode == VK_RSHIFT;
        case VK_LCONTROL:
        case VK_RCONTROL:
            return heldVkCode == VK_CONTROL;
        case VK_LMENU:
        case VK_RMENU:
            return heldVkCode == VK_MENU;
        case VK_LSHIFT:
        case VK_RSHIFT:
            return heldVkCode == VK_SHIFT;
        default:
            return false;
        }
    };

    for (auto& [id, key] : m_heldKeys)
    {
        if (sameKey(key.vkCode))
        {
            key.asyncStateUnreliable = true;
        }
    }
}

void ProfileSwapGate::SeedOrphans(const std::vector<KeySnapshot>& keysDown, const TimePoint now)
{
    m_heldKeys.clear();
    for (const KeySnapshot& snapshot : keysDown)
    {
        HeldKey key;
        key.vkCode = snapshot.vkCode;
        key.downData.vkCode = snapshot.vkCode;
        key.downData.scanCode = snapshot.scanCode;
        key.downData.flags = snapshot.extended ? LLKHF_EXTENDED : 0;
        key.suppressed = false; // pass through: no hook was there to remap its key-down
        key.orphan = true;
        key.lastEvent = now;
        key.sequence = ++m_sequence;
        m_heldKeys[MakeKeyId(snapshot.scanCode, snapshot.extended, snapshot.vkCode)] = key;
    }
}

bool ProfileSwapGate::ApplyStageWithoutHook(const std::vector<KeySnapshot>& keysDown, const AdoptFunction& adopt)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    if (!m_staged)
    {
        return false;
    }

    SeedOrphans(keysDown, m_now());

    std::unique_ptr<State> staged = std::move(m_staged);
    m_hasStage.store(false);
    m_appliedProfile = staged->currentConfig;
    adopt(std::move(staged));
    return true;
}

void ProfileSwapGate::ResetHeldKeys(const std::vector<KeySnapshot>& keysDown)
{
    SeedOrphans(keysDown, m_now());
}

size_t ProfileSwapGate::HeldKeyCount() const noexcept
{
    return m_heldKeys.size();
}

size_t ProfileSwapGate::OrphanKeyCount() const noexcept
{
    return static_cast<size_t>(std::count_if(m_heldKeys.begin(), m_heldKeys.end(), [](const auto& entry) { return entry.second.orphan; }));
}
