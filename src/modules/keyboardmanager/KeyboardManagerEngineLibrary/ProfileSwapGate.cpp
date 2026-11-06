#include "pch.h"
#include "ProfileSwapGate.h"

#include <algorithm>

#include <common/interop/shared_constants.h>
#include <keyboardmanager/common/Helpers.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>

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

ProfileSwapGate::OfferResult ProfileSwapGate::Offer(const MappingConfiguration::LoadOutcome outcome, std::unique_ptr<State> configuration, const ApplyInPlaceFunction& applyInPlace, const std::function<void(const std::wstring&)>& onRequested)
{
    // A transient failure says nothing about what the file holds: keep the live tables and leave any
    // pending stage alone (the caller arranges a retry). The engine is still heading to the staged
    // profile if there is one, so report that (not "nothing requested"): otherwise the tracker would
    // compare keyboards with the applied profile while the stage is still waiting to be applied.
    if (outcome == MappingConfiguration::LoadOutcome::Failed || !configuration)
    {
        ReportPendingProfile(onRequested);
        return OfferResult::Discarded;
    }

    // Objects released below hold tables; destroy them after the lock is dropped.
    std::unique_ptr<State> replaced;
    std::unique_ptr<State> cancelled;
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        const bool sameProfile = configuration->currentConfig == m_appliedProfile;

        // A stage of the applied profile itself (a switch away that was superseded by a switch back)
        // changes nothing a held key could depend on, so a reload of that profile does not wait for it.
        const bool stagedIsApplied = m_staged && m_staged->currentConfig == m_appliedProfile;
        if (sameProfile && (!m_staged || stagedIsApplied))
        {
            // Same profile, no switch to another profile pending: an edit of the live profile. Keep the
            // existing behaviour and apply it right away (in place) instead of waiting for an idle
            // key-down, cancelling the pending same-profile stage if there is one.
            cancelled = std::move(m_staged);
            m_hasStage.store(false);
            applyInPlace(*configuration);
            replaced = std::move(configuration);
            if (onRequested)
            {
                onRequested(std::wstring{});
            }
        }
        else
        {
            // A profile change, or a reload that supersedes a pending switch (including a switch back
            // to the applied profile, which must cancel the pending switch through the same gate).
            if (!m_staged)
            {
                m_stagedSince = m_now();
            }

            replaced = std::move(m_staged);
            m_staged = std::move(configuration);
            m_hasStage.store(true);
            if (onRequested)
            {
                onRequested(m_staged->currentConfig);
            }

            return OfferResult::Staged;
        }
    }

    return OfferResult::AppliedInPlace;
}

