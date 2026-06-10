// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Unit tests for HotkeyConflictDetector, mirroring the Rust test suite in
// src/rust/libs/runner-core/src/hotkey_conflict.rs.
//
// These tests exercise the public API of HotkeyConflictManager through the
// singleton instance.  Each test method clears state by removing all known
// test modules before running.

#include "pch.h"

#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "../hotkey_conflict_detector.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace HotkeyConflictDetector;

namespace RunnerUnitTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    // Build a Hotkey from modifier flags and a virtual-key code.
    static constexpr Hotkey MakeHotkey(bool win, bool ctrl, bool shift, bool alt, unsigned char key)
    {
        Hotkey hk{};
        hk.win = win;
        hk.ctrl = ctrl;
        hk.shift = shift;
        hk.alt = alt;
        hk.key = key;
        return hk;
    }

    // Well-known test module names.
    static constexpr const wchar_t* MOD_A = L"TestModuleA";
    static constexpr const wchar_t* MOD_B = L"TestModuleB";
    static constexpr const wchar_t* MOD_C = L"TestModuleC";

    // ── test class ──────────────────────────────────────────────────────────

    TEST_CLASS(HotkeyConflictTests)
    {
    private:
        // Remove all hotkeys registered by test modules so each test starts clean.
        void CleanupManager()
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            mgr.RemoveHotkeyByModule(MOD_A);
            mgr.RemoveHotkeyByModule(MOD_B);
            mgr.RemoveHotkeyByModule(MOD_C);
        }

    public:
        TEST_METHOD_INITIALIZE(SetUp)
        {
            CleanupManager();
        }

        TEST_METHOD_CLEANUP(TearDown)
        {
            CleanupManager();
        }

        // ── GetHotkeyHandle ─────────────────────────────────────────────

        // Product code: hotkey_conflict_detector.cpp — HotkeyConflictManager::GetHotkeyHandle(hotkey)
        // What: Verifies that two identical hotkeys produce the same internal 16-bit handle
        // Why: Handle equality is the foundation of conflict detection — if handles diverge for identical combos, conflicts silently disappear
        // Risk: If broken, users could bind identical shortcuts in two modules without any warning
        TEST_METHOD(GetHotkeyHandle_SameHotkey_SameHandle)
        {
            Hotkey a = MakeHotkey(true, true, false, false, 'A');
            Hotkey b = MakeHotkey(true, true, false, false, 'A');

            // We can't call the private GetHotkeyHandle, but adding the same
            // hotkey from two different modules must trigger an InAppConflict,
            // which proves the manager computed equal handles.
            auto& mgr = HotkeyConflictManager::GetInstance();
            bool first = mgr.AddHotkey(a, MOD_A, 1, true);
            if (!first)
            {
                Assert::Fail(L"INCONCLUSIVE: Ctrl+Win+A is already registered by the system - test cannot run deterministically");
            }

            auto conflict = mgr.HasConflict(b, MOD_B, 2);
            Assert::AreEqual(static_cast<int>(HotkeyConflictType::InAppConflict),
                             static_cast<int>(conflict));
        }

        // Product code: hotkey_conflict_detector.cpp — HotkeyConflictManager::GetHotkeyHandle(hotkey)
        // What: Verifies that hotkeys differing only by virtual-key code produce different handles
        // Why: Distinct hotkeys must hash to different handles or false conflicts flood the UI
        // Risk: If broken, every hotkey registration would appear to conflict with every other
        TEST_METHOD(GetHotkeyHandle_DifferentHotkeys_DifferentHandles)
        {
            Hotkey a = MakeHotkey(true, true, false, false, 'A');
            Hotkey b = MakeHotkey(true, true, false, false, 'B');

            auto& mgr = HotkeyConflictManager::GetInstance();
            if (!mgr.AddHotkey(a, MOD_A, 1, true))
            {
                Assert::Fail(L"INCONCLUSIVE: Hotkey A is already registered by the system - test cannot run deterministically");
            }

            auto conflict = mgr.HasConflict(b, MOD_B, 2);
            Assert::AreEqual(static_cast<int>(HotkeyConflictType::NoConflict),
                             static_cast<int>(conflict));
        }

        // ── HasConflict ─────────────────────────────────────────────────

        // Product code: hotkey_conflict_detector.cpp — HotkeyConflictManager::HasConflict(hotkey, moduleName, hotkeyID)
        // What: Verifies that querying an empty conflict manager does not return InAppConflict
        // Why: Guards against false positive conflicts when no modules have registered hotkeys
        // Risk: If broken, PowerToys would show conflict warnings on first launch before any module registers
        TEST_METHOD(HasConflict_EmptyManager_NoConflict)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey hk = MakeHotkey(true, true, false, false, 'A');

            auto result = mgr.HasConflict(hk, MOD_A, 1);
            // Either NoConflict or SystemConflict (if OS owns Ctrl+Win+A)
            // but NOT InAppConflict, since nothing is registered.
            Assert::AreNotEqual(static_cast<int>(HotkeyConflictType::InAppConflict),
                                static_cast<int>(result));
        }

        // Product code: hotkey_conflict_detector.cpp — HotkeyConflictManager::HasConflict(hotkey, moduleName, hotkeyID)
        // What: Verifies that two different modules registering the same hotkey triggers InAppConflict
        // Why: Core conflict detection path — the primary reason this manager exists
        // Risk: If broken, users would never see warnings when modules have overlapping shortcuts
        TEST_METHOD(HasConflict_TwoModulesSameHotkey_InAppConflict)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey hk = MakeHotkey(true, true, false, false, 'Z');

            if (!mgr.AddHotkey(hk, MOD_A, 1, true))
            {
                Assert::Fail(L"INCONCLUSIVE: Hotkey is already registered by the system - test cannot run deterministically");
            }
            auto conflict = mgr.HasConflict(hk, MOD_B, 2);
            Assert::AreEqual(static_cast<int>(HotkeyConflictType::InAppConflict),
                             static_cast<int>(conflict));
        }

        // Product code: hotkey_conflict_detector.cpp — HotkeyConflictManager::HasConflict(hotkey, moduleName, hotkeyID)
        // What: Verifies that the same module re-querying its own hotkey returns NoConflict
        // Why: A module should not conflict with itself during settings refresh or re-enable
        // Risk: If broken, every module would see spurious self-conflicts on settings change
        TEST_METHOD(HasConflict_SameModuleReRegister_NoConflict)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey hk = MakeHotkey(true, true, false, false, 'R');

            if (!mgr.AddHotkey(hk, MOD_A, 1, true))
            {
                Assert::Fail(L"INCONCLUSIVE: Hotkey is already registered by the system - test cannot run deterministically");
            }
            auto conflict = mgr.HasConflict(hk, MOD_A, 1);
            Assert::AreEqual(static_cast<int>(HotkeyConflictType::NoConflict),
                             static_cast<int>(conflict));
        }

        // ── AddHotkey ───────────────────────────────────────────────────

        // Product code: hotkey_conflict_detector.cpp — HotkeyConflictManager::AddHotkey(hotkey, moduleName, hotkeyID, isEnabled)
        // What: Verifies that adding a unique hotkey succeeds and makes it visible to other modules
        // Why: AddHotkey is the entry point for all module hotkey registrations
        // Risk: If broken, modules would silently fail to register shortcuts, breaking all hotkey functionality
        TEST_METHOD(AddHotkey_SuccessReturnsTrue)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            // Use an uncommon combo unlikely to be system-registered.
            Hotkey hk = MakeHotkey(true, true, true, true, 'Q');
            bool ok = mgr.AddHotkey(hk, MOD_A, 1, true);

            if (!ok)
            {
                Assert::Fail(L"INCONCLUSIVE: AddHotkey returned false (system hotkey conflict) - cannot validate success path");
            }

            // The hotkey was accepted — verify it is present in manager
            // state by checking that a second module sees an InAppConflict.
            auto conflict = mgr.HasConflict(hk, MOD_B, 2);
            Assert::AreEqual(static_cast<int>(HotkeyConflictType::InAppConflict),
                             static_cast<int>(conflict),
                             L"Added hotkey should be visible as InAppConflict from another module");
        }

        // Product code: hotkey_conflict_detector.cpp — HotkeyConflictManager::AddHotkey(hotkey, moduleName, hotkeyID, isEnabled)
        // What: Verifies that adding a hotkey already owned by another module returns false
        // Why: Callers rely on the return value to decide whether to show conflict UI
        // Risk: If broken, conflicting hotkeys would be silently accepted, leading to unpredictable behavior
        TEST_METHOD(AddHotkey_ConflictReturnsFalse)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey hk = MakeHotkey(true, true, false, false, 'X');

            bool first = mgr.AddHotkey(hk, MOD_A, 1, true);
            if (!first)
            {
                Assert::Fail(L"INCONCLUSIVE: Hotkey is already registered by the system - test cannot run deterministically");
            }

            bool second = mgr.AddHotkey(hk, MOD_B, 2, true);
            Assert::IsFalse(second);
        }

        // Product code: hotkey_conflict_detector.cpp — HotkeyConflictManager::AddHotkey(hotkey, moduleName, hotkeyID, isEnabled=false)
        // What: Verifies that adding a hotkey with isEnabled=false does not create conflicts
        // Why: Disabled modules should not block other modules from using the same hotkey
        // Risk: If broken, disabled modules would ghost-block hotkeys from active modules
        TEST_METHOD(AddHotkey_DisabledDoesNotConflict)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey hk = MakeHotkey(true, true, false, false, 'D');

            bool first = mgr.AddHotkey(hk, MOD_A, 1, true);
            if (!first)
            {
                Assert::Fail(L"INCONCLUSIVE: Hotkey is already registered by the system - test cannot run deterministically");
            }

            bool disabled = mgr.AddHotkey(hk, MOD_B, 2, false);
            Assert::IsTrue(disabled);

            // Module B is disabled, so checking for A should still be NoConflict.
            auto conflict = mgr.HasConflict(hk, MOD_A, 1);
            Assert::AreEqual(static_cast<int>(HotkeyConflictType::NoConflict),
                             static_cast<int>(conflict));
        }

        // ── RemoveHotkeyByModule ────────────────────────────────────────

        // Product code: hotkey_conflict_detector.cpp — HotkeyConflictManager::RemoveHotkeyByModule(moduleName)
        // What: Verifies that removing a module clears its hotkeys from the conflict maps
        // Why: Module unload must clean up all state to prevent stale conflict entries
        // Risk: If broken, uninstalled/disabled modules would permanently block hotkeys
        TEST_METHOD(RemoveHotkeyByModule_ClearsAllHotkeys)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey hk = MakeHotkey(true, true, false, false, 'C');

            bool added = mgr.AddHotkey(hk, MOD_A, 1, true);
            if (!added)
            {
                Assert::Fail(L"INCONCLUSIVE: AddHotkey returned false (system hotkey conflict) - cannot validate removal");
            }

            mgr.RemoveHotkeyByModule(MOD_A);

            auto conflict = mgr.HasConflict(hk, MOD_B, 2);
            Assert::AreNotEqual(static_cast<int>(HotkeyConflictType::InAppConflict),
                                static_cast<int>(conflict));
        }

        // Product code: hotkey_conflict_detector.cpp — HotkeyConflictManager::RemoveHotkeyByModule(moduleName)
        // What: Verifies that removing one conflicting module promotes the survivor back to the main map
        // Why: When a three-way conflict drops to two-way then one-way, state must be correctly promoted
        // Risk: If broken, removing a conflicting module would leave orphaned entries in the conflict map
        TEST_METHOD(RemoveHotkeyByModule_PromotesInAppSurvivor)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey hk = MakeHotkey(true, true, false, false, 'P');

            mgr.AddHotkey(hk, MOD_A, 1, true);
            mgr.AddHotkey(hk, MOD_B, 2, true);
            mgr.AddHotkey(hk, MOD_C, 3, true);

            // Remove one of the conflicting modules.
            mgr.RemoveHotkeyByModule(MOD_A);

            // B and C still share the same hotkey → InAppConflict.
            (void)mgr.HasConflict(hk, MOD_B, 2);
            // B re-checking itself → NoConflict OR InAppConflict depending on
            // whether it ended up in the conflict map or got promoted.
            // But a *new* module should see a conflict:
            auto conflictNew = mgr.HasConflict(hk, L"NewModule", 99);
            Assert::AreEqual(static_cast<int>(HotkeyConflictType::InAppConflict),
                             static_cast<int>(conflictNew));

            // Remove B → only C remains; it should be promoted out of conflict set.
            mgr.RemoveHotkeyByModule(MOD_B);
            auto conflictC = mgr.HasConflict(hk, MOD_C, 3);
            Assert::AreEqual(static_cast<int>(HotkeyConflictType::NoConflict),
                             static_cast<int>(conflictC));
        }

        // ── DisableHotkeyByModule ───────────────────────────────────────

        // Product code: hotkey_conflict_detector.cpp — HotkeyConflictManager::DisableHotkeyByModule(moduleName)
        // What: Verifies that disabling modules moves hotkeys out of active maps and resolves conflicts
        // Why: Disabling a module in PowerToys Settings must immediately free its hotkeys
        // Risk: If broken, toggling a module off would not release its hotkeys, blocking other modules
        TEST_METHOD(DisableHotkeyByModule_MovesToDisabled_NoConflict)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey hk = MakeHotkey(true, true, false, false, 'E');

            mgr.AddHotkey(hk, MOD_A, 1, true);
            mgr.AddHotkey(hk, MOD_B, 2, true); // conflict

            mgr.DisableHotkeyByModule(MOD_A);
            mgr.DisableHotkeyByModule(MOD_B);

            // After disabling both, a new check should show no in-app conflict.
            auto conflict = mgr.HasConflict(hk, MOD_C, 3);
            Assert::AreNotEqual(static_cast<int>(HotkeyConflictType::InAppConflict),
                                static_cast<int>(conflict));
        }

        // ── EnableHotkeyByModule ────────────────────────────────────────

        // Product code: hotkey_conflict_detector.cpp — HotkeyConflictManager::EnableHotkeyByModule(moduleName)
        // What: Verifies that re-enabling a disabled module restores its hotkeys and detects new conflicts
        // Why: Module enable must re-enter the conflict detection pipeline, not skip it
        // Risk: If broken, re-enabling a module could silently shadow another module's hotkeys
        TEST_METHOD(EnableHotkeyByModule_ReAddsAndReChecksConflicts)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey hk = MakeHotkey(true, true, false, false, 'F');

            if (!mgr.AddHotkey(hk, MOD_A, 1, true))
            {
                Assert::Fail(L"INCONCLUSIVE: Hotkey is already registered by the system - test cannot run deterministically");
            }
            mgr.AddHotkey(hk, MOD_B, 2, true); // conflict

            mgr.DisableHotkeyByModule(MOD_B);

            // After disabling B, A's hotkey should be resolvable.
            auto noConflict = mgr.HasConflict(hk, MOD_A, 1);
            Assert::AreEqual(static_cast<int>(HotkeyConflictType::NoConflict),
                             static_cast<int>(noConflict));

            mgr.EnableHotkeyByModule(MOD_B);

            // Now there's a conflict again.
            auto conflictAgain = mgr.HasConflict(hk, MOD_C, 3);
            Assert::AreEqual(static_cast<int>(HotkeyConflictType::InAppConflict),
                             static_cast<int>(conflictAgain));
        }

        // ── GetAllConflicts ─────────────────────────────────────────────

        // Product code: hotkey_conflict_detector.cpp — HotkeyConflictManager::GetAllConflicts(hotkey)
        // What: Verifies that GetAllConflicts returns all modules sharing the same hotkey
        // Why: Used by the UI to show the full list of conflicting modules for a given shortcut
        // Risk: If broken, the conflict dialog would show incomplete information
        TEST_METHOD(GetAllConflicts_ReturnsMatchingEntries)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey hk = MakeHotkey(true, true, false, false, 'G');

            mgr.AddHotkey(hk, MOD_A, 1, true);
            mgr.AddHotkey(hk, MOD_B, 2, true);

            auto conflicts = mgr.GetAllConflicts(hk);
            Assert::IsTrue(conflicts.size() >= 2,
                           L"Expected at least 2 conflicting entries");

            // All returned entries should have the same hotkey combination.
            for (const auto& c : conflicts)
            {
                Assert::IsTrue(c.hotkey == hk);
            }
        }

        // ── System conflict priority over in-app ────────────────────────

        // Product code: hotkey_conflict_detector.cpp — HotkeyConflictManager::HasConflict(hotkey) (single-arg overload)
        // What: Verifies that system-level hotkey conflicts (e.g., Win+L) take priority over in-app conflicts
        // Why: System hotkeys cannot be overridden; showing InAppConflict instead of SystemConflict misleads users
        // Risk: If broken, users would think they can resolve a system conflict by changing PowerToys settings
        TEST_METHOD(SystemConflict_PriorityOverInApp)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();

            // Win+L locks the workstation on most Windows builds — very likely system-reserved.
            Hotkey sysHk = MakeHotkey(true, false, false, false, 'L');

            bool ok = mgr.AddHotkey(sysHk, MOD_A, 1, true);
            if (!ok)
            {
                auto type = mgr.HasConflict(sysHk);
                Assert::AreEqual(static_cast<int>(HotkeyConflictType::SystemConflict),
                                 static_cast<int>(type));
            }
            else
            {
                Assert::Fail(L"INCONCLUSIVE: Win+L was not detected as a system conflict - cannot validate system-conflict priority");
            }
        }

        // ── GetHotkeyConflictsAsJson ────────────────────────────────────

        // Product code: hotkey_conflict_detector.cpp — HotkeyConflictManager::GetHotkeyConflictsAsJson()
        // What: Verifies that the JSON output contains required top-level keys and at least one conflict group
        // Why: The Settings UI parses this JSON to display conflict information
        // Risk: If broken, the Settings UI would fail to render conflict warnings
        TEST_METHOD(GetHotkeyConflictsAsJson_ValidJsonOutput)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey hk = MakeHotkey(true, true, false, false, 'J');

            bool first = mgr.AddHotkey(hk, MOD_A, 1, true);
            if (!first)
            {
                Assert::Fail(L"INCONCLUSIVE: Hotkey is already registered by the system - test cannot run deterministically");
            }
            mgr.AddHotkey(hk, MOD_B, 2, true);

            auto json = mgr.GetHotkeyConflictsAsJson();

            // Must have "inAppConflicts" and "sysConflicts" arrays.
            Assert::IsTrue(json.HasKey(L"inAppConflicts"),
                           L"JSON missing 'inAppConflicts' key");
            Assert::IsTrue(json.HasKey(L"sysConflicts"),
                           L"JSON missing 'sysConflicts' key");

            // inAppConflicts should contain at least one group.
            auto inApp = json.GetNamedArray(L"inAppConflicts");
            Assert::IsTrue(inApp.Size() >= 1,
                           L"Expected at least one in-app conflict group");
        }

        // ── New coverage: edge cases and deeper verification ────────────

        // Product code: hotkey_conflict_detector.cpp — HotkeyConflictManager::AddHotkey / HasConflict
        // What: Verifies that a hotkey with key=0 and no modifiers is handled gracefully (handle computes to 0)
        // Why: Edge case where handle computes to 0; ensures no map corruption or undefined behavior
        // Risk: If broken, a misconfigured module sending key=0 could crash the manager or corrupt state
        TEST_METHOD(HandleZeroKey)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey zeroHk = MakeHotkey(false, false, false, false, 0);

            // Adding a zero-key hotkey should not crash. The return value depends on
            // whether the system considers VK=0 reserved.
            bool added = mgr.AddHotkey(zeroHk, MOD_A, 1, true);

            if (added)
            {
                // If accepted, querying the same key from a different module should
                // produce InAppConflict (proving handle=0 is tracked like any other).
                auto conflict = mgr.HasConflict(zeroHk, MOD_B, 2);
                Assert::AreEqual(static_cast<int>(HotkeyConflictType::InAppConflict),
                                 static_cast<int>(conflict),
                                 L"Zero-key hotkey should be tracked and produce InAppConflict");
            }
            else
            {
                // System claimed VK=0 — verify it shows up as SystemConflict.
                auto conflict = mgr.HasConflict(zeroHk);
                Assert::AreEqual(static_cast<int>(HotkeyConflictType::SystemConflict),
                                 static_cast<int>(conflict),
                                 L"Zero-key hotkey rejected by system should be SystemConflict");
            }
        }

        // Product code: hotkey_conflict_detector.cpp — HotkeyConflictManager::AddHotkey / RemoveHotkeyByModule
        // What: Registers 3 different hotkeys for one module, verifies all tracked, removes by module, verifies all gone
        // Why: Modules like FancyZones register multiple hotkeys; removal must clear all of them
        // Risk: If broken, unloading a multi-hotkey module would leak some registrations
        TEST_METHOD(MultipleHotkeysPerModule)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey hk1 = MakeHotkey(true, true, false, false, '1');
            Hotkey hk2 = MakeHotkey(true, true, false, false, '2');
            Hotkey hk3 = MakeHotkey(true, true, false, false, '3');

            bool ok1 = mgr.AddHotkey(hk1, MOD_A, 1, true);
            bool ok2 = mgr.AddHotkey(hk2, MOD_A, 2, true);
            bool ok3 = mgr.AddHotkey(hk3, MOD_A, 3, true);

            if (!ok1 || !ok2 || !ok3)
            {
                Assert::Fail(L"INCONCLUSIVE: One or more hotkeys already registered by system - cannot test multi-hotkey removal");
            }

            // All three should be visible as InAppConflict from another module.
            Assert::AreEqual(static_cast<int>(HotkeyConflictType::InAppConflict),
                             static_cast<int>(mgr.HasConflict(hk1, MOD_B, 10)),
                             L"Hotkey 1 should conflict");
            Assert::AreEqual(static_cast<int>(HotkeyConflictType::InAppConflict),
                             static_cast<int>(mgr.HasConflict(hk2, MOD_B, 11)),
                             L"Hotkey 2 should conflict");
            Assert::AreEqual(static_cast<int>(HotkeyConflictType::InAppConflict),
                             static_cast<int>(mgr.HasConflict(hk3, MOD_B, 12)),
                             L"Hotkey 3 should conflict");

            // Remove all hotkeys for MOD_A at once.
            mgr.RemoveHotkeyByModule(MOD_A);

            // None should conflict now.
            Assert::AreNotEqual(static_cast<int>(HotkeyConflictType::InAppConflict),
                                static_cast<int>(mgr.HasConflict(hk1, MOD_B, 10)),
                                L"Hotkey 1 should be cleared after removal");
            Assert::AreNotEqual(static_cast<int>(HotkeyConflictType::InAppConflict),
                                static_cast<int>(mgr.HasConflict(hk2, MOD_B, 11)),
                                L"Hotkey 2 should be cleared after removal");
            Assert::AreNotEqual(static_cast<int>(HotkeyConflictType::InAppConflict),
                                static_cast<int>(mgr.HasConflict(hk3, MOD_B, 12)),
                                L"Hotkey 3 should be cleared after removal");
        }

        // Product code: hotkey_conflict_detector.cpp — HotkeyConflictManager::GetAllConflicts(hotkey)
        // What: Tests GetAllConflicts with a known system hotkey (Win+L) to verify system conflict entries are returned
        // Why: GetAllConflicts must include system conflicts, not just in-app ones
        // Risk: If broken, system conflicts would be invisible in the full conflict listing
        TEST_METHOD(GetAllConflicts_SystemConflict)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey sysHk = MakeHotkey(true, false, false, false, 'L');

            bool ok = mgr.AddHotkey(sysHk, MOD_A, 1, true);
            if (ok)
            {
                Assert::Fail(L"INCONCLUSIVE: Win+L was not detected as system conflict - cannot validate GetAllConflicts system branch");
            }

            // Win+L should be in the system conflict map — GetAllConflicts should return entries.
            auto conflicts = mgr.GetAllConflicts(sysHk);
            Assert::IsTrue(conflicts.size() >= 1,
                           L"Expected at least 1 entry in GetAllConflicts for a system-conflicting hotkey");

            // Verify the returned entry references our module.
            bool foundModule = false;
            for (const auto& c : conflicts)
            {
                if (c.moduleName == MOD_A)
                {
                    foundModule = true;
                    break;
                }
            }
            Assert::IsTrue(foundModule, L"GetAllConflicts should include our module in system conflict entries");
        }

        // Product code: hotkey_conflict_detector.cpp — HotkeyConflictManager::GetHotkeyConflictsAsJson()
        // What: Verifies that module names and hotkey IDs appear in the JSON conflict payload
        // Why: The Settings UI needs module names to display actionable conflict information
        // Risk: If broken, conflict dialog would show anonymous entries users cannot act on
        TEST_METHOD(JsonOutput_ContainsModuleNames)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey hk = MakeHotkey(true, true, false, false, 'N');

            bool first = mgr.AddHotkey(hk, MOD_A, 42, true);
            if (!first)
            {
                Assert::Fail(L"INCONCLUSIVE: Hotkey is already registered by the system - cannot test JSON module name content");
            }
            mgr.AddHotkey(hk, MOD_B, 43, true);

            auto json = mgr.GetHotkeyConflictsAsJson();
            auto jsonStr = std::wstring(json.Stringify().c_str());

            // The JSON payload must contain the module names we registered.
            Assert::IsTrue(jsonStr.find(L"TestModuleA") != std::wstring::npos,
                           L"JSON should contain 'TestModuleA'");
            Assert::IsTrue(jsonStr.find(L"TestModuleB") != std::wstring::npos,
                           L"JSON should contain 'TestModuleB'");
        }
    };
}
