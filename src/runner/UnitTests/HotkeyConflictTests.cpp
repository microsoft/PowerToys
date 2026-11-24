// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CppUnitTest.h"

#include "../hotkey_conflict_detector.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RunnerUnitTests
{
    TEST_CLASS(HotkeyConflictTests)
    {
    public:
        TEST_METHOD_INITIALIZE(ClearHotkeyConflictManagerBeforeTest)
        {
            ClearTestModules();
        }

        TEST_METHOD_CLEANUP(ClearHotkeyConflictManagerAfterTest)
        {
            ClearTestModules();
        }

        TEST_METHOD(HasConflict_TwoModulesSameHotkey_InAppConflict)
        {
            using namespace HotkeyConflictDetector;

            auto& manager = HotkeyConflictManager::GetInstance();
            const Hotkey hotkey{ .win = false, .ctrl = false, .shift = false, .alt = false, .key = 'T' };

            Assert::IsTrue(manager.AddHotkey(hotkey, L"ModuleA", 1, true));
            Assert::AreEqual(static_cast<int>(InAppConflict), static_cast<int>(manager.HasConflict(hotkey, L"ModuleB", 1)));
        }

        TEST_METHOD(HasConflict_LegacyWinShiftC_OnWindows11_NoConflict)
        {
            using namespace HotkeyConflictDetector;

            // Windows 10 still reserves this shortcut for Cortana.
            if (!IsWindows11OrGreater())
            {
                return;
            }

            auto& manager = HotkeyConflictManager::GetInstance();
            const Hotkey hotkey{ .win = true, .ctrl = false, .shift = true, .alt = false, .key = 'C' };

            Assert::AreEqual(static_cast<int>(NoConflict), static_cast<int>(manager.HasConflict(hotkey)));
        }

        TEST_METHOD(GetAllConflicts_EmptyHotkey_ReturnsEmpty)
        {
            using namespace HotkeyConflictDetector;

            auto& manager = HotkeyConflictManager::GetInstance();
            const Hotkey hotkey{ .win = false, .ctrl = false, .shift = false, .alt = false, .key = 0 };

            const auto conflicts = manager.GetAllConflicts(hotkey);
            Assert::AreEqual<size_t>(0, conflicts.size());
        }

        TEST_METHOD(GetAllConflicts_NoConflict_ReturnsEmpty)
        {
            using namespace HotkeyConflictDetector;

            auto& manager = HotkeyConflictManager::GetInstance();
            const Hotkey unregisteredHotkey{ .win = false, .ctrl = false, .shift = false, .alt = false, .key = 'T' };

            Assert::AreEqual<size_t>(0, manager.GetAllConflicts(unregisteredHotkey).size());

            if (IsWindows11OrGreater())
            {
                const Hotkey legacyCortanaHotkey{ .win = true, .ctrl = false, .shift = true, .alt = false, .key = 'C' };
                Assert::AreEqual<size_t>(0, manager.GetAllConflicts(legacyCortanaHotkey).size());
            }
        }

        TEST_METHOD(GetAllConflicts_SystemReservedHotkey_ReturnsSystemConflict)
        {
            using namespace HotkeyConflictDetector;

            constexpr int testHotkeyId = 0x0FFE;
            struct HotkeyRegistrationGuard
            {
                bool registered = false;
                ~HotkeyRegistrationGuard()
                {
                    if (registered)
                    {
                        UnregisterHotKey(nullptr, testHotkeyId);
                    }
                }
            } guard;

            guard.registered = RegisterHotKey(nullptr, testHotkeyId, MOD_CONTROL | MOD_ALT | MOD_SHIFT | MOD_NOREPEAT, VK_F24) != FALSE;
            Assert::IsTrue(guard.registered);

            auto& manager = HotkeyConflictManager::GetInstance();
            const Hotkey hotkey{ .win = false, .ctrl = true, .shift = true, .alt = true, .key = VK_F24 };

            const auto conflicts = manager.GetAllConflicts(hotkey);
            Assert::AreEqual<size_t>(1, conflicts.size());
            Assert::AreEqual(L"System", conflicts[0].moduleName.c_str());
            Assert::AreEqual(0, conflicts[0].hotkeyID);
        }

    private:
        static void ClearTestModules()
        {
            auto& manager = HotkeyConflictDetector::HotkeyConflictManager::GetInstance();
            manager.RemoveHotkeyByModule(L"ModuleA");
            manager.RemoveHotkeyByModule(L"ModuleB");
        }
    };
}
