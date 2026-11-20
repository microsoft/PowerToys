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

// Decides when a profile change may take effect, so that a key-down and its matching key-up are
// resolved under the same profile.
//
// A reload that changes the active profile is parsed off the hook thread into a separate State and
// handed to Offer(), which stages it. The low-level hook thread applies the staged configuration only
// at a tracked key-down that is not an auto-repeat, and only while it sees no tracked key held, so a
// key pressed under one profile is released under the same one. The recovery rules below bound how
// long a lost key-up can block a swap; a forced swap still resolves held keys by the old profile's
// rules, while a stale drop relies on GetAsyncKeyState being right. A newer stage replaces an
// older one that was not applied yet. A reload of the profile that is already applied (an editor edit)
// is applied in place right away, as before this gate existed, unless a switch to another profile is
// pending (a pending "switch" to the applied profile itself is cancelled by such a reload instead).
//
// Tracked keys are physical input and input injected by other software whose dwExtraInfo has bit 0
// (KEYBOARDMANAGER_INJECTED_FLAG) clear, e.g. a plain SendInput with dwExtraInfo 0: KBM's
// handlers remap both, so both need their down and up resolved under one profile. Any injected event
// with bit 0 set is treated as KBM output by the gate and by the handlers alike (GeneratedByKBM): not
// tracked and not remapped. That includes other programs that happen to use an odd dwExtraInfo (some
// tools tag their own SendInput that way; AutoHotkey's default tag is believed to be odd, not
// verified here), so their keys are neither remapped nor held back. See ClassifyKeyEvent.
//
// Known limitations (bounded by the stale-key and forced-swap rules below, not fixed):
//  - a held key is identified by scan code + extended flag only, so the same key held at once from
//    two sources (two keyboards, or a keyboard and another program's injection) shares one record:
//    the first key-up releases it while the other is still down (review L-3 / N-3);
//  - a key-up that is lost after the hook suppressed its key-down keeps the key "held" until the
//    forced swap (L-4);
//  - on a forced swap, key-ups the old profile injects while releasing held keys reach the hook
//    after the swap and are seen by the new profile's handlers (L-5);
//  - a key seeded from GetAsyncKeyState (the hook was reinstalled, or a stage applied without a hook,
//    while it was held and had stopped repeating) whose virtual key is one EncodeKeyNumpadOrigin
//    distinguishes (Enter, arrows, Home/End, PgUp/PgDn, Ins/Del, numpad /) is matched by virtual key until its own
//    event arrives, so the first event of the other key with that virtual key takes its record over;
//    the seeded key's key-up is then resolved by the new profile (same family as L-3; review R-1).
//
// KBM's re-sent key-downs (KEYBOARDMANAGER_REPROCESS_FLAG, matched by exact value, sent by KBM on
// key-downs only) are not tracked; a program that copies GetMessageExtraInfo() into its own injection
// could carry that value over, as it could KBM's other flags (review Nit-B).
//
// An auto-repeat is recognised as a key-down of a key already in the held set. A held key that stops
// repeating (assumed: Windows repeats only the most recently pressed key) stays in the set until its
// key-up; GetAsyncKeyState is assumed not to reflect keys the hook suppressed. Neither assumption has
// been measured on a real machine yet.
//
// Held keys whose key-up is lost (secure desktop, sleep, unplug, a later hook swallowing it, an
// injecting program that exits mid-press) would otherwise block the swap forever, so:
//  - a key whose key-down reached the system is dropped after HeldKeyStaleTimeout without events,
//    once GetAsyncKeyState also reports it up (not if an injected key-up of it was seen while held:
//    then GetAsyncKeyState no longer says anything about the physical key). Keys injected by other
//    software follow the same rule: an injected key-down the hook passes on is assumed to update
//    GetAsyncKeyState like a physical one, and one the hook suppressed is assumed not to, exactly as
//    for physical keys. An injected key never auto-repeats, so a silent injected key is normal; it
//    is only dropped once the system also says it is up, and otherwise waits for the forced swap;
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

    // Where a low-level keyboard event comes from, as far as the gate is concerned.
    enum class KeyEventSource
    {
        // No LLKHF_INJECTED: a real keyboard. Tracked.
        Physical,

        // LLKHF_INJECTED without KBM's marker: another program's SendInput. Tracked, like Physical.
        ForeignInjected,

        // LLKHF_INJECTED and dwExtraInfo has KEYBOARDMANAGER_INJECTED_FLAG (bit 0): treated as KBM's own
        // output (all KBM flags include that bit; other PowerToys modules set it so KBM ignores their
        // input; any other program setting it is treated the same). Not tracked: handled by the live
        // tables directly, and the handlers ignore it as KBM output.
        OwnInjected,

        // LLKHF_INJECTED and dwExtraInfo == KEYBOARDMANAGER_REPROCESS_FLAG: a key-down KBM re-sends so
        // that its handlers process it again ("send current key pressed without shortcut flag"). It
        // stands for a physical key that is already held (and tracked) while it is being handled, and it
        // is re-sent with MapVirtualKey's scan code, which can differ from the physical key's (numpad
        // Enter, numpad navigation keys with NumLock off), so it must not create a held-key record of its
        // own: one would never see a key-up. Not tracked; passed to the handlers, which reprocess it
        // (bit 0 is clear).
        OwnReprocessed,
    };

    // Pure classification from KBDLLHOOKSTRUCT::flags and ::dwExtraInfo.
    static KeyEventSource ClassifyKeyEvent(DWORD flags, ULONG_PTR extraInfo) noexcept;

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
    // `applyInPlace` and `onRequested` run under the gate lock, so they cannot interleave with a
    // hook-thread swap. `onRequested` is given the profile the engine is heading to but has not
    // applied yet: the staged profile, or empty when nothing is staged afterwards (in-place apply, or
    // a discarded load with no stage pending).
    OfferResult Offer(MappingConfiguration::LoadOutcome outcome, std::unique_ptr<State> configuration, const ApplyInPlaceFunction& applyInPlace, const std::function<void(const std::wstring&)>& onRequested = nullptr);

    // For a profile switch that failed before reaching a reload (e.g. settings.json unreadable): runs
    // `onRequested` under the gate lock with the profile the engine is still heading to, i.e. the
    // staged profile or empty when nothing is staged (the same value a discarded Offer reports).
    void ReportPendingProfile(const std::function<void(const std::wstring&)>& onRequested) const;

    bool HasStage() const noexcept;

    // Name of the staged profile, or empty when nothing is staged.
    std::wstring StagedProfile() const;

    // Hook thread: the hook procedure's handling of one event (KeyboardManager::HookProc and the tests
    // both call this). Classifies the event (ClassifyKeyEvent, read before the encoding below), encodes
    // the numpad origin into `event->lParam->vkCode` (the value the handlers see), and then:
    //  - injected events (own or foreign) first mark held keys whose async state they invalidate;
    //  - KBM's own output, and key-downs KBM re-sends for reprocessing, go to `handle` directly;
    //  - tracked events go through the held-key set: a pending stage is applied first when allowed,
    //    then the event is either answered for an orphan key or passed to `handle`.
    // Returns 1 to suppress the event. `handle` must not re-enter this function synchronously for a
    // tracked event (in Windows, input injected from the hook callback arrives after it returns).
    intptr_t OnHookEvent(LowlevelKeyboardEvent* event, const HandleFunction& handle, const AdoptFunction& adopt) noexcept;

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

        // For a normal key (physical or foreign-injected): whether the hook suppressed its first
        // key-down (then the system never saw it, GetAsyncKeyState cannot vouch for it, and it is never
        // dropped as stale).
        // For an orphan: whether its remaining events are suppressed (true) or passed through (false).
        bool suppressed = false;

        bool orphan = false;

        // Seeded from GetAsyncKeyState: the scan code (and so the id) is MapVirtualKey's estimate and
        // not confirmed by an event of the key yet. See FindHeldKey.
        bool estimatedScanCode = false;

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

    // A tracked (physical or foreign-injected) event; see OnHookEvent.
    intptr_t OnTrackedKeyEvent(LowlevelKeyboardEvent* event, const HandleFunction& handle, const AdoptFunction& adopt);

    // An injected event (KBM's own or foreign), before it is handled. An injected key-up for a key
    // believed held (e.g. KBM releasing a held modifier while invoking a shortcut or a text remap)
    // makes GetAsyncKeyState report that key up while it is still held, so from then on the key is
    // never dropped as stale (only the forced swap can release it).
    void OnInjectedKeyEvent(const LowlevelKeyboardEvent* event) noexcept;

    // The held entry for an event: by key id; when no record has that id, by virtual key, but only for
    // an orphan seeded from GetAsyncKeyState whose scan code is still an estimate (MapVirtualKey's
    // guess can differ from the real key, e.g. numpad Enter or the numpad navigation keys with NumLock
    // off). Other records carry the real id, so a different physical key with the same virtual key
    // (main vs numpad Enter, arrows vs NumLock-off numpad) never matches them. An estimate is settled
    // by the key's first event: an exact id match confirms it, a virtual-key match re-keys the record
    // to the event's id; either way the virtual-key fallback no longer applies to that record.
    std::unordered_map<uint32_t, HeldKey>::iterator FindHeldKey(uint32_t id, DWORD rawVkCode);

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
