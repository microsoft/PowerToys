#pragma once
#include <atomic>
#include <chrono>
#include <cstdint>
#include <functional>
#include <memory>
#include <mutex>
#include <string>
#include <unordered_map>
#include <vector>

#include <common/hooks/LowlevelKeyboardEvent.h>
#include "State.h"

// Decides when a profile change may take effect, so that a physical key-down and its matching key-up
// are resolved under the same profile.
//
// A reload that changes the active profile is parsed off the hook thread into a separate State and
// handed to Offer(), which stages it. The low-level hook thread applies the staged configuration only
// at a physical key-down that is not an auto-repeat, and only while it sees no physical key held, so a
// key pressed under one profile is released under the same one. The recovery rules below bound how
// long a lost key-up can block a swap; a forced swap still resolves held keys by the old profile's
// rules, while a stale drop relies on GetAsyncKeyState being right. A newer stage replaces an
// older one that was not applied yet. A reload of the profile that is already applied (an editor edit)
// is applied in place right away, as before this gate existed, unless a stage is pending.
//
// An auto-repeat is recognised as a key-down of a key already in the held set. A held key that stops
// repeating (assumed: Windows repeats only the most recently pressed key) stays in the set until its
// key-up; GetAsyncKeyState is assumed not to reflect keys the hook suppressed. Neither assumption has
// been measured on a real machine yet.
//
// Held keys whose key-up is lost (secure desktop, sleep, unplug, a later hook swallowing it) would
// otherwise block the swap forever, so:
//  - a key whose key-down reached the system is dropped after HeldKeyStaleTimeout without events,
//    once GetAsyncKeyState also reports it up (not if an injected key-up of it was seen while held:
//    then GetAsyncKeyState no longer says anything about the physical key);
//  - when a stage has waited ForcedSwapTimeout and every held key has been silent that long, the swap
//    is forced at the next fresh key-down: each held key's key-up is first replayed through the
//    current (old) profile's handlers, which releases whatever that profile injected for it, and the
//    key becomes an "orphan". An orphan's later physical events are suppressed or passed through as
//    the old profile did with the replayed key-up, without consulting the new profile.
//
// Thread use: Offer / SetAppliedProfile / AppliedProfile / HasStage / StagedProfile from any thread; everything else
// on the low-level hook thread only (the thread that installs the hook and runs its message loop).
class ProfileSwapGate
{
public:
    using TimePoint = std::chrono::steady_clock::time_point;
    using NowFunction = std::function<TimePoint()>;

    // Returns true while the given virtual key is down according to GetAsyncKeyState (injectable for tests).
    using AsyncKeyDownFunction = std::function<bool(DWORD vkCode)>;

    // Runs the active profile's remap handlers on an event; returns 1 to suppress it, 0 to pass it on.
    using HandleFunction = std::function<intptr_t(LowlevelKeyboardEvent*)>;

    // Makes the staged configuration live (State::AdoptConfiguration). Receives ownership; after the
    // swap the object holds the previous tables, which the callee should free off the hook thread.
    using AdoptFunction = std::function<void(std::unique_ptr<State>)>;

    // Applies a same-profile reload in place (the pre-existing reload behaviour).
    using ApplyInPlaceFunction = std::function<void(State&)>;

    static constexpr std::chrono::seconds HeldKeyStaleTimeout{ 3 };
    static constexpr std::chrono::seconds ForcedSwapTimeout{ 30 };

    enum class OfferResult
    {
        // Transient load failure: nothing changed (current tables and any pending stage are kept).
        Discarded,

        // Reload of the already-applied profile with nothing staged: applied in place immediately.
        AppliedInPlace,

        // Staged; the hook thread applies it at the next safe key-down.
        Staged,
    };

    // A key reported down by GetAsyncKeyState when the hook is (re)installed.
    struct KeySnapshot
    {
        DWORD vkCode = 0;
        DWORD scanCode = 0;
        bool extended = false;
    };

    ProfileSwapGate(NowFunction now, AsyncKeyDownFunction asyncKeyDown);

    ProfileSwapGate(const ProfileSwapGate&) = delete;
    ProfileSwapGate& operator=(const ProfileSwapGate&) = delete;

    // The profile whose tables are live. Set once after the initial in-place load; updated by the gate
    // whenever it applies a stage.
    void SetAppliedProfile(const std::wstring& profile);
    std::wstring AppliedProfile() const;

