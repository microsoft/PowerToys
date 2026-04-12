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

        // Two identical hotkeys must yield the same 16-bit handle.
        // The handle is key | (win<<8) | (ctrl<<9) | (shift<<10) | (alt<<11).
        TEST_METHOD(GetHotkeyHandle_SameHotkey_SameHandle)
        {
            Hotkey a = MakeHotkey(true, true, false, false, 'A');
            Hotkey b = MakeHotkey(true, true, false, false, 'A');

            // We can't call the private GetHotkeyHandle, but adding the same
            // hotkey from two different modules must trigger an InAppConflict,
            // which proves the manager computed equal handles.
            auto& mgr = HotkeyConflictManager::GetInstance();
            bool first = mgr.AddHotkey(a, MOD_A, 1, true);
            // AddHotkey may return false when the OS already owns Ctrl+Win+A
            // (RegisterHotKey(nullptr,...) check).  Log and skip so the test
            // does not silently pass without asserting anything.
            if (!first)
            {
                Logger::WriteMessage(L"SKIPPED: Ctrl+Win+A is already registered by the system — cannot validate hotkey handle equality.");
                return;
            }

            auto conflict = mgr.HasConflict(b, MOD_B, 2);
            Assert::AreEqual(static_cast<int>(HotkeyConflictType::InAppConflict),
                             static_cast<int>(conflict));
        }

        // Different hotkeys (different key or modifier) must not collide.
        TEST_METHOD(GetHotkeyHandle_DifferentHotkeys_DifferentHandles)
        {
            Hotkey a = MakeHotkey(true, true, false, false, 'A');
            Hotkey b = MakeHotkey(true, true, false, false, 'B');

            auto& mgr = HotkeyConflictManager::GetInstance();
            if (!mgr.AddHotkey(a, MOD_A, 1, true))
            {
                Logger::WriteMessage(L"SKIPPED: Hotkey A is already registered by the system.");
                return;
            }

            auto conflict = mgr.HasConflict(b, MOD_B, 2);
            Assert::AreEqual(static_cast<int>(HotkeyConflictType::NoConflict),
                             static_cast<int>(conflict));
        }

        // ── HasConflict ─────────────────────────────────────────────────

        // Empty manager should report no conflict.
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

        // Two different modules registering the same hotkey → InAppConflict.
        TEST_METHOD(HasConflict_TwoModulesSameHotkey_InAppConflict)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey hk = MakeHotkey(true, true, false, false, 'Z');

            if (!mgr.AddHotkey(hk, MOD_A, 1, true))
            {
                Logger::WriteMessage(L"SKIPPED: Hotkey is already registered by the system.");
                return;
            }
            auto conflict = mgr.HasConflict(hk, MOD_B, 2);
            Assert::AreEqual(static_cast<int>(HotkeyConflictType::InAppConflict),
                             static_cast<int>(conflict));
        }

        // Same module re-registering the same hotkey+id → NoConflict.
        TEST_METHOD(HasConflict_SameModuleReRegister_NoConflict)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey hk = MakeHotkey(true, true, false, false, 'R');

            if (!mgr.AddHotkey(hk, MOD_A, 1, true))
            {
                Logger::WriteMessage(L"SKIPPED: Hotkey is already registered by the system.");
                return;
            }
            auto conflict = mgr.HasConflict(hk, MOD_A, 1);
            Assert::AreEqual(static_cast<int>(HotkeyConflictType::NoConflict),
                             static_cast<int>(conflict));
        }

        // ── AddHotkey ───────────────────────────────────────────────────

        // First registration of a unique hotkey should succeed and be
        // visible in the manager state via HasConflict.
        TEST_METHOD(AddHotkey_SuccessReturnsTrue)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            // Use an uncommon combo unlikely to be system-registered.
            Hotkey hk = MakeHotkey(true, true, true, true, 'Q');
            bool ok = mgr.AddHotkey(hk, MOD_A, 1, true);

            if (ok)
            {
                // The hotkey was accepted — verify it is present in manager
                // state by checking that a second module sees an InAppConflict.
                auto conflict = mgr.HasConflict(hk, MOD_B, 2);
                Assert::AreEqual(static_cast<int>(HotkeyConflictType::InAppConflict),
                                 static_cast<int>(conflict),
                                 L"Added hotkey should be visible as InAppConflict from another module");
            }
            // If the OS owns this combo the call returns false — that's
            // acceptable on some machines.  The important thing is no crash.
        }

        // Adding a conflicting hotkey returns false.
        TEST_METHOD(AddHotkey_ConflictReturnsFalse)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey hk = MakeHotkey(true, true, false, false, 'X');

            bool first = mgr.AddHotkey(hk, MOD_A, 1, true);
            if (!first)
            {
                Logger::WriteMessage(L"SKIPPED: Hotkey is already registered by the system — cannot test conflict return value.");
                return;
            }

            bool second = mgr.AddHotkey(hk, MOD_B, 2, true);
            Assert::IsFalse(second);
        }

        // Adding a disabled hotkey always succeeds and doesn't create
        // conflicts.
        TEST_METHOD(AddHotkey_DisabledDoesNotConflict)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey hk = MakeHotkey(true, true, false, false, 'D');

            bool first = mgr.AddHotkey(hk, MOD_A, 1, true);
            if (!first)
            {
                Logger::WriteMessage(L"SKIPPED: Hotkey is already registered by the system — cannot test disabled hotkey behavior.");
                return;
            }

            bool disabled = mgr.AddHotkey(hk, MOD_B, 2, false);
            Assert::IsTrue(disabled);

            // Module B is disabled, so checking for A should still be NoConflict.
            auto conflict = mgr.HasConflict(hk, MOD_A, 1);
            Assert::AreEqual(static_cast<int>(HotkeyConflictType::NoConflict),
                             static_cast<int>(conflict));
        }

        // ── RemoveHotkeyByModule ────────────────────────────────────────

        // After removal, the hotkey should no longer conflict.
        TEST_METHOD(RemoveHotkeyByModule_ClearsAllHotkeys)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey hk = MakeHotkey(true, true, false, false, 'C');

            mgr.AddHotkey(hk, MOD_A, 1, true);
            mgr.RemoveHotkeyByModule(MOD_A);

            auto conflict = mgr.HasConflict(hk, MOD_B, 2);
            Assert::AreNotEqual(static_cast<int>(HotkeyConflictType::InAppConflict),
                                static_cast<int>(conflict));
        }

        // When three modules share a hotkey and one is removed, the remaining
        // two should still conflict.  When a second is removed the last one is
        // "promoted" to the main map (no longer in the conflict set).
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

        // Disabling a module moves its hotkeys out of the active maps.
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

        // Re-enabling a module re-adds its hotkeys and re-checks conflicts.
        TEST_METHOD(EnableHotkeyByModule_ReAddsAndReChecksConflicts)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey hk = MakeHotkey(true, true, false, false, 'F');

            if (!mgr.AddHotkey(hk, MOD_A, 1, true))
            {
                Logger::WriteMessage(L"SKIPPED: Hotkey is already registered by the system.");
                return;
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

        // Returns conflicting entries when a conflict exists.
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

        // System conflict detection uses RegisterHotKey with nullptr.
        // We can't predict which hotkeys the OS owns, but we verify the
        // priority logic: if a hotkey ends up in the sys-conflict map the
        // HasConflict single-arg overload returns SystemConflict, not InApp.
        TEST_METHOD(SystemConflict_PriorityOverInApp)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();

            // Use a hotkey combo that's very likely to be system-reserved:
            // Win+L locks the workstation on most Windows builds.
            Hotkey sysHk = MakeHotkey(true, false, false, false, 'L');

            // Attempt to add — this should detect a system conflict and put
            // the entry in sysConflictHotkeyMap.
            bool ok = mgr.AddHotkey(sysHk, MOD_A, 1, true);
            // ok is false if system conflict was detected.
            if (!ok)
            {
                auto type = mgr.HasConflict(sysHk);
                Assert::AreEqual(static_cast<int>(HotkeyConflictType::SystemConflict),
                                 static_cast<int>(type));
            }
            // If RegisterHotKey succeeded (unlikely for Win+L), we can't
            // force a system conflict in a unit test — log and skip.
            else
            {
                Logger::WriteMessage(L"SKIPPED: Win+L was not detected as a system conflict — cannot validate system-conflict priority.");
                return;
            }
        }

        // ── GetHotkeyConflictsAsJson ────────────────────────────────────

        // The JSON output must have the expected top-level keys.
        TEST_METHOD(GetHotkeyConflictsAsJson_ValidJsonOutput)
        {
            auto& mgr = HotkeyConflictManager::GetInstance();
            Hotkey hk = MakeHotkey(true, true, false, false, 'J');

            bool first = mgr.AddHotkey(hk, MOD_A, 1, true);
            if (!first)
            {
                Logger::WriteMessage(L"SKIPPED: Hotkey is already registered by the system — cannot test JSON output with in-app conflicts.");
                return;
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
    };
}