void ProfileSwapGate::ReportPendingProfile(const std::function<void(const std::wstring&)>& onRequested) const
{
    std::lock_guard<std::mutex> lock(m_mutex);
    if (onRequested)
    {
        onRequested(m_staged ? m_staged->currentConfig : std::wstring{});
    }
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

ProfileSwapGate::KeyEventSource ProfileSwapGate::ClassifyKeyEvent(const DWORD flags, const ULONG_PTR extraInfo) noexcept
{
    if (!(flags & LLKHF_INJECTED))
    {
        return KeyEventSource::Physical;
    }

    // Same test as the handlers' GeneratedByKBM, so "not tracked" and "ignored as KBM output by the
    // handlers" mean the same events.
    if (extraInfo & CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG)
    {
        return KeyEventSource::OwnInjected;
    }

    // Bit 0 is clear, so the handlers reprocess it like any other input; it is only not a new key.
    if (extraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_REPROCESS_FLAG)
    {
        return KeyEventSource::OwnReprocessed;
    }

    return KeyEventSource::ForeignInjected;
}

intptr_t ProfileSwapGate::OnHookEvent(LowlevelKeyboardEvent* event, const HandleFunction& handle, const AdoptFunction& adopt) noexcept
{
    // Read before the numpad-origin encoding below changes vkCode.
    const KeyEventSource source = ClassifyKeyEvent(event->lParam->flags, event->lParam->dwExtraInfo);
    event->lParam->vkCode = Helpers::EncodeKeyNumpadOrigin(event->lParam->vkCode, event->lParam->flags & LLKHF_EXTENDED);

    if (source != KeyEventSource::Physical)
    {
        OnInjectedKeyEvent(event);
    }

    if (source == KeyEventSource::OwnInjected || source == KeyEventSource::OwnReprocessed)
    {
        return handle(event);
    }

    try
    {
        return OnTrackedKeyEvent(event, handle, adopt);
    }
    catch (...)
    {
        // Bookkeeping failed (e.g. out of memory). OnTrackedKeyEvent does everything that can throw
        // before it hands the event to `handle`, so `handle` has not run for this event: passing it on
        // cannot deliver it twice.
        return 0;
    }
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

std::unordered_map<uint32_t, ProfileSwapGate::HeldKey>::iterator ProfileSwapGate::FindHeldKey(const uint32_t id, const DWORD rawVkCode)
{
    auto found = m_heldKeys.find(id);
    if (found != m_heldKeys.end())
    {
        // The key's own event: its id is now known to be right.
        found->second.estimatedScanCode = false;
        return found;
    }

    // No record has this id. Only a seeded orphan's id may be wrong, so only such a record may stand
    // for a key with another id.
    found = std::find_if(m_heldKeys.begin(), m_heldKeys.end(), [rawVkCode](const auto& entry) {
        return entry.second.orphan && entry.second.estimatedScanCode && entry.second.vkCode == rawVkCode;
    });
    if (found == m_heldKeys.end())
    {
        return found;
    }

    // Re-key it to the real id, so its later events match exactly and the fallback no longer applies.
    auto node = m_heldKeys.extract(found);
    node.key() = id;
    node.mapped().estimatedScanCode = false;
    return m_heldKeys.insert(std::move(node)).position;
}

intptr_t ProfileSwapGate::OnTrackedKeyEvent(LowlevelKeyboardEvent* event, const HandleFunction& handle, const AdoptFunction& adopt)
{
    const bool keyDown = event->wParam == WM_KEYDOWN || event->wParam == WM_SYSKEYDOWN;
    const TimePoint now = m_now();
    const DWORD rawVkCode = Helpers::ClearKeyNumpadOrigin(event->lParam->vkCode);
    const uint32_t id = MakeKeyId(event->lParam->scanCode, (event->lParam->flags & LLKHF_EXTENDED) != 0, rawVkCode);

    PruneStaleKeys(now, id);

    auto found = FindHeldKey(id, rawVkCode);
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

    // Record the key-down before the handlers run, so nothing after `handle` can throw (see the catch in
    // OnHookEvent). The entry is complete except for `suppressed`, which needs the handlers' answer.
    // `downData` is copied here, before the handlers may rewrite vkCode (numpad/Shift handling).
    bool inserted = false;
    if (keyDown)
    {
        auto [it, isNew] = m_heldKeys.try_emplace(id);
        inserted = isNew;
        if (inserted)
        {
            it->second.vkCode = rawVkCode;
            it->second.downData = *event->lParam;
            it->second.downMessage = event->wParam;
            it->second.sequence = ++m_sequence;
        }

        it->second.lastEvent = now;
    }

    const intptr_t result = handle(event);

    if (keyDown)
    {
        // Look it up again rather than keep the iterator: a test hook that delivers injected input
        // synchronously can re-enter the gate from `handle` and rehash the set.
        if (inserted)
        {
            if (auto it = m_heldKeys.find(id); it != m_heldKeys.end())
            {
                it->second.suppressed = result == 1;
            }
        }
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
        // The scan code is MapVirtualKey's guess, which can differ from what the hook reports for this
        // key: the numpad/non-numpad pairs (Enter, arrows, Home/End, PgUp/PgDn, Ins/Del, numpad /) and
        // also keys such as PrintScreen, Pause and NumLock. So every seeded key may be matched by
        // virtual key until its own event settles the id (see FindHeldKey; limitation R-1 in the header).
        key.estimatedScanCode = true;
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