    // Hand over the result of a reload. `configuration` must have been loaded into a fresh State;
    // for LoadOutcome::ConfirmedAbsent it is the (empty) configuration of a profile with no file.
    // `applyInPlace` and `onStaged` (given the staged profile name) run under the gate lock, so they
    // cannot interleave with a hook-thread swap.
    OfferResult Offer(MappingConfiguration::LoadOutcome outcome, std::unique_ptr<State> configuration, const ApplyInPlaceFunction& applyInPlace, const std::function<void(const std::wstring&)>& onStaged = nullptr);

    bool HasStage() const noexcept;

    // Name of the staged profile, or empty when nothing is staged.
    std::wstring StagedProfile() const;

    // Hook thread: process one physical (non-injected) keyboard event. `event->lParam->vkCode` is the
    // value the handlers see (numpad-origin encoded). Applies a pending stage first when allowed,
    // then either answers for an orphan key or calls `handle`. Returns 1 to suppress the event.
    // `handle` must not re-enter this function for a physical event.
    intptr_t OnPhysicalKeyEvent(LowlevelKeyboardEvent* event, const HandleFunction& handle, const AdoptFunction& adopt);

    // Hook thread: observe an injected (non-physical) event before it is handled. An injected key-up
    // for a key believed physically held (e.g. KBM releasing a held modifier while invoking a shortcut
    // or a text remap) makes GetAsyncKeyState report that key up while it is still held, so from then on
    // the key is never dropped as stale (only the forced swap can release it).
    void OnInjectedKeyEvent(const LowlevelKeyboardEvent* event) noexcept;

    // Hook thread, while no keyboard hook is installed (so no key was remapped): forget the held-key
    // set, record `keysDown` as pass-through orphans, and apply the pending stage now. Returns false
    // when nothing was staged.
    bool ApplyStageWithoutHook(const std::vector<KeySnapshot>& keysDown, const AdoptFunction& adopt);

    // Hook thread, when the hook is (re)installed: the held-key set is stale (events were not seen
    // while unhooked), so replace it with `keysDown` as pass-through orphans.
    void ResetHeldKeys(const std::vector<KeySnapshot>& keysDown);

    // Test/diagnostic accessors (hook thread).
    size_t HeldKeyCount() const noexcept;
    size_t OrphanKeyCount() const noexcept;

private:
    struct HeldKey
    {
        // Raw virtual key (numpad-origin bit cleared), for GetAsyncKeyState.
        DWORD vkCode = 0;

        // The key-down as the handlers saw it, for replaying its key-up on a forced swap.
        KBDLLHOOKSTRUCT downData{};
        WPARAM downMessage = WM_KEYDOWN;

        // For a normal key: whether the hook suppressed its first key-down (then the system never saw
        // it, GetAsyncKeyState cannot vouch for it, and it is never dropped as stale).
        // For an orphan: whether its remaining events are suppressed (true) or passed through (false).
        bool suppressed = false;

        bool orphan = false;

        // An injected key-up of this key was seen while it was held: GetAsyncKeyState no longer
        // reflects the physical key, so it is not dropped as stale.
        bool asyncStateUnreliable = false;
        TimePoint lastEvent{};
        uint64_t sequence = 0;
    };

    // Physical identity of a key: scan code + extended flag, which stays the same between a key's down
    // and up even when NumLock/Shift changes the reported virtual key. Falls back to the virtual key
    // when the scan code is 0.
    static uint32_t MakeKeyId(DWORD scanCode, bool extended, DWORD vkCode) noexcept;

    void PruneStaleKeys(TimePoint now, uint32_t keepId);
    void TryApplyStage(TimePoint now, const HandleFunction& handle, const AdoptFunction& adopt);
    void SeedOrphans(const std::vector<KeySnapshot>& keysDown, TimePoint now);

    NowFunction m_now;
    AsyncKeyDownFunction m_asyncKeyDown;

    // Guards the members below it up to the hook-thread-only section.
    mutable std::mutex m_mutex;
    std::unique_ptr<State> m_staged;
    TimePoint m_stagedSince{};
    std::wstring m_appliedProfile;
    std::atomic_bool m_hasStage{ false };

    // Hook-thread only.
    std::unordered_map<uint32_t, HeldKey> m_heldKeys;
    uint64_t m_sequence = 0;
};
